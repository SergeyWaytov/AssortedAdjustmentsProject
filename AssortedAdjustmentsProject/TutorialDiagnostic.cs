using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class TutorialDiagnostic
    {
        // Patch ALL methods of GeoPhoenixFaction to log when they're called
        [HarmonyTargetMethods]
        public static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.Levels.Factions.GeoPhoenixFaction");
            if (type == null)
            {
                Debug.LogError("[AAP DIAGNOSTIC] GeoPhoenixFaction type not found!");
                yield break;
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                // Only log methods that might be related to soldier creation
                if (method.Name.Contains("Soldier") || method.Name.Contains("Create") || method.Name.Contains("Starting") || method.Name.Contains("Tutorial"))
                {
                    yield return method;
                }
            }
        }

        [HarmonyPrefix]
        public static void Prefix(MethodBase __originalMethod, object __instance)
        {
            Debug.Log($"[AAP DIAGNOSTIC] Called: {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
        }
    }
}