using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities.Weapons;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Ground vehicle turret weapon ammo multiplier (config, default 1.5x).
    /// AAP 1.1 NOTE: the old filter required "GroundVehicle" in the def name,
    /// which does not match the actual turret def names. Mad's AssortedAdjustments
    /// proved the working name substrings; Kaos_Buggy covers the Kaos Engines
    /// vehicle. Base charges are captured on first apply so re-applies
    /// (config changes) do not compound the multiplier.
    /// Note: Mutog pets are not ground vehicles - their worm launchers are
    /// handled separately in WeaponAdjustments (fixed 5 charges).
    /// </summary>
    public static class VehicleAdjustments
    {
        private static readonly string[] TurretKeywords =
        {
            "Armadillo_Gauss_Turret",   // NJ Armadillo turret
            "Scarab_Missile_Turret",    // PX Scarab turret
            "Aspida_Arms",              // SY Aspida arms
            "Kaos_Buggy"                // Kaos Engines vehicle weapons (DLC5)
        };

        // def name -> original ChargesMax (captured on first apply)
        private static readonly Dictionary<string, int> BaseCharges = new Dictionary<string, int>();

        public static void Apply(DefCache cache)
        {
            float multiplier = Mathf.Max(0.1f, ModMain.Cfg?.VehicleAmmoMultiplier ?? 1.5f);
            var repo = GameUtl.GameComponent<DefRepository>();

            var vehicleWeapons = repo.GetAllDefs<WeaponDef>()
                .Where(w => TurretKeywords.Any(k => w.name.Contains(k)))
                .ToList();

            int patched = 0;
            foreach (var weapon in vehicleWeapons)
            {
                if (!BaseCharges.TryGetValue(weapon.name, out int orig))
                {
                    orig = weapon.ChargesMax;
                    BaseCharges[weapon.name] = orig;
                }

                int newCharges = Mathf.Max(1, Mathf.RoundToInt(orig * multiplier));
                if (weapon.ChargesMax != newCharges)
                {
                    weapon.ChargesMax = newCharges;
                    patched++;
                }
                Debug.Log($"[AAP] {weapon.name} ammo: {orig} -> {newCharges} (x{multiplier}).");
            }

            Debug.Log($"[AAP] Vehicle ammo: matched {vehicleWeapons.Count} weapons, updated {patched} (multiplier {multiplier}).");
            if (vehicleWeapons.Count == 0)
                Debug.LogWarning("[AAP] Vehicle ammo: no turret weapons matched - check Player.log def names.");
        }
    }
}
