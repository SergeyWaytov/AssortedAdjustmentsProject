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
        // ===== Configurable feel =====
        private const float ConeAngle = 12f;   // narrow, focused spray
        private const float SpreadMult = 1.8f;  // wild scatter inside the cone
        private const int MaxTriggers = 10;    // safety cap for ammo‑dump
        // ============================

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
                rageDef.ExecutionsCount = 5;   // fallback if patch fails

                Debug.Log("[AAP] Rage Burst configured: cone " + ConeAngle + "°, spread " + SpreadMult + "x, dynamic mag dump.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AAP] RageBurstPatch.Apply failed: {e.Message}");
            }
        }

        // Harmony patch that dynamically sets the number of bursts before Rage Burst activates.
        [HarmonyPatch(typeof(RageBurstInConeAbility), "Activate")]
        public static class RageBurst_MagazineDump
        {
            static void Prefix(RageBurstInConeAbility __instance)
            {
                try
                {
                    // Get the weapon that is performing the ability (the caster's equipped weapon)
                    Weapon weapon = __instance.GetSource<Weapon>();
                    if (weapon == null) return;

                    int projectilesPerShot = weapon.WeaponDef.DamagePayload.ProjectilesPerShot;
                    int charges = weapon.CommonItemData.CurrentCharges;

                    // For infinite‑ammo weapons (e.g. Living Weapons), use the cap
                    if (weapon.WeaponDef.ChargesMax == 0)
                        charges = MaxTriggers * projectilesPerShot;

                    // Calculate how many bursts (trigger pulls) we can fire
                    int executions = Mathf.CeilToInt((float)charges / projectilesPerShot);
                    executions = Mathf.Min(executions, MaxTriggers);   // safety cap
                    executions = Mathf.Max(executions, 1);            // always at least one burst

                    // Override the ability def's execution count for this activation
                    __instance.RageBurstInConeAbilityDef.ExecutionsCount = executions;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[AAP] RageBurst magazine dump failed: {e.Message}");
                }
            }
        }
    }
}