using Base.Core;
using Base.Defs;
using Base.Entities.Abilities;
using Base.Entities.Statuses;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Animations;
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

            // Clone the view element so we don’t corrupt Quick Aim
            precisionShot.ViewElementDef = Helpers.CreateDefFromClone(
                quickAim.ViewElementDef,
                "e5f6a7b8-c9d0-4e1a-9bcd-ef0123456789",
                "E_ViewElement [AAP_PrecisionShot_AbilityDef]") as TacticalAbilityViewElementDef;

            // Hardcoded English text – always works
            precisionShot.ViewElementDef.DisplayName1 = new LocalizedTextBind("Precision Shot", true);
            precisionShot.ViewElementDef.Description = new LocalizedTextBind(
                "The next attack costs 0 AP and gains +20% accuracy. Costs 4 WP. Limited to 1 use per turn.",
                true);

            // Configure the ability
            precisionShot.ActionPointCost = 0f;
            precisionShot.WillPointCost = 4f;
            precisionShot.UsesPerTurn = 1;
            precisionShot.ShowNotificationOnUse = true;

            // ---- FACTION RESTRICTION: Phoenix only ----
            var phoenixTag = cache.GetDef<GameTagDef>("PhoenixPoint_UniformTagDef");
            if (phoenixTag != null)
                precisionShot.ActorTags = new GameTagDef[] { phoenixTag };

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

            // ---- ANIMATION: copy QuickAim's AnimActionDef ----
            var repo = GameUtl.GameComponent<DefRepository>();
            var animAction = Traverse.Create(quickAim).Field("AnimActionDef")
                .GetValue<TacActorSimpleAbilityAnimActionDef>();
            if (animAction != null)
            {
                // Set the same animation controller on PrecisionShot
                Traverse.Create(precisionShot).Field("AnimActionDef").SetValue(animAction);

                // Also add the new ability to the animation's list
                if (!animAction.AbilityDefs.Contains(precisionShot))
                {
                    animAction.AbilityDefs = animAction.AbilityDefs.Append(precisionShot).ToArray();
                    Debug.Log("[AAP] Linked Precision Shot animation to " + animAction.name);
                }
            }
            else
            {
                Debug.LogWarning("[AAP] QuickAim has no AnimActionDef – Precision Shot will have no animation.");
            }

            // === MODIFY SNIPER CLASS ===
            var sniperClass = cache.GetDef<ClassProficiencyAbilityDef>("Sniper_ClassProficiency_AbilityDef");
            if (sniperClass != null)
            {
                var abilities = sniperClass.AbilityDefs?.ToList() ?? new List<AbilityDef>();

                // PistolProficiency mod may have removed Quick Aim – add it back if missing
                if (quickAim != null && !abilities.Contains(quickAim))
                    abilities.Insert(0, quickAim);

                if (!abilities.Contains(precisionShot))
                    abilities.Add(precisionShot);

                sniperClass.AbilityDefs = abilities.ToArray();
            }
        }
    }
}