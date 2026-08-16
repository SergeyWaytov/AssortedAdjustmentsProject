using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Geoscape.Entities.Sites;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class GeoscapeFacilitiesAdjustments
    {
        public static void Apply(DefCache cache)
        {
            // Training Facility
            var trainingFacility = cache.GetDef<BaseDef>("TrainingFacility_PhoenixFacilityDef");
            if (trainingFacility != null)
            {
                var components = Traverse.Create(trainingFacility).Field("GeoFacilityComponentDefs").GetValue<IList>();
                if (components != null)
                {
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        var typeName = comp.GetType().Name;
                        if (typeName == "ExperienceFacilityComponentDef")
                        {
                            var compT = Traverse.Create(comp);
                            compT.Field("SkillPointsPerDay")?.SetValue(2);
                            compT.Field("ExperiencePerUser")?.SetValue(4);
                            Debug.Log("[AAP] Training Facility patched: +2 SP/day, +4 XP/user.");
                            break;
                        }
                    }
                }
            }

            // Mist Repeller
            /*var mistRepeller = cache.GetDef<BaseDef>("MistRepeller_PhoenixFacilityDef");
            if (mistRepeller != null)
            {
                var components = Traverse.Create(mistRepeller).Field("GeoFacilityComponentDefs").GetValue<IList>();
                if (components != null)
                {
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        var typeName = comp.GetType().Name;
                        if (typeName == "MistRepellerFacilityComponentDef")
                        {
                            Traverse.Create(comp).Field("MaxMistRepellerRange")?.SetValue(2250f);
                            Debug.Log("[AAP] Mist Repeller range set to 2250.");
                            break;
                        }
                    }
                }
            }*/

            // Satellite Uplink
            /*var satelliteUplink = cache.GetDef<BaseDef>("SatelliteUplink_PhoenixFacilityDef");
            if (satelliteUplink != null)
            {
                var components = Traverse.Create(satelliteUplink).Field("GeoFacilityComponentDefs").GetValue<IList>();
                if (components != null)
                {
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        var typeName = comp.GetType().Name;
                        if (typeName == "SatelliteUplinkFacilityComponentDef")
                        {
                            var rangeIncrement = Traverse.Create(comp).Field("SiteScannerRangeIncrement").GetValue();
                            if (rangeIncrement != null)
                            {
                                Traverse.Create(rangeIncrement).Property("Value")?.SetValue(800f);
                                Debug.Log("[AAP] Satellite Uplink scanner range increment set to 800.");
                            }
                            break;
                        }
                    }
                }
            }*/

            // Access Lift Protection
            // AAP 1.1 NOTE: the real flag is PhoenixFacilityDef.CannotDemolish (a field).
            // The old property-name guessing (CanBeDemolished/IsIndestructible/...)
            // never matched anything - the "still demolishable" report. Verified
            // against the decompiled PhoenixFacilityDef.
            var accessLift = cache.GetDef<PhoenixPoint.Common.Entities.PhoenixFacilityDef>("AccessLift_PhoenixFacilityDef");
            if (accessLift != null)
            {
                accessLift.CannotDemolish = true;
                Debug.Log("[AAP] Access Lift: CannotDemolish = true.");
            }
            else
            {
                Debug.LogWarning("[AAP] AccessLift_PhoenixFacilityDef not found.");
            }
            // ===== Global Mist Repeller & Scanner max ranges (on GeoPhoenixBaseDef) =====
            /*{
                var repo = GameUtl.GameComponent<DefRepository>();
                var geoBase = repo.GetAllDefs<GeoPhoenixBaseDef>()
                    .FirstOrDefault(d => d.name.Contains("GeoPhoenixBaseDef"));
                if (geoBase != null)
                {
                    geoBase.MaxMistRepellerRange.Value = 2250f;
                    geoBase.MaxSiteScannerRange.Value = 800f;
                    Debug.Log("[AAP] GeoPhoenixBaseDef global ranges updated (Mist 2250, Scanner 800).");
                }
            }*/
        }
    }
}