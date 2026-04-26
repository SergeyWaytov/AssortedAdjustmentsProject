// PsychicBuffPatches.cs
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Base.Core;
using Base.Defs;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Geoscape.Entities.Research;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Statuses;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    internal static class PsychicBuffManager
    {
        public static GameTagDef MindfraggerBonusTag;
        public static GameTagDef PsychicInfluencesTag;
        public static bool MindfraggerResearchCompleted = false;
        public static bool PsychicInfluencesCompleted = false;

        // Temporary storage for original MinUpkeepCost values
        private static Dictionary<ApplyStatusAbilityDef, float> originalUpkeepCache = new Dictionary<ApplyStatusAbilityDef, float>();

        public static void Init()
        {
            CreateTags();
        }

        private static void CreateTags()
        {
            var baseTag = ModMain.DefCache.GetDef<GameTagDef>("Civilian_ClassTagDef");
            if (MindfraggerBonusTag == null)
                MindfraggerBonusTag = Helpers.CreateDefFromClone(baseTag,
                    "a1b2c3d4-e5f6-4789-0abc-def012345678",
                    "AAP_MindfraggerBonus_Tag") as GameTagDef;
            if (PsychicInfluencesTag == null)
                PsychicInfluencesTag = Helpers.CreateDefFromClone(baseTag,
                    "b2c3d4e5-f6a7-4b89-1bcd-ef0123456789",
                    "AAP_PsychicInfluences_Tag") as GameTagDef;
        }
    }

    // --- Research hooks ---
    [HarmonyPatch(typeof(GeoPhoenixFaction), "OnResearchCompleted")]
    public static class PsychicResearch_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(GeoPhoenixFaction __instance, ResearchElement research)
        {
            string defName = research?.ResearchDef?.name ?? "";
            if (defName.IndexOf("Mindfragger", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                PsychicBuffManager.MindfraggerResearchCompleted = true;
                Debug.Log("[AAP] Mindfragger research complete. Defensive WP bonus active.");
                AddTagToAll(PsychicBuffManager.MindfraggerBonusTag, __instance);
            }
            else if (defName.IndexOf("PsychicInfluences", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                PsychicBuffManager.PsychicInfluencesCompleted = true;
                Debug.Log("[AAP] Psychic Influences research complete. Offensive WP bonus & threshold active.");
                AddTagToAll(PsychicBuffManager.PsychicInfluencesTag, __instance);
            }
        }

        private static void AddTagToAll(GameTagDef tag, GeoPhoenixFaction faction)
        {
            if (tag == null) return;
            foreach (var c in faction.Characters)
            {
                if (!c.TemplateDef.IsHuman) continue;
                if (!c.GameTags.Contains(tag))
                    c.GameTags.Add(tag);
            }
        }
    }

    // --- Inherit tags on tactical spawn ---
    [HarmonyPatch(typeof(TacticalActor), "ProcessInstanceData")]
    public static class PsychicTagInheritance_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(TacticalActor __instance)
        {
            if (__instance.TacticalFaction != __instance.TacticalLevel?.View?.ViewerFaction)
                return;

            var geoLevel = GameUtl.CurrentLevel()?.GetComponent<GeoLevelController>();
            var geoChar = geoLevel?.PhoenixFaction?.Characters
                .FirstOrDefault(c => c.Id == __instance.GeoUnitId);
            if (geoChar == null) return;

            if (PsychicBuffManager.MindfraggerResearchCompleted &&
                !__instance.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
            {
                var list = new GameTagsList();
                list.Add(PsychicBuffManager.MindfraggerBonusTag);
                __instance.AddGameTags(list);
            }
            if (PsychicBuffManager.PsychicInfluencesCompleted &&
                !__instance.GameTags.Contains(PsychicBuffManager.PsychicInfluencesTag))
            {
                var list = new GameTagsList();
                list.Add(PsychicBuffManager.PsychicInfluencesTag);
                __instance.AddGameTags(list);
            }
        }
    }

    // --- Defensive boost for Mind Control ---
    [HarmonyPatch(typeof(MindControlStatus), "WillBreakControl")]
    public static class MindControlDefense_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(MindControlStatus __instance, ref float ____minUpkeepCost)
        {
            if (!PsychicBuffManager.MindfraggerResearchCompleted) return;
            TacticalActor defender = __instance.TacticalActor;
            if (defender == null || !defender.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag)) return;

            float defBoost = defender.CharacterStats.WillPoints.Max * 0.5f;
            ____minUpkeepCost += defBoost;
            Debug.Log($"[AAP] MindControl defense: cost increased by +{defBoost}");
        }
    }

    // --- Global psychic defence (non‑MC) and offensive bonus ---
    private static HashSet<string> PsychicAbilityNames = new HashSet<string>
    {
        "Priest_MindControl_AbilityDef", "Exalted_MindControl_AbilityDef",
        "InducePanic_AbilityDef", "Exalted_InducePanic_AbilityDef",
        "Priest_PsychicScream_AbilityDef", "Siren_PsychicScream_AbilityDef",
        "MindCrush_AbilityDef", "Exalted_MindCrush_AbilityDef",
        "Priest_InstilFrenzy_AbilityDef", "Queen_InstilFrenzy_AbilityDef",
        "Siren_InstilFrenzy_AbilityDef"
    };

    [HarmonyPatch(typeof(ApplyStatusAbility), "Activate")]
    public static class PsychicStatusActivate_Patch
    {
        // Prefix: adjust MinUpkeepCost before activation
        [HarmonyPrefix]
        public static void Prefix(ApplyStatusAbility __instance)
        {
            if (__instance?.ApplyStatusAbilityDef == null) return;

            float originalMin = __instance.ApplyStatusAbilityDef.MinUpkeepCost;

            // --- DEFENSIVE BOOST (increase cost when target is a tagged Phoenix soldier) ---
            if (PsychicBuffManager.MindfraggerResearchCompleted &&
                PsychicAbilityNames.Contains(__instance.TacticalAbilityDef.name))
            {
                TacticalActor defender = __instance.TargetActor;
                if (defender != null && defender.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
                {
                    float defBoost = defender.CharacterStats.WillPoints.Max * 0.5f;
                    float newCost = originalMin + defBoost;

                    // Cache original (once per def, per activation)
                    if (!PsychicBuffManager.originalUpkeepCache.ContainsKey(__instance.ApplyStatusAbilityDef))
                        PsychicBuffManager.originalUpkeepCache[__instance.ApplyStatusAbilityDef] = originalMin;

                    __instance.ApplyStatusAbilityDef.MinUpkeepCost = newCost;
                }
            }

            // --- OFFENSIVE BONUS (decrease cost when attacker is a tagged Phoenix soldier) ---
            if (PsychicBuffManager.PsychicInfluencesCompleted &&
                PsychicAbilityNames.Contains(__instance.TacticalAbilityDef.name))
            {
                TacticalActor attacker = __instance.TacticalActor;
                if (attacker != null && attacker.GameTags.Contains(PsychicBuffManager.PsychicInfluencesTag))
                {
                    float attackerWP = attacker.CharacterStats.WillPoints.IntValue;
                    if (attackerWP <= 56f)
                    {
                        Debug.Log($"[AAP] Psychic attack blocked: attacker WP {attackerWP} ≤ 56");
                        // Block by setting a huge cost; the ability will likely fail silently.
                        __instance.ApplyStatusAbilityDef.MinUpkeepCost = float.MaxValue;
                        return;
                    }

                    // Cache original if not already cached
                    if (!PsychicBuffManager.originalUpkeepCache.ContainsKey(__instance.ApplyStatusAbilityDef))
                        PsychicBuffManager.originalUpkeepCache[__instance.ApplyStatusAbilityDef] = originalMin;

                    float newCost = Mathf.Max(0, originalMin - 30f);
                    __instance.ApplyStatusAbilityDef.MinUpkeepCost = newCost;
                }
            }
        }

        // Postfix: restore original MinUpkeepCost
        [HarmonyPostfix]
        public static void Postfix(ApplyStatusAbility __instance)
        {
            if (PsychicBuffManager.originalUpkeepCache.TryGetValue(__instance.ApplyStatusAbilityDef, out float original))
            {
                __instance.ApplyStatusAbilityDef.MinUpkeepCost = original;
                PsychicBuffManager.originalUpkeepCache.Remove(__instance.ApplyStatusAbilityDef);
            }
        }
    }
}