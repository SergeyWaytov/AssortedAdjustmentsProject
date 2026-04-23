using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class UIDiscovery
    {
        private static readonly string[] MethodNamesToFind =
        {
            "SetInfo", "SetZoneInfo", "EnterState",
            "PhoenixFaction_OnExcavationStarted", "ShowSiteDefenseTimer",
        };

        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException) { continue; }

                foreach (var type in types)
                {
                    if (type.IsGenericTypeDefinition) continue; // Safe: prevents Harmony crash

                    foreach (var methodName in MethodNamesToFind)
                    {
                        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (method != null)
                            yield return method;
                    }
                }
            }
        }

        [HarmonyPrefix]
        public static void Prefix(MethodBase __originalMethod)
        {
            Debug.Log($"[AAP DISCOVERY] {__originalMethod.DeclaringType.FullName}.{__originalMethod.Name}");
        }
    }
}