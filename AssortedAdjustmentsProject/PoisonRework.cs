// PoisonRework.cs -- AAP Phase 3 poison system (NEW FILE).
// ----------------------------------------------------------------------------
// Why this file exists (the PsychicBuff <-> Poison interaction):
//
// The old poison rework applied BOTH penalties as StatModifications through
// StatusStat.ApplyStatModification:
//     Accuracy : Multiply 0.5   (-50% accuracy)
//     WillPoints : Add -3       (-3 WP)
//
// But WillPoints stat-modifications are the EXACT seam the psychic defense
// (PsychicBuffManager.PsychicWillpointLoss_Defense_V3, a prefix on the same
// ApplyStatModification method) monitors. V3 inspects every negative WP
// modification, tries to attribute it to a psychic caster, and can halve or
// null it. A poison-sourced modification (source = the poison StatusDef)
// gave V3 no caster it recognized, so it fell through to a scan of the
// ACTOR'S OWN statuses -- and if the actor happened to also carry a
// PsychicScream/MindCrush status, the poison's -3 WP got misattributed to
// the screamer and halved or blocked. Two systems fighting over one hook.
//
// Phase 3 orthogonalization:
//   - Accuracy keeps its Multiply stat-mod (V3 only inspects WillPoints, so
//     there is no conflict).
//   - The -3 WP is now a DIRECT current-WP deduction via
//     WillPoints.Add(-3f) (the TFTV-confirmed mechanism for in-combat WP
//     changes). It never passes through ApplyStatModification, so the
//     psychic defense never sees it. Poison and psychic systems no longer
//     share a seam.
//   - Saves made under the old build may still carry serialized poison
//     stat-mods; OnApply scrubs them (RemoveStatModificationsWithSource is
//     a no-op when nothing matches) so old saves don't double-penalize.
//   - The exact deducted amount is recorded per actor and restored on
//     OnUnapply, so a WP that was already at 0 when poisoned is not
//     "refunded" beyond what was actually taken.
//
// Config: "Poison rework" option (AAPConfig.EnablePoisonRework, default on).
// Read at runtime -- no restart needed; affects new poison applications.
//
// This file is NEW in Phase 3: drop it into the AssortedAdjustmentsProject
// folder -- the updated AssortedAdjustmentsProject.csproj already includes
// it, and the updated AbilityPatches.cs no longer contains the old poison
// runtime (moved here).
//
// PHASE 3.1 BUILD FIX (from the first VS build attempt):
//   (a) StatModificationTarget did not resolve with the original import
//       list. The enum lives in the game's shared/common layer; the import
//       set below is the EXACT set the Phase 2b AbilityPatches.cs poison
//       enforcer compiled against (it used the same enum in the user's
//       environment), so it is proven to resolve every type this file
//       references.
//   (b) PruneStale was private while being called from the sibling patch
//       classes (PoisonRework_OnApply_Patch) -- now internal.
// ----------------------------------------------------------------------------
using Base.Core;
using Base.Defs;
using Base.Entities.Abilities;
using Base.Entities.Statuses;
using HarmonyLib;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Statuses;
using PhoenixPoint.Tactical.Levels;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    internal static class PoisonRework
    {
        internal const string PoisonDefName = "Poison_DamageOverTimeStatusDef";

        // Per-actor bookkeeping: how much current WP we actually deducted
        // (less than 3 when the actor was nearly drained), plus the tactical
        // level the deduction happened on, so stale entries are pruned when
        // a new mission starts.
        private static readonly Dictionary<TacticalActor, PoisonLedgerEntry> _ledger
            = new Dictionary<TacticalActor, PoisonLedgerEntry>();

        private sealed class PoisonLedgerEntry
        {
            public float Deducted;
            public TacticalLevelController Level;
        }

        internal static bool Enabled => ModMain.Cfg?.EnablePoisonRework != false;

        /// <summary>
        /// Prunes ledger entries from finished missions. Internal (3.1):
        /// called from the sibling patch classes in this file, which are
        /// separate classes -- private made the call inaccessible.
        /// </summary>
        internal static void PruneStale(TacticalLevelController currentLevel)
        {
            if (currentLevel == null) return;
            List<TacticalActor> stale = null;
            foreach (var kv in _ledger)
            {
                if (kv.Value.Level != currentLevel)
                    (stale ?? (stale = new List<TacticalActor>())).Add(kv.Key);
            }
            if (stale == null) return;
            foreach (TacticalActor key in stale) _ledger.Remove(key);
            Seeker.Log($"PoisonRework: pruned {stale.Count} stale ledger entr(ies) from finished missions.");
        }

        internal static bool HasDeduction(TacticalActor actor)
            => _ledger.ContainsKey(actor);

        internal static void RecordDeduction(TacticalActor actor, float deducted)
            => _ledger[actor] = new PoisonLedgerEntry { Deducted = deducted, Level = actor.TacticalLevel };

        /// <summary>Takes (removes) the recorded deduction, or null.</summary>
        internal static float? TakeDeduction(TacticalActor actor)
        {
            if (!_ledger.TryGetValue(actor, out PoisonLedgerEntry entry)) return null;
            _ledger.Remove(actor);
            return entry.Deducted;
        }

        /// <summary>
        /// Removes stat-mods the OLD (pre-Phase-3) build serialized onto the
        /// actor: the legacy shape applied BOTH the Accuracy multiply and the
        /// WillPoints add as stat-mods sourced from the poison StatusDef.
        /// RemoveStatModificationsWithSource is a no-op when nothing matches,
        /// so this is safe to call on every apply -- and it cleans a save from
        /// the old build the first time the poison status refreshes.
        /// </summary>
        internal static void ScrubLegacy(TacticalActor actor, StatusDef poisonDef)
        {
            try
            {
                actor.CharacterStats.TryGetStat(StatModificationTarget.Accuracy)
                    ?.RemoveStatModificationsWithSource(poisonDef, true);
                actor.CharacterStats.TryGetStat(StatModificationTarget.WillPoints)
                    ?.RemoveStatModificationsWithSource(poisonDef, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AAP] PoisonRework legacy scrub failed: " + e.Message);
            }
        }
    }

    // ================================================================
    // ON APPLY: deduct 3 current WP (direct, psychic-orthogonal) and
    // apply the -50% accuracy stat-mod.
    // ================================================================
    [HarmonyPatch(typeof(Status), "OnApply")]
    public static class PoisonRework_OnApply_Patch
    {
        static void Postfix(Status __instance)
        {
            try
            {
                if (__instance?.Def?.name != PoisonRework.PoisonDefName) return;
                TacticalActor actor = (__instance as TacStatus)?.TacticalActor;
                if (actor == null) return;

                // Save-compat: strip any stat-mods the OLD build applied
                // (also the correct behavior when the rework is disabled).
                PoisonRework.ScrubLegacy(actor, __instance.Def);

                if (!PoisonRework.Enabled) return;
                PoisonRework.PruneStale(actor.TacticalLevel);

                // Accuracy: -50% via Multiply stat-mod (the psychic defense
                // only inspects WillPoints, so this stays conflict-free).
                var accStat = actor.CharacterStats.TryGetStat(StatModificationTarget.Accuracy);
                accStat?.RemoveStatModificationsWithSource(__instance.Def, true);
                accStat?.AddStatModification(new StatModification(
                    StatModificationType.Multiply, StatModificationTarget.Accuracy.ToString(), 0.5f, __instance.Def, 0f), true);

                // WillPoints: DIRECT current-WP deduction -- never routed
                // through ApplyStatModification, so the psychic defense
                // cannot see or modify it (Phase 3 orthogonalization).
                if (PoisonRework.HasDeduction(actor))
                {
                    // Already deducted this session (poison refreshed / save
                    // reloaded in the same session): do not stack.
                    Seeker.Log($"PoisonRework: poison re-applied to {actor.DisplayName} while a deduction is already active -- skipped (no stacking).");
                    return;
                }

                float wpBefore = actor.CharacterStats.WillPoints.Value;
                actor.CharacterStats.WillPoints.Add(-3f);
                float wpAfter = actor.CharacterStats.WillPoints.Value;
                float actual = Mathf.Max(0f, wpBefore - wpAfter);
                PoisonRework.RecordDeduction(actor, actual);
                Debug.Log($"[AAP] Poison debuff applied to {actor.DisplayName} (-50% Acc, WP {wpBefore:F0} -> {wpAfter:F0}, refundable: {actual:F0}).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] PoisonRework OnApply failed: {e.Message}");
            }
        }
    }

    // ================================================================
    // ON UNAPPLY: refund the exact deducted WP and remove the accuracy
    // stat-mod.
    // ================================================================
    [HarmonyPatch(typeof(Status), "OnUnapply")]
    public static class PoisonRework_OnUnapply_Patch
    {
        static void Postfix(Status __instance)
        {
            try
            {
                if (__instance?.Def?.name != PoisonRework.PoisonDefName) return;
                TacticalActor actor = (__instance as TacStatus)?.TacticalActor;
                if (actor == null) return;

                // Always remove the accuracy stat-mod (no-op when the
                // rework was disabled and none was applied).
                actor.CharacterStats.TryGetStat(StatModificationTarget.Accuracy)
                    ?.RemoveStatModificationsWithSource(__instance.Def, true);

                float? deducted = PoisonRework.TakeDeduction(actor);
                if (deducted == null)
                {
                    // Nothing recorded this session (e.g. game restarted on a
                    // poisoned save -- a fresh OnApply re-deducted, or the
                    // rework was off). Nothing to refund.
                    Seeker.Log($"PoisonRework: poison removed from {actor.DisplayName}; no session deduction to refund.");
                    return;
                }

                float wpBefore = actor.CharacterStats.WillPoints.Value;
                actor.CharacterStats.WillPoints.Add(deducted.Value);
                float wpAfter = actor.CharacterStats.WillPoints.Value;
                Debug.Log($"[AAP] Poison debuff removed from {actor.DisplayName} (refunded {deducted.Value:F0} WP, {wpBefore:F0} -> {wpAfter:F0}).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] PoisonRework OnUnapply failed: {e.Message}");
            }
        }
    }
}
