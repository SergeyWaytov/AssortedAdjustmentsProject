using HarmonyLib;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Weapons;
using PhoenixPoint.Tactical.View.ViewStates;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class SniperPrecisionShotUI
    {
        // Cached factory method
        private static MethodInfo _createShootAbilityMethod = null;

        [HarmonyPatch(typeof(UIStateAbilitySelected), "EnterState")]
        [HarmonyPostfix]
        public static void EnterState_Postfix(UIStateAbilitySelected __instance)
        {
            var actor = Traverse.Create(__instance).Property("SelectedActor").GetValue<TacticalActor>();
            if (actor == null) return;

            var sniperTag = ModMain.DefCache?.GetDef<GameTagDef>("Sniper_ClassTagDef");
            if (sniperTag == null) return;
            if (!actor.HasGameTag(sniperTag)) return;

            var precisionShotDef = SniperPrecisionShotAbility.PrecisionShotDef;
            if (precisionShotDef == null) return;

            var abilitiesList = Traverse.Create(actor).Field("_abilities").GetValue<List<TacticalAbility>>();
            if (abilitiesList == null) return;

            // Already injected?
            if (abilitiesList.Any(a => a.TacticalAbilityDef == precisionShotDef))
                return;

            // Get the weapon from the normal Shoot ability
            var normalShoot = abilitiesList.OfType<ShootAbility>()
                .FirstOrDefault(a => a.TacticalAbilityDef.name == "Shoot_AbilityDef");
            if (normalShoot?.Weapon == null)
            {
                Debug.LogWarning("[AAP] Precision Shot: no normal Shoot ability with weapon found.");
                return;
            }

            // Create a new ShootAbility via the weapon/weapondef factory
            ShootAbility preciseShoot = CreateShootAbilityFromWeapon(normalShoot.Weapon, actor, precisionShotDef);
            if (preciseShoot == null) return;

            abilitiesList.Add(preciseShoot);
            Debug.Log($"[AAP] Precision Shot injected for {actor.DisplayName}");
        }

        private static ShootAbility CreateShootAbilityFromWeapon(Weapon weapon, TacticalActor actor, ShootAbilityDef def)
        {
            WeaponDef weaponDef = weapon.WeaponDef;
            if (weaponDef == null) return null;

            // --- Discover the factory method once ---
            if (_createShootAbilityMethod == null)
            {
                // Most common: WeaponDef.CreateShootAbility(TacticalActor)
                _createShootAbilityMethod = weaponDef.GetType().GetMethod("CreateShootAbility",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(TacticalActor) }, null);

                // Alternative: WeaponDef.CreateShootAbility(TacticalActor, Weapon)
                if (_createShootAbilityMethod == null)
                    _createShootAbilityMethod = weaponDef.GetType().GetMethod("CreateShootAbility",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null, new[] { typeof(TacticalActor), typeof(Weapon) }, null);

                // Another alternative: Weapon.GetShootAbility(TacticalActor)
                if (_createShootAbilityMethod == null)
                    _createShootAbilityMethod = weapon.GetType().GetMethod("GetShootAbility",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null, new[] { typeof(TacticalActor) }, null);
            }

            if (_createShootAbilityMethod == null)
            {
                Debug.LogWarning("[AAP] Could not find CreateShootAbility or GetShootAbility method on Weapon/WeaponDef.");
                return null;
            }

            // --- Call the factory ---
            object target = _createShootAbilityMethod.DeclaringType == typeof(Weapon)
                ? (object)weapon
                : weaponDef;

            object[] args = _createShootAbilityMethod.GetParameters().Length == 1
                ? new object[] { actor }
                : new object[] { actor, weapon };

            ShootAbility shootAbility = _createShootAbilityMethod.Invoke(target, args) as ShootAbility;

            if (shootAbility == null)
            {
                Debug.LogWarning("[AAP] Factory method returned null.");
                return null;
            }

            // --- Replace its def with our custom one ---
            Traverse.Create(shootAbility).Property("TacticalAbilityDef").SetValue(def);
            Traverse.Create(def).Property("ActionPointCost").SetValue(0f);
            Traverse.Create(def).Property("WillPointCost").SetValue(3);
            Traverse.Create(def).Field("MaxUsesPerTurn").SetValue(1);

            return shootAbility;
        }
    }
}