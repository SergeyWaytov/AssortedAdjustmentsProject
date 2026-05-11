using Base.Core;
using Base.Defs;
using Base.UI;
using Base.UI.MessageBox;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View;
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
    /// <summary>
    /// Allows scrapping of aircraft from the Personnel Roster screen.
    /// Hardened version with extensive null checks to prevent crashes.
    /// </summary>
    internal static class EnableScrapAircraft
    {
        // Stored defaults for restoring the empty slot text
        internal static Color emptySlotDefaultColor = new Color32(0, 0, 0, 128);
        internal static string emptySlotDefaultText = "EMPTY";
        internal static string emptySlotScrapText = "SCRAP AIRCRAFT?";

        private class ContainerInfo
        {
            public string Name;
            public int Index;
            public ContainerInfo(string n, int i) { Name = n; Index = i; }
        }

        internal static MethodInfo UpdateResourceInfoMethod =
            typeof(UIModuleInfoBar).GetMethod("UpdateResourceInfo",
                BindingFlags.NonPublic | BindingFlags.Instance);

        // Cache original text/color on first init
        [HarmonyPatch]
        public static class GeoRosterContainterItem_Init_Patch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() =>
                AccessTools.Method("PhoenixPoint.Geoscape.View.ViewControllers.Roster.GeoRosterContainterItem:Init");

            [HarmonyPrefix]
            public static void Prefix(object __instance)
            {
                try
                {
                    var emptySlot = Traverse.Create(__instance).Field("EmptySlot").GetValue<GameObject>();
                    if (emptySlot != null)
                    {
                        Text t = emptySlot.GetComponentInChildren<Text>(true);
                        if (t != null)
                        {
                            emptySlotDefaultColor = t.color;
                            emptySlotDefaultText = t.text;
                        }
                    }
                }
                catch (Exception e) { Debug.LogError($"[AAP] ScrapAircraft Init error: {e.Message}"); }
            }
        }

        // Change empty slot text when container is empty
        [HarmonyPatch]
        public static class GeoRosterContainterItem_Refresh_Patch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod() =>
                AccessTools.Method("PhoenixPoint.Geoscape.View.ViewControllers.Roster.GeoRosterContainterItem:Refresh");

            [HarmonyPostfix]
            public static void Postfix(object __instance)
            {
                try
                {
                    var traverse = Traverse.Create(__instance);
                    var container = traverse.Property("Container").GetValue<IGeoCharacterContainer>();
                    if (container == null || container.MaxCharacterSpace <= 0) return;

                    if (container.CurrentOccupiedSpace == 0)
                    {
                        var emptySlot = traverse.Field("EmptySlot").GetValue<GameObject>();
                        if (emptySlot != null)
                        {
                            Text t = emptySlot.GetComponentInChildren<Text>(true);
                            if (t != null)
                                t.text = (container.MaxCharacterSpace != int.MaxValue)
                                    ? emptySlotScrapText
                                    : emptySlotDefaultText;
                        }
                    }
                }
                catch (Exception e) { Debug.LogError($"[AAP] ScrapAircraft Refresh error: {e.Message}"); }
            }
        }

        // Main patch: add scrap triggers and handle the scrap logic
        [HarmonyPatch]
        public static class UIStateGeoRoster_EnterState_Patch
        {
            private static bool patchDisabled = false;

            [HarmonyPrepare]
            public static bool Prepare()
            {
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewStates.UIStateGeoRoster");
                if (type == null)
                {
                    Debug.LogWarning("[AAP] ScrapAircraft: UIStateGeoRoster type not found. Patch disabled.");
                    patchDisabled = true;
                    return false;
                }
                var method = AccessTools.Method(type, "EnterState");
                if (method == null)
                {
                    Debug.LogWarning("[AAP] ScrapAircraft: EnterState method not found. Patch disabled.");
                    patchDisabled = true;
                    return false;
                }
                return true;
            }

            [HarmonyTargetMethod]
            public static MethodBase TargetMethod()
            {
                if (patchDisabled) return null;
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewStates.UIStateGeoRoster");
                return AccessTools.Method(type, "EnterState");
            }

            [HarmonyPostfix]
            public static void Postfix(object __instance,
                List<IGeoCharacterContainer> ____characterContainers,
                GeoRosterFilterMode ____preferableFilterMode)
            {
                if (patchDisabled) return;
                try
                {
                    var traverse = Traverse.Create(__instance);
                    var context = traverse.Property("Context").GetValue<GeoscapeViewContext>();
                    var geoRosterModule = traverse.Field("_geoRosterModule").GetValue<UIModuleGeneralPersonelRoster>();
                    var geoscapeModules = traverse.Field("_geoscapeModules").GetValue<GeoscapeModulesData>();

                    if (context?.ViewerFaction == null || geoRosterModule?.Groups == null || geoscapeModules == null)
                        return;

                    RefreshScrapTriggers(geoRosterModule, context, geoscapeModules, ____characterContainers, ____preferableFilterMode);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AAP] ScrapAircraft EnterState error: {e.Message}");
                }
            }

            private static void RefreshScrapTriggers(
                UIModuleGeneralPersonelRoster geoRosterModule,
                GeoscapeViewContext context,
                GeoscapeModulesData geoscapeModules,
                List<IGeoCharacterContainer> characterContainers,
                GeoRosterFilterMode preferFilterMode)
            {
                if (geoRosterModule.Groups == null) return;

                for (int i = 0; i < geoRosterModule.Groups.Count; i++)
                {
                    var c = geoRosterModule.Groups[i];
                    if (c?.Container == null) continue;

                    var emptySlot = Traverse.Create(c).Field("EmptySlot").GetValue<GameObject>();
                    if (emptySlot == null) continue;

                    // Clean up old EventTrigger components to avoid duplicate listeners
                    var existingTriggers = emptySlot.GetComponents<EventTrigger>();
                    foreach (var et in existingTriggers)
                        UnityEngine.Object.Destroy(et);

                    // Only apply to real vehicles (not personnel rows with int.MaxValue capacity)
                    if (c.Container.MaxCharacterSpace == int.MaxValue) continue;

                    var etNew = emptySlot.AddComponent<EventTrigger>();
                    var emptySlotText = emptySlot.GetComponentInChildren<Text>(true);
                    var info = new ContainerInfo(c.Container.Name, i);
                    var originalColor = emptySlotText != null ? emptySlotText.color : Color.white;

                    // Pointer enter: highlight
                    RegisterEvent(etNew, EventTriggerType.PointerEnter, (data) =>
                    {
                        if (emptySlotText != null) emptySlotText.color = Color.red;
                    });

                    // Pointer exit: restore
                    RegisterEvent(etNew, EventTriggerType.PointerExit, (data) =>
                    {
                        if (emptySlotText != null) emptySlotText.color = originalColor;
                    });

                    // Click: attempt to scrap
                    RegisterEvent(etNew, EventTriggerType.PointerClick, (data) =>
                    {
                        OnScrapAircraftClick(info, context, geoscapeModules, geoRosterModule, characterContainers, preferFilterMode);
                    });
                }
            }

            private static void RegisterEvent(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> action)
            {
                var entry = new EventTrigger.Entry { eventID = eventType };
                entry.callback.AddListener(action);
                trigger.triggers.Add(entry);
            }

            private static void OnScrapAircraftClick(
                ContainerInfo info,
                GeoscapeViewContext context,
                GeoscapeModulesData geoscapeModules,
                UIModuleGeneralPersonelRoster geoRosterModule,
                List<IGeoCharacterContainer> characterContainers,
                GeoRosterFilterMode preferFilterMode)
            {
                try
                {
                    if (context?.ViewerFaction?.Vehicles == null) return;

                    var aircraft = context.ViewerFaction.Vehicles.FirstOrDefault(v => v.Name == info.Name);
                    if (aircraft == null) return;

                    var utils = geoscapeModules.GeoscapeScreenUtilsModule;
                    string msg = utils.DismissVehiclePrompt.Localize(null);

                    var def = GameUtl.GameComponent<DefRepository>()
                        .GetAllDefs<VehicleItemDef>()
                        .FirstOrDefault(d => d.ComponentSetDef?.Components?.Contains(aircraft.VehicleDef) == true);

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

                    // Safety: prevent scrapping the last aircraft
                    if (context.ViewerFaction.Vehicles.Count() <= 1)
                    {
                        GameUtl.GetMessageBox().ShowSimplePrompt(
                            "This is Phoenix Point's last aircraft available",
                            MessageBoxIcon.Error,
                            MessageBoxButtons.OK,
                            _ => { },
                            null);
                        return;
                    }

                    GameUtl.GetMessageBox().ShowSimplePrompt(
                        string.Format(msg, info.Name),
                        MessageBoxIcon.Warning,
                        MessageBoxButtons.YesNo,
                        result =>
                        {
                            if (result.DialogResult == MessageBoxResult.Yes)
                            {
                                try
                                {
                                    aircraft.Travelling = true;
                                    aircraft.Destroy();

                                    if (def != null && !def.ScrapPrice.IsEmpty)
                                    {
                                        context.Level.PhoenixFaction.Wallet.Give(
                                            def.ScrapPrice, OperationReason.Scrap);
                                        UpdateResourceInfoMethod?.Invoke(
                                            geoscapeModules.ResourcesModule,
                                            new object[] { context.ViewerFaction, true });
                                    }

                                    // Remove from containers list and refresh UI
                                    characterContainers.RemoveAt(info.Index);
                                    geoRosterModule.Init(context, characterContainers,
                                        null, preferFilterMode,
                                        RosterSelectionMode.SingleSelect);

                                    // Reapply triggers after UI update
                                    RefreshScrapTriggers(geoRosterModule, context, geoscapeModules, characterContainers, preferFilterMode);
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogError($"[AAP] Scrap execution error: {ex.Message}");
                                }
                            }
                        },
                        info);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AAP] Scrap click error: {ex.Message}");
                }
            }
        }
    }
}