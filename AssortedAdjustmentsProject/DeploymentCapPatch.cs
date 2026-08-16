using HarmonyLib;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Geoscape.View.ViewStates;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    // Patch 1: Increase mission max player units
    [HarmonyPatch(typeof(UIStateRosterDeployment), "SetUpInitialDeployment")]
    public static class DeploymentCap_IncreaseMaxUnits
    {
        private static int MinimumDeploymentSlots => Mathf.Clamp(ModMain.Cfg?.DeploymentCap ?? 16, 8, 32);

        [HarmonyPrefix]
        public static void Prefix(GeoMission ____mission)
        {
            if (____mission?.MissionDef == null) return;

            int cap = MinimumDeploymentSlots;
            int currentMax = ____mission.MissionDef.MaxPlayerUnits;
            if (currentMax < cap)
            {
                ____mission.MissionDef.MaxPlayerUnits = cap;
                Debug.Log($"[AAP] Deployment cap raised from {currentMax} to {cap}.");
            }
        }
    }

    // Patch 2: Update UI text to show actual cap
    [HarmonyPatch(typeof(UIModuleDeploymentMissionBriefing), "SetCurrentDeployment")]
    public static class DeploymentCap_UpdateUIText
    {
        [HarmonyPostfix]
        public static void Postfix(UIModuleDeploymentMissionBriefing __instance, int currentDeploymentNumber)
        {
            try
            {
                var mission = Traverse.Create(__instance).Field("_mission").GetValue<GeoMission>();
                if (mission?.MissionDef == null) return;

                int maxUnits = mission.MissionDef.MaxPlayerUnits;
                __instance.SquadSlotsUsedText.text = $"{currentDeploymentNumber} / {maxUnits}";
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AAP] Deployment UI patch failed: {e.Message}");
            }
        }
    }
}