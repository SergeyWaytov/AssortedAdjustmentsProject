// WPCostDiscovery.cs
using Base.Entities.Statuses;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class WPCostDiscovery
    {
        private static TacticalAbility _currentWPAbility = null;

        [HarmonyPatch(typeof(TacticalAbility), "Activate")]
        [HarmonyPrefix]
        static void AbilityActivatePre(TacticalAbility __instance)
        {
            if (__instance.TacticalAbilityDef.WillPointCost > 0f)
                _currentWPAbility = __instance;
        }

        [HarmonyPatch(typeof(TacticalAbility), "Activate")]
        [HarmonyPostfix]
        static void AbilityActivatePost(TacticalAbility __instance)
        {
            if (_currentWPAbility == __instance)
                _currentWPAbility = null;
        }

        // The real parameters: (float f, bool triggerStatChangeEvent)
        [HarmonyPatch(typeof(StatusStat), "Set", new System.Type[] { typeof(float), typeof(bool) })]
        [HarmonyPrefix]
        static void OnWPSet(StatusStat __instance, float f, bool triggerStatChangeEvent)
        {
            if (__instance.Name != "WillPoints" || _currentWPAbility == null)
                return;

            // Only log reductions
            float oldValue = (float)__instance;
            if (f >= oldValue)
                return;

            TacticalActor actor = __instance.Owner as TacticalActor;
            if (actor == null || actor != _currentWPAbility.TacticalActor)
                return;

            float delta = oldValue - f;
            Debug.Log($"[AAP WPDISC] WP reduced by {delta} while executing {_currentWPAbility.TacticalAbilityDef.name} (cost={_currentWPAbility.TacticalAbilityDef.WillPointCost})");
            Debug.Log(new System.Diagnostics.StackTrace().ToString());
        }
    }
}