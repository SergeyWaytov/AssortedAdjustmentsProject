// PsychicBuffManager.cs
using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Geoscape.Entities.Research;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Tactical;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Effects;
using PhoenixPoint.Tactical.Entities.Statuses;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class PsychicBuffManager
    {
        public static GameTagDef MindfraggerBonusTag;
        public static GameTagDef PsychicInfluencesTag;
        public static bool MindfraggerResearchCompleted = false;
        public static bool PsychicInfluencesCompleted = false;

        public static Dictionary<TacticalAbility, (float savedMax, float savedCurrent)> offensiveSwitcheroo
    = new Dictionary<TacticalAbility, (float savedMax, float savedCurrent)>();

        public static void Init() => CreateTags();

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

    // =============== RESEARCH HOOKS ===============
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
            else if (defName.IndexOf("PsychicInfluences", StringComparison.OrdinalIgnoreCase) >= 0
                  || defName.IndexOf("PyschicAttack", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                PsychicBuffManager.PsychicInfluencesCompleted = true;
                Debug.Log("[AAP] Psychic Influences research complete. Full psychic shell active.");
                AddTagToAll(PsychicBuffManager.PsychicInfluencesTag, __instance);
            }
        }
        private static void AddTagToAll(GameTagDef tag, GeoPhoenixFaction faction)
        {
            if (tag == null) return;
            foreach (var c in faction.Characters.Where(c => c.TemplateDef.IsHuman))
                if (!c.GameTags.Contains(tag)) c.GameTags.Add(tag);
        }
    }

    // =============== TAG INHERITANCE ON SPAWN ===============
    [HarmonyPatch(typeof(TacticalActor), "ProcessInstanceData")]
    public static class PsychicTagInheritance_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(TacticalActor __instance)
        {
            if (__instance.TacticalFaction != __instance.TacticalLevel?.View?.ViewerFaction) return;
            var geoLevel = GameUtl.CurrentLevel()?.GetComponent<GeoLevelController>();
            var geoChar = geoLevel?.PhoenixFaction?.Characters.FirstOrDefault(c => c.Id == __instance.GeoUnitId);
            if (geoChar == null) return;
            if (PsychicBuffManager.MindfraggerResearchCompleted && !__instance.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
                __instance.AddGameTags(new GameTagsList() { PsychicBuffManager.MindfraggerBonusTag });
            if (PsychicBuffManager.PsychicInfluencesCompleted && !__instance.GameTags.Contains(PsychicBuffManager.PsychicInfluencesTag))
                __instance.AddGameTags(new GameTagsList() { PsychicBuffManager.PsychicInfluencesTag });
        }
    }

    // =============== OFFENSIVE JUGGLING ===============
    [HarmonyPatch(typeof(TacticalAbility), "ApplyCosts")]
    public static class OffensivePsychicApplyCosts_Patch
    {
        static bool Prefix(TacticalAbility __instance)
        {
            if (!PsychicBuffManager.PsychicInfluencesCompleted) return true;
            if (!IsOffensivePsychic(__instance.TacticalAbilityDef.name)) return true;
            var caster = __instance.TacticalActor;
            if (caster == null || !caster.GameTags.Contains(PsychicBuffManager.PsychicInfluencesTag)) return true;
            if (caster.CharacterStats.WillPoints.Max <= 56f) return false; // block
            var wp = caster.CharacterStats.WillPoints;
            float savedMax = wp.Max, savedCurrent = wp.Value;
            PsychicBuffManager.offensiveSwitcheroo[__instance] = (savedMax, savedCurrent);
            wp.Set(savedMax + 30f, false);
            wp.Set(savedCurrent + 30f, false);
            return true;
        }
        static void Postfix(TacticalAbility __instance)
        {
            if (PsychicBuffManager.offensiveSwitcheroo.TryGetValue(__instance, out var saved))
            {
                var wp = __instance.TacticalActor.CharacterStats.WillPoints;
                float costPaid = saved.savedCurrent - (wp.Value - 30f);
                wp.Set(saved.savedMax, false);
                wp.Set(saved.savedCurrent - costPaid, false);
                PsychicBuffManager.offensiveSwitcheroo.Remove(__instance);
            }
        }
        private static bool IsOffensivePsychic(string n) =>
            n.Contains("MindControl") || n.Contains("Panic") ||
            n.Contains("PsychicScream") || n.Contains("MindCrush") ||
            n.Contains("InstilFrenzy");
    }

    // =============== DEFENSIVE TARGET FILTER FOR INDUCE PANIC / MIND CONTROL ===============
    [HarmonyPatch(typeof(ApplyEffectAbility), "TargetFilterPredicate")]
    public static class DefensiveTargetFilter_Patch
    {
        static bool Prefix(ApplyEffectAbility __instance, TacticalActorBase targetActor, ref bool __result)
        {
            string defName = __instance.TacticalAbilityDef.name;
            if (!defName.Contains("InducePanic") && !defName.Contains("MindControl"))
                return true; // run original predicate

            var target = targetActor as TacticalActor;
            if (target?.TacticalFaction != target.TacticalLevel.View.ViewerFaction)
                return true;

            // Stage 2 block if attacker max WP ≤ 56
            if (PsychicBuffManager.PsychicInfluencesCompleted)
            {
                var caster = __instance.TacticalActor;
                if (caster != null && caster.CharacterStats.WillPoints.Max <= 56f)
                {
                    __result = false; // blocked
                    return false;
                }
            }

            // Temporarily inflate target WP for comparison (restore in postfix)
            if (PsychicBuffManager.PsychicInfluencesCompleted)
            {
                var caster = __instance.TacticalActor;
                if (caster != null && caster.CharacterStats.WillPoints.Value > 20f)
                {
                    // Fake 56 WP
                    var wp = target.CharacterStats.WillPoints;
                    // Store backup (we'll use a static dict for simplicity)
                    _targetWpBackup[target] = (wp.Value, wp.Max);
                    wp.Set(56f, false);
                    wp.Set(56f, false);
                }
                // else factual – no change
            }
            else if (PsychicBuffManager.MindfraggerResearchCompleted &&
                     target.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
            {
                var wp = target.CharacterStats.WillPoints;
                float boost = wp.Max * 0.5f;
                _targetWpBackup[target] = (wp.Value, wp.Max);
                wp.Set(wp.Value + boost, false);
                wp.Set(wp.Max + boost, false);
            }

            return true; // continue original predicate (which will now see inflated WP)
        }

        static void Postfix(ApplyEffectAbility __instance, TacticalActorBase targetActor)
        {
            var target = targetActor as TacticalActor;
            if (target == null) return;
            if (_targetWpBackup.TryGetValue(target, out var saved))
            {
                target.CharacterStats.WillPoints.Set(saved.max, false);
                target.CharacterStats.WillPoints.Set(saved.cur, false);
                _targetWpBackup.Remove(target);
            }
        }

        private static Dictionary<TacticalActor, (float cur, float max)> _targetWpBackup
    = new Dictionary<TacticalActor, (float cur, float max)>();
    }

    // =============== PSYCHIC DAMAGE (SCREAM & CRUSH) DEFENSE ===============
    [HarmonyPatch(typeof(DamageAccumulation), "GenerateStandardDamageTargetData")]
    public static class PsychicDamage_Defense
    {
        static void Prefix(DamageAccumulation __instance, IDamageReceiver target)
        {
            var actor = target.GetActor() as TacticalActor;
            if (actor?.TacticalFaction != actor.TacticalLevel.View.ViewerFaction) return;

            string effectName = __instance.DamageEffectDef?.name ?? "";
            if (!effectName.Contains("MindCrush") && !effectName.Contains("PsychicScream"))
                return;

            var caster = TacUtil.GetSourceTacticalActorBase(__instance.Source) as TacticalActor;
            if (caster == null) return;

            // Stage 2 full block
            if (PsychicBuffManager.PsychicInfluencesCompleted && caster.CharacterStats.WillPoints.Max <= 56f)
            {
                __instance.Amount = 0f;
                Debug.Log("[AAP] Psychic damage blocked (shell)");
                return;
            }

            // Stage 1 universal halving for Scream only
            bool hasStage1 = PsychicBuffManager.MindfraggerResearchCompleted &&
                             effectName.Contains("PsychicScream") &&
                             actor.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag);
            // Stage 2 exhaustion halving (defender WP - attacker WP > 7)
            bool stage2Exhausted = PsychicBuffManager.PsychicInfluencesCompleted &&
                                   (actor.CharacterStats.WillPoints.Value - caster.CharacterStats.WillPoints.Value > 7f);

            if (hasStage1 || stage2Exhausted)
            {
                __instance.Amount *= 0.5f;
                Debug.Log($"[AAP] Psychic damage halved ({(hasStage1 ? "Stage1" : "exhaustion")})");
            }
        }
    }
}