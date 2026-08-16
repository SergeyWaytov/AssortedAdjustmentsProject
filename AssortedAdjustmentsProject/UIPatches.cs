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
    // Configurable since AAP 1.1: some players found the change awkward with
    // vehicles (Workshop feedback) - toggle "Disable right-click move" in the
    // mod options (main menu).
    [HarmonyPatch]
    public static class DisableRightClickMovePatch
    {
        public static bool Prepare()
        {
            return ModMain.Cfg?.DisableRightClickMove != false;
        }

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

    // ===== EXTENDED AGENDA TRACKER ETA =====
    
    internal static class ExtendedAgendaTrackerETA
    {
        private static bool GetTravelTime(GeoVehicle v, out float t, GeoSite target = null)
        {
            t = 0f;
            // ---- ADDED NULL CHECKS ----
            if (v == null || v.Navigation == null) return false;
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
            // ---- ADDED NULL CHECK ----
            if (v == null) return h;

            try
            {
                var u = typeof(GeoVehicle).GetField("_explorationUpdateable", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(v);
                if (u == null) return h;
                var e = (NextUpdate)u.GetType().GetProperty("NextUpdate")?.GetValue(u);
                return (float)-(v.Timing.Now - e.NextTime).TimeSpan.TotalHours;
            }
            catch { return h; }
        }

        private static string AppendTime(float h)
        {
            var tu = TimeUnit.FromHours(h);
            // ---- FIX: Use ScriptableObject.CreateInstance instead of new ----
            var tf = ScriptableObject.CreateInstance<TimeRemainingFormatterDef>();
            tf.DaysText = new LocalizedTextBind("{0}d", true);
            tf.HoursText = new LocalizedTextBind("{0}h", true);
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
                            // ---- ADDED NULL CHECKS ----
                            if (mv == null || mv.Navigation == null)
                                continue;

                            if (GetTravelTime(mv, out float eta, site))
                                item.ItemText.text += AppendTime(eta);
                        }
                        else if (item.Ability is ExploreSiteAbility)
                        {
                            // ---- ADDED NULL CHECKS ----
                            if (v == null || v.Navigation == null)
                                continue;

                            float h = GetExplorationTime(v, (float)site.ExplorationTime.TimeSpan.TotalHours);
                            item.ItemText.text += AppendTime(h);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AAP] SetMenuItems ETA failed: {e.Message}");
                }
            }
        }
    }
    
}