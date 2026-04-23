using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class LootMechanics
    {
        public static void Apply(DefCache cache)
        {
            // Loot settings are typically on the same TacticalSettings asset as ambush,
            // but since that asset doesn't exist, we'll search for any object with "AlwaysRecoverAllItems"
            // field. As a fallback, we'll use the first Ambush_CustomMissionTypeDef (which is always loaded)
            // and apply the loot fields there via Traverse.
            var allAssets = Resources.FindObjectsOfTypeAll<ScriptableObject>();
            ScriptableObject target = null;

            // First: try to find any object that already has loot fields
            foreach (var asset in allAssets)
            {
                if (asset == null) continue;
                var field = asset.GetType().GetField("AlwaysRecoverAllItemsFromTacticalMissions",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    target = asset;
                    Debug.Log($"[AAP] Found loot settings object via field detection: {asset.GetType().Name} ({asset.name})");
                    break;
                }
            }

            // Fallback: use the first Ambush_CustomMissionTypeDef as a vessel
            if (target == null)
            {
                target = allAssets.FirstOrDefault(a => a != null && a.name == "Ambush_CustomMissionTypeDef");
                if (target != null)
                {
                    Debug.Log("[AAP] Using Ambush_CustomMissionTypeDef as vessel for loot settings.");
                }
                else
                {
                    Debug.LogWarning("[AAP] No suitable object found to apply loot settings. Loot unchanged.");
                    return;
                }
            }

            var t = Traverse.Create(target);
            t.Field("AlwaysRecoverAllItemsFromTacticalMissions")?.SetValue(false);
            t.Field("EnablePlentifulItemDrops")?.SetValue(true);
            t.Field("ItemDestructionChance")?.SetValue(10);
            t.Field("AllowWeaponDrops")?.SetValue(true);
            t.Field("FlatWeaponDestructionChance")?.SetValue(30);
            t.Field("HealthBasedWeaponDestruction")?.SetValue(true);
            t.Field("AllowArmorDrops")?.SetValue(true);
            t.Field("FlatArmorDestructionChance")?.SetValue(70);
            t.Field("HealthBasedArmorDestruction")?.SetValue(true);
            t.Field("AllowAmmoDrops")?.SetValue(true);
            t.Field("AmmoDestructionChance")?.SetValue(10);
            t.Field("AllowInventoryItemDrops")?.SetValue(true);
            t.Field("InventoryItemDestructionChance")?.SetValue(10);

            Debug.Log("[AAP] LootMechanics applied.");
        }
    }
}