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
                rageDef.ExecutionsCount = 5;   // static fallback

                Debug.Log("[AAP] Rage Burst configured: cone " + ConeAngle + "°, spread " + SpreadMult + "x.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AAP] RageBurstPatch.Apply failed: {e.Message}");
            }
        }
        // NO HARMONY PATCH HERE
    }
}