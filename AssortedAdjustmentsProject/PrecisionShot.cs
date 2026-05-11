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
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class PrecisionShot
    {
        private const string PrecisionShotGuid = "a1b2c3d4-e5f6-4789-abcd-ef0123456789";
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

                // ---- 1. Create the Precision Shot ability ----
                var precisionShot = Helpers.CreateDefFromClone(
                    quickAim, PrecisionShotGuid, "AAP_PrecisionShot_AbilityDef"
                ) as ApplyStatusAbilityDef;
                if (precisionShot == null) return;

                precisionShot.ViewElementDef = Helpers.CreateDefFromClone(
                    quickAim.ViewElementDef,
                    "e5f6a7b8-c9d0-4e1a-bcde-ef0123456789",
                    "E_ViewElement [AAP_PrecisionShot_AbilityDef]"
                ) as TacticalAbilityViewElementDef;

                precisionShot.ViewElementDef.DisplayName1 = new LocalizedTextBind("Precision Shot", true);
                precisionShot.ViewElementDef.Description = new LocalizedTextBind(
                    "The next attack costs 0 AP and gains +20% accuracy. Costs 4 WP. Limited to 1 use per turn.",
                    true);

                precisionShot.ActionPointCost = 0f;
                precisionShot.WillPointCost = 4f;
                precisionShot.UsesPerTurn = 1;
                precisionShot.ShowNotificationOnUse = true;

                var phoenixTag = cache.GetDef<GameTagDef>("PhoenixPoint_UniformTagDef");
                precisionShot.ActorTags = new GameTagDef[] { phoenixTag };

                // ---- 2. Create the status that permanently adds the ability ----
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

                // ---------- CRITICAL FIX: give the status a visual with a SmallIcon ----------
                addAbilityStatus.Visuals = precisionShot.ViewElementDef;
                // -------------------------------------------------------------------------

                _precisionShotAdder = addAbilityStatus;

                // ---- 3. Animation ----
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