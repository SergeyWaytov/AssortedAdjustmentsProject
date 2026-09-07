using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Weapons;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class RageBurstPatch
    {
        private const float ConeAngle = 12f;
        private const float SpreadMult = 1.8f;
        // Q1: 5 shots is intentional. Laser AR + Rage Burst DPS abuse is a separate balance follow-up.
        private const int ExecutionsCount = 5;

        public static void Apply(DefCache cache)
        {
            try
            {
                var rageDef = cache.GetDef<RageBurstInConeAbilityDef>("RageBurst_RageBurstInConeAbilityDef");
                if (rageDef == null)
                {
                    Debug.LogWarning("[AAP] RageBurst def not found – patch skipped.");
                    return;
                }

                rageDef.ConeSpread = ConeAngle;
                rageDef.ProjectileSpreadMultiplier = SpreadMult;
                rageDef.ExecutionsCount = ExecutionsCount;   // static fallback

                Debug.Log($"[AAP] Rage Burst configured: cone {ConeAngle}°, spread {SpreadMult}x, {ExecutionsCount} executions (intentional).");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AAP] RageBurstPatch.Apply failed: {e.Message}");
            }
        }
        // NO HARMONY PATCH HERE
    }
}
