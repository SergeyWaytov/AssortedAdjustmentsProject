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
            if (getFreeElementMethod != null) return;
            Type t = typeof(UIModuleFactionAgendaTracker);
            getFreeElementMethod = t.GetMethod("GetFreeElement", BindingFlags.NonPublic | BindingFlags.Instance);
            onAddedElementMethod = t.GetMethod("OnAddedElement", BindingFlags.NonPublic | BindingFlags.Instance);
            updateDataMethod = t.GetMethod("UpdateData", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
            disposeElementMethod = t.GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        // CacheTracker
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
                    if (factionTracker == null)
                    {
                        factionTracker = (UIModuleFactionAgendaTracker)AccessTools.Property(__instance.GetType(), "_factionTracker").GetValue(__instance);
                        InitReflection();
                    }
                }
                catch (Exception e) { Debug.LogError($"[AAP] CacheTracker failed: {e.Message}"); }
            }
        }

        // ===== OnExcavationStarted (fault‑tolerant) =====
        [HarmonyPatch]
        public static class OnExcavationStartedPatch
        {
            private static bool patchDisabled = false;

            [HarmonyPrepare]
            public static bool Prepare()
            {
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.Core.GeoscapeLog");
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
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.Core.GeoscapeLog");
                return AccessTools.Method(type, "PhoenixFaction_OnExcavationStarted");
            }

            [HarmonyPostfix]
            public static void Postfix(GeoFaction faction, SiteExcavationState excavation)
            {
                if (patchDisabled) return;
                try
                {
                    if (factionTracker != null && faction is GeoPhoenixFaction)
                        AddOrUpdate(excavation.Site, $"{ActionExcavating} {excavation.Site.LocalizedSiteName}", "ArcheologyLab_PhoenixFacilityDef");
                }
                catch (Exception e) { Debug.LogError($"[AAP] OnExcavationStarted failed: {e.Message}"); }
            }
        }

        // OnExcavationComplete
        [HarmonyPatch]
        public static class OnExcavationCompletePatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewStates.UIStateVehicleSelected:OnVehicleSiteExcavated");

            [HarmonyPostfix]
            public static void Postfix(GeoPhoenixFaction faction, SiteExcavationState excavation)
            {
                try { if (factionTracker != null) Remove(excavation.Site); }
                catch (Exception e) { Debug.LogError($"[AAP] OnExcavationComplete failed: {e.Message}"); }
            }
        }

        // ===== OnDefenseScheduled (fault‑tolerant) =====
        [HarmonyPatch]
        public static class OnDefenseScheduledPatch
        {
            private static bool patchDisabled = false;

            [HarmonyPrepare]
            public static bool Prepare()
            {
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.Core.GeoscapeLog");
                if (type == null)
                {
                    Debug.LogWarning("[AAP] ExtendedAgendaTracker: GeoscapeLog type not found. OnDefenseScheduled patch disabled.");
                    patchDisabled = true;
                    return false;
                }
                var method = AccessTools.Method(type, "ShowSiteDefenseTimer");
                if (method == null)
                {
                    Debug.LogWarning("[AAP] ExtendedAgendaTracker: ShowSiteDefenseTimer method not found. Patch disabled.");
                    patchDisabled = true;
                    return false;
                }
                return true;
            }

            [HarmonyTargetMethod]
            public static MethodBase TargetMethod()
            {
                if (patchDisabled) return null;
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.Core.GeoscapeLog");
                return AccessTools.Method(type, "ShowSiteDefenseTimer");
            }

            [HarmonyPostfix]
            public static void Postfix(GeoFaction faction, SiteAttackSchedule target)
            {
                if (patchDisabled) return;
                try
                {
                    if (factionTracker == null) return;
                    string icon = target.Site.IsArcheologySite ? "ArcheologyLab_PhoenixFacilityDef" : "Crabman_ActorViewDef";
                    string factionName = faction.Name.Localize(null) ?? faction.Name.ToString();
                    AddOrUpdate(target.Site, $"{factionName.ToUpperInvariant()} {ActionAttack} {target.Site.LocalizedSiteName}", icon);
                    Traverse.Create(target.Site).Property("ExpiringTimerAt")?.SetValue(target.ScheduledFor);
                }
                catch (Exception e) { Debug.LogError($"[AAP] OnDefenseScheduled failed: {e.Message}"); }
            }
        }

        // AddRepairTrackers
        [HarmonyPatch]
        public static class AddRepairTrackersPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewModules.UIModuleFactionAgendaTracker:InitialSetup");

            [HarmonyPostfix]
            public static void Postfix(UIModuleFactionAgendaTracker __instance, GeoFaction ____faction)
            {
                try
                {
                    if (factionTracker == null) factionTracker = __instance;
                    InitReflection();
                    if (!(____faction is GeoPhoenixFaction pf)) return;
                    foreach (var f in pf.Bases.SelectMany(b => b.Layout.Facilities).Where(f => f.IsRepairing && f.GetTimeLeftToUpdate() != TimeUnit.Zero))
                    {
                        string n = LocalizationManager.GetTranslation(f.Def.ViewElementDef.DisplayName1.LocalizationKey);
                        AddOrUpdate(f, $"{ActionRepairing} {n}", f.Def.ViewElementDef);
                    }
                }
                catch (Exception e) { Debug.LogError($"[AAP] AddRepairTrackers failed: {e.Message}"); }
            }
        }

        private static void AddOrUpdate(object obj, string text, string iconDefName)
        {
            if (factionTracker == null) return;
            var elements = (List<UIFactionDataTrackerElement>)AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements").GetValue(factionTracker);
            foreach (var e in elements) { if (e.TrackedObject == obj) { e.TrackedName.text = text; updateDataMethod?.Invoke(factionTracker, null); return; } }
            var viewDef = GameUtl.GameComponent<DefRepository>().DefRepositoryDef.AllDefs.OfType<ViewElementDef>().FirstOrDefault(d => d.name.Contains(iconDefName));
            var free = (UIFactionDataTrackerElement)getFreeElementMethod?.Invoke(factionTracker, null);
            if (free != null) { free.Init(obj, text, viewDef, false); onAddedElementMethod?.Invoke(factionTracker, new[] { free }); }
        }

        private static void AddOrUpdate(object obj, string text, ViewElementDef viewDef)
        {
            if (factionTracker == null) return;
            var elements = (List<UIFactionDataTrackerElement>)AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements").GetValue(factionTracker);
            foreach (var e in elements) { if (e.TrackedObject == obj) { e.TrackedName.text = text; updateDataMethod?.Invoke(factionTracker, null); return; } }
            var free = (UIFactionDataTrackerElement)getFreeElementMethod?.Invoke(factionTracker, null);
            if (free != null) { free.Init(obj, text, viewDef, false); onAddedElementMethod?.Invoke(factionTracker, new[] { free }); }
        }

        private static void Remove(object obj)
        {
            if (factionTracker == null) return;
            var elements = (List<UIFactionDataTrackerElement>)AccessTools.Field(typeof(UIModuleFactionAgendaTracker), "_currentTrackedElements").GetValue(factionTracker);
            foreach (var e in elements) { if (e.TrackedObject == obj) { disposeElementMethod?.Invoke(factionTracker, new[] { e }); break; } }
            updateDataMethod?.Invoke(factionTracker, null);
        }

        // UpdateDataPrefix
        [HarmonyPatch]
        public static class UpdateDataPrefixPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewModules.UIModuleFactionAgendaTracker:UpdateData", new[] { typeof(UIFactionDataTrackerElement) });

            [HarmonyPrefix]
            public static bool Prefix(UIModuleFactionAgendaTracker __instance, ref bool __result, UIFactionDataTrackerElement element, GeoscapeViewContext ____context)
            {
                try
                {
                    if (element.TrackedObject is GeoPhoenixFacility f && f.IsRepairing) { var t = f.GetTimeLeftToUpdate(); element.UpdateData(t, true, null); __result = t <= TimeUnit.Zero; return false; }
                    if (element.TrackedObject is GeoSite site)
                    {
                        if (site.IsArcheologySite && !site.IsOwnedByViewer)
                        {
                            var exc = site.GeoLevel.PhoenixFaction.ExcavatingSites.FirstOrDefault(s => s.Site == site);
                            if (exc != null) { var t = TimeUnit.FromHours((float)(exc.ExcavationEndDate - ____context.Level.Timing.Now).TimeSpan.TotalHours); element.UpdateData(t, true, null); __result = t <= TimeUnit.Zero; return false; }
                        }
                        else if (site.IsOwnedByViewer)
                        {
                            foreach (var fac in ____context.Level.Factions)
                            {
                                if (fac.IsViewerFaction || fac.IsEnvironmentFaction || fac.IsNeutralFaction) continue;
                                var sch = (site.Type == GeoSiteType.PhoenixBase) ? fac.PhoenixBaseAttackSchedule.FirstOrDefault(s => s.Site == site) : fac.AncientSiteAttackSchedule.FirstOrDefault(s => s.Site == site);
                                if (sch != null && sch.HasAttackScheduled) { var t = TimeUnit.FromHours((float)(sch.ScheduledFor - ____context.Level.Timing.Now).TimeSpan.TotalHours); element.UpdateData(t, true, null); __result = t <= TimeUnit.Zero; return false; }
                            }
                        }
                    }
                }
                catch (Exception e) { Debug.LogError($"[AAP] UpdateDataPrefix failed: {e.Message}"); }
                return true;
            }
        }

        // AddClickToFocus
        [HarmonyPatch]
        public static class AddClickToFocusPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewModules.UIModuleFactionAgendaTracker:UpdateData", new[] { typeof(UIFactionDataTrackerElement) });

            [HarmonyPostfix]
            public static void Postfix(UIModuleFactionAgendaTracker __instance, UIFactionDataTrackerElement element, GeoscapeViewContext ____context)
            {
                try
                {
                    if (element == null || ____context == null) return;
                    var go = element.gameObject;
                    var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
                    et.triggers.Clear();
                    var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                    click.callback.AddListener((_) => {
                        switch (element.TrackedObject)
                        {
                            case GeoVehicle v: ____context.View.ChaseTarget(v, false); break;
                            case GeoSite s: ____context.View.ChaseTarget(s, false); break;
                            case GeoPhoenixFacility f: if (f.PxBase?.Site != null) ____context.View.ChaseTarget(f.PxBase.Site, false); break;
                            case ResearchElement r: ____context.Level.Timing.Paused = true; ____context.View.ToResearchState(); break;
                            case ItemManufacturing.ManufactureQueueItem m: ____context.Level.Timing.Paused = true; ____context.View.ToManufacturingState(null, null, StateStackAction.ClearStackAndPush); break;
                        }
                    });
                    et.triggers.Add(click);
                }
                catch (Exception e) { Debug.LogError($"[AAP] AddClickToFocus failed: {e.Message}"); }
            }
        }

        // AddHoverEffect
        [HarmonyPatch]
        public static class AddHoverEffectPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewControllers.UIFactionDataTrackerElement:Init");

            [HarmonyPostfix]
            public static void Postfix(UIFactionDataTrackerElement __instance)
            {
                try
                {
                    var go = __instance.gameObject;
                    var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
                    var orig = __instance.TrackedName?.color ?? Color.white;
                    var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    enter.callback.AddListener((_) => { if (__instance.TrackedName != null) __instance.TrackedName.color = Color.yellow; });
                    var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                    exit.callback.AddListener((_) => { if (__instance.TrackedName != null) __instance.TrackedName.color = orig; });
                    et.triggers.Add(enter); et.triggers.Add(exit);
                }
                catch (Exception e) { Debug.LogError($"[AAP] AddHoverEffect failed: {e.Message}"); }
            }
        }
    }
}