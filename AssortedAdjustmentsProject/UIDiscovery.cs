// UIDiscovery.cs – updated with Psychic target checks
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Harmony-based discovery: logs every call to methods whose name matches a set of keywords.
    /// Also performs a one‑time scan for types containing "Log" on first use,
    /// and now verifies the Psychic buff patch targets.
    /// </summary>
    [HarmonyPatch]
    public static class UIDiscovery
    {
        // ---------- One‑time type scan ----------
        private static bool scannedTypes = false;

        static UIDiscovery()
        {
            ScanLogTypes();
            CheckPsychicTargets();   // <-- NEW
        }

        private static void ScanLogTypes()
        {
            if (scannedTypes) return;
            scannedTypes = true;

            Debug.Log("[AAP DISCOVERY] === Scanning all types containing 'Log' ===");
            int found = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException) { continue; }

                foreach (var t in types)
                {
                    // Look for "Log" anywhere in the full type name (case‑insensitive)
                    if (t.FullName?.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.Log($"[AAP DISCOVERY] Log type candidate: {t.FullName}");
                        found++;
                    }
                }
            }
            Debug.Log($"[AAP DISCOVERY] === Scan complete. {found} candidate(s) found. ===");
        }

        // ---------- NEW: Psychic target verification ----------
        private static void CheckPsychicTargets()
        {
            Debug.Log("[AAP DISCOVERY] === Checking Psychic Buff patch targets ===");

            // Already confirmed
            var researchMethod = AccessTools.Method(
                "PhoenixPoint.Geoscape.Levels.Factions.GeoPhoenixFaction:OnResearchCompleted");
            if (researchMethod != null) Debug.Log("[AAP DISCOVERY]   GeoPhoenixFaction.OnResearchCompleted – FOUND");

            // Try each known TFTV target location
            var methodsToCheck = new[] {
        "PhoenixPoint.Tactical.Entities.Abilities.ApplyStatusAbility:GetWillCheckCost",
        "Base.Entities.Statuses.StatusTemplate:GetEffectiveWillCheckCost",
        "Base.Entities.Statuses.StatusTemplate:GetWillCheckCost",
        "PhoenixPoint.Tactical.Entities.Statuses.StatusTemplate:GetEffectiveWillCheckCost",
        "PhoenixPoint.Tactical.Entities.Statuses.StatusTemplate:GetWillCheckCost"
    };

            foreach (var m in methodsToCheck)
            {
                var method = AccessTools.Method(m);
                if (method != null)
                    Debug.Log($"[AAP DISCOVERY]   {m} – FOUND");
                else
                    Debug.LogWarning($"[AAP DISCOVERY]   {m} – MISSING");
            }
            // Discover WillBreakControl on psychic statuses
            string[] statusNames = { "InducePanicStatus", "PsychicScreamStatus", "MindCrushStatus", "FrenzyStatus" };
            foreach (var sn in statusNames)
            {
                var t = AccessTools.TypeByName("PhoenixPoint.Tactical.Entities.Statuses." + sn);
                if (t != null)
                {
                    var wbc = AccessTools.Method(t, "WillBreakControl");
                    if (wbc != null)
                        Debug.Log($"[AAP DISCOVERY]   {sn}.WillBreakControl – FOUND");
                    else
                        Debug.LogWarning($"[AAP DISCOVERY]   {sn}.WillBreakControl – MISSING");
                }
            }

            Debug.Log("[AAP DISCOVERY] === Psychic target check complete ===");
        }
        // ---------- END NEW ----------

        // ---------- Existing runtime method logging ----------
        private static readonly HashSet<string> TargetMethodNames = new HashSet<string>
        {
            "SetInfo", "SetZoneInfo", "EnterState", "Show",
            "SetData", "SetFacility", "Refresh", "Init"
        };

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException) { continue; }

                foreach (var t in types)
                {
                    if (t.IsGenericTypeDefinition) continue;
                    if (t.Namespace == null || !(t.Namespace.Contains("UI") || t.Namespace.Contains("View"))) continue;

                    MethodInfo[] methods;
                    try { methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static); }
                    catch { continue; }

                    foreach (var m in methods)
                    {
                        if (m.IsAbstract) continue;
                        if (m.DeclaringType.IsInterface) continue;
                        if (m.GetMethodBody() == null) continue;
                        if (m.IsGenericMethod) continue;

                        if (TargetMethodNames.Contains(m.Name))
                        {
                            yield return m;
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        public static void Prefix(MethodBase __originalMethod, object[] __args)
        {
            string argsStr = __args != null
                ? string.Join(", ", __args.Select(a => a?.GetType().Name ?? "null"))
                : "";
            Debug.Log($"[AAP DISCOVERY] {__originalMethod.DeclaringType.FullName}.{__originalMethod.Name}({argsStr})");
        }
    }
}