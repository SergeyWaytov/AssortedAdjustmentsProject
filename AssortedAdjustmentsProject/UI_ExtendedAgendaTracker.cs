using Base.Core;
using Base.Defs;
using Base.UI;
using HarmonyLib;
using I2.Loc;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Geoscape.Core;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Entities.PhoenixBases;
using PhoenixPoint.Geoscape.Entities.Research;
using PhoenixPoint.Geoscape.Entities.Sites;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Geoscape.Levels.Factions.Archeology;
using PhoenixPoint.Geoscape.View;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Geoscape.View.ViewStates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    internal static class ExtendedAgendaTrackerFull
    {
        private static string ActionRepairing => ModMain.Localize("Repairing");
        private static string ActionExcavating => ModMain.Localize("Excavating");
        private static string ActionAttack => ModMain.Localize("WillAttack");

        private static MethodInfo getFreeElementMethod, onAddedElementMethod, updateDataMethod, disposeElementMethod;
        private static UIModuleFactionAgendaTracker factionTracker;

        private static void InitReflection()
        {
            try
            {
                if (getFreeElementMethod != null) return;
                Type t = typeof(UIModuleFactionAgendaTracker);
                getFreeElementMethod = t.GetMethod("GetFreeElement", BindingFlags.NonPublic | BindingFlags.Instance);
                onAddedElementMethod = t.GetMethod("OnAddedElement", BindingFlags.NonPublic | BindingFlags.Instance);
                updateDataMethod = t.GetMethod("UpdateData", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
                disposeElementMethod = t.GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception e) { Debug.LogError($"[AAP] InitReflection failed: {e.Message}"); }
        }

        // Cache the tracker reference
        
        [HarmonyPatch]
        public static class CacheTrackerPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewStates.UIStateVehicleSelected:EnterState");

            [HarmonyPostfix]
            public static void Postfix(object __instance)
            {
                try
                {
                    if (__instance == null) return;
                    if (factionTracker == null)
                    {
                        var prop = AccessTools.Property(__instance.GetType(), "_factionTracker");
                        if (prop != null)
                        {
                            factionTracker = (UIModuleFactionAgendaTracker)prop.GetValue(__instance);
                            InitReflection();
                        }
                    }
                }
                catch (Exception e) { Debug.LogError($"[AAP] CacheTracker failed: {e.Message}"); }
            }
        }
        
        // Excavation start tracker
        [HarmonyPatch]
        public static class OnExcavationStartedPatch
        {
            private static bool patchDisabled = false;

            [HarmonyPrepare]
            public static bool Prepare()
            {
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.Levels.GeoscapeLog");
                if (type == null)
                {
                    Debug.LogWarning("[AAP] ExtendedAgendaTracker: GeoscapeLog type not found. OnExcavationStarted patch disabled.");
                    patchDisabled = true;
                    return false;
                }
                var method = AccessTools.Method(type, "PhoenixFaction_OnExcavationStarted");
                if (method == null)
                {
                    Debug.LogWarning("[AAP] ExtendedAgendaTracker: PhoenixFaction_OnExcavationStarted method not found. Patch disabled.");
                    patchDisabled = true;
                    return false;
                }
                return true;
            }

            [HarmonyTargetMethod]
            public static MethodBase TargetMethod()
            {
                if (patchDisabled) return null;
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.Levels.GeoscapeLog");
                return AccessTools.Method(type, "PhoenixFaction_OnExcavationStarted");
            }

            [HarmonyPostfix]
            public static void Postfix(GeoFaction faction, SiteExcavationState excavation)
            {
                if (patchDisabled) return;
                try
                {
                    if (factionTracker == null || excavation?.Site == null || !(faction is GeoPhoenixFaction)) return;
                    AddOrUpdate(excavation.Site, $"{ActionExcavating} {excavation.Site.LocalizedSiteName}", "ArcheologyLab_PhoenixFacilityDef");
                }
                catch (Exception e) { Debug.LogError($"[AAP] OnExcavationStarted failed: {e.Message}"); }
            }
        }

        // Excavation complete tracker
        [HarmonyPatch]
        public static class OnExcavationCompletePatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewStates.UIStateVehicleSelected:OnVehicleSiteExcavated");

            [HarmonyPostfix]
            public static void Postfix(GeoPhoenixFaction faction, SiteExcavationState excavation)
            {
                try
                {
                    if (factionTracker != null && excavation?.Site != null)
                        Remove(excavation.Site);
                }
                catch (Exception e) { Debug.LogError($"[AAP] OnExcavationComplete failed: {e.Message}"); }
            }
        }
        /*
        // ========== Defence scheduled tracker – robust version ==========
        [HarmonyPatch]
        public static class OnDefenseScheduledPatch
        {
            [HarmonyTargetMethod]
            public static System.Reflection.MethodBase TargetMethod()
            {
                // This method is called every time an attack is scheduled against a Phoenix base or Ancient site.
                return AccessTools.Method("PhoenixPoint.Geoscape.Levels.Factions.GeoPhoenixFaction:AddDefenseTimer");
            }

            [HarmonyPostfix]
            public static void Postfix(GeoFaction faction, object target)
            {
                try
                {
                    if (factionTracker == null) return;

                    // The 'target' parameter is a SiteAttackSchedule object.
                    SiteAttackSchedule sch = target as SiteAttackSchedule;
                    if (sch == null || sch.Site == null) return;

                    string siteName = sch.Site.LocalizedSiteName;
                    if (string.IsNullOrEmpty(siteName)) return;

                    string factionName = faction?.Name?.Localize(null) ?? "???";
                    string text = $"{factionName.ToUpperInvariant()} {ModMain.Localize("WillAttack")} {siteName}";

                    string iconDefName = sch.Site.IsArcheologySite ? "ArcheologyLab_PhoenixFacilityDef" : "Crabman_ActorViewDef";

                    AddOrUpdate(sch.Site, text, iconDefName);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[AAP] OnDefenseScheduled failed: {e.Message}");
                }
            }
        }
        
        // Repair trackers on init
        [HarmonyPatch]
        public static class AddRepairTrackersPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() =>
                AccessTools.Method("PhoenixPoint.Geoscape.View.ViewModules.UIModuleFactionAgendaTracker:InitialSetup");

            [HarmonyPostfix]
            public static void Postfix(UIModuleFactionAgendaTracker __instance, GeoFaction ____faction)
            {
                try
                {
                    if (__instance == null) return;
                    factionTracker = __instance;
                    InitReflection();

                    if (!(____faction is GeoPhoenixFaction pf) || pf.Bases == null) return;

                    foreach (var b in pf.Bases)
                    {
                        if (b?.Layout?.Facilities == null) continue;
                        foreach (var f in b.Layout.Facilities)
                        {
                            // Null‑checks for every element in the chain
                            if (f == null || !f.IsRepairing) continue;
                            if (f.GetTimeLeftToUpdate() == TimeUnit.Zero) continue;
                            if (f.Def?.ViewElementDef?.DisplayName1 == null) continue;

                            try
                            {
                                string n = LocalizationManager.GetTranslation(
                                    f.Def.ViewElementDef.DisplayName1.LocalizationKey);
                                AddOrUpdate(f, $"{ActionRepairing} {n}", f.Def.ViewElementDef);
                            }
                            catch (Exception facilityEx)
                            {
                                Debug.LogError($"[AAP] Failed to add repair tracker for facility {f.Def?.name}: {facilityEx.Message}");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AAP] AddRepairTrackers failed: {e.Message}");
                }
            }
        }
        */

        // Helper: AddOrUpdate by icon name
        private static void AddOrUpdate(object obj, string text, string iconDefName)
        {
            try
            {
                if (factionTracker == null || obj == null || text == null) return;
                var elementsField = AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements");
                if (elementsField == null) return;
                var elements = elementsField.GetValue(factionTracker) as List<UIFactionDataTrackerElement>;
                if (elements == null) return;

                foreach (var e in elements)
                {
                    if (e?.TrackedObject == obj)
                    {
                        if (e.TrackedName != null) e.TrackedName.text = text;
                        updateDataMethod?.Invoke(factionTracker, null);
                        return;
                    }
                }

                var viewDef = GameUtl.GameComponent<DefRepository>()?.DefRepositoryDef?.AllDefs?.OfType<ViewElementDef>()
                    .FirstOrDefault(d => d.name.Contains(iconDefName));
                if (viewDef == null) return;

                var free = (UIFactionDataTrackerElement)getFreeElementMethod?.Invoke(factionTracker, null);
                if (free == null) return;

                free.Init(obj, text, viewDef, false);
                onAddedElementMethod?.Invoke(factionTracker, new[] { free });
            }
            catch (Exception e) { Debug.LogError($"[AAP] AddOrUpdate(string) failed: {e.Message}"); }
        }

        // Helper: AddOrUpdate by ViewElementDef
        private static void AddOrUpdate(object obj, string text, ViewElementDef viewDef)
        {
            try
            {
                if (factionTracker == null || obj == null || text == null || viewDef == null) return;
                var elementsField = AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements");
                if (elementsField == null) return;
                var elements = elementsField.GetValue(factionTracker) as List<UIFactionDataTrackerElement>;
                if (elements == null) return;

                foreach (var e in elements)
                {
                    if (e?.TrackedObject == obj)
                    {
                        if (e.TrackedName != null) e.TrackedName.text = text;
                        updateDataMethod?.Invoke(factionTracker, null);
                        return;
                    }
                }

                var free = (UIFactionDataTrackerElement)getFreeElementMethod?.Invoke(factionTracker, null);
                if (free == null) return;

                free.Init(obj, text, viewDef, false);
                onAddedElementMethod?.Invoke(factionTracker, new[] { free });
            }
            catch (Exception e) { Debug.LogError($"[AAP] AddOrUpdate(viewDef) failed: {e.Message}"); }
        }

        // Helper: Remove tracker
        private static void Remove(object obj)
        {
            try
            {
                if (factionTracker == null || obj == null) return;
                var elementsField = AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements");
                if (elementsField == null) return;
                var elements = elementsField.GetValue(factionTracker) as List<UIFactionDataTrackerElement>;
                if (elements == null) return;

                for (int i = 0; i < elements.Count; i++)
                {
                    if (elements[i]?.TrackedObject == obj)
                    {
                        disposeElementMethod?.Invoke(factionTracker, new[] { elements[i] });
                        elements.RemoveAt(i);
                        break;
                    }
                }
                updateDataMethod?.Invoke(factionTracker, null);
            }
            catch (Exception e) { Debug.LogError($"[AAP] Remove failed: {e.Message}"); }
        }

        // Replace the timer update logic – patched with null guards
        
        [HarmonyPatch]
        public static class UpdateDataPrefixPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() =>
                AccessTools.Method("PhoenixPoint.Geoscape.View.ViewModules.UIModuleFactionAgendaTracker:UpdateData",
                    new[] { typeof(UIFactionDataTrackerElement) });

            [HarmonyPrefix]
            public static bool Prefix(UIModuleFactionAgendaTracker __instance, ref bool __result,
                UIFactionDataTrackerElement element, GeoscapeViewContext ____context)
            {
                try
                {
                    if (element == null || element.TrackedObject == null || ____context == null)
                        return true; // let the original method handle it

                    if (element.TrackedObject is GeoPhoenixFacility f && f.IsRepairing)
                    {
                        var t = f.GetTimeLeftToUpdate();
                        element.UpdateData(t, true, null);
                        __result = t <= TimeUnit.Zero;
                        return false;
                    }

                    if (element.TrackedObject is GeoSite site)
                    {
                        if (site.IsArcheologySite && !site.IsOwnedByViewer)
                        {
                            var phoenixFaction = ____context.Level?.PhoenixFaction;
                            if (phoenixFaction == null) return true;

                            var exc = phoenixFaction.ExcavatingSites?
                                .FirstOrDefault(s => s.Site == site);
                            if (exc != null && exc.ExcavationEndDate > ____context.Level.Timing.Now)
                            {
                                var hours = (float)(exc.ExcavationEndDate - ____context.Level.Timing.Now).TimeSpan.TotalHours;
                                if (float.IsNaN(hours) || hours < 0f) hours = 0f;
                                var t = TimeUnit.FromHours(hours);
                                element.UpdateData(t, true, null);
                                __result = t <= TimeUnit.Zero;
                                return false;
                            }
                        }
                        else if (site.IsOwnedByViewer && ____context.Level?.Factions != null)
                        {
                            foreach (var fac in ____context.Level.Factions)
                            {
                                if (fac == null || fac.IsViewerFaction || fac.IsEnvironmentFaction || fac.IsNeutralFaction)
                                    continue;

                                SiteAttackSchedule sch = null;
                                if (site.Type == GeoSiteType.PhoenixBase && fac.PhoenixBaseAttackSchedule != null)
                                    sch = fac.PhoenixBaseAttackSchedule.FirstOrDefault(s => s.Site == site);
                                else if (site.Type != GeoSiteType.PhoenixBase && fac.AncientSiteAttackSchedule != null)
                                    sch = fac.AncientSiteAttackSchedule.FirstOrDefault(s => s.Site == site);

                                if (sch == null || !sch.HasAttackScheduled) continue;
                                if (sch.ScheduledFor <= ____context.Level.Timing.Now) continue;

                                var hours = (float)(sch.ScheduledFor - ____context.Level.Timing.Now).TimeSpan.TotalHours;
                                if (float.IsNaN(hours) || hours < 0f) hours = 0f;
                                var t = TimeUnit.FromHours(hours);
                                element.UpdateData(t, true, null);
                                __result = t <= TimeUnit.Zero;
                                return false;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AAP] UpdateDataPrefix failed: {e.Message}");
                }
                return true; // fallback to original
            }
        }
        
        // Click-to-focus patch
        [HarmonyPatch]
        public static class AddClickToFocusPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() =>
                AccessTools.Method("PhoenixPoint.Geoscape.View.ViewModules.UIModuleFactionAgendaTracker:UpdateData",
                    new[] { typeof(UIFactionDataTrackerElement) });

            [HarmonyPostfix]
            public static void Postfix(UIModuleFactionAgendaTracker __instance, UIFactionDataTrackerElement element,
                GeoscapeViewContext ____context)
            {
                try
                {
                    if (element == null || ____context?.View == null) return;
                    var go = element.gameObject;
                    if (go == null) return;

                    var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
                    et.triggers.Clear();
                    var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                    click.callback.AddListener((_) =>
                    {
                        try
                        {
                            switch (element.TrackedObject)
                            {
                                case GeoVehicle v: ____context.View.ChaseTarget(v, false); break;
                                case GeoSite s: ____context.View.ChaseTarget(s, false); break;
                                case GeoPhoenixFacility f:
                                    if (f.PxBase?.Site != null) ____context.View.ChaseTarget(f.PxBase.Site, false);
                                    break;
                                case ResearchElement r:
                                    ____context.Level.Timing.Paused = true;
                                    ____context.View.ToResearchState();
                                    break;
                                case ItemManufacturing.ManufactureQueueItem m:
                                    ____context.Level.Timing.Paused = true;
                                    ____context.View.ToManufacturingState(null, null, StateStackAction.ClearStackAndPush);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[AAP] Click-to-focus callback failed: {ex.Message}");
                        }
                    });
                    et.triggers.Add(click);
                }
                catch (Exception e) { Debug.LogError($"[AAP] AddClickToFocus failed: {e.Message}"); }
            }
        }

        // Hover effect patch
        [HarmonyPatch]
        public static class AddHoverEffectPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() =>
                AccessTools.Method("PhoenixPoint.Geoscape.View.ViewControllers.UIFactionDataTrackerElement:Init");

            [HarmonyPostfix]
            public static void Postfix(UIFactionDataTrackerElement __instance)
            {
                try
                {
                    if (__instance == null) return;
                    var go = __instance.gameObject;
                    if (go == null) return;

                    var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
                    var orig = __instance.TrackedName?.color ?? Color.white;
                    var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    enter.callback.AddListener((_) =>
                    {
                        if (__instance.TrackedName != null) __instance.TrackedName.color = Color.yellow;
                    });
                    var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                    exit.callback.AddListener((_) =>
                    {
                        if (__instance.TrackedName != null) __instance.TrackedName.color = orig;
                    });
                    et.triggers.Add(enter);
                    et.triggers.Add(exit);
                }
                catch (Exception e) { Debug.LogError($"[AAP] AddHoverEffect failed: {e.Message}"); }
            }
        }
    }
}