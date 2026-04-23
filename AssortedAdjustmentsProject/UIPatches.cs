using Base.Core;
using Base.Defs;
using Base.UI;
using Base.UI.MessageBox;
using HarmonyLib;
using I2.Loc;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Entities.Abilities;
using PhoenixPoint.Geoscape.Entities.Sites;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View;
using PhoenixPoint.Geoscape.View.DataObjects;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Geoscape.View.ViewStates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    // ===== DISABLE RIGHT-CLICK MOVE (Dynamic Type Resolution) =====
    [HarmonyPatch]
    public static class DisableRightClickMovePatch
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("UIStateCharacterSelected");
            return AccessTools.Method(type, "OnRightClickMove");
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                var contextualMenuModule = traverse.Field("_contextualMenuModule").GetValue<object>();
                if (contextualMenuModule != null)
                {
                    bool isVisible = Traverse.Create(contextualMenuModule).Property("IsContextualMenuVisible").GetValue<bool>();
                    if (isVisible)
                        traverse.Method("CloseContextualMenu").GetValue();
                }
                return false; // Skip original move
            }
            catch (Exception e) { Debug.LogError($"[AAP] DisableRightClickMove failed: {e.Message}"); return true; }
        }
    }

    // ===== SCRAP AIRCRAFT (Dynamic Reflection for Private Fields) =====
    internal static class EnableScrapAircraft
    {
        internal static Color emptySlotDefaultColor = new Color32(0, 0, 0, 128);
        internal static string emptySlotDefaultText = "EMPTY";
        internal static string emptySlotScrapText = "SCRAP AIRCRAFT?";
        private class ContainerInfo { public string Name; public int Index; public ContainerInfo(string n, int i) { Name = n; Index = i; } }
        internal static MethodInfo UpdateResourceInfoMethod = typeof(UIModuleInfoBar).GetMethod("UpdateResourceInfo", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch]
        public static class GeoRosterContainterItem_Init_Patch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewControllers.Roster.GeoRosterContainterItem:Init");

            [HarmonyPrefix]
            public static void Prefix(object __instance)
            {
                try
                {
                    var emptySlot = Traverse.Create(__instance).Field("EmptySlot").GetValue<GameObject>();
                    if (emptySlot != null)
                    {
                        Text t = emptySlot.GetComponentInChildren<Text>(true);
                        if (t != null) { emptySlotDefaultColor = t.color; emptySlotDefaultText = t.text; }
                    }
                }
                catch (Exception e) { Debug.LogError($"[AAP] GeoRosterContainterItem_Init failed: {e.Message}"); }
            }
        }

        [HarmonyPatch]
        public static class GeoRosterContainterItem_Refresh_Patch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewControllers.Roster.GeoRosterContainterItem:Refresh");

            [HarmonyPostfix]
            public static void Postfix(object __instance)
            {
                try
                {
                    var traverse = Traverse.Create(__instance);
                    var container = traverse.Property("Container").GetValue<IGeoCharacterContainer>();
                    if (container.MaxCharacterSpace > 0 && container.CurrentOccupiedSpace == 0)
                    {
                        var emptySlot = traverse.Field("EmptySlot").GetValue<GameObject>();
                        if (emptySlot != null)
                        {
                            Text t = emptySlot.GetComponentInChildren<Text>(true);
                            if (t != null) t.text = (container.MaxCharacterSpace != int.MaxValue) ? emptySlotScrapText : emptySlotDefaultText;
                        }
                    }
                }
                catch (Exception e) { Debug.LogError($"[AAP] GeoRosterContainterItem_Refresh failed: {e.Message}"); }
            }
        }

        [HarmonyPatch]
        public static class UIStateGeoRoster_EnterState_Patch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewStates.UIStateGeoRoster:EnterState");

            [HarmonyPostfix]
            public static void Postfix(object __instance, List<IGeoCharacterContainer> ____characterContainers, GeoRosterFilterMode ____preferableFilterMode)
            {
                try
                {
                    var traverse = Traverse.Create(__instance);
                    var context = traverse.Property("Context").GetValue<GeoscapeViewContext>();
                    var geoRosterModule = traverse.Field("_geoRosterModule").GetValue<UIModuleGeneralPersonelRoster>();
                    var geoscapeModules = traverse.Field("_geoscapeModules").GetValue<GeoscapeModulesData>();
                    RefreshScrapTriggers();

                    void RefreshScrapTriggers()
                    {
                        for (int i = 0; i < geoRosterModule.Groups.Count; i++)
                        {
                            var c = geoRosterModule.Groups[i];
                            var emptySlot = Traverse.Create(c).Field("EmptySlot").GetValue<GameObject>();
                            if (emptySlot == null) continue;
                            if (!emptySlot.GetComponent<EventTrigger>()) emptySlot.AddComponent<EventTrigger>();
                            var et = emptySlot.GetComponent<EventTrigger>();
                            et.triggers.Clear();
                            c.Refresh();
                            if (c.Container.MaxCharacterSpace == int.MaxValue) continue;
                            var emptySlotText = emptySlot.GetComponentInChildren<Text>(true);
                            var info = new ContainerInfo(c.Container.Name, i);
                            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                            enter.callback.AddListener((_) => emptySlotText.color = Color.red);
                            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                            exit.callback.AddListener((_) => emptySlotText.color = emptySlotDefaultColor);
                            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                            click.callback.AddListener((_) => OnScrapAircraftClick(info));
                            et.triggers.Add(enter); et.triggers.Add(exit); et.triggers.Add(click);
                        }
                    }

                    void OnScrapAircraftClick(ContainerInfo info)
                    {
                        var aircraft = context.ViewerFaction.Vehicles.FirstOrDefault(v => v.Name == info.Name);
                        if (aircraft == null) return;
                        var utils = geoscapeModules.GeoscapeScreenUtilsModule;
                        string msg = utils.DismissVehiclePrompt.Localize(null);
                        var def = GameUtl.GameComponent<DefRepository>().GetAllDefs<VehicleItemDef>().FirstOrDefault(d => d.ComponentSetDef.Components.Contains(aircraft.VehicleDef));
                        if (def != null && !def.ScrapPrice.IsEmpty)
                        {
                            msg += "\n" + utils.ScrapResourcesBack.Localize(null) + "\n \n";
                            foreach (var ru in def.ScrapPrice)
                            {
                                if (ru.RoundedValue > 0)
                                {
                                    string r = ru.Type switch
                                    {
                                        ResourceType.Supplies => utils.ScrapSuppliesResources.Localize(null),
                                        ResourceType.Materials => utils.ScrapMaterialsResources.Localize(null),
                                        ResourceType.Tech => utils.ScrapTechResources.Localize(null),
                                        ResourceType.Mutagen => utils.ScrapMutagenResources.Localize(null),
                                        _ => ""
                                    };
                                    msg += r.Replace("{0}", ru.RoundedValue.ToString());
                                }
                            }
                        }
                        if (context.ViewerFaction.Vehicles.Count() <= 1)
                            GameUtl.GetMessageBox().ShowSimplePrompt("This is Phoenix Point's last aircraft available", MessageBoxIcon.Error, MessageBoxButtons.OK, _ => { }, null);
                        else
                            GameUtl.GetMessageBox().ShowSimplePrompt(string.Format(msg, info.Name), MessageBoxIcon.Warning, MessageBoxButtons.YesNo, result =>
                            {
                                if (result.DialogResult == MessageBoxResult.Yes)
                                {
                                    aircraft.Travelling = true; aircraft.Destroy();
                                    if (def != null && !def.ScrapPrice.IsEmpty)
                                    {
                                        context.Level.PhoenixFaction.Wallet.Give(def.ScrapPrice, OperationReason.Scrap);
                                        UpdateResourceInfoMethod.Invoke(geoscapeModules.ResourcesModule, new object[] { context.ViewerFaction, true });
                                    }
                                    ____characterContainers.RemoveAt(info.Index);
                                    geoRosterModule.Init(context, ____characterContainers, null, ____preferableFilterMode, RosterSelectionMode.SingleSelect);
                                    RefreshScrapTriggers();
                                }
                            }, info);
                    }
                }
                catch (Exception e) { Debug.LogError($"[AAP] UIStateGeoRoster_EnterState failed: {e.Message}"); }
            }
        }
    }

    // ===== EXTENDED AGENDA TRACKER ETA (unchanged, already dynamic) =====
    internal static class ExtendedAgendaTrackerETA
    {
        private static bool GetTravelTime(GeoVehicle v, out float t, GeoSite target = null)
        {
            t = 0f;
            if (target == null && v.FinalDestination == null) return false;
            var cur = v.CurrentSite?.WorldPosition ?? v.WorldPosition;
            var dest = target == null ? v.FinalDestination.WorldPosition : target.WorldPosition;
            var path = v.Navigation.FindPath(cur, dest, out bool ok);
            if (!ok || path.Count < 2) return false;
            float d = 0;
            for (int i = 0; i < path.Count - 1; i++) d += GeoMap.Distance(path[i].Pos.WorldPosition, path[i + 1].Pos.WorldPosition).Value;
            t = d / v.Stats.Speed.Value;
            return true;
        }
        private static float GetExplorationTime(GeoVehicle v, float h)
        {
            try { if (v == null) return h; var u = typeof(GeoVehicle).GetField("_explorationUpdateable", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(v); if (u == null) return h; var e = (NextUpdate)u.GetType().GetProperty("NextUpdate")?.GetValue(u); return (float)-(v.Timing.Now - e.NextTime).TimeSpan.TotalHours; }
            catch { return h; }
        }
        private static string AppendTime(float h)
        {
            var tu = TimeUnit.FromHours(h);
            var tf = new TimeRemainingFormatterDef { DaysText = new LocalizedTextBind("{0}d", true), HoursText = new LocalizedTextBind("{0}h", true) };
            return "   ~ " + UIUtil.FormatTimeRemaining(tu, tf);
        }

        [HarmonyPatch]
        public static class UIModuleSiteContextualMenu_SetMenuItems_Patch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() => AccessTools.Method("PhoenixPoint.Geoscape.View.ViewModules.UIModuleSiteContextualMenu:SetMenuItems");

            [HarmonyPostfix]
            public static void Postfix(object __instance, GeoSite site, List<SiteContextualMenuItem> ____menuItems)
            {
                try
                {
                    foreach (var item in ____menuItems)
                    {
                        GeoVehicle v = item.Ability?.GeoActor as GeoVehicle;
                        if (item.Ability is MoveVehicleAbility move && move.GeoActor is GeoVehicle mv && mv.CurrentSite != site)
                        {
                            if (GetTravelTime(mv, out float eta, site)) item.ItemText.text += AppendTime(eta);
                        }
                        else if (item.Ability is ExploreSiteAbility)
                        {
                            float h = GetExplorationTime(v, (float)site.ExplorationTime.TimeSpan.TotalHours);
                            item.ItemText.text += AppendTime(h);
                        }
                    }
                }
                catch (Exception e) { Debug.LogError($"[AAP] SetMenuItems ETA failed: {e.Message}"); }
            }
        }
    }
}