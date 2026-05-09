using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class TacticalSpawnScanner
    {
        public static void Run()
        {
            if (!ModMain.DiagnosticsEnabled) return;
            Debug.Log("[AAP SCAN] === Starting tactical method scan ===");
            int found = 0;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException) { continue; }

                foreach (var type in types)
                {
                    if (type.IsGenericTypeDefinition) continue;
                    if (!type.Namespace?.StartsWith("PhoenixPoint.Tactical") ?? true) continue;

                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        var fullName = $"{type.Name}.{method.Name}".ToLower();
                        if (fullName.Contains("deploy") || fullName.Contains("spawn") ||
                            fullName.Contains("create") || fullName.Contains("actor") ||
                            fullName.Contains("character"))
                        {
                            Debug.Log($"[AAP SCAN] {type.FullName}.{method.Name}");
                            found++;
                        }
                    }
                }
            }
            Debug.Log($"[AAP SCAN] === Scan complete: {found} methods found ===");
        }
    }
}