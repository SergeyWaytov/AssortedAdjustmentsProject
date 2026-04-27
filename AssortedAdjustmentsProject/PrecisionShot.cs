using Base.Core;
using Base.Defs;
using Base.Entities.Abilities;
using Base.Entities.Statuses;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Statuses;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Standalone implementation of "Precision Shot":
    /// - 0 AP / 3 WP, once per turn
    /// - Applies a status that makes the next attack cost 0 AP
    /// - Granted to any character with the Sniper class
    /// - Uses the same pattern as vanilla Quick Aim
    /// </summary>
    public static class PrecisionShot
    {
        // ----- GUIDs -----
        private const string PrecisionShotGuid = "a1b2c3d4-e5f6-4789-abcd-ef0123456789";
        private const string PrecisionCostGuid = "b2c3d4e5-f6a7-4b89-bcde-f01234567890";
        private const string PrecisionStatusGuid = "c3d4e5f6-a7b8-4c9a-cdef-012345678901";

        public static void Apply(DefCache cache)
        {
            // --- 1. Clone Quick Aim ability def ---
            var quickAim = cache.GetDef<ApplyStatusAbilityDef>("QuickAim_AbilityDef");
            if (quickAim == null)
            {
                Debug.LogWarning("[AAP] PrecisionShot: QuickAim_AbilityDef not found – aborting.");
                return;
            }

            var precisionShot = Helpers.CreateDefFromClone(quickAim, PrecisionShotGuid,
                "AAP_PrecisionShot_AbilityDef") as ApplyStatusAbilityDef;
            if (precisionShot == null) return;

            // Configure the ability itself
            var tAbility = Traverse.Create(precisionShot);
            tAbility.Property("ActionPointCost")?.SetValue(0f);
            tAbility.Property("WillPointCost")?.SetValue(3);
            precisionShot.UsesPerTurn = 1;
            precisionShot.ShowNotificationOnUse = true;

            // Name & description (localised later)
            precisionShot.ViewElementDef.DisplayName1.LocalizationKey = "AAP_PRECISION_SHOT";
            precisionShot.ViewElementDef.Description.LocalizationKey = "AAP_PRECISION_SHOT_DESC";

            // --- 2. Clone the AP cost modifier ---
            var qaCostMod = cache.GetDef<ChangeAbilitiesCostStatusDef>(
                "E_AbilityCostModifier [QuickAim_AbilityDef]");
            if (qaCostMod == null)
            {
                Debug.LogWarning("[AAP] PrecisionShot: QuickAim cost modifier not found.");
                return;
            }

            var psCostMod = Helpers.CreateDefFromClone(qaCostMod, PrecisionCostGuid,
                "E_AbilityCostModifier [AAP_PrecisionShot_AbilityDef]") as ChangeAbilitiesCostStatusDef;
            if (psCostMod == null) return;

            // Make any attack cost 0 AP (-10 is enough, engine clamps to 0)
            psCostMod.AbilityCostModification.ActionPointMod = -10f;
            psCostMod.Visuals = precisionShot.ViewElementDef;

            // --- 3. Clone the AddAttackBoostStatus (holds cost modifier, consumed after 1 attack) ---
            var qaBoost = cache.GetDef<AddAttackBoostStatusDef>("E_Status [QuickAim_AbilityDef]");
            if (qaBoost == null)
            {
                Debug.LogWarning("[AAP] PrecisionShot: QuickAim status not found.");
                return;
            }

            var psBoost = Helpers.CreateDefFromClone(qaBoost, PrecisionStatusGuid,
                "E_Status [AAP_PrecisionShot_AbilityDef]") as AddAttackBoostStatusDef;
            if (psBoost == null) return;

            psBoost.AdditionalStatusesToApply = new TacStatusDef[] { psCostMod };
            psBoost.Visuals = precisionShot.ViewElementDef;
            psBoost.ShowNotification = true;

            // (Optional) Add a small accuracy bonus – uncomment if desired
            // -------------------------------------------------------------------
            // var accuracyMult = cache.GetDef<StatMultiplierStatusDef>("Trembling_StatusDef");
            // if (accuracyMult != null)
            // {
            //     var psAccMod = Helpers.CreateDefFromClone(accuracyMult, "optional-guid",
            //         "E_AccuracyMultiplier [AAP_PrecisionShot_AbilityDef]") as StatMultiplierStatusDef;
            //     if (psAccMod != null)
            //     {
            //         psAccMod.StatsMultipliers[0].Multiplier = 1.2f; // +20% accuracy
            //         psAccMod.Visuals = null;
            //         psBoost.AdditionalStatusesToApply = psBoost.AdditionalStatusesToApply
            //             .Append(psAccMod).ToArray();
            //     }
            // }
            // -------------------------------------------------------------------

            // Link the status to the ability
            precisionShot.StatusDef = psBoost;

            // --- 4. Add ability to Sniper class ---
            var sniperClass = cache.GetDef<ClassProficiencyAbilityDef>("Sniper_ClassProficiency_AbilityDef");
            if (sniperClass != null)
            {
                var abilities = sniperClass.AbilityDefs?.ToList() ?? new List<AbilityDef>();
                if (!abilities.Contains(precisionShot))
                {
                    abilities.Add(precisionShot);
                    sniperClass.AbilityDefs = abilities.ToArray();
                    Debug.Log("[AAP] Precision Shot added to Sniper class abilities.");
                }
            }
            else
            {
                Debug.LogWarning("[AAP] PrecisionShot: Sniper_ClassProficiency_AbilityDef not found.");
            }
        }
    }
}