using Base.Core;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Weapons;
using PhoenixPoint.Tactical.Levels;
using PhoenixPoint.Tactical.View.ViewStates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Prevents Return Fire when the shooter steps out of full cover.
    /// Based on the implementation from Mad's Assorted Adjustments.
    /// </summary>
    [HarmonyPatch]
    public static class ReturnFireCoverCancelPatch
    {
        private static KeyValuePair<bool, string> stepOutTracker = new KeyValuePair<bool, string>(false, "");

        [HarmonyPatch(typeof(TacticalLevelController), "FireWeaponAtTargetCrt")]
        [HarmonyPrefix]
        public static void FireWeaponAtTargetCrt_Prefix(Weapon weapon, TacticalAbilityTarget abilityTarget)
        {
            try
            {
                if (abilityTarget.AttackType != AttackType.Regular) return;
                TacticalActor shooter = weapon.TacticalActor;
                bool shooterStepsOut = Vector3.SqrMagnitude(shooter.Pos - abilityTarget.ShootFromPos) > 0.01f;
                if (shooterStepsOut)
                {
                    string msg = $"{shooter.DisplayName} stepped out to shoot with {weapon.DisplayName}.";
                    stepOutTracker = new KeyValuePair<bool, string>(true, msg);
                    Debug.Log($"[AAP] {msg}");
                }
                else
                {
                    stepOutTracker = new KeyValuePair<bool, string>(false, "");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] ReturnFireCoverCancelPatch (FireWeaponAtTargetCrt) failed: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(UIStateShoot), "CalculateReturnFirePredictions")]
        [HarmonyPrefix]
        public static void CalculateReturnFirePredictions_Shoot_Prefix(UIStateShoot __instance)
        {
            try
            {
                ShootAbility shootAbility = (ShootAbility)AccessTools.Property(typeof(UIStateShoot), "_shootAbility").GetValue(__instance);
                if (__instance.AbilityTarget == null || shootAbility?.Weapon == null) return;
                TacticalActor shooter = shootAbility.TacticalActor;
                TacticalAbilityTarget abilityTarget = __instance.AbilityTarget;
                if (abilityTarget.AttackType == AttackType.Regular)
                {
                    bool shooterWillStepOut = Vector3.SqrMagnitude(shooter.Pos - abilityTarget.ShootFromPos) > 0.01f;
                    if (shooterWillStepOut)
                    {
                        string msg = $"{shooter.DisplayName} will step out to shoot with {shootAbility.Weapon.DisplayName}.";
                        stepOutTracker = new KeyValuePair<bool, string>(true, msg);
                        Debug.Log($"[AAP] Predicted: {msg}");
                    }
                    else
                    {
                        stepOutTracker = new KeyValuePair<bool, string>(false, "");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] ReturnFireCoverCancelPatch (UIStateShoot) failed: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(UIStateAbilitySelected), "CalculateReturnFirePredictions")]
        [HarmonyPrefix]
        public static void CalculateReturnFirePredictions_Ability_Prefix(UIStateAbilitySelected __instance, List<TacticalActor> ____targetActors, TacticalAbility ____selectedAbility)
        {
            try
            {
                if (!____targetActors.Any() || __instance.SelectedAbilityTarget == null || !(____selectedAbility is IAttackAbility))
                    return;
                TacticalActor shooter = ____selectedAbility.TacticalActor;
                TacticalAbilityTarget abilityTarget = __instance.SelectedAbilityTarget;
                if (abilityTarget.AttackType == AttackType.Regular)
                {
                    bool performerWillStepOut = Vector3.SqrMagnitude(shooter.Pos - abilityTarget.ShootFromPos) > 0.01f;
                    if (performerWillStepOut)
                    {
                        string msg = $"{shooter.DisplayName} will step out to use {____selectedAbility.TacticalAbilityDef?.ViewElementDef?.DisplayName1?.Localize()} ({____selectedAbility.TargetEquipmentName}).";
                        stepOutTracker = new KeyValuePair<bool, string>(true, msg);
                        Debug.Log($"[AAP] Predicted: {msg}");
                    }
                    else
                    {
                        stepOutTracker = new KeyValuePair<bool, string>(false, "");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] ReturnFireCoverCancelPatch (UIStateAbilitySelected) failed: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(TacticalLevelController), "GetReturnFireAbilities")]
        [HarmonyPostfix]
        public static void GetReturnFireAbilities_Postfix(ref List<ReturnFireAbility> __result, TacticalActor shooter)
        {
            try
            {
                if (__result == null || __result.Count == 0) return;
                if (!stepOutTracker.Key) return;
                for (int i = __result.Count - 1; i >= 0; i--)
                {
                    TacticalActor target = __result[i].TacticalActor;
                    if (target == shooter)
                    {
                        Debug.Log($"[AAP] Return Fire prevented for {target.DisplayName} because {stepOutTracker.Value}");
                        __result.RemoveAt(i);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] ReturnFireCoverCancelPatch (GetReturnFireAbilities) failed: {e.Message}");
            }
        }
    }
}