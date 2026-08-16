using Base.Core;
using Base.Defs;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Geoscape.Levels;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// AAP 1.1 NOTE: the old version scanned defs for a "BionicRepairCostPerHP"
    /// property that does not exist anywhere in the game code - it never applied.
    /// The real repair-cost mechanism is GeoscapeSettingsDef (shared data):
    /// AllItemRepairCost plus a per-item-type RepairCost multiplier matched by
    /// tag (GeoscapeSettingsDef.GetItemTypeSettings). Verified against the
    /// decompiled GeoCharacter.GetRepairCost.
    /// Intent from the Workshop feature list: mutation repairs free, bionic
    /// repairs at normal price.
    /// </summary>
    public static class RepairCosts
    {
        public static void Apply(DefCache cache)
        {
            var settings = SharedData.GetSharedDataFromGame()?.GeoscapeSettingsDef;
            if (settings?.ItemsSettings == null)
            {
                Debug.LogWarning("[AAP] GeoscapeSettingsDef not found - repair costs unchanged.");
                return;
            }

            int changed = 0;
            foreach (GeoscapeSettingsDef.ItemTypeSettings typeSettings in settings.ItemsSettings)
            {
                if (typeSettings?.Tag == null) continue;
                string tagName = typeSettings.Tag.name;

                // Mutations repair for free. Bionics are left at their vanilla
                // multiplier (0.5) - the 1.0 log line in the first 1.1 build
                // actually doubled bionic repair costs against the design
                // philosophy of never adding costs to the player.
                if (tagName.Contains("Mutat") && typeSettings.RepairCost != 0f)
                {
                    float old = typeSettings.RepairCost;
                    typeSettings.RepairCost = 0f;
                    changed++;
                    Debug.Log($"[AAP] Repair cost for '{tagName}': {old} -> 0 (mutations repair for free).");
                }
            }

            Debug.Log(changed > 0
                ? $"[AAP] RepairCosts applied to {changed} item types."
                : "[AAP] RepairCosts: no bionic/mutation item types found (nothing to change).");
        }
    }
}
