using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Displays a recruit's personal abilities in the haven tooltip.
    /// If the target type/method is not found at runtime, the patch is safely ignored.
    /// </summary>
    [HarmonyPatch]
    public static class RecruitInfoInHavenTooltip
    {
        private static bool patchDisabled = false;
        private static string Label => ModMain.Localize("PersonalAbilities");

        [HarmonyPrepare]
        public static bool Prepare()
        {
            var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewModules.UIModuleHavenInfo");
            if (type == null)
            {
                Debug.LogWarning("[AAP] RecruitInfo: UIModuleHavenInfo type not found. Patch disabled.");
                patchDisabled = true;
                return false;
            }
            var method = AccessTools.Method(type, "SetZoneInfo");
            if (method == null)
            {
                Debug.LogWarning("[AAP] RecruitInfo: SetZoneInfo method not found. Patch disabled.");
                patchDisabled = true;
                return false;
            }
            return true;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            if (patchDisabled) return null;
            var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewModules.UIModuleHavenInfo");
            return AccessTools.Method(type, "SetZoneInfo");
        }

        [HarmonyPostfix]
        public static void SetZoneInfo_Postfix(object __instance, object site)
        {
            if (patchDisabled) return;
            try
            {
                if (__instance == null || site == null) return;
                var haven = Traverse.Create(site).Method("GetComponent", new[] { typeof(Type) })
                    .GetValue<object>(AccessTools.TypeByName("PhoenixPoint.Geoscape.Entities.Sites.GeoHaven"));
                if (haven == null) return;

                var data = Traverse.Create(haven).Method("GetRecruitData").GetValue<object>();
                if (data == null) return;
                var template = Traverse.Create(data).Property("SoldierTemplate").GetValue<object>();
                if (template == null) return;

                var abilities = Traverse.Create(template).Property("PersonalAbilities").GetValue<IList>();
                if (abilities == null || abilities.Count == 0) return;

                var abilityNames = new List<string>();
                foreach (var a in abilities)
                {
                    var viewDef = Traverse.Create(a).Property("ViewElementDef").GetValue<object>();
                    if (viewDef != null)
                    {
                        var displayName = Traverse.Create(viewDef).Property("DisplayName").GetValue<object>();
                        if (displayName != null)
                        {
                            var localized = Traverse.Create(displayName).Method("Localize").GetValue<string>();
                            abilityNames.Add(localized ?? Traverse.Create(a).Field("name").GetValue<string>());
                        }
                        else abilityNames.Add(Traverse.Create(a).Field("name").GetValue<string>());
                    }
                    else abilityNames.Add(Traverse.Create(a).Field("name").GetValue<string>());
                }
                if (abilityNames.Count == 0) return;

                Transform container = Traverse.Create(__instance).Field("transform").GetValue<Transform>()
                    .Find("InfoPanel/Content/StatsContainer");
                if (container == null) return;

                // Clear old custom rows
                foreach (Transform child in container)
                {
                    if (child.name.StartsWith("Header") || child.name.StartsWith("Entry"))
                        UnityEngine.Object.Destroy(child.gameObject);
                }

                var header = new GameObject("Header", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                header.transform.SetParent(container, false);
                header.text = Label;
                header.fontSize = 16;
                header.fontStyle = FontStyles.Bold;
                header.color = new Color(0.9f, 0.9f, 0.5f);

                foreach (string a in abilityNames)
                {
                    var entry = new GameObject("Entry", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                    entry.transform.SetParent(container, false);
                    entry.text = $"• {a}";
                    entry.fontSize = 14;
                    entry.color = Color.white;
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(container as RectTransform);
            }
            catch (Exception e) { Debug.LogError($"[AAP] RecruitInfo failed: {e.Message}"); }
        }

        [HarmonyPatch]
        public static class ShowPatch
        {
            private static bool patchDisabled = false;

            [HarmonyPrepare]
            public static bool Prepare()
            {
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewModules.UIModuleHavenInfo");
                if (type == null)
                {
                    patchDisabled = true;
                    return false;
                }
                var method = AccessTools.Method(type, "Show");
                if (method == null) patchDisabled = true;
                return !patchDisabled;
            }

            [HarmonyTargetMethod]
            public static MethodBase TargetMethod()
            {
                if (patchDisabled) return null;
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewModules.UIModuleHavenInfo");
                return AccessTools.Method(type, "Show");
            }

            [HarmonyPostfix]
            public static void Show_Postfix(object __instance)
            {
                if (patchDisabled) return;
                try
                {
                    Transform container = Traverse.Create(__instance).Field("transform").GetValue<Transform>()
                        .Find("InfoPanel/Content/StatsContainer");
                    LayoutRebuilder.ForceRebuildLayoutImmediate(container as RectTransform);
                }
                catch (Exception e) { Debug.LogError($"[AAP] RecruitInfo layout failed: {e.Message}"); }
            }
        }
    }
}