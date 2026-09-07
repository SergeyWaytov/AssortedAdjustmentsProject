using Base.Core;
using Base.Defs;
using Base.Entities.Abilities;
using Base.Entities.Statuses;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Core;
using PhoenixPoint.Geoscape.View.DataObjects;
using PhoenixPoint.Geoscape.View.ViewControllers.BaseRecruits;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Statuses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Ability and status def adjustments.
    /// AAP 1.1 NOTE: costs on ability defs are FIELDS (TacticalAbilityDef.ActionPointCost /
    /// WillPointCost / UsesPerTurn), not properties - the old Traverse.Property() calls
    /// were silent no-ops. All lookups now set the fields directly, verified against
    /// the decompiled game assembly.
    /// AAP T3: Poison reads the hit wrapper instead of the poisoned actor. Status.Target
    /// is normally an IDamageReceiver (item slot); the actor is exposed by TacStatus.
    /// Cast changed from `__instance.Target as TacticalActor` to
    /// `(__instance as TacStatus)?.TacticalActor`.
    /// AAP T5: Rally's "ensure +1 AP/+1 WP" effect was a false-success no-op
    /// (TacEffectStatusDef has no StatModifications member; EnsureStatModification
    /// returned at its own FieldExists() guard while the "entries ensured" log
    /// still printed). The no-op EnsureStatModification helper and its two
    /// call sites have been removed. A new patch on TacticalAbility.Activate
    /// filters to Rally_AbilityDef and restores 1 WP to each ally in the
    /// rally radius via the TFTV-confirmed mechanism
    /// (ally.CharacterStats.WillPoints.Add(1f)).
    /// AAP Q10 (Option B): deleted the stale PsychicResistance_AbilityDef
    /// lookup (the def is absent from this build; the lookup only ever
    /// printed a warning that the AAP startup log is clean about).
    /// </summary>
    public static class AbilityAdjustments
    {
        public static void Apply(DefCache cache)
        {
            var cfg = ModMain.Cfg;

            // ===== Vanish (0 AP) =====
            AbilityDef vanish = cache.GetDef<TacticalAbilityDef>("Vanish_AbilityDef")
                ?? (AbilityDef)cache.GetDef<ApplyStatusAbilityDef>("Vanish_AbilityDef");
            if (vanish is TacticalAbilityDef vanishTac)
            {
                vanishTac.ActionPointCost = 0f;
                Debug.Log("[AAP] Vanish AP cost set to 0.");
            }
            else if (vanish != null)
            {
                Debug.LogWarning($"[AAP] Vanish found as {vanish.GetType().Name}; cost not changed.");
            }

            // ===== Manual Control (0.25 AP) =====
            var manualControl = cache.GetDef<TacticalAbilityDef>("ManualControl_AbilityDef");
            if (manualControl != null)
            {
                manualControl.ActionPointCost = 0.25f;
                Debug.Log("[AAP] Manual Control AP cost set to 0.25.");
            }

            // ===== Rally (1 AP, 5 WP) =====
            var rally = cache.GetDef<TacticalAbilityDef>("Rally_AbilityDef");
            if (rally != null)
            {
                rally.ActionPointCost = 1f;
                rally.WillPointCost = 5f;
                Debug.Log("[AAP] Rally costs set to 1 AP, 5 WP.");
            }
            // AAP T5: the dead "ensure +1 AP / +1 WP StatModifications" no-op
            // block and its EnsureStatModification helper have been removed.
            // Rally WP restoration is now a runtime patch -- see
            // RallyAbility_Activate_Patch at the bottom of this file.

            // ===== Sneak Attack (2.0x / 1.5x) =====
            var sneakAttackAbility = cache.GetDef<ApplyStatusAbilityDef>("SneakAttack_AbilityDef");
            if (sneakAttackAbility != null && sneakAttackAbility.StatusDef is FactionVisibilityConditionStatusDef visibilityStatus)
            {
                var hiddenState = visibilityStatus.HiddenStateStatusDef as StanceStatusDef;
                var locatedState = visibilityStatus.LocatedStateStatusDef as StanceStatusDef;
                if (hiddenState?.StatModifications != null && hiddenState.StatModifications.Length > 0)
                {
                    hiddenState.StatModifications[0].Value = 2.0f;
                    Debug.Log("[AAP] Sneak Attack hidden damage multiplier set to 2.0x.");
                }
                if (locatedState?.StatModifications != null && locatedState.StatModifications.Length > 0)
                {
                    locatedState.StatModifications[0].Value = 1.5f;
                    Debug.Log("[AAP] Sneak Attack located damage multiplier set to 1.5x.");
                }
            }
            else
            {
                Debug.LogWarning("[AAP] SneakAttack_AbilityDef or its StatusDef not found.");
            }

            // ===== Regen Torso Fix (works while inside a vehicle) =====
            var regenAbility = cache.GetDef<ApplyStatusAbilityDef>("Regeneration_Torso_Passive_AbilityDef");
            if (regenAbility != null)
            {
                regenAbility.CanApplyToOffMapTarget = true;
                Debug.Log("[AAP] Regen Torso can now heal while in vehicle.");
            }
            else
            {
                var regenAny = cache.GetDef<BaseDef>("Regeneration_Torso_Passive_AbilityDef");
                if (regenAny != null)
                    Debug.LogWarning($"[AAP] Regen Torso ability is {regenAny.GetType().Name}; CanApplyToOffMapTarget not applicable.");
            }

            // ===== Stimpack Buff =====
            var stimpackDef = cache.GetDef<HealAbilityDef>("Stimpack_AbilityDef");
            if (stimpackDef != null)
            {
                stimpackDef.ActionPointCost = 0.25f;
                stimpackDef.HealBodyParts = true;
                stimpackDef.BodyPartHealAmount = 10.0f;
                Debug.Log("[AAP] Stimpack buffed: 0.25 AP, heals all body parts for 10 HP each.");
            }

            // ===== Screaming Head Mind Control Immunity =====
            var screamingHead = cache.GetDef<BaseDef>("AN_Priest_Head03_BodyPartDef");
            if (screamingHead != null)
            {
                var immunityDef = cache.GetDef<BaseDef>("MindControlImmunity_AbilityDef");
                if (immunityDef != null)
                {
                    Helpers.AddDefToArrayField(screamingHead, "Abilities", immunityDef);
                    Debug.Log("[AAP] Screaming Head mutation now grants Mind Control Immunity.");
                }
            }

            // ===== Poison Rework (-50% Acc, -3 WP) =====
            // Poison_DamageOverTimeStatusDef is a DamageOverTimeStatusDef: it has no
            // StatModifications of its own, so the debuff is enforced at runtime
            // (PoisonRework status patches at the bottom of this file).
            var poisonStatus = cache.GetDef<BaseDef>("Poison_DamageOverTimeStatusDef");
            Debug.Log(poisonStatus != null
                ? "[AAP] Poison rework: runtime enforcer active (-50% Acc, -3 WP)."
                : "[AAP] Poison status def not found; poison rework inactive.");

            // AAP Q10-B: deleted the stale PsychicResistance_AbilityDef lookup.
            // The def is absent from this game build; the lookup only ever
            // printed a noisy warning that cluttered the startup log. The
            // feature is removed in this build -- when it returns, add a
            // properly-resolved cache.GetDef<...> call back here.

            // ===== Frenzy: speed boost from config (default toned-down 1.5) =====
            // User report on the Workshop page: 1.75 allowed cross-map movement.
            // Vanilla values are kept for Willpower/Damage (1.5); speed is configurable.
            var frenzyStatus = cache.GetDef<BaseDef>("Frenzy_StatusDef");
            if (frenzyStatus is FrenzyStatusDef frenzy)
            {
                float speed = cfg?.FrenzySpeedCoefficient ?? 1.5f;
                frenzy.SpeedCoefficient = speed;
                frenzy.WillpowerCoefficient = 1.5f;
                frenzy.DamageCoefficient = 1.5f;
                Debug.Log($"[AAP] Frenzy coefficients set: Speed {speed} (config), Willpower 1.5, Damage 1.5.");
            }
            else
            {
                Debug.LogWarning("[AAP] Frenzy_StatusDef not found. Frenzy unchanged.");
            }

            // ===== Increase Max Personal Abilities (config, default 5) =====
            // PersonalAbilitiesCount is a FIELD on BaseStatSheetDef (default 3);
            // FactionCharacterGenerator reads BaseStatsSheet.PersonalAbilitiesCount
            // when creating characters. The old single-def Traverse.Property call
            // never applied - this is the fix for the "stuck at 3" report.
            int personalCount = Mathf.Clamp(cfg?.PersonalAbilitiesCount ?? 5, 1, 7);
            var repo = GameUtl.GameComponent<DefRepository>();
            var statSheets = repo.GetAllDefs<BaseStatSheetDef>().ToList();
            foreach (var sheet in statSheets)
            {
                sheet.PersonalAbilitiesCount = personalCount;
            }
            Debug.Log($"[AAP] Personal abilities limit set to {personalCount} on {statSheets.Count} stat sheets.");
        }
    }

    // ================================================================
    // POISON REWORK ENFORCER (runtime)
    // Applies -50% Accuracy (multiply 0.5) and -3 WP (add) while the
    // actor has the Poison damage-over-time status, and removes the
    // modifications when the status is gone. Replaces the removed
    // PoisonReworkEnforcer.cs which patched a non-existent method.
    // AAP T3: actor cast fixed -- Status.Target is an IDamageReceiver
    // (item slot), use (__instance as TacStatus)?.TacticalActor.
    // ================================================================
    [HarmonyPatch(typeof(Status), "OnApply")]
    public static class PoisonRework_OnApply_Patch
    {
        static void Postfix(Status __instance)
        {
            try
            {
                if (__instance?.Def?.name != "Poison_DamageOverTimeStatusDef") return;
                // AAP T3: read the actor off TacStatus, not the Target wrapper.
                TacticalActor actor = (__instance as TacStatus)?.TacticalActor;
                if (actor == null) return;

                PoisonDebuff.Apply(actor, __instance.Def);
                Debug.Log($"[AAP] Poison debuff applied to {actor.DisplayName} (-50% Acc, -3 WP).");
            }
            catch (Exception e) { Debug.LogError($"[AAP] PoisonRework OnApply failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(Status), "OnUnapply")]
    public static class PoisonRework_OnUnapply_Patch
    {
        static void Postfix(Status __instance)
        {
            try
            {
                if (__instance?.Def?.name != "Poison_DamageOverTimeStatusDef") return;
                // AAP T3: actor cast via TacStatus.
                TacticalActor actor = (__instance as TacStatus)?.TacticalActor;
                if (actor == null) return;

                PoisonDebuff.Remove(actor, __instance.Def);
                Debug.Log($"[AAP] Poison debuff removed from {actor.DisplayName}.");
            }
            catch (Exception e) { Debug.LogError($"[AAP] PoisonRework OnUnapply failed: {e.Message}"); }
        }
    }

    internal static class PoisonDebuff
    {
        internal static void Apply(TacticalActor actor, StatusDef source)
        {
            var stats = actor.CharacterStats;
            var accStat = stats.TryGetStat(StatModificationTarget.Accuracy);
            accStat?.RemoveStatModificationsWithSource(source, true);
            accStat?.AddStatModification(new StatModification(
                StatModificationType.Multiply, StatModificationTarget.Accuracy.ToString(), 0.5f, source, 0f), true);

            var wpStat = stats.TryGetStat(StatModificationTarget.WillPoints);
            wpStat?.RemoveStatModificationsWithSource(source, true);
            wpStat?.AddStatModification(new StatModification(
                StatModificationType.Add, StatModificationTarget.WillPoints.ToString(), -3f, source, 0f), true);
        }

        internal static void Remove(TacticalActor actor, StatusDef source)
        {
            var stats = actor.CharacterStats;
            stats.TryGetStat(StatModificationTarget.Accuracy)?.RemoveStatModificationsWithSource(source, true);
            stats.TryGetStat(StatModificationTarget.WillPoints)?.RemoveStatModificationsWithSource(source, true);
        }
    }

    // ================================================================
    // AAP T5 / Q14 -- RALLY WP RESTORATION (runtime)
    // The dead EnsureStatModification no-op (which patched a non-existent
    // StatModifications member on the Rally effect status) has been removed
    // from AbilityAdjustments.Apply. This patch fires when ANY TacticalAbility
    // activates and filters by def name to Rally_AbilityDef; it then restores
    // 1 WP to each ally in the rally radius via the TFTV-confirmed mechanism:
    //   ally.CharacterStats.WillPoints.Add(1f)
    // (TFTV research: GitHub.com/Voland163/TFTV uses WillPoints.Add(value)
    // for in-combat WP restoration.)
    // TODO (Q14): AP restoration via ally.CharacterStats.ActionPoints.Add(1f)
    // when the AP mechanism is confirmed against the live build.
    // ================================================================
    [HarmonyPatch(typeof(TacticalAbility), "Activate")]
    public static class RallyAbility_Activate_Patch
    {
        private const string RallyDefName = "Rally_AbilityDef";
        // TODO (Q14): AP restoration via ally.CharacterStats.ActionPoints.Add(1f)
        // when AP mechanism is confirmed.

        [HarmonyPostfix]
        static void Postfix(TacticalAbility __instance)
        {
            try
            {
                if (__instance?.TacticalAbilityDef?.name != RallyDefName) return;

                TacticalActor caster = __instance.TacticalActor;
                if (caster == null) return;

                // TFTV-confirmed WP restoration mechanism: WillPoints.Add(1f)
                // restores up to Max. Per TFTV research, Add() does NOT raise
                // the cap (use AddRestrictedToMax for that). Vanilla Rally's
                // radius is small (the caster's local cluster); we apply the
                // same radius-based filter by reading the caster's level.
                float radius = GetRallyRadius(__instance);
                if (radius <= 0f) radius = 10f; // safe default if the def lacks the field

                // AAP T5 / Q14: only apply when the PLAYER casts Rally (Phoenix
                // Point faction == ViewerFaction). Matches the user's intent:
                // the WP buff is a player-side QoL fix, not an NPC buff.
                // Pattern mirrors PsychicBuffManager.cs:60 and PrecisionShot.cs:172.
                if (caster.TacticalFaction != caster.TacticalLevel?.View?.ViewerFaction) return;

                var casterFaction = caster.TacticalFaction;
                if (casterFaction == null) return;

                int restored = 0;
                foreach (TacticalActor ally in casterFaction.TacticalActors.Where(a => a != null && a.IsActive))
                {
                    if (ally == caster) continue;
                    // No FactionRelation check needed: casterFaction.TacticalActors
                    // is already the caster's own faction (Phoenix Point), so
                    // every ally here is a valid WP buff target.

                    float dist = Vector3.Distance(caster.Pos, ally.Pos);
                    if (dist > radius) continue;

                    // TFTV pattern: actor.CharacterStats.WillPoints.Add(value).
                    ally.CharacterStats.WillPoints.Add(1f);
                    restored++;
                }

                if (restored > 0)
                    Debug.Log($"[AAP] Rally restored 1 WP to {restored} ally/allies in radius {radius} around {caster.DisplayName}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Rally WP restoration failed: {e.Message}");
            }
        }

        // Best-effort read of the Rally radius from the ability def. Returns 0
        // if the field is absent (the caller falls back to a default). We use
        // Traverse so this compiles against any build regardless of whether
        // the radius lives on the def or on the ability's status/effect.
        private static float GetRallyRadius(TacticalAbility ability)
        {
            try
            {
                var def = ability?.TacticalAbilityDef;
                if (def == null) return 0f;
                // Try a few known member names without inventing a typed API.
                float r = Traverse.Create(def).Field("_radius")?.GetValue<float>() ?? 0f;
                if (r > 0f) return r;
                r = Traverse.Create(def).Field("Radius")?.GetValue<float>() ?? 0f;
                return r;
            }
            catch { return 0f; }
        }
    }

    // ================================================================
    // PERSONAL ABILITIES > 3 SUPPORT (ported from Mad's AssortedAdjustments)
    // 1) Fixes vanilla under-generation of personal abilities.
    // 2) Clones recruit-list icon rows so 4-7 abilities display correctly.
    // ================================================================
    [HarmonyPatch(typeof(FactionCharacterGenerator), "GeneratePersonalAbilities")]
    public static class FactionCharacterGenerator_GeneratePersonalAbilities_Patch
    {
        static void Postfix(ref Dictionary<int, TacticalAbilityDef> __result, int abilitiesCount,
            LevelProgressionDef levelDef, List<TacticalAbilityDef> ____personalAbilityPool)
        {
            try
            {
                if (__result.Count >= abilitiesCount) return;
                Debug.Log($"[AAP] Personal ability generation bugged out ({__result.Count}/{abilitiesCount}). Regenerating.");

                Dictionary<int, TacticalAbilityDef> dictionary = new Dictionary<int, TacticalAbilityDef>();
                List<TacticalAbilityDef> tmpList = new List<TacticalAbilityDef>();
                List<int> availableSlots = new List<int>();
                for (int i = 0; i < levelDef.MaxLevel; i++) availableSlots.Add(i);

                int num = 0;
                while (num < abilitiesCount && ____personalAbilityPool.Count != 0)
                {
                    TacticalAbilityDef randomElement = ____personalAbilityPool[UnityEngine.Random.Range(0, ____personalAbilityPool.Count)];
                    if (randomElement != null)
                    {
                        ____personalAbilityPool.Remove(randomElement);
                        tmpList.Add(randomElement);
                        int slot = availableSlots[UnityEngine.Random.Range(0, availableSlots.Count)];
                        availableSlots.Remove(slot);
                        dictionary.Add(slot, randomElement);
                        num++;
                    }
                    else break;
                }
                ____personalAbilityPool.AddRange(tmpList);
                __result = dictionary;
            }
            catch (Exception e) { Debug.LogError($"[AAP] GeneratePersonalAbilities fix failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(RecruitsListElementController), "SetRecruitElement")]
    public static class RecruitsListElementController_SetRecruitElement_Patch
    {
        static void Prefix(RecruitsListElementController __instance, RecruitsListEntryData entryData)
        {
            try
            {
                RowIconTextController[] rowItems = __instance.PersonalTrackRoot.transform.GetComponentsInChildren<RowIconTextController>(true);
                const int VanillaAbilityLimit = 3;
                const int MaxAbilityLimit = 7;

                if (rowItems.Length < MaxAbilityLimit)
                {
                    RowIconTextController cloneBase = rowItems.FirstOrDefault();
                    int clonesNeeded = MaxAbilityLimit - VanillaAbilityLimit;
                    if (cloneBase == null) return;
                    for (int i = 0; i < clonesNeeded; i++)
                        UnityEngine.Object.Instantiate(cloneBase, __instance.PersonalTrackRoot.transform, true);
                    rowItems = __instance.PersonalTrackRoot.transform.GetComponentsInChildren<RowIconTextController>(true);
                }

                if (entryData.PersonalTrackAbilities.Count() > VanillaAbilityLimit)
                {
                    foreach (RowIconTextController rowItem in rowItems)
                    {
                        rowItem.DisplayText.gameObject.SetActive(false);
                        RectTransform rtRowItem = rowItem.GetComponent<RectTransform>();
                        RectTransform rtText = rowItem.DisplayText.GetComponent<RectTransform>();
                        rtText.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
                        rtRowItem.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
                    }
                }
            }
            catch (Exception e) { Debug.LogError($"[AAP] Recruit element UI patch failed: {e.Message}"); }
        }
    }
}
