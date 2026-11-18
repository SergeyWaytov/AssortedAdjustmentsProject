// UI_SmartBaseSelection.cs -- AAP Phase 3 (WHOLE-FILE REPLACEMENT).
// ----------------------------------------------------------------------------
// Reported bug (Phase 3): "clicking Base instead of 'closest base to camera
// position', returns to same Base that the Sub-screen was closed on last
// time."
//
// Diagnosis of the 2b version: it wrote the camera-nearest base in a
// POSTFIX on UIStatePhoenixBaseLayout.EnterState -- but by the time
// EnterState has finished, the camera has typically already been flown to
// (or started flying toward) the REMEMBERED base by vanilla's own enter
// logic. Measuring "nearest to camera" at postfix time therefore measured
// the camera ALREADY ANCHORED at the remembered base -- so the override
// selected the very same base the user complained about. (If instead the
// field name did not match, the FieldExists guard silently no-op'd; the
// new seeker dumps distinguish the two cases in the next Player.log.)
//
// Phase 3 fixes:
//   1. The camera position is captured in a PREFIX -- BEFORE vanilla's
//      EnterState body runs and before any camera focusing can start --
//      and the nearest base is computed from that pre-enter position.
//   2. The override is applied in the POSTFIX (after vanilla assigned its
//      remembered base), writing to the first field that verifies by
//      read-back: "_base", then "_selectedBase", then "_currentBase".
//   3. If nothing verifies, a ONE-SHOT member dump of the state object is
//      written to Player.log ([AAP][SEEKER]) so the real field name can be
//      identified for a follow-up fix.
//   4. Seeker logs on every enter: captured camera position, every base's
//      distance, the value vanilla chose, and the value we overrode with.
// ----------------------------------------------------------------------------
using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class SmartBaseSelection
    {
        // Captured in the prefix (pre-focus camera), applied in the postfix.
        private static Vector3 _cameraAtEnter;
        private static object _nearestBase;
        private static float _nearestDistance = float.MaxValue;
        private static bool _captured;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewStates.UIStatePhoenixBaseLayout");
            return AccessTools.Method(type, "EnterState");
        }

        public static bool Prepare()
        {
            MethodBase target = TargetMethod();
            if (target == null)
            {
                Debug.LogWarning("[AAP] SmartBaseSelection: UIStatePhoenixBaseLayout.EnterState could not be resolved in this build -- patch skipped (the base button will keep vanilla behavior).");
            }
            return target != null;
        }

        // ── PREFIX: capture the camera BEFORE vanilla can focus it ──────
        // Pure observer (void, cannot block) -- the camera position and the
        // nearest base computed here are what the postfix writes.
        public static void Prefix(object __instance)
        {
            _captured = false;
            _nearestBase = null;
            _nearestDistance = float.MaxValue;
            try
            {
                Traverse state = Traverse.Create(__instance);
                object context = state.Property("Context").GetValue<object>();
                if (context == null) return;

                object view = Traverse.Create(context).Property("View").GetValue<object>();
                if (view == null) return;
                object camera = Traverse.Create(view).Property("CameraController").GetValue<object>();
                if (camera == null) return;
                Vector3 cameraPosition = Traverse.Create(camera).Method("GetCurrentPosition").GetValue<Vector3>();

                object level = Traverse.Create(context).Property("Level").GetValue<object>();
                if (level == null) return;
                object faction = Traverse.Create(level).Property("PhoenixFaction").GetValue<object>();
                if (faction == null) return;
                IList bases = Traverse.Create(faction).Property("Bases").GetValue<IList>();
                if (bases == null || bases.Count == 0) return;

                _cameraAtEnter = cameraPosition;
                _nearestBase = null;
                _nearestDistance = float.MaxValue;
                int index = 0;
                foreach (object candidate in bases)
                {
                    object site = Traverse.Create(candidate).Property("Site").GetValue<object>();
                    if (site == null) { index++; continue; }
                    Vector3 position = Traverse.Create(site).Property("WorldPosition").GetValue<Vector3>();
                    float distance = Vector3.Distance(cameraPosition, position);
                    if (Seeker.Enabled)
                        Debug.Log($"[AAP][SEEKER] BaseSelection enter: base #{index} distance from camera = {distance:F0}");
                    if (distance < _nearestDistance)
                    {
                        _nearestDistance = distance;
                        _nearestBase = candidate;
                    }
                    index++;
                }
                _captured = _nearestBase != null;
                Seeker.Log(_nearestBase != null
                    ? $"BaseSelection prefix: camera captured at {cameraPosition}; nearest base is #{bases.IndexOf(_nearestBase)} at distance {_nearestDistance:F0} (pre-focus measurement)."
                    : $"BaseSelection prefix: camera captured at {cameraPosition}, but no base had a resolvable Site.WorldPosition.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] SmartBaseSelection prefix failed: {e}");
            }
        }

        // ── POSTFIX: override vanilla's remembered base ────────────────
        public static void Postfix(object __instance)
        {
            try
            {
                if (!_captured || _nearestBase == null)
                {
                    Debug.LogWarning("[AAP] SmartBaseSelection: no captured camera position / no bases -- vanilla base selection kept.");
                    return;
                }

                Traverse state = Traverse.Create(__instance);

                // Log what vanilla chose (the remembered base), including how
                // far it sits from the CAPTURED camera position -- this is the
                // number that proves the mis-selection in the Player.log.
                object vanillaValue = state.Field("_base").GetValue<object>();
                if (vanillaValue != null)
                {
                    object vanillaSite = Traverse.Create(vanillaValue).Property("Site").GetValue<object>();
                    if (vanillaSite != null)
                    {
                        Vector3 vanillaPos = Traverse.Create(vanillaSite).Property("WorldPosition").GetValue<Vector3>();
                        float vanillaDist = Vector3.Distance(_cameraAtEnter, vanillaPos);
                        Seeker.Log($"BaseSelection postfix: vanilla selected a base {vanillaDist:F0} from the captured camera (type {vanillaValue.GetType().Name}).");
                    }
                }

                // Write the captured-nearest base to the first field that
                // verifies by read-back.
                string[] candidates = { "_base", "_selectedBase", "_currentBase" };
                foreach (string fieldName in candidates)
                {
                    try
                    {
                        Traverse field = state.Field(fieldName);
                        if (!field.FieldExists()) continue;
                        field.SetValue(_nearestBase);
                        object readBack = state.Field(fieldName).GetValue<object>();
                        if (ReferenceEquals(readBack, _nearestBase))
                        {
                            Debug.Log($"[AAP] Smart Base Selection: wrote camera-nearest base to '{fieldName}' (distance {_nearestDistance:F0}, captured pre-focus).");
                            return;
                        }
                        Seeker.Log($"BaseSelection: field '{fieldName}' did not verify by read-back -- trying next candidate.");
                    }
                    catch (Exception e)
                    {
                        Seeker.Log($"BaseSelection: field '{fieldName}' write failed ({e.Message}) -- trying next candidate.");
                    }
                }

                // Nothing verified: dump the state's members ONCE so the next
                // Player.log reveals the real field name for a follow-up fix.
                Debug.LogWarning("[AAP] SmartBaseSelection: no writable base field verified -- vanilla base kept. Member dump follows (once).");
                Seeker.DumpMembersOnce(__instance, "UIStatePhoenixBaseLayout(base field hunt)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] SmartBaseSelection postfix failed: {e}");
            }
        }
    }
}
