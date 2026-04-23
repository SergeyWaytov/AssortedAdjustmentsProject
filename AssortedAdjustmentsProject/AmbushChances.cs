using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class AmbushChances
    {
        public static void Apply(DefCache cache)
        {
            // Since no AmbushChanceOutsideMist field exists in v1.30.1,
            // we force‑disable every ambush mechanic by zeroing all numeric fields
            // on every ambush‑related asset.
            var allAssets = Resources.FindObjectsOfTypeAll<ScriptableObject>();
            int disabledFields = 0;
            int disabledAssets = 0;

            foreach (var asset in allAssets)
            {
                if (asset == null) continue;
                // Only process assets whose name contains "Ambush"
                if (!asset.name.Contains("Ambush")) continue;

                bool modified = false;
                var fields = asset.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    // Zero out any numeric field that might control ambush chances
                    if (field.FieldType == typeof(float) || field.FieldType == typeof(int) ||
                        field.FieldType == typeof(double) || field.FieldType == typeof(long))
                    {
                        try
                        {
                            if (field.GetValue(asset) is float f && f != 0f) field.SetValue(asset, 0f);
                            else if (field.GetValue(asset) is int i && i != 0) field.SetValue(asset, 0);
                            else if (field.GetValue(asset) is double d && d != 0.0) field.SetValue(asset, 0.0);
                            else if (field.GetValue(asset) is long l && l != 0L) field.SetValue(asset, 0L);
                            else continue;
                            disabledFields++;
                            modified = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AAP] Could not zero field '{field.Name}' on {asset.name}: {ex.Message}");
                        }
                    }
                }
                if (modified) disabledAssets++;
            }

            if (disabledFields > 0)
                Debug.Log($"[AAP] Disabled {disabledFields} ambush‑related numeric fields across {disabledAssets} assets. NO AMBUSHES.");
            else
                Debug.LogWarning("[AAP] No ambush fields found to disable. Ambush mechanics may still occur.");
        }
    }
}