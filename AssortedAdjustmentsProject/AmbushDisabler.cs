// AmbushDisabler.cs
using HarmonyLib;
using PhoenixPoint.Geoscape.Events;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch(typeof(GeoscapeEventSystem), "OnLevelStart")]
    public static class AmbushDisabler_OnLevelStart
    {
        static void Postfix(GeoscapeEventSystem __instance)
        {
            __instance.ExplorationAmbushChance = 0;
            Debug.Log("[AAP] Ambushes disabled (ExplorationAmbushChance = 0).");
        }
    }
}