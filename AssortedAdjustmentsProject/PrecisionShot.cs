using Base.Core;
using Base.Defs;
using Base.Entities.Abilities;
using Base.Entities.Statuses;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Animations;
using PhoenixPoint.Tactical.Entities.Statuses;
using System;
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

        private static AddAbilityStatusDef _precisionShotAdder;

        public static void Apply(DefCache cache)
        {
            try
            {
                var quickAim = cache.GetDef<ApplyStatusAbilityDef>("QuickAim_AbilityDef");
                if (quickAim == null)
                {
                    Debug.LogError("[AAP] PrecisionShot: QuickAim_AbilityDef not found!");
                    return;
                }

                // ---------- 1. Create the Precision Shot ability ----------
                var precisionShot = Helpers.CreateDefFromClone(
                    quickAim, PrecisionShotGuid, "AAP_PrecisionShot_AbilityDef"
                ) as ApplyStatusAbilityDef;
                if (precisionShot == null) return;

                precisionShot.ViewElementDef = Helpers.CreateDefFromClone(
                    quickAim.ViewElementDef,
                    "e5f6a7b8-c9d0-4e1a-bcde-ef0123456789",
                    "E_ViewElement [AAP_PrecisionShot_AbilityDef]"
                ) as TacticalAbilityViewElementDef;

                // Localized via AAP_Localization.csv (EN + RU). The old
                // LocalizedTextBind("...", true) hardcoded English: the second
                // constructor argument is doNotLocalize, not a localize flag.
                precisionShot.ViewElementDef.DisplayName1 = new LocalizedTextBind("AAP_PRECISION_SHOT");
                precisionShot.ViewElementDef.Description = new LocalizedTextBind("AAP_PRECISION_SHOT_DESC");

                precisionShot.ActionPointCost = 0f;
                precisionShot.WillPointCost = 4f;
                precisionShot.UsesPerTurn = 1;
                precisionShot.ShowNotificationOnUse = true;

                var phoenixTag = cache.GetDef<GameTagDef>("PhoenixPoint_UniformTagDef");
                precisionShot.ActorTags = new GameTagDef[] { phoenixTag };

                // ---------- 2. Create custom status that reduces next attack AP to 0 ----------
                var qaCostMod = cache.GetDef<ChangeAbilitiesCostStatusDef>(
                    "E_AbilityCostModifier [QuickAim_AbilityDef]");
                var psCostMod = Helpers.CreateDefFromClone(
                    qaCostMod, PrecisionCostGuid,
                    "E_AbilityCostModifier [AAP_PrecisionShot_AbilityDef]"
                ) as ChangeAbilitiesCostStatusDef;
                psCostMod.AbilityCostModification.ActionPointMod = -10f;   // makes next attack cost 0 AP
                psCostMod.Visuals = Helpers.CreateDefFromClone(
                    precisionShot.ViewElementDef,
                    "c9d0e1f2-a3b4-4c5d-9e0f-abcdef012345",
                    "E_Visuals [AAP_PrecisionShot_CostMod]"
                ) as ViewElementDef;

                // ---------- 3. Create accuracy buff status (+20%) ----------
                var trembling = cache.GetDef<StatMultiplierStatusDef>("Trembling_StatusDef");
                StatMultiplierStatusDef psAccMod = null;
                if (trembling != null)
                {
                    psAccMod = Helpers.CreateDefFromClone(
                        trembling, PrecisionAccGuid,
                        "E_AccuracyMultiplier [AAP_PrecisionShot_AbilityDef]"
                    ) as StatMultiplierStatusDef;
                    psAccMod.StatsMultipliers[0].Multiplier = 1.2f;   // +20% accuracy
                    psAccMod.EffectName = string.Empty;
                    psAccMod.ShowNotification = false;
                    psAccMod.VisibleOnHealthbar = 0;
                    psAccMod.VisibleOnStatusScreen = 0;
                    psAccMod.Visuals = null;
                }

                // ---------- 4. Assemble boost status and assign to ability ----------
                var qaBoost = cache.GetDef<AddAttackBoostStatusDef>("E_Status [QuickAim_AbilityDef]");
                var psBoost = Helpers.CreateDefFromClone(
                    qaBoost, PrecisionStatusGuid,
                    "E_Status [AAP_PrecisionShot_AbilityDef]"
                ) as AddAttackBoostStatusDef;

                var additionalStatuses = new List<TacStatusDef> { psCostMod };
                if (psAccMod != null) additionalStatuses.Add(psAccMod);
                psBoost.AdditionalStatusesToApply = additionalStatuses.ToArray();

                psBoost.Visuals = Helpers.CreateDefFromClone(
                    precisionShot.ViewElementDef,
                    "b8c9d0e1-f2a3-4b4c-9d5e-0f1234567890",
                    "E_Visuals [AAP_PrecisionShot_Status]"
                ) as ViewElementDef;
                psBoost.ShowNotification = true;

                precisionShot.StatusDef = psBoost;   // <-- critical: use custom status

                // ---------- 5. Permanent adder status (gives ability to snipers) ----------
                var addAbilityStatus = Helpers.CreateDefFromClone(
                    cache.GetDef<AddAbilityStatusDef>("E_AddAbilityStatus [DeployBeacon_StatusDef]"),
                    "f7a1b2c3-d4e5-4f6a-9bc0-123456789abc",
                    "E_AddAbilityStatus [AAP_PrecisionShot_Status]"
                ) as AddAbilityStatusDef;

                addAbilityStatus.AbilityDef = precisionShot;
                addAbilityStatus.DurationTurns = -1;
                addAbilityStatus.ExpireOnEndOfTurn = false;
                addAbilityStatus.SingleInstance = true;
                addAbilityStatus.ShowNotification = false;
                addAbilityStatus.VisibleOnPassiveBar = true;
                addAbilityStatus.Visuals = precisionShot.ViewElementDef;   // prevents UI nullref

                _precisionShotAdder = addAbilityStatus;

                // ---------- 6. Animation ----------
                var repo = GameUtl.GameComponent<DefRepository>();
                var animAction = repo.GetAllDefs<TacActorSimpleAbilityAnimActionDef>()
                    .FirstOrDefault(a => a.name.Contains("Soldier_Utka_AnimActionsDef") &&
                                         a.AbilityDefs != null &&
                                         a.AbilityDefs.Contains(quickAim));
                if (animAction != null)
                {
                    if (animAction.AbilityDefs == null)
                        animAction.AbilityDefs = new AbilityDef[0];

                    if (!animAction.AbilityDefs.Contains(precisionShot))
                    {
                        animAction.AbilityDefs = animAction.AbilityDefs.Append(precisionShot).ToArray();
                        Debug.Log("[AAP] Linked Precision Shot animation to " + animAction.name);
                    }
                }
                else
                {
                    Debug.LogWarning("[AAP] Could not assign any animation to Precision Shot. Ability may cause problems.");
                }

                Debug.Log("[AAP] Precision Shot ability and adder status created successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] PrecisionShot.Apply failed: {e.Message}");
            }
        }

        // ===================== HARMONY PATCH =====================
        [HarmonyPatch(typeof(TacticalActor), "ProcessInstanceData")]
        internal static class PrecisionShot_ApplyToPhoenixSnipers
        {
            static void Postfix(TacticalActor __instance)
            {
                try
                {
                    var viewer = __instance.TacticalLevel?.View?.ViewerFaction;
                    if (viewer == null || __instance.TacticalFaction != viewer)
                        return;

                    var sniperTag = ModMain.DefCache?.GetDef<GameTagDef>("Sniper_ClassTagDef");
                    if (sniperTag == null || !__instance.HasGameTag(sniperTag))
                        return;

                    if (_precisionShotAdder == null)
                    {
                        Debug.LogWarning("[AAP] PrecisionShot: adder status not created yet.");
                        return;
                    }

                    if (!__instance.Status.HasStatus(_precisionShotAdder))
                    {
                        __instance.Status.ApplyStatus(_precisionShotAdder);
                        Debug.Log($"[AAP] Precision Shot granted to {__instance.DisplayName}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AAP] PrecisionShot patch error: {e.Message}");
                }
            }
        }
    }
}