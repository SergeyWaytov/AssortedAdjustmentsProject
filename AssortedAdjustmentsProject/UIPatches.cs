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
    // ===== MOD OPTIONS LOCALIZATION RE-ASSERT (menu) =====
    // AAP 1.2.2 fix: the boot-time import can run before the game finishes
    // populating source 0's language table (Sep-23 build), so the Mod Options
    // panel can render English even in RU locale until a level is loaded. The
    // level-start patch (PsychicRetroactiveGates_Patch) already re-asserts on
    // save load; this covers the main-menu path - the moment the mod
    // management screen opens, before its panel is built.
    [HarmonyPatch(typeof(PhoenixPoint.Home.View.ViewStates.UIStateModManagment), "EnterState")]
    public static class ModOptionsLocalizationReassert_Patch
    {
        static void Postfix()
        {
            try
            {
                if (ModMain.Self == null) return;
                ModMain.Self.ImportLocalization();
                I2.Loc.LocalizationManager.LocalizeAll(true);
            }
            catch (Exception e)
            {
                Debug.LogError("[AAP] Mod Options localization re-assert failed: " + e);
            }
        }
    }

    // ===== SMART RIGHT-CLICK (accidental-move guard) =====
    // AAP 1.2.2: replaces the blunt "RMB never moves" patch. Root cause of the
    // accidental moves players reported: vanilla's OnRightClickMove closes a
    // contextual menu on RMB and sets a 0.5s cooldown - but a SECOND right
    // click after that cooldown (the natural "close it again" habit, or just
    // the next cancel-click) issues a move to whatever tile is under the
    // cursor. The old fix disabled RMB-move entirely, which also hit every
    // other use of the shared UIStateCharacterSelected state. Vehicles have
    // their own selected state and never use "Right Mouse Click Move", so
    // they were collateral damage of the global suppression.
    //
    // Proper fix: RMB-move stays fully available; a right click is only
    // swallowed when it lands on the exact tile that was under the cursor
    // when a contextual menu closed, within a short echo window (1.2s).
    // Moving the mouse to a different tile = intentional move, always allowed.
    [HarmonyPatch]
    public static class SmartRightClickPatch
    {
        private static Vector3? _lastCloseHover;
        private static float _lastCloseTime;

        public static bool Prepare()
        {
            return ModMain.Cfg != null;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("PhoenixPoint.Tactical.View.ViewStates.UIStateCharacterSelected");
            return AccessTools.Method(type, "OnRightClickMove");
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance)
        {
            try
            {
                if (ModMain.Cfg?.DisableRightClickMove == true)
                {
                    // Legacy hard-disable: RMB never moves; it only closes the
                    // contextual menu. Kept for players who prefer it.
                    object menu = Traverse.Create(__instance).Property("_contextualMenuModule").GetValue<object>();
                    if (menu != null)
                    {
                        Traverse.Create(__instance).Method("CloseContextualMenu", new object[] { true }).GetValue();
                    }
                    return false;
                }

                if (ModMain.Cfg?.SmartRightClickGuard != true)
                {
                    return true;   // vanilla behavior, no guard
                }

                Traverse state = Traverse.Create(__instance);
                object menu2 = state.Property("_contextualMenuModule").GetValue<object>();
                bool menuVisible = menu2 != null && Traverse.Create(menu2).Field("IsContextualMenuVisible").GetValue<bool>();
                object hover = state.Field("_hoverSelection").GetValue<object>();
                Vector3 hoverPos = hover != null
                    ? (Vector3)Traverse.Create(hover).Field("ActionGridPos").GetValue()
                    : Vector3.zero;

                if (menuVisible)
                {
                    // This RMB closes a menu: remember what tile is under the
                    // cursor so the echo click cannot move there.
                    _lastCloseHover = hoverPos;
                    _lastCloseTime = Time.time;
                    return true;   // vanilla closes the menu (and re-arms its own 0.5s cooldown)
                }

                // Menu-close echo: same tile, within the echo window after a
                // menu was closed by RMB -> swallow instead of moving.
                if (_lastCloseHover.HasValue
                    && hoverPos == _lastCloseHover.Value
                    && Time.time - _lastCloseTime < 1.2f)
                {
                    _lastCloseHover = null;
                    return false;
                }

                _lastCloseHover = null;
                return true;   // normal RMB-move (incl. re-aiming after moving the cursor)
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] SmartRightClick failed: {e.Message}");
                return true;
            }
        }
    }

    // AAP U7: ExtendedAgendaTrackerETA and the nested
    // UIModuleSiteContextualMenu_SetMenuItems_Patch class have been DELETEd.
    // They duplicated vanilla's site-contextual-menu ETA computation (the
    // game already shows travel/exploration ETA in those menus) and were
    // a maintenance trap. The B6 raw-literal fallbacks at the old lines
    // 108-109 ({0}d / {0}h) lived inside the deleted class and are gone too.
}
