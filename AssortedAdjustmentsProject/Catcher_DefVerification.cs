using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities.Abilities;
using System;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// One‑shot diagnostic: verifies every def that might have silently failed.
    /// Runs when the mod is enabled. Check Player.log for [AAP DEF CATCHER] lines.
    /// Remove this file after analysis.
    /// </summary>
    public static class DefVerificationCatcher
    {
        public static void Run(DefCache cache)
        {
            Debug.Log("[AAP DEF CATCHER] === Starting missing def verification ===");

            // 1. Sniper Precision Shot
            VerifyPrecisionShot(cache);

            // 2. Psychic Resistance
            VerifyPsychicResistance(cache);

            // 3. Frenzy rework
            VerifyFrenzy(cache);

            // 4. Stimpack
            VerifyStimpack(cache);

            // 5. Personal Abilities count
            VerifyPersonalAbilityLimits(cache);

            // 6. Screaming Head (already OK, just double-check)
            VerifyScreamingHead(cache);

            Debug.Log("[AAP DEF CATCHER] === Verification complete ===");
        }

        private static void VerifyPrecisionShot(DefCache cache)
        {
            var sniperClass = cache.GetDef<BaseDef>("Sniper_SoldierClassDef");
            var baseShoot = cache.GetDef<ShootAbilityDef>("SniperRifle_Shoot_AbilityDef");
            Debug.Log($"[AAP DEF CATCHER] PrecisionShot: SniperClass={(sniperClass != null)} BaseShoot={(baseShoot != null)}");

            if (sniperClass != null && baseShoot != null)
            {
                // Check if our custom ability already exists
                var existing = cache.GetDef<BaseDef>("AAP_PrecisionShot_AbilityDef");
                Debug.Log($"[AAP DEF CATCHER] PrecisionShot: Existing ability={(existing != null)}");
                if (existing == null)
                {
                    Debug.Log("[AAP DEF CATCHER] PrecisionShot: Would attempt creation – but Guid must be fresh.");
                }
            }
            else
            {
                Debug.LogWarning("[AAP DEF CATCHER] PrecisionShot: Missing prerequisite defs, cannot create.");
            }
        }

        private static void VerifyPsychicResistance(DefCache cache)
        {
            var ability = cache.GetDef<BaseDef>("PsychicResistance_AbilityDef");
            Debug.Log($"[AAP DEF CATCHER] PsychicResistance: Ability={(ability != null)}");

            if (ability != null)
            {
                var statusDef = Traverse.Create(ability).Property("StatusDef").GetValue<object>();
                Debug.Log($"[AAP DEF CATCHER] PsychicResistance: StatusDef={(statusDef != null)}");
                if (statusDef != null)
                {
                    var modsField = Traverse.Create(statusDef).Field("StatModifications");
                    var mods = modsField.GetValue<object[]>();
                    Debug.Log($"[AAP DEF CATCHER] PsychicResistance: Mods count={mods?.Length ?? 0}");
                    if (mods != null)
                    {
                        foreach (var mod in mods)
                        {
                            var statName = Traverse.Create(mod).Field("StatName").GetValue<string>();
                            Debug.Log($"[AAP DEF CATCHER]   Mod Stat: {statName}");
                        }
                    }
                }
            }
        }

        private static void VerifyFrenzy(DefCache cache)
        {
            var ability = cache.GetDef<BaseDef>("Frenzy_AbilityDef");
            Debug.Log($"[AAP DEF CATCHER] Frenzy: Ability={(ability != null)}");

            if (ability != null)
            {
                var statusDef = Traverse.Create(ability).Property("StatusDef").GetValue<object>();
                Debug.Log($"[AAP DEF CATCHER] Frenzy: StatusDef={(statusDef != null)}");
                if (statusDef != null)
                {
                    var modsField = Traverse.Create(statusDef).Field("StatModifications");
                    var mods = modsField.GetValue<object[]>();
                    Debug.Log($"[AAP DEF CATCHER] Frenzy: Mods count={mods?.Length ?? 0}");
                    if (mods != null)
                    {
                        foreach (var mod in mods)
                        {
                            var statName = Traverse.Create(mod).Field("StatName").GetValue<string>();
                            var value = Traverse.Create(mod).Field("Value").GetValue();
                            Debug.Log($"[AAP DEF CATCHER]   Mod Stat: {statName} = {value}");
                        }
                    }
                }
            }
        }

        private static void VerifyStimpack(DefCache cache)
        {
            var equipment = cache.GetDef<BaseDef>("Stimpack_EquipmentDef");
            Debug.Log($"[AAP DEF CATCHER] Stimpack: Equipment={(equipment != null)}");

            if (equipment != null)
            {
                var healDef = Traverse.Create(equipment).Property("HealAbilityDef").GetValue<object>();
                Debug.Log($"[AAP DEF CATCHER] Stimpack: HealAbility={(healDef != null)}");
                if (healDef != null)
                {
                    var apCost = Traverse.Create(healDef).Property("ActionPointCost").GetValue();
                    var healBodyParts = Traverse.Create(healDef).Property("HealBodyParts").GetValue();
                    Debug.Log($"[AAP DEF CATCHER] Stimpack: AP cost={apCost}, HealBodyParts={healBodyParts}");
                }
            }
        }

        private static void VerifyPersonalAbilityLimits(DefCache cache)
        {
            string[] statSheetDefs = {
                "Assault_BaseStatSheetDef",
                "Heavy_BaseStatSheetDef",
                "Sniper_BaseStatSheetDef",
                "Berserker_BaseStatSheetDef",
                "Priest_BaseStatSheetDef",
                "Infiltrator_BaseStatSheetDef",
                "Technician_BaseStatSheetDef"
            };

            foreach (var defName in statSheetDefs)
            {
                var statSheet = cache.GetDef<BaseDef>(defName);
                if (statSheet != null)
                {
                    var count = Traverse.Create(statSheet).Property("PersonalAbilitiesCount").GetValue();
                    Debug.Log($"[AAP DEF CATCHER] PersonalAbilities: {defName} current count = {count}");
                }
                else
                {
                    Debug.LogWarning($"[AAP DEF CATCHER] PersonalAbilities: {defName} NOT FOUND");
                }
            }
        }

        private static void VerifyScreamingHead(DefCache cache)
        {
            var head = cache.GetDef<BaseDef>("AN_Priest_Head03_BodyPartDef");
            if (head != null)
            {
                var abilitiesField = Traverse.Create(head).Field("Abilities");
                var abilities = abilitiesField.GetValue<object[]>();
                bool hasMCImmunity = abilities?.Any(a => a != null && a.ToString().Contains("MindControlImmunity")) ?? false;
                Debug.Log($"[AAP DEF CATCHER] ScreamingHead: Has MC Immunity = {hasMCImmunity}");
            }
            else
            {
                Debug.LogWarning("[AAP DEF CATCHER] ScreamingHead: def not found");
            }
        }
    }
}