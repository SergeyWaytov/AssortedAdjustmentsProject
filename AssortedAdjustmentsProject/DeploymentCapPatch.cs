using HarmonyLib;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewStates;
using System.Collections.Generic;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    // AAP G3: deployment cap is a one-way shared-def write. Capture the
    // vanilla MaxPlayerUnits once per MissionDef, then always use Max()
    // so increasing the cap above vanilla never lowers a previously-raised
    // value, and lowering the config never lowers below the captured vanilla.
    [HarmonyPatch(typeof(UIStateRosterDeployment), "SetUpInitialDeployment")]
    public static class DeploymentCap_IncreaseMaxUnits
    {
        // AAP G3: key by def.name (string) so we don't need to import the
        // MissionDef type's namespace. Def names are unique identifiers.

        private static readonly Dictionary<string, int> OriginalCaps = new Dictionary<string, int>();

        [HarmonyPrefix]
        public static void Prefix(GeoMission ____mission)
        {
            var def = ____mission?.MissionDef;
            if (def == null) return;

            if (!OriginalCaps.TryGetValue(def.name, out int original))
            {
                original = def.MaxPlayerUnits;
                OriginalCaps.Add(def.name, original);
            }

            int configured = Mathf.Clamp(ModMain.Cfg?.DeploymentCap ?? 16, 8, 32);
            int target = Mathf.Max(original, configured);
            if (def.MaxPlayerUnits != target)
            {
                Debug.Log($"[AAP] Deployment cap {def.MaxPlayerUnits} -> {target} (vanilla {original}, config {configured}).");
                def.MaxPlayerUnits = target;
            }
        }
    }

    // AAP U9: DeploymentCap_UpdateUIText was DELETEd. It read a nonexistent
    // _mission field (the postfix returned immediately when the lookup
    // returned null) and was redundant with the native roster UI counter.
    // Native UI already formats the "{used} / {max}" squad count display.
}
