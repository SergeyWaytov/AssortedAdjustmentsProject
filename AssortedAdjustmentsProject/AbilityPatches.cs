using Base.Core;
using Base.Defs;
using Base.Entities.Abilities;
using Base.Entities.Statuses;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Statuses;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class AbilityAdjustments
    {
        public static void Apply(DefCache cache)
        {
            // ===== Vanish (0 AP) =====
            AbilityDef vanish = cache.GetDef<TacticalAbilityDef>("Vanish_AbilityDef")
                ?? (AbilityDef)cache.GetDef<ApplyStatusAbilityDef>("Vanish_AbilityDef");
            if (vanish != null)
            {
                var t = Traverse.Create(vanish);
                t.Property("ActionPointCost")?.SetValue(0f);
                t.Field("_actionPointCost")?.SetValue(0f);
                Debug.Log("[AAP] Vanish AP cost set to 0.");
            }

            // ===== Manual Control (0.25 AP) =====
            var manualControl = cache.GetDef<TacticalAbilityDef>("ManualControl_AbilityDef");
            if (manualControl != null)
            {
                Traverse.Create(manualControl).Property("ActionPointCost")?.SetValue(0.25f);
                Debug.Log("[AAP] Manual Control AP cost set to 0.25.");
            }

            // ===== Rally (1 AP, 5 WP) =====
            var rally = cache.GetDef<TacticalAbilityDef>("Rally_AbilityDef");
            if (rally != null)
            {
                var t = Traverse.Create(rally);
                t.Property("ActionPointCost")?.SetValue(1f);
                t.Property("WillPointCost")?.SetValue(5f);
                Debug.Log("[AAP] Rally costs set to 1 AP, 5 WP.");
            }

            // ===== Rally Effect (+1 AP, +1 WP) =====
            var rallyEffect = cache.GetDef<BaseDef>("E_Status [Rally_AbilityDef]");
            if (rallyEffect != null)
            {
                var t = Traverse.Create(rallyEffect);
                t.Property("ActionPointRestoration")?.SetValue(1);
                t.Property("WillPointRestoration")?.SetValue(1);
                Debug.Log("[AAP] Rally effect set to +1 AP, +1 WP.");
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

            // ===== Regen Torso Fix =====
            var regenAbility = cache.GetDef<BaseDef>("Regeneration_Torso_Passive_AbilityDef");
            if (regenAbility != null)
            {
                Traverse.Create(regenAbility).Property("CanApplyToOffMapTarget")?.SetValue(true);
                Debug.Log("[AAP] Regen Torso can now heal while in vehicle.");
            }

            // ===== Stimpack Buff =====
            var stimpackDef = cache.GetDef<HealAbilityDef>("Stimpack_AbilityDef");
            if (stimpackDef != null)
            {
                Traverse.Create(stimpackDef).Property("ActionPointCost")?.SetValue(0.25f);
                Traverse.Create(stimpackDef).Property("HealBodyParts")?.SetValue(true);
                Traverse.Create(stimpackDef).Property("BodyPartHealAmount")?.SetValue(10.0f);
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
            var poisonStatus = cache.GetDef<BaseDef>("Poison_DamageOverTimeStatusDef");
            if (poisonStatus != null)
            {
                var tPoison = Traverse.Create(poisonStatus);
                var modsField = tPoison.Field("StatModifications");
                var statMods = modsField.GetValue<object[]>()?.ToList() ?? new List<object>();

                bool foundAcc = false, foundWP = false;
                foreach (var mod in statMods)
                {
                    var statName = Traverse.Create(mod).Field("StatName").GetValue<string>();
                    if (statName == "Accuracy")
                    {
                        Traverse.Create(mod).Field("Value").SetValue(0.5f);
                        foundAcc = true;
                    }
                    else if (statName == "WillPoints")
                    {
                        Traverse.Create(mod).Field("Value").SetValue(-3f);
                        foundWP = true;
                    }
                }

                if (!foundAcc)
                {
                    var newMod = Activator.CreateInstance(statMods.GetType().GetGenericArguments()[0]);
                    Traverse.Create(newMod).Field("StatName").SetValue("Accuracy");
                    Traverse.Create(newMod).Field("ModificationType").SetValue(1);
                    Traverse.Create(newMod).Field("Value").SetValue(0.5f);
                    statMods.Add(newMod);
                }
                if (!foundWP)
                {
                    var newMod = Activator.CreateInstance(statMods.GetType().GetGenericArguments()[0]);
                    Traverse.Create(newMod).Field("StatName").SetValue("WillPoints");
                    Traverse.Create(newMod).Field("ModificationType").SetValue(0);
                    Traverse.Create(newMod).Field("Value").SetValue(-3f);
                    statMods.Add(newMod);
                }

                modsField.SetValue(statMods.ToArray());
                Debug.Log("[AAP] Poison reworked: -50% accuracy, -3 WP per turn.");
            }

            // ===== Psychic Resistance Fix =====
            var psychicResistance = cache.GetDef<BaseDef>("PsychicResistance_AbilityDef");
            if (psychicResistance == null)
            {
                Debug.LogWarning("[AAP] PsychicResistance_AbilityDef not found in this game version. Skipping.");
            }
            else
            {
                // existing code...
            }

            // ===== Sniper Precision Shot (0 AP, 3 WP, once per turn) =====
            var sniperClass = cache.GetDef<BaseDef>("SniperSpecializationDef");
            if (sniperClass == null)
            {
                Debug.LogWarning("[AAP] Precision Shot: SniperSpecializationDef not found. Skipping.");
            }
            else
            {
                Debug.Log("[AAP] Precision Shot: Found SniperSpecializationDef. Manual implementation needed.");
                // Future: add ability to specialization or find the soldier class abilities list
            }

            // ===== Frenzy: percentage Speed boost (multiplier) =====
            var frenzyStatus = cache.GetDef<BaseDef>("Frenzy_StatusDef");
            if (frenzyStatus != null)
            {
                // Change the multiplier from default 1.5 (+50%) to 1.75 (+75%)
                Traverse.Create(frenzyStatus).Field("SpeedCoefficient")?.SetValue(1.75f);
                Traverse.Create(frenzyStatus).Field("WillpowerCoefficient")?.SetValue(1.5f);
                Traverse.Create(frenzyStatus).Field("DamageCoefficient")?.SetValue(1.5f);
                Debug.Log("[AAP] Frenzy: Speed bonus increased to +75%, Willpower/Damage kept at +50%.");
            }
            else
            {
                Debug.LogWarning("[AAP] Frenzy_StatusDef not found. Frenzy unchanged.");
            }

            // ===== Increase Max Personal Abilities from 3 to 5 =====
            var humanStatSheet = cache.GetDef<BaseDef>("HumanSoldier_BaseStatSheetDef");
            if (humanStatSheet != null)
            {
                Traverse.Create(humanStatSheet).Property("PersonalAbilitiesCount")?.SetValue(5);
                Debug.Log("[AAP] Personal abilities limit set to 5 (all classes).");
            }
            else
            {
                Debug.LogWarning("[AAP] HumanSoldier_BaseStatSheetDef not found.");
            }
        }
    }
}