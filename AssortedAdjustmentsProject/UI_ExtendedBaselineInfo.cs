using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Adds healing rate, repair rate, and vehicle status to the base info panel.
    /// If the target type/method is not found at runtime, the patch is safely ignored.
    /// </summary>
    [HarmonyPatch]
    public static class ExtendedBaseInfo
    {
        private static bool patchDisabled = false;

        [HarmonyPrepare]
        public static bool Prepare()
        {
            var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewModules.UIModuleBaseInfo");
            if (type == null)
            {
                Debug.LogWarning("[AAP] ExtendedBaseInfo: UIModuleBaseInfo type not found. Patch disabled.");
                patchDisabled = true;
                return false;
            }
            var method = AccessTools.Method(type, "SetInfo");
            if (method == null)
            {
                Debug.LogWarning("[AAP] ExtendedBaseInfo: SetInfo method not found. Patch disabled.");
                patchDisabled = true;
                return false;
            }
            return true;
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            if (patchDisabled) return null;
            var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewModules.UIModuleBaseInfo");
            return AccessTools.Method(type, "SetInfo");
        }

        [HarmonyPostfix]
        public static void SetInfo_Postfix(object __instance, object pxBase)
        {
            if (patchDisabled) return;
            try
            {
                if (__instance == null || pxBase == null) return;
                var transform = Traverse.Create(__instance).Field("transform").GetValue<Transform>();
                Transform container = transform.Find("InfoPanel/Content/StatsContainer");
                if (container == null) return;

                var layout = Traverse.Create(pxBase).Property("Layout").GetValue<object>();
                var facilityList = Traverse.Create(layout).Property("Facilities").GetValue<IList>();

                float heal = 0f;
                foreach (var f in facilityList)
                {
                    var method = Traverse.Create(f).Method("GetHealingPerDay");
                    if (method.MethodExists())
                        heal += method.GetValue<float>();
                }
                if (heal > 0)
                    AddRow(container, ModMain.Localize("HealingRate"), $"{heal:F0} {ModMain.Localize("HpPerDay")}", new Color(0.3f, 0.9f, 0.3f));

                var soldiers = Traverse.Create(pxBase).Property("Soldiers").GetValue<IList>();
                int inTreatment = soldiers.Cast<object>().Count(s => Traverse.Create(s).Method("IsHealing").GetValue<bool>());
                if (inTreatment > 0)
                    AddRow(container, ModMain.Localize("InTreatment"), inTreatment.ToString(), new Color(0.9f, 0.5f, 0.2f));

                float repair = 0f;
                foreach (var f in facilityList)
                {
                    var method = Traverse.Create(f).Method("GetVehicleRepairPerDay");
                    if (method.MethodExists())
                        repair += method.GetValue<float>();
                }
                if (repair > 0)
                    AddRow(container, ModMain.Localize("RepairRate"), $"{repair:F0} {ModMain.Localize("HpPerDay")}", new Color(0.5f, 0.7f, 1.0f));

                var site = Traverse.Create(pxBase).Property("Site").GetValue<object>();
                var geoLevel = Traverse.Create(site).Property("GeoLevel").GetValue<object>();
                var faction = Traverse.Create(geoLevel).Property("PhoenixFaction").GetValue<object>();
                var vehicles = Traverse.Create(faction).Property("Vehicles").GetValue<IList>();
                var baseVehicles = vehicles.Cast<object>().Where(v => Traverse.Create(v).Property("CurrentSite").GetValue<object>() == site).ToList();
                if (baseVehicles.Any())
                {
                    AddRow(container, ModMain.Localize("Vehicles"), baseVehicles.Count.ToString(), new Color(0.7f, 0.7f, 0.7f));
                    foreach (var v in baseVehicles)
                    {
                        var name = Traverse.Create(v).Property("Name").GetValue<string>();
                        var health = Traverse.Create(v).Method("GetHealth").GetValue<object>();
                        var cur = Traverse.Create(health).Property("IntValue").GetValue<int>();
                        var max = Traverse.Create(health).Property("IntMax").GetValue<int>();
                        AddRow(container, $"  {name}", $"{cur}/{max}", Color.white, indent: true);
                    }
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(container as RectTransform);
            }
            catch (Exception e) { Debug.LogError($"[AAP] ExtendedBaseInfo failed: {e.Message}"); }
        }

        private static void AddRow(Transform parent, string label, string value, Color color, bool indent = false)
        {
            var row = new GameObject($"Row_{label}", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.spacing = 8;
            hl.padding = indent ? new RectOffset(20, 0, 0, 0) : new RectOffset(0, 0, 0, 0);

            var l = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            l.transform.SetParent(row.transform, false);
            l.text = label; l.fontSize = 14; l.color = new Color(0.7f, 0.7f, 0.7f);

            var s = new GameObject("Spacer", typeof(RectTransform)).AddComponent<LayoutElement>();
            s.transform.SetParent(row.transform, false);
            s.flexibleWidth = 1;

            var v = new GameObject("Value", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            v.transform.SetParent(row.transform, false);
            v.text = value; v.fontSize = 14; v.color = color; v.alignment = TextAlignmentOptions.Right;
        }
    }
}