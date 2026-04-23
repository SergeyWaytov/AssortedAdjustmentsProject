using HarmonyLib;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Events;
using PhoenixPoint.Geoscape.Events.Eventus;
using PhoenixPoint.Geoscape.Levels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    

    // ===== DISABLE NOTHING FOUND =====
    internal static class DisableNothingFound
    {
        private static GeoFaction visitingFaction = null;
        private static readonly string NothingFoundID = "EXPSITE_02";

        [HarmonyPatch(typeof(GeoscapeEventSystem), "PhoenixFaction_OnSiteFirstTimeVisited")]
        public static class GeoscapeEventSystem_PhoenixFaction_OnSiteFirstTimeVisited_Patch
        {
            public static void Prefix(GeoFaction controller) { visitingFaction = controller; }
            public static void Postfix() { visitingFaction = null; }
        }

        [HarmonyPatch(typeof(GeoscapeEventSystem), "GetValidEventsForSite")]
        public static class GeoscapeEventSystem_GetValidEventsForSite_Patch
        {
            public static void Postfix(List<GeoscapeEventDef> outEvents)
            {
                try { if (outEvents != null && outEvents.Count > 1) outEvents.RemoveAll(e => e.EventID == NothingFoundID); }
                catch (Exception e) { Debug.LogError($"[AAP] DisableNothingFound GetValidEventsForSite failed: {e.Message}"); }
            }
        }

        [HarmonyPatch(typeof(GeoscapeEventSystem), "SetEventForSite")]
        public static class GeoscapeEventSystem_SetEventForSite_Patch
        {
            public static void Prefix(GeoscapeEventSystem __instance, GeoSite site, ref string eventID)
            {
                try
                {
                    if (eventID != NothingFoundID) return;
                    List<string> events = __instance.EmptyExplorationEventIds;
                    if (events.Count <= 1)
                    {
                        if (visitingFaction == null) return;
                        List<GeoscapeEventDef> newEventList = new List<GeoscapeEventDef>();
                        __instance.GetValidEventsForSite(site, visitingFaction, newEventList, true);
                        events = newEventList.Select(e => e.EventID).Where(e => e != NothingFoundID).ToList();
                    }
                    else events.RemoveAll(e => e == NothingFoundID);
                    if (events.Count > 0) eventID = events[UnityEngine.Random.Range(0, events.Count)];
                }
                catch (Exception e) { Debug.LogError($"[AAP] DisableNothingFound SetEventForSite failed: {e.Message}"); }
            }
        }
    }
}