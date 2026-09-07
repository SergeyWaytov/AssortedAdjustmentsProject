using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Base.Core;
using Base.UI.MessageBox;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.View;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Smart squad evacuation, ported to native Workshop infrastructure from Mad's
    /// AssortedAdjustments (Modnix era). Replaces the old SquadEvacPatch which
    /// hooked TacticalAbility.Activate and force-activated every soldier's
    /// ExitMission ability without targets or zone checks - the source of the
    /// reported crash. This version only offers squad evacuation when the whole
    /// active squad can evacuate (each member has a valid exit target), asks for
    /// confirmation first, and activates each ability with its proper target.
    /// AAP T1: prefix rewritten so vanilla's AP refresh (UpdateApPool) always
    /// runs first, and the suppress-individual-prompts behavior fires only
    /// after a no-squad-evac move (i.e., when the WHOLE squad can leave).
    /// AAP G1 (live-guard): Prepare() stays false when the toggle is off, AND
    /// the prefix re-checks the toggle so a config change does not require
    /// a restart (Prepare() latches the patch in).
    /// </summary>
    internal static class SmartEvacuation
    {
        [HarmonyPatch(typeof(TacticalView), "OnAbilityExecuted")]
        public static class TacticalView_OnAbilityExecuted_Patch
        {
            internal static IEnumerable<TacticalActor> allActiveSquadmembers;

            public static bool Prepare()
            {
                return ModMain.Cfg?.EnableSmartEvacuation != false;
            }

            public static void OnEvacuateSquadConfirmationResult(MessageBoxCallbackResult res)
            {
                if (res.DialogResult != MessageBoxResult.Yes)
                {
                    return;
                }

                // Evacuate current actor
                TacticalAbility tacticalAbility = res.UserData as TacticalAbility;
                TacticalAbilityTarget tacticalAbilityTarget = tacticalAbility?.GetTargets().FirstOrDefault();
                if (tacticalAbilityTarget != null)
                {
                    tacticalAbility.Activate(tacticalAbilityTarget);
                }

                // Evacuate squad members
                foreach (TacticalActor tActor in allActiveSquadmembers)
                {
                    try
                    {
                        TacticalAbility tAbility = tActor.GetAbility<ExitMissionAbility>() as TacticalAbility;
                        if (tAbility == null)
                        {
                            tAbility = tActor.GetAbility<EvacuateMountedActorsAbility>() as TacticalAbility;
                        }
                        TacticalAbilityTarget taTarget = tAbility?.GetTargets().FirstOrDefault();
                        if (taTarget != null)
                        {
                            tAbility.Activate(taTarget);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log($"[AAP] SmartEvacuation: failed to evacuate {tActor?.DisplayName}: {e.Message}");
                    }
                }
            }

            // AAP T1: rewritten prefix. Vanilla's UpdateApPool(false) is called
            // unconditionally first so the AP display always refreshes. Then
            // the player-faction check + IdleAbility check return false (vanilla
            // also returns at this point -- its AP refresh already ran). Only
            // a move by the selected actor that has ExitMission or
            // EvacuateMountedActors enabled triggers the squad-evacuation
            // prompt; otherwise we return true so vanilla's own prompts run.
            public static bool Prefix(TacticalView __instance, TacticalAbility ability, TacticalActor ____selectedActor)
            {
                try
                {
                    // AAP G1 (live-guard): re-check the toggle.
                    if (ModMain.Cfg?.EnableSmartEvacuation == false) return true;

                    // Always refresh the AP pool first. Vanilla calls this at the
                    // top of OnAbilityExecuted; we replicate so our early returns
                    // below do not skip the AP refresh.
                    if (!(ability is IdleAbility))
                    {
                        typeof(TacticalView)
                            .GetMethod("UpdateApPool", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.Invoke(__instance, new object[] { false });
                    }

                    if (!__instance.ViewerFaction.IsPlayingTurn ||
                        (ability.TacticalActorBase && ability.TacticalActorBase.TacticalFaction != __instance.ViewerFaction) ||
                        ability is IdleAbility)
                    {
                        return false; // vanilla also returns here; AP refresh already ran above
                    }

                    bool exitEnabled = ability?.TacticalActorBase?.GetAbility<ExitMissionAbility>()?.IsEnabled(null) == true;
                    bool mountedExitEnabled = ability?.TacticalActorBase?.GetAbility<EvacuateMountedActorsAbility>()?.IsEnabled(null) == true;
                    if (!(ability is IMoveAbility) || ability.TacticalActor != ____selectedActor || (!exitEnabled && !mountedExitEnabled))
                        return true;

                    TacticalAbility evacuateAbility = ____selectedActor.GetAbility<ExitMissionAbility>() ??
                        (TacticalAbility)____selectedActor.GetAbility<EvacuateMountedActorsAbility>();

                    allActiveSquadmembers = __instance.TacticalLevel.CurrentFaction.TacticalActors
                        .Where(actor => actor != ____selectedActor && actor.IsActive).ToList();

                    // Whole-squad-can-leave check: if any active squad member
                    // lacks a valid exit target, return true so vanilla's own
                    // individual evac / interaction / enter-vehicle prompts fire.
                    foreach (TacticalActor actor in allActiveSquadmembers)
                    {
                        TacticalAbility actorExit = actor.GetAbility<ExitMissionAbility>() as TacticalAbility ??
                            actor.GetAbility<EvacuateMountedActorsAbility>() as TacticalAbility;
                        if (actorExit != null && !actorExit.HasValidTargets)
                            return true; // whole squad cannot leave: preserve every vanilla prompt
                    }

                    GameUtl.GetMessageBox().ShowSimplePrompt(
                        ModMain.Localize("EVAC_PROMPT"), MessageBoxIcon.Question, MessageBoxButtons.YesNo,
                        OnEvacuateSquadConfirmationResult, null, evacuateAbility);
                    return false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AAP] SmartEvacuation failed: {e}");
                    return true;
                }
            }
        }
    }
}
