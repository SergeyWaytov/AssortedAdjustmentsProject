using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities.Weapons;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class VehicleAdjustments
    {
        public static void Apply(DefCache cache)
        {
            string[] vehicleKeywords = { "Armadillo", "Scarab", "Aspida", "Mutog" };
            var repo = GameUtl.GameComponent<DefRepository>();
            var vehicleWeapons = repo.GetAllDefs<WeaponDef>()
                .Where(w => w.name.Contains("GroundVehicle") && vehicleKeywords.Any(k => w.name.Contains(k)))
                .ToList();

            float ammoMultiplier = 1.5f;
            foreach (var weapon in vehicleWeapons)
            {
                var orig = weapon.ChargesMax;
                int newCharges = Mathf.RoundToInt(orig * ammoMultiplier);
                Traverse.Create(weapon).Property("ChargesMax").SetValue(newCharges);
                Debug.Log($"[AAP] {weapon.name} ammo: {orig} -> {newCharges} (1.5x)");
            }
            Debug.Log($"[AAP] Vehicle ammo multiplier applied to {vehicleWeapons.Count} weapons.");
        }
    }
}