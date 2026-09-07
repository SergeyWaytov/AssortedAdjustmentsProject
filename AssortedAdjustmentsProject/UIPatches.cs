using Base.Core;
using Base.Defs;
using Base.UI;
using Base.UI.MessageBox;
using HarmonyLib;
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
            // AAP G1 (live-guard variant): re-check the toggle on every patch
            // installation instead of latching the value at first Prepare().
            return ModMain.Cfg?.DisableRightClickMove != false;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            // AAP B4: full type name (PhoenixPoint.Tactical.View.ViewStates.UIStateCharacterSelected)
            // resolves unambiguously; the short name collides with the Geoscape-side
            // UIStateCharacterSelected type in some builds.
            Type type = AccessTools.TypeByName("PhoenixPoint.Tactical.View.ViewStates.UIStateCharacterSelected");
            return AccessTools.Method(type, "OnRightClickMove");
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance)
        {
            try
            {
                // AAP U8: _contextualMenuModule is a PROPERTY (not a field), and
                // CloseContextualMenu takes one Boolean. The old Field() +
                // no-arg Method() calls silently no-op'd, so the menu never
                // closed when right-click suppressed a move order.
                Traverse state = Traverse.Create(__instance);
                object contextualMenuModule = state.Property("_contextualMenuModule").GetValue<object>();
                if (contextualMenuModule != null)
                {
                    state.Method("CloseContextualMenu", new object[] { true }).GetValue();
                }
                return false; // Skip original move
            }
            catch (Exception e) { Debug.LogError($"[AAP] DisableRightClickMove failed: {e.Message}"); return true; }
        }
    }

    // AAP U7: ExtendedAgendaTrackerETA and the nested
    // UIModuleSiteContextualMenu_SetMenuItems_Patch class have been DELETEd.
    // They duplicated vanilla's site-contextual-menu ETA computation (the
    // game already shows travel/exploration ETA in those menus) and were
    // a maintenance trap. The B6 raw-literal fallbacks at the old lines
    // 108-109 ({0}d / {0}h) lived inside the deleted class and are gone too.
}
