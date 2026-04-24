using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Dead‑simple UI catcher: logs every MonoBehaviour that gets enabled
    /// and whose type name contains "Haven", "Base", "GeoBase", or "Roster".
    /// </summary>
    [HarmonyPatch(typeof(MonoBehaviour), "OnEnable")]
    public static class SimpleUILogger
    {
        private static HashSet<string> seen = new HashSet<string>();

        [HarmonyPostfix]
        public static void Postfix(MonoBehaviour __instance)
        {
            if (__instance == null) return;
            string name = __instance.GetType().FullName;
            if (seen.Contains(name)) return;

            // Only log classes related to the UI screens we care about
            if (name.Contains("Haven") || name.Contains("BaseInfo") || name.Contains("GeoBase") || name.Contains("GeoRoster") || name.Contains("FactionAgenda") || name.Contains("VehicleSelected"))
            {
                seen.Add(name);
                Debug.Log($"[AAP UI CATCH] {name} (GameObject: {__instance.gameObject.name})");
            }
        }
    }
}