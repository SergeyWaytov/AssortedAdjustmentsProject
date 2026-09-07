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
    /// AAP G10-A: removed the dead mutation branch (the live GeoscapeSettingsDef
    /// ItemsSettings table in this build contains Bionic_TagDef only; no
    /// mutation row exists for the "Mutat" substring to match). The bionic
    /// baseline is left at its vanilla multiplier.
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

            // AAP G10-A: keep the bionic baseline only. The mutation branch was
            // dead (no mutation row in the live table for any "Mutat" substring
            // to match). Honest contract: do not advertise mutation free-repair
            // until the augmentation/body-part seam is identified and the row
            // actually exists.
            // G10 verify in-game: do mutated body parts incur repair cost?
            int bionicsSeen = 0;
            foreach (GeoscapeSettingsDef.ItemTypeSettings itemType in settings.ItemsSettings)
            {
                if (itemType?.Tag?.name == "Bionic_TagDef")
                {
                    bionicsSeen++;
                    Debug.Log($"[AAP] Bionic repair multiplier left at {itemType.RepairCost} (vanilla baseline preserved).");
                }
            }

            Debug.Log(bionicsSeen > 0
                ? $"[AAP] RepairCosts: {bionicsSeen} bionic item type(s) verified (multiplier left at vanilla)."
                : "[AAP] RepairCosts: no bionic item types found (nothing to change).");
        }
    }
}
