using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// One‑shot diagnostic: catches the real C# types of all missing/suspect UI panels.
    /// Patches OnEnable on every MonoBehaviour type whose name contains any of the keywords.
    /// After opening each UI screen once, check Player.log for [AAP CATCHER] lines.
    /// Remove this file when done.
    /// </summary>
    [HarmonyPatch]
    public static class AllUITypeCatcher
    {
        private static HashSet<string> seenTypes = new HashSet<string>();
        private static readonly string[] Keywords = new[] {
            "HavenInfo", "BaseInfo", "GeoBase", "AgendaTracker",
            "Roster", "GeoRoster", "FactionAgenda", "GeoscapeLog",
            "GeoVehicle", "VehicleSelected", "GeoscapeEvent",
            "CharacterSelected", "ContextualMenu"
        };

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            // Scan all assemblies for any MonoBehaviour named with our keywords
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException) { continue; }

                foreach (Type type in types)
                {
                    if (type.IsAbstract || type.IsGenericTypeDefinition) continue;
                    if (!typeof(MonoBehaviour).IsAssignableFrom(type)) continue;

                    if (Keywords.Any(kw => type.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        var method = AccessTools.Method(type, "OnEnable");
                        if (method != null)
                            yield return method;
                    }
                }
            }
        }

        [HarmonyPostfix]
        public static void Postfix(MethodBase __originalMethod, object __instance)
        {
            var type = __instance.GetType();
            string fullName = type.FullName;
            if (seenTypes.Contains(fullName)) return;
            seenTypes.Add(fullName);

            Debug.Log($"[AAP CATCHER] Caught UI type: {fullName} (from {__originalMethod.DeclaringType.FullName})");
        }
    }
}