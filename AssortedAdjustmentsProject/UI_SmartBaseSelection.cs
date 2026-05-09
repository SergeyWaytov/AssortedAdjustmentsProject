using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class SmartBaseSelection
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewStates.UIStatePhoenixBaseLayout");
            return AccessTools.Method(type, "EnterState");
        }

        [HarmonyPrefix]
        public static void EnterState_Prefix(object __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                var context = traverse.Property("Context").GetValue<object>();
                if (context == null) return;

                var view = Traverse.Create(context).Property("View").GetValue<object>();
                var camController = Traverse.Create(view).Property("CameraController").GetValue<object>();
                Vector3 camPos = Traverse.Create(camController).Method("GetCurrentPosition").GetValue<Vector3>();

                var level = Traverse.Create(context).Property("Level").GetValue<object>();
                var faction = Traverse.Create(level).Property("PhoenixFaction").GetValue<object>();
                var bases = Traverse.Create(faction).Property("Bases").GetValue<IList>();
                if (bases == null || bases.Count == 0) return;

                object nearest = null;
                float dist = float.MaxValue;
                foreach (var b in bases)
                {
                    var site = Traverse.Create(b).Property("Site").GetValue<object>();
                    if (site == null) continue;
                    Vector3 pos = Traverse.Create(site).Property("WorldPosition").GetValue<Vector3>();
                    float d = Vector3.Distance(camPos, pos);
                    if (d < dist) { dist = d; nearest = b; }
                }
                if (nearest != null)
                {
                    traverse.Field("_selectedBase").SetValue(nearest);
                    var name = Traverse.Create(Traverse.Create(nearest).Property("Site").GetValue<object>()).Property("LocalizedSiteName").GetValue<string>();
                    Debug.Log($"[AAP] Smart Base Selection: {name}");
                }
            }
            catch (Exception e) { Debug.LogError($"[AAP] SmartBaseSelection failed: {e.Message}"); }
        }
    }
}