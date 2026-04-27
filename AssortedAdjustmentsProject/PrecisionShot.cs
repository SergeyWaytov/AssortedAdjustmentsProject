using Base.Core;
using Base.Defs;
using Base.Entities.Abilities;
using Base.Entities.Statuses;
using Base.UI;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Statuses;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class PrecisionShot
    {
        private const string PrecisionShotGuid = "a1b2c3d4-e5f6-4789-abcd-ef0123456789";
        private const string PrecisionCostGuid = "b2c3d4e5-f6a7-4b89-bcde-f01234567890";
        private const string PrecisionStatusGuid = "c3d4e5f6-a7b8-4c9a-cdef-012345678901";
        private const string PrecisionAccGuid = "d4e5f6a7-b8c9-4d0a-defa-012345678901";

        public static void Apply(DefCache cache)
        {
            var quickAim = cache.GetDef<ApplyStatusAbilityDef>("QuickAim_AbilityDef");
            if (quickAim == null) return;

            var precisionShot = Helpers.CreateDefFromClone(quickAim, PrecisionShotGuid,
                "AAP_PrecisionShot_AbilityDef") as ApplyStatusAbilityDef;
            if (precisionShot == null) return;

            // Clone the view element so we don't corrupt Quick Aim
            precisionShot.ViewElementDef = Helpers.CreateDefFromClone(
                quickAim.ViewElementDef,
                "e5f6a7b8-c9d0-4e1a-9bcd-ef0123456789",
                "E_ViewElement [AAP_PrecisionShot_AbilityDef]") as TacticalAbilityViewElementDef;

            // Force localisation keys
            precisionShot.ViewElementDef.DisplayName1.LocalizationKey = "AAP_PRECISION_SHOT";
            precisionShot.ViewElementDef.Description.LocalizationKey = "AAP_PRECISION_SHOT_DESC";

            // Configure the ability
            precisionShot.ActionPointCost = 0f;
            precisionShot.WillPointCost = 4f;
            precisionShot.UsesPerTurn = 1;
            precisionShot.ShowNotificationOnUse = true;

            // Cost modifier
            var qaCostMod = cache.GetDef<ChangeAbilitiesCostStatusDef>(
                "E_AbilityCostModifier [QuickAim_AbilityDef]");
            var psCostMod = Helpers.CreateDefFromClone(qaCostMod, PrecisionCostGuid,
                "E_AbilityCostModifier [AAP_PrecisionShot_AbilityDef]") as ChangeAbilitiesCostStatusDef;
            psCostMod.AbilityCostModification.ActionPointMod = -10f;
            psCostMod.Visuals = Helpers.CreateDefFromClone(
                precisionShot.ViewElementDef,
                "c9d0e1f2-a3b4-4c5d-9e0f-abcdef012345",
                "E_Visuals [AAP_PrecisionShot_CostMod]") as ViewElementDef;

            // Accuracy multiplier (+20%)
            var trembling = cache.GetDef<StatMultiplierStatusDef>("Trembling_StatusDef");
            StatMultiplierStatusDef psAccMod = null;
            if (trembling != null)
            {
                psAccMod = Helpers.CreateDefFromClone(trembling, PrecisionAccGuid,
                    "E_AccuracyMultiplier [AAP_PrecisionShot_AbilityDef]") as StatMultiplierStatusDef;
                psAccMod.StatsMultipliers[0].Multiplier = 1.2f;
                psAccMod.EffectName = string.Empty;
                psAccMod.ShowNotification = false;
                psAccMod.VisibleOnHealthbar = 0;
                psAccMod.VisibleOnStatusScreen = 0;
                psAccMod.Visuals = null;
            }

            // Boost status
            var qaBoost = cache.GetDef<AddAttackBoostStatusDef>("E_Status [QuickAim_AbilityDef]");
            var psBoost = Helpers.CreateDefFromClone(qaBoost, PrecisionStatusGuid,
                "E_Status [AAP_PrecisionShot_AbilityDef]") as AddAttackBoostStatusDef;

            var additionalStatuses = new List<TacStatusDef> { psCostMod };
            if (psAccMod != null) additionalStatuses.Add(psAccMod);
            psBoost.AdditionalStatusesToApply = additionalStatuses.ToArray();

            psBoost.Visuals = Helpers.CreateDefFromClone(
                precisionShot.ViewElementDef,
                "b8c9d0e1-f2a3-4b4c-9d5e-0f1234567890",
                "E_Visuals [AAP_PrecisionShot_Status]") as ViewElementDef;
            psBoost.ShowNotification = true;

            precisionShot.StatusDef = psBoost;

            // === MODIFY SNIPER CLASS ===
            var sniperClass = cache.GetDef<ClassProficiencyAbilityDef>("Sniper_ClassProficiency_AbilityDef");
            if (sniperClass != null)
            {
                var abilities = sniperClass.AbilityDefs?.ToList() ?? new List<AbilityDef>();

                // PistolProficiency mod may have removed Quick Aim – add it back if missing
                if (quickAim != null && !abilities.Contains(quickAim))
                {
                    abilities.Insert(0, quickAim);   // put it at the top (or wherever you like)
                }

                if (!abilities.Contains(precisionShot))
                {
                    abilities.Add(precisionShot);
                }

                sniperClass.AbilityDefs = abilities.ToArray();
                Debug.Log("[AAP] Sniper abilities after fix:");
                foreach (var a in sniperClass.AbilityDefs) Debug.Log("  " + a.name);
            }
        }
    }
}