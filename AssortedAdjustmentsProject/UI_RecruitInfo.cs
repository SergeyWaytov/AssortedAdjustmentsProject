using HarmonyLib;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Entities.Sites;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class RecruitInfoInHavenTooltip
    {
        private static string Label => ModMain.Localize("PersonalAbilities");

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewModules.UIModuleHavenDetailsScreen");
            return AccessTools.Method(type, "SetHavenDetails");
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance, GeoscapeViewContext context, GeoSite site)
        {
            if (__instance == null || site == null) return;
            try
            {
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
                    string name = null;
                    if (viewDef != null)
                    {
                        var displayName = Traverse.Create(viewDef).Property("DisplayName").GetValue<object>();
                        if (displayName != null)
                            name = Traverse.Create(displayName).Method("Localize").GetValue<string>();
                    }
                    abilityNames.Add(name ?? Traverse.Create(a).Field("name").GetValue<string>());
                }
                if (abilityNames.Count == 0) return;

                Transform container = ((Component)__instance).transform.Find("InfoPanel/Content/StatsContainer");
                if (container == null) return;

                // Remove old custom rows
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
    }
}