// SmartEvacuationPatch.cs -- AAP Phase 3 (WHOLE-FILE REPLACEMENT).
// ----------------------------------------------------------------------------
// Reported bug (Phase 3): "Whole Squad Exfil hiccups and you'd have to
// 1-by-1 exfil troops even if multiple Actors of PX faction are inside
// escape zone."
//
// Root cause in the Phase 2b version: the squad-evacuation prompt was only
// offered when the WHOLE squad could leave (any member without a valid exit
// target downgraded the flow back to vanilla's per-actor prompts), AND the
// prompt only triggered after a MOVE into the escape zone -- clicking each
// soldier's own Exit Mission button evacuated exactly one soldier at a
// time, which is the reported 1-by-1 grind.
//
// Phase 3 fixes:
//   1. PARTIAL-SQUAD EVACUATION (option "Smart evacuation: partial squads",
//      default on): the prompt now offers to evacuate every member who CAN
//      currently leave (ready N of M), leaving the not-ready ones in place.
//      The full-squad case still shows the original "Evacuate Squad?" text.
//   2. BUTTON-FLOW PROMPT (postfix): after the player manually evacuates
//      one actor with the Exit Mission / Evacuate Mounted Actors button and
//      other members are still ready, a follow-up prompt offers to evacuate
//      the remaining ready members in one go. No more 1-by-1 clicking.
//   3. Re-validation at callback time: every actor's exit target is
//      re-checked when the prompt is answered (state may change between
//      prompt and click), with per-actor try/catch and a skip-reason log.
//   4. Re-entrancy guards: a mass evacuation sets an in-flight flag and a
//      timestamp; the button-flow prompt stays quiet while a mass evac is
//      running or within 3 seconds after one (the per-actor ability
//      activations re-enter OnAbilityExecuted and would otherwise re-prompt
//      mid-loop).
//   5. Readiness table logged (seeker lines, gated by the Diagnostics
//      option) at every prompt decision -- the next Player.log shows
//      exactly why each member was ready or not.
// ----------------------------------------------------------------------------
using Base.Core;
using Base.UI.MessageBox;
using HarmonyLib;
using PhoenixPoint.Tactical;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Levels;
using PhoenixPoint.Tactical.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    internal static class SmartEvacuation
    {
        // Re-entrancy guards (see header point 4).
        private static bool _massEvacInFlight;
        private static float _lastMassEvacAt = -999f;
        private const float PromptCooldownSeconds = 3f;

        private static bool CoreEnabled => ModMain.Cfg?.EnableSmartEvacuation != false;
        private static bool PartialEnabled => ModMain.Cfg?.SmartEvacPartialSquad != false;

        // ── Readiness model ────────────────────────────────────────────
        private sealed class MemberReadiness
        {
            public TacticalActor Actor;
            public TacticalAbility ExitAbility;   // ExitMission or EvacuateMountedActors
            public bool HasExitAbility;
            public bool HasTargets;
        }

        /// <summary>
        /// Computes per-member evacuation readiness for the player faction.
        /// The selected actor (move-branch) or the departed actor's
        /// exclusion (button-branch) is handled by the caller's exclude set.
        /// </summary>
        private static List<MemberReadiness> AssessReadiness(TacticalFaction faction, TacticalActor exclude)
        {
            var result = new List<MemberReadiness>();
            if (faction == null) return result;
            foreach (TacticalActor actor in faction.TacticalActors)
            {
                if (actor == null || !actor.IsActive || actor == exclude) continue;
                var entry = new MemberReadiness { Actor = actor };
                TacticalAbility exit = actor.GetAbility<ExitMissionAbility>() as TacticalAbility;
                if (exit == null)
                    exit = actor.GetAbility<EvacuateMountedActorsAbility>() as TacticalAbility;
                entry.ExitAbility = exit;
                entry.HasExitAbility = exit != null;
                entry.HasTargets = exit != null && exit.HasValidTargets;
                result.Add(entry);
            }
            return result;
        }

        private static void LogReadinessTable(string branch, IEnumerable<MemberReadiness> members)
        {
            if (!Seeker.Enabled) return;
            try
            {
                foreach (MemberReadiness m in members)
                {
                    string state = !m.HasExitAbility ? "no exit ability"
                        : m.HasTargets ? "READY"
                        : "not in escape zone (no valid target)";
                    Debug.Log($"[AAP][SEEKER] Evac {branch}: {m.Actor?.DisplayName} -> {state}");
                }
            }
            catch { }
        }

        /// <summary>Localized text with an English fallback (CSV-safe).</summary>
        private static string LocalizeOr(string key, string fallback)
        {
            string s = ModMain.Localize(key);
            return string.IsNullOrEmpty(s) ? fallback : s;
        }

        // ── Mass evacuation executor ───────────────────────────────────
        /// <summary>
        /// Evacuates every READY member (plus the optional selected actor's
        /// own ability). Re-validates targets per actor at execution time.
        /// </summary>
        private static void ExecuteMassEvacuation(TacticalAbility selectedExitAbility, TacticalFaction faction, TacticalActor exclude)
        {
            _massEvacInFlight = true;
            try
            {
                // The mover's own evacuation (move-branch only).
                if (selectedExitAbility != null)
                {
                    TacticalAbilityTarget own = selectedExitAbility.GetTargets().FirstOrDefault();
                    if (own != null)
                    {
                        selectedExitAbility.Activate(own);
                        Debug.Log($"[AAP] SmartEvacuation: evacuated {selectedExitAbility.TacticalActor?.DisplayName} (selected actor).");
                    }
                    else
                    {
                        Debug.Log("[AAP] SmartEvacuation: selected actor's exit target vanished between prompt and confirm -- skipped.");
                    }
                }

                foreach (MemberReadiness m in AssessReadiness(faction, exclude))
                {
                    try
                    {
                        if (!m.HasTargets) continue; // re-validated at execution time
                        TacticalAbilityTarget target = m.ExitAbility?.GetTargets().FirstOrDefault();
                        if (target == null)
                        {
                            Seeker.Log($"Evac execute: {m.Actor?.DisplayName} lost its exit target between prompt and confirm -- skipped.");
                            continue;
                        }
                        m.ExitAbility.Activate(target);
                        Debug.Log($"[AAP] SmartEvacuation: evacuated {m.Actor?.DisplayName}.");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[AAP] SmartEvacuation: failed to evacuate {m.Actor?.DisplayName}: {e.Message}");
                    }
                }
            }
            finally
            {
                _massEvacInFlight = false;
                _lastMassEvacAt = Time.realtimeSinceStartup;
            }
        }

        [HarmonyPatch(typeof(TacticalView), "OnAbilityExecuted")]
        public static class TacticalView_OnAbilityExecuted_Patch
        {
            // AAP T1 (kept from 2b): vanilla's AP refresh (UpdateApPool) is
            // replicated first so our early returns never skip it.
            public static bool Prepare() => CoreEnabled;

            // ── MOVE BRANCH (prefix) ────────────────────────────────────
            // Fires after a MOVE by the selected actor while its exit
            // ability is enabled (the vanilla "moved into escape zone"
            // moment). Offers full- or partial-squad evacuation.
            public static bool Prefix(TacticalView __instance, TacticalAbility ability, TacticalActor ____selectedActor)
            {
                try
                {
                    // AAP G1 (live-guard): re-check the toggle.
                    if (ModMain.Cfg?.EnableSmartEvacuation == false) return true;

                    // Always refresh the AP pool first (vanilla does this at
                    // the top of OnAbilityExecuted; we replicate so our early
                    // returns below do not skip the AP refresh).
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

                    if (_massEvacInFlight) return true; // we caused this move ourselves

                    TacticalActor selected = ____selectedActor;
                    TacticalAbility evacuateAbility = selected.GetAbility<ExitMissionAbility>() ??
                        (TacticalAbility)selected.GetAbility<EvacuateMountedActorsAbility>();
                    if (evacuateAbility == null) return true;

                    List<MemberReadiness> others = AssessReadiness(__instance.TacticalLevel.CurrentFaction, selected);
                    List<MemberReadiness> evacuable = others.Where(m => m.HasExitAbility).ToList();
                    int othersReady = others.Count(m => m.HasTargets);
                    LogReadinessTable("move-branch", others);

                    if (othersReady == 0)
                    {
                        // Nobody else can leave: let vanilla's own single-actor
                        // prompt run (vanilla behavior, nothing to improve).
                        return true;
                    }

                    bool fullSquad = evacuable.Count > 0 && evacuable.All(m => m.HasTargets);
                    if (fullSquad)
                    {
                        // Whole squad can leave: original full-squad prompt.
                        GameUtl.GetMessageBox().ShowSimplePrompt(
                            LocalizeOr("EVAC_PROMPT", "Evacuate Squad?"),
                            MessageBoxIcon.Question, MessageBoxButtons.YesNo,
                            OnEvacuateConfirmationResult, null, evacuateAbility);
                        return false;
                    }

                    if (!PartialEnabled)
                    {
                        // Partial evacuation turned off: preserve the 2b
                        // strict behavior (whole squad or vanilla prompts).
                        return true;
                    }

                    // PARTIAL squad evacuation (Phase 3): evacuate everyone
                    // who can leave; the rest stay in the mission.
                    int readyTotal = othersReady + 1; // + the selected actor
                    int memberTotal = evacuable.Count + 1;
                    string prompt = string.Format(
                        LocalizeOr("EVAC_PROMPT_PARTIAL",
                            "Evacuate all ready squad members? ({0} of {1} ready)"),
                        readyTotal, memberTotal);
                    GameUtl.GetMessageBox().ShowSimplePrompt(
                        prompt, MessageBoxIcon.Question, MessageBoxButtons.YesNo,
                        OnEvacuateConfirmationResult, null, evacuateAbility);
                    // Returning true keeps vanilla's single-actor prompt alive
                    // behind ours, so answering "No" still allows evacuating
                    // just the mover (vanilla behavior preserved).
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AAP] SmartEvacuation failed: {e}");
                    return true;
                }
            }

            // ── BUTTON BRANCH (postfix, NEW in Phase 3) ─────────────────
            // Fires after the player manually executed an Exit Mission /
            // Evacuate Mounted Actors ability (the 1-by-1 grind). If other
            // members are still ready to leave, offers to evacuate them all.
            // Runs AFTER vanilla finished its own processing for the
            // executed actor -- nothing of vanilla's is suppressed.
            public static void Postfix(TacticalView __instance, TacticalAbility ability)
            {
                try
                {
                    if (ModMain.Cfg?.EnableSmartEvacuation == false) return;
                    if (!PartialEnabled) return;

                    // Only the two exit abilities, only player-side actors.
                    if (!(ability is ExitMissionAbility) && !(ability is EvacuateMountedActorsAbility)) return;
                    TacticalActorBase departed = ability?.TacticalActorBase;
                    if (departed == null || departed.TacticalFaction != __instance.ViewerFaction) return;

                    // Re-entrancy / cooldown guards (see header point 4).
                    if (_massEvacInFlight) return;
                    if (Time.realtimeSinceStartup - _lastMassEvacAt < PromptCooldownSeconds) return;

                    // Only while the player's turn is still running (once
                    // the last actor leaves, the mission-end transition stops
                    // the turn and no prompt should fire).
                    if (!__instance.ViewerFaction.IsPlayingTurn) return;

                    List<MemberReadiness> others = AssessReadiness(__instance.ViewerFaction, null);
                    List<MemberReadiness> ready = others.Where(m => m.HasTargets).ToList();
                    if (ready.Count == 0) return;
                    LogReadinessTable("button-branch", others);

                    string prompt = string.Format(
                        LocalizeOr("EVAC_PROMPT_PARTIAL",
                            "Evacuate all ready squad members? ({0} of {1} ready)"),
                        ready.Count, others.Count(m => m.HasExitAbility));
                    // Park the faction for the callback (UserData stays null
                    // here -- the clicked actor already left).
                    TacticalView_OnAbilityExecuted_Patch._buttonBranchFaction = __instance.ViewerFaction;
                    GameUtl.GetMessageBox().ShowSimplePrompt(
                        prompt, MessageBoxIcon.Question, MessageBoxButtons.YesNo,
                        OnEvacuateConfirmationResult, null, null);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AAP] SmartEvacuation button-branch failed: {e.Message}");
                }
            }

            // ── Shared confirmation callback ────────────────────────────
            // UserData carries the selected actor's exit ability (move
            // branch) or null (button branch: the clicked actor already
            // left, only the remaining members are evacuated).
            public static void OnEvacuateConfirmationResult(MessageBoxCallbackResult res)
            {
                if (res.DialogResult != MessageBoxResult.Yes) return;

                TacticalAbility selectedExit = res.UserData as TacticalAbility;
                // The faction to sweep: the ability's actor's faction when
                // available, else null (callback cannot reach the view).
                TacticalFaction faction = selectedExit?.TacticalActor?.TacticalFaction;
                TacticalActor exclude = selectedExit?.TacticalActor;
                if (faction == null)
                {
                    // Button branch: faction unknown from UserData; the
                    // executor prunes non-ready members anyway, and with a
                    // null faction nothing happens -- guarded by the caller
                    // capturing the faction via a captured variable instead.
                    faction = _buttonBranchFaction;
                    exclude = null;
                }
                ExecuteMassEvacuation(selectedExit, faction, exclude);
            }

            // The button branch cannot pass the faction through the
            // message-box UserData (that slot carries the selected ability
            // in the move branch), so it is parked here right before the
            // prompt is shown.
            internal static TacticalFaction _buttonBranchFaction;
        }
    }
}
