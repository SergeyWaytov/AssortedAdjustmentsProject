using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Diagnostic tool to verify the existence of all UI Harmony patch targets.
    /// Call UIDiagnostics.Run() from ModMain.OnModEnabled() to log results.
    /// No patches are applied; this only checks type/method availability.
    /// </summary>
    public static class UIDiagnostics
    {
        private class PatchTarget
        {
            public string FileName;
            public string TypeName;
            public string MethodName;
            public Type[] ParameterTypes; // null means any signature

            public PatchTarget(string file, string type, string method, Type[] paramTypes = null)
            {
                FileName = file;
                TypeName = type;
                MethodName = method;
                ParameterTypes = paramTypes;
            }
        }

        public static void Run()
        {
            Debug.Log("[AAP DIAGNOSTIC] === Starting UI Patch Target Verification ===");

            var targets = new List<PatchTarget>
            {
                // UI_SmartBaseSelection.cs
                new PatchTarget("UI_SmartBaseSelection", "PhoenixPoint.Geoscape.View.ViewStates.UIStateGeoBases", "EnterState"),
                
                // UI_RecruitInfo.cs
                new PatchTarget("UI_RecruitInfo", "PhoenixPoint.Geoscape.View.ViewModules.UIModuleHavenInfo", "SetZoneInfo"),
                new PatchTarget("UI_RecruitInfo", "PhoenixPoint.Geoscape.View.ViewModules.UIModuleHavenInfo", "Show"),
                
                // UIPatches.cs - DisableRightClickMovePatch
                new PatchTarget("UIPatches (RightClick)", "UIStateCharacterSelected", "OnRightClickMove"),
                
                // UIPatches.cs - EnableScrapAircraft
                new PatchTarget("UIPatches (ScrapAircraft)", "PhoenixPoint.Geoscape.View.ViewControllers.Roster.GeoRosterContainterItem", "Init"),
                new PatchTarget("UIPatches (ScrapAircraft)", "PhoenixPoint.Geoscape.View.ViewControllers.Roster.GeoRosterContainterItem", "Refresh"),
                new PatchTarget("UIPatches (ScrapAircraft)", "PhoenixPoint.Geoscape.View.ViewStates.UIStateGeoRoster", "EnterState"),
                
                // UIPatches.cs - ExtendedAgendaTrackerETA
                new PatchTarget("UIPatches (AgendaETA)", "PhoenixPoint.Geoscape.View.ViewModules.UIModuleSiteContextualMenu", "SetMenuItems"),
                
                // UI_ExtendedAgendaTracker.cs
                new PatchTarget("ExtendedAgendaTracker", "PhoenixPoint.Geoscape.View.ViewStates.UIStateVehicleSelected", "EnterState"),
                new PatchTarget("ExtendedAgendaTracker", "PhoenixPoint.Geoscape.Core.GeoscapeLog", "PhoenixFaction_OnExcavationStarted"),
                new PatchTarget("ExtendedAgendaTracker", "PhoenixPoint.Geoscape.View.ViewStates.UIStateVehicleSelected", "OnVehicleSiteExcavated"),
                new PatchTarget("ExtendedAgendaTracker", "PhoenixPoint.Geoscape.Core.GeoscapeLog", "ShowSiteDefenseTimer"),
                new PatchTarget("ExtendedAgendaTracker", "PhoenixPoint.Geoscape.View.ViewModules.UIModuleFactionAgendaTracker", "InitialSetup"),
                new PatchTarget("ExtendedAgendaTracker", "PhoenixPoint.Geoscape.View.ViewModules.UIModuleFactionAgendaTracker", "UpdateData", new[] { typeof(PhoenixPoint.Geoscape.View.ViewControllers.UIFactionDataTrackerElement) }),
                new PatchTarget("ExtendedAgendaTracker", "PhoenixPoint.Geoscape.View.ViewControllers.UIFactionDataTrackerElement", "Init"),
                
                // UI_ExtendedBaselineInfo.cs
                new PatchTarget("ExtendedBaseInfo", "PhoenixPoint.Geoscape.View.ViewModules.UIModuleBaseInfo", "SetInfo"),
            };

            int found = 0, missing = 0;

            foreach (var t in targets)
            {
                var type = AccessTools.TypeByName(t.TypeName);
                if (type == null)
                {
                    Debug.LogWarning($"[AAP DIAGNOSTIC] MISSING TYPE: {t.FileName} -> {t.TypeName}");
                    missing++;
                    continue;
                }

                MethodBase method;
                if (t.ParameterTypes != null)
                    method = AccessTools.Method(type, t.MethodName, t.ParameterTypes);
                else
                    method = AccessTools.Method(type, t.MethodName);

                if (method != null)
                {
                    Debug.Log($"[AAP DIAGNOSTIC] FOUND: {t.FileName} -> {type.Name}.{method.Name}");
                    found++;
                }
                else
                {
                    // Try to list available methods for debugging
                    var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    Debug.LogWarning($"[AAP DIAGNOSTIC] MISSING METHOD: {t.FileName} -> {t.TypeName}.{t.MethodName}");
                    Debug.LogWarning($"[AAP DIAGNOSTIC]   Available methods on {type.Name}:");
                    foreach (var m in allMethods)
                        Debug.LogWarning($"[AAP DIAGNOSTIC]     - {m.Name}");
                    missing++;
                }
            }

            Debug.Log($"[AAP DIAGNOSTIC] === Verification complete: {found} found, {missing} missing ===");
        }
    }
}