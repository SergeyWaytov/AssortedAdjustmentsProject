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
    /// Also performs a one‑time scan for types containing "Log" on first use.
    /// </summary>
    [HarmonyPatch]
    public static class UIDiscovery
    {
        // ---------- One‑time type scan ----------
        private static bool scannedTypes = false;

        static UIDiscovery()
        {
            ScanLogTypes();
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