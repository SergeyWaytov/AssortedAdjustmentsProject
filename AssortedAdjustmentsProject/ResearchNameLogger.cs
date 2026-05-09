// ResearchNameLogger.cs
using HarmonyLib;
using Base.Core;
using Base.Defs;
using PhoenixPoint.Geoscape.Entities.Research;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class ResearchNameLogger
    {
        [HarmonyPatch(typeof(ResearchElement), "Init")] // any early method – runs once
        [HarmonyPostfix]
        static void LogAllResearchDefs()
        {
            var repo = GameUtl.GameComponent<DefRepository>();
            var allResearch = repo.GetAllDefs<ResearchDef>();
            Debug.Log("[AAP RESEARCH NAMES] ====== All ResearchDefs ======");
            foreach (var r in allResearch)
                Debug.Log($"[AAP RESEARCH NAMES] {r.name}  (GUID: {r.Guid})");
        }
    }
}