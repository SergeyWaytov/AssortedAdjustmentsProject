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

            // ===== Rally Effect (ensure +1 AP, +1 WP restoration) =====
            // The Rally status is data-driven; if it exposes a StatModifications list
            // (StatsModifyStatusDef pattern) make sure +1 ActionPoints / +1 WillPoints
            // entries exist. The old ActionPointRestoration/WillPointRestoration
            // properties do not exist in the game code and never applied.
            var rallyEffect = cache.GetDef<BaseDef>("E_Status [Rally_AbilityDef]");
            if (rallyEffect != null)
            {
                EnsureStatModification(rallyEffect, "ActionPoints", 1f);
                EnsureStatModification(rallyEffect, "WillPoints", 1f);
                Debug.Log("[AAP] Rally effect: ActionPoints/WillPoints +1 entries ensured.");
            }
            else
            {
                Debug.LogWarning("[AAP] Rally effect def not found.");
            }

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

            // ===== Psychic Resistance Fix =====
            var psychicResistance = cache.GetDef<BaseDef>("PsychicResistance_AbilityDef");
            if (psychicResistance == null)
            {
                Debug.LogWarning("[AAP] PsychicResistance_AbilityDef not found in this game version. Skipping.");
            }

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

        /// <summary>
        /// Ensures an Add-type StatModification entry exists on a def that has a
        /// "StatModifications" list field (StatsModifyStatusDef pattern).
        /// </summary>
        private static void EnsureStatModification(BaseDef target, string statName, float value)
        {
            try
            {
                var t = Traverse.Create(target).Field("StatModifications");
                if (t == null || !t.FieldExists()) return;
                var list = t.GetValue() as System.Collections.IList;
                if (list == null) return;

                // Update existing entry if present
                foreach (var mod in list)
                {
                    var mt = Traverse.Create(mod);
                    if (mt.Field("StatName")?.GetValue<string>() == statName)
                    {
                        mt.Field("Value")?.SetValue(value);
                        return;
                    }
                }

                // Append a new entry using an existing element as template
                if (list.Count > 0)
                {
                    var template = list[0];
                    var newMod = Activator.CreateInstance(template.GetType());
                    var nt = Traverse.Create(newMod);
                    nt.Field("Modification")?.SetValue(StatModificationType.Add);
                    nt.Field("StatName")?.SetValue(statName);
                    nt.Field("Value")?.SetValue(value);
                    list.Add(newMod);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] EnsureStatModification({target?.name}, {statName}) failed: {e.Message}");
            }
        }
    }

    // ================================================================
    // POISON REWORK ENFORCER (runtime)
    // Applies -50% Accuracy (multiply 0.5) and -3 WP (add) while the
    // actor has the Poison damage-over-time status, and removes the
    // modifications when the status is gone. Replaces the removed
    // PoisonReworkEnforcer.cs which patched a non-existent method.
    // ================================================================
    [HarmonyPatch(typeof(Status), "OnApply")]
    public static class PoisonRework_OnApply_Patch
    {
        static void Postfix(Status __instance)
        {
            try
            {
                if (__instance?.Def?.name != "Poison_DamageOverTimeStatusDef") return;
                var actor = __instance.Target as TacticalActor;
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
                var actor = __instance.Target as TacticalActor;
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
