using System;
using System.Collections;
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

        // AAP U3: postfix writes the real field name (_base, not _selectedBase)
        // and runs AFTER vanilla's Bases.First() assignment so our selection
        // actually overrides. The original prefix wrote to a nonexistent
        // _selectedBase field and was silently dropped.
        [HarmonyPostfix]
        public static void EnterState_Postfix(object __instance)
        {
            try
            {
                Traverse state = Traverse.Create(__instance);
                object context = state.Property("Context").GetValue<object>();
                if (context == null) return;

                object view = Traverse.Create(context).Property("View").GetValue<object>();
                object camera = Traverse.Create(view).Property("CameraController").GetValue<object>();
                Vector3 cameraPosition = Traverse.Create(camera).Method("GetCurrentPosition").GetValue<Vector3>();

                object level = Traverse.Create(context).Property("Level").GetValue<object>();
                object faction = Traverse.Create(level).Property("PhoenixFaction").GetValue<object>();
                IList bases = Traverse.Create(faction).Property("Bases").GetValue<IList>();
                if (bases == null || bases.Count == 0) return;

                object nearest = null; float nearestDistance = float.MaxValue;
                foreach (object candidate in bases)
                {
                    object site = Traverse.Create(candidate).Property("Site").GetValue<object>();
                    if (site == null) continue;
                    Vector3 position = Traverse.Create(site).Property("WorldPosition").GetValue<Vector3>();
                    float distance = Vector3.Distance(cameraPosition, position);
                    if (distance < nearestDistance) { nearestDistance = distance; nearest = candidate; }
                }

                Traverse field = state.Field("_base");
                if (nearest == null || !field.FieldExists())
                {
                    Debug.LogWarning("[AAP] SmartBaseSelection: no nearest base / _base field missing.");
                    return;
                }
                field.SetValue(nearest);
                Debug.Log($"[AAP] Smart Base Selection wrote _base; distance={nearestDistance}");
            }
            catch (Exception e) { Debug.LogError($"[AAP] SmartBaseSelection failed: {e}"); }
        }
    }
}
