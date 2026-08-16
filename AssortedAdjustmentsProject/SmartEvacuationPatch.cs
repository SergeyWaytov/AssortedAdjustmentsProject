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

            // Override!
            public static bool Prefix(TacticalView __instance, TacticalAbility ability, TacticalActor ____selectedActor)
            {
                try
                {
                    if (!__instance.ViewerFaction.IsPlayingTurn ||
                        (ability.TacticalActorBase && ability.TacticalActorBase.TacticalFaction != __instance.ViewerFaction) ||
                        ability is IdleAbility)
                    {
                        return false;
                    }

                    bool isExitMissionAbilityEnabled = ability?.TacticalActorBase?.GetAbility<ExitMissionAbility>()?.IsEnabled(null) == true;
                    bool isEvacuateMountedActorsAbilityEnabled = ability?.TacticalActorBase?.GetAbility<EvacuateMountedActorsAbility>()?.IsEnabled(null) == true;
                    bool shouldOverridePrompt = isExitMissionAbilityEnabled || isEvacuateMountedActorsAbilityEnabled;

                    if (ability is IMoveAbility && ability.TacticalActor == ____selectedActor && shouldOverridePrompt)
                    {
                        Debug.Log("[AAP] SmartEvacuation: overriding exit mission prompt (checking squad exit zone status).");

                        // Always called by original method
                        typeof(TacticalView).GetMethod("UpdateApPool", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.Invoke(__instance, new object[] { false });

                        TacticalAbility evacuateAbility = ____selectedActor.GetAbility<ExitMissionAbility>();
                        if (evacuateAbility == null)
                        {
                            evacuateAbility = ____selectedActor.GetAbility<EvacuateMountedActorsAbility>();
                        }

                        allActiveSquadmembers = __instance.TacticalLevel.CurrentFaction.TacticalActors
                            .Where(a => a != ____selectedActor && a.IsActive).ToList();

                        bool isSquadInExitZone = true;
                        foreach (TacticalActor tActor in allActiveSquadmembers)
                        {
                            TacticalAbility tAbility = tActor.GetAbility<ExitMissionAbility>() as TacticalAbility;
                            if (tAbility == null)
                            {
                                tAbility = tActor.GetAbility<EvacuateMountedActorsAbility>() as TacticalAbility;
                            }
                            if (tAbility == null)
                            {
                                // Has no relevant ability, most likely a turret
                                continue;
                            }
                            if (!tAbility.HasValidTargets)
                            {
                                isSquadInExitZone = false;
                            }
                        }

                        if (isSquadInExitZone)
                        {
                            GameUtl.GetMessageBox().ShowSimplePrompt(
                                "Evacuate Squad?",
                                MessageBoxIcon.Question,
                                MessageBoxButtons.YesNo,
                                OnEvacuateSquadConfirmationResult,
                                null,
                                evacuateAbility);
                        }

                        return false;
                    }
                    else
                    {
                        return true;
                    }
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
