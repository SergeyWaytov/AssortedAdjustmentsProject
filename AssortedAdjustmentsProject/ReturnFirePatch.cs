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
    /// AAP T4a/T4b fix: state is keyed to the actual shooter (not a
    /// boolean tracker that any step-out sets), and the gameplay writer
    /// is the real-fire prefix only. The two hover hooks compute local
    /// visual prediction but do NOT assign stepOutShooter/stepOutReason.
    /// AAP Q13-A: the AbilitySelected postfix param is now
    /// List&lt;TacticalActorBase&gt; to match the game's field type
    /// (Mono tolerates the old TacticalActor param; this is future-safe).
    /// AAP B6: the prediction sentences below are diagnostic-only
    /// (wrapped in Debug.Log calls, never displayed to the player). Per
    /// the audit's own B6 note ("If the prediction sentences are
    /// diagnostic-only, keep them as English debug logs"), these are
    /// intentionally left as English debug strings -- not localized.
    /// </summary>
    [HarmonyPatch]
    public static class ReturnFireCoverCancelPatch
    {
        // T4b: state keyed to the actual shooter. Only the real-fire prefix
        // assigns these; hover hooks compute local prediction but do not write.
        private static TacticalActor stepOutShooter;
        private static string stepOutReason = string.Empty;

        [HarmonyPatch(typeof(TacticalLevelController), "FireWeaponAtTargetCrt")]
        [HarmonyPrefix]
        public static void FireWeaponAtTargetCrt_Prefix(Weapon weapon, TacticalAbilityTarget abilityTarget)
        {
            try
            {
                if (abilityTarget.AttackType != AttackType.Regular) return;
                TacticalActor shooter = weapon.TacticalActor;
                bool steppedOut = Vector3.SqrMagnitude(shooter.Pos - abilityTarget.ShootFromPos) > 0.01f;
                stepOutShooter = steppedOut ? shooter : null;
                stepOutReason = steppedOut
                    ? $"{shooter.DisplayName} stepped out to shoot with {weapon.DisplayName}."
                    : string.Empty;
                if (steppedOut)
                    Debug.Log($"[AAP] {stepOutReason}");
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
                    // T4b: hover prediction computes the LOCAL string for
                    // debug only; it MUST NOT assign stepOutShooter/stepOutReason.
                    // The gameplay writer is the FireWeaponAtTargetCrt prefix above.
                    if (shooterWillStepOut)
                    {
                        string msg = $"{shooter.DisplayName} will step out to shoot with {shootAbility.Weapon.DisplayName}.";
                        Debug.Log($"[AAP] Predicted: {msg}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] ReturnFireCoverCancelPatch (UIStateShoot) failed: {e.Message}");
            }
        }

        // Q13-A: ____targetActors is now List<TacticalActorBase> (matches
        // the game's field type; future-safe against stricter mono metadata).
        [HarmonyPatch(typeof(UIStateAbilitySelected), "CalculateReturnFirePredictions")]
        [HarmonyPrefix]
        public static void CalculateReturnFirePredictions_Ability_Prefix(UIStateAbilitySelected __instance, List<TacticalActorBase> ____targetActors, TacticalAbility ____selectedAbility)
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
                    // T4b: diagnostic-only; does NOT assign stepOutShooter/stepOutReason.
                    if (performerWillStepOut)
                    {
                        string msg = $"{shooter.DisplayName} will step out to use {____selectedAbility.TacticalAbilityDef?.ViewElementDef?.DisplayName1?.Localize()} ({____selectedAbility.TargetEquipmentName}).";
                        Debug.Log($"[AAP] Predicted: {msg}");
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
                // T4a: the old loop checked `target == shooter` against a list
                // built from actors Enemy-to-shooter -- none could ever equal.
                // Clear the result outright when the real-fire prefix set the
                // shooter and this call's shooter matches.
                if (__result == null || stepOutShooter != shooter) return;
                Debug.Log($"[AAP] Return Fire prevented ({__result.Count}) because {stepOutReason}");
                __result.Clear();
                stepOutShooter = null;
                stepOutReason = string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] ReturnFireCoverCancelPatch (GetReturnFireAbilities) failed: {e.Message}");
            }
        }
    }
}
