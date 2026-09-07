using HarmonyLib;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Entities.Sites;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View;
using PhoenixPoint.Tactical.Entities.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
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
                // AAP U2: typed access chain replaces the reflected data path
                // (the old Traverse-based chain hit a non-existent PersonalAbilities
                // property on the template and silently returned nothing).
                GeoHaven haven = site.GetComponent<GeoHaven>();
                if (haven == null) return;

                Dictionary<int, TacticalAbilityDef> abilities =
                    haven.AvailableRecruit?.Progression?.PersonalAbilities;
                if (abilities == null || abilities.Count == 0) return;

                List<string> abilityNames = abilities
                    .OrderBy(pair => pair.Key)
                    .Select(pair => pair.Value?.ViewElementDef?.DisplayName1?.Localize() ?? pair.Value?.name)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();
                if (abilityNames.Count == 0) return;

                Transform container = ((Component)__instance).transform.Find("InfoPanel/Content/StatsContainer");
                if (container == null) return;

                // AAP U2: cleanup uses AAP_ prefix (the cloned objects below are
                // renamed AAP_Header / AAP_Entry) so we don't destroy vanilla
                // Header/Entry rows that other code may have placed.
                foreach (Transform child in container)
                {
                    if (child.name.StartsWith("AAP_", StringComparison.Ordinal))
                        UnityEngine.Object.Destroy(child.gameObject);
                }

                // Q12-B: worldPositionStays=false is the correct mode for UI
                // layout groups (the new child inherits the parent layout
                // instead of fighting it with world-coords).
                var header = new GameObject("AAP_Header", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                header.transform.SetParent(container, false);
                header.text = Label;
                header.fontSize = 16;
                header.fontStyle = FontStyles.Bold;
                header.color = new Color(0.9f, 0.9f, 0.5f);

                foreach (string a in abilityNames)
                {
                    var entry = new GameObject("AAP_Entry", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
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
