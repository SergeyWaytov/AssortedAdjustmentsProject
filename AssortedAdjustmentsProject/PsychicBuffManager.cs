// PsychicBuffManager.cs (Final – Approved design, 2026‑04‑27)
using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Geoscape.Levels;
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

        // ---------- Flags (forced on for testing; replace with research gating before release) ----------
        public static bool MindfraggerResearchCompleted = true;
        public static bool PsychicInfluencesCompleted = true;

        // ---------- Offensive WP switcheroo state ----------
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

    // ================================================================
    // TAG INHERITANCE – adds tags to all Phoenix soldiers on mission start
    // ================================================================
    [HarmonyPatch(typeof(TacticalActor), "ProcessInstanceData")]
    public static class PsychicTagInheritance_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(TacticalActor __instance)
        {
            if (__instance.TacticalFaction != __instance.TacticalLevel?.View?.ViewerFaction)
                return;

            var geoLevel = GameUtl.CurrentLevel()?.GetComponent<GeoLevelController>();
            var geoChar = geoLevel?.PhoenixFaction?.Characters.FirstOrDefault(c => c.Id == __instance.GeoUnitId);
            if (geoChar == null) return;

            if (PsychicBuffManager.MindfraggerResearchCompleted &&
                !__instance.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
                __instance.AddGameTags(new GameTagsList() { PsychicBuffManager.MindfraggerBonusTag });

            if (PsychicBuffManager.PsychicInfluencesCompleted &&
                !__instance.GameTags.Contains(PsychicBuffManager.PsychicInfluencesTag))
                __instance.AddGameTags(new GameTagsList() { PsychicBuffManager.PsychicInfluencesTag });
        }
    }

    // ================================================================
    // STAGE 2 – OFFENSIVE SWITCHEROO (Phoenix casters only, NO WP BLOCK)
    // ================================================================
    [HarmonyPatch(typeof(TacticalAbility), "ApplyCosts")]
    public static class OffensivePsychicApplyCosts_Patch
    {
        static bool Prefix(TacticalAbility __instance)
        {
            if (!PsychicBuffManager.PsychicInfluencesCompleted) return true;
            if (!IsOffensivePsychic(__instance.TacticalAbilityDef.name)) return true;

            var caster = __instance.TacticalActor;
            if (caster == null || !caster.GameTags.Contains(PsychicBuffManager.PsychicInfluencesTag))
                return true;   // only Phoenix tagged soldiers get the bonus

            // Switcheroo (rule #9)
            var wp = caster.CharacterStats.WillPoints;
            float savedMax = wp.Max, savedCurrent = wp.Value;
            PsychicBuffManager.offensiveSwitcheroo[__instance] = (savedMax, savedCurrent);

            wp.Set(savedMax + 30f, false);
            wp.Set(savedCurrent + 30f, false);

            Debug.Log($"[AAP] Offensive WP boost: {__instance.TacticalAbilityDef.name} cast by {caster.DisplayName} (WP {savedCurrent}/{savedMax} → {wp.Value}/{wp.Max})");
            return true;
        }

        static void Postfix(TacticalAbility __instance)
        {
            if (!PsychicBuffManager.offensiveSwitcheroo.TryGetValue(__instance, out var saved))
                return;

            var wp = __instance.TacticalActor.CharacterStats.WillPoints;
            // cost paid = original current − (current after cost − 30 boost)
            float costPaid = saved.savedCurrent - (wp.Value - 30f);
            if (costPaid < 0f) costPaid = 0f;

            wp.Set(saved.savedMax, false);
            wp.Set(saved.savedCurrent - costPaid, false);
            PsychicBuffManager.offensiveSwitcheroo.Remove(__instance);

            Debug.Log($"[AAP] Offensive WP restored: cost = {costPaid}, WP now {wp.Value}/{wp.Max}");
        }

        private static bool IsOffensivePsychic(string name) =>
            name.Contains("MindControl") || name.Contains("InducePanic") ||
            name.Contains("PsychicScream") || name.Contains("MindCrush") ||
            name.Contains("InstilFrenzy");
    }

    // ================================================================
    // DEFENSIVE PATCHES (rules #1‑8) – only when an ENEMY attacks a Phoenix soldier
    // ================================================================

    // --- MC / Panic target filter (rules #1, #2, #3, #4) ---
    [HarmonyPatch(typeof(ApplyEffectAbility), "TargetFilterPredicate")]
    public static class DefensiveTargetFilter_Patch
    {
        static bool Prefix(ApplyEffectAbility __instance, TacticalActorBase targetActor, ref bool __result)
        {
            string defName = __instance.TacticalAbilityDef.name;
            if (!defName.Contains("InducePanic") && !defName.Contains("MindControl"))
                return true;

            TacticalActor target = targetActor as TacticalActor;
            TacticalActor caster = __instance.TacticalActor;
            if (target == null || caster == null) return true;

            // Only defend Phoenix soldiers against enemies
            if (target.TacticalFaction != target.TacticalLevel.View.ViewerFaction ||
                caster.TacticalFaction.GetRelationTo(target.TacticalFaction) != FactionRelation.Enemy)
                return true;

            // Stage 2 (PsychicInfluences)
            if (PsychicBuffManager.PsychicInfluencesCompleted)
            {
                // Rule #3: block if attacker Max WP ≤ 56
                if (caster.CharacterStats.WillPoints.Max <= 56f)
                {
                    __result = false;
                    Debug.Log($"[AAP] Defensive block (shell): {defName} from {caster.DisplayName} (WPmax≤56) against {target.DisplayName}");
                    return false;
                }

                // Rule #4: inflate defender WP to 56 if attacker fresh (>20 current WP)
                if (caster.CharacterStats.WillPoints.Value > 20f)
                {
                    var wp = target.CharacterStats.WillPoints;
                    _targetWpBackup[target] = (wp.Value, wp.Max);
                    wp.Set(56f, false);
                    wp.Set(56f, false);
                    Debug.Log($"[AAP] Defensive WP inflated to 56 for {target.DisplayName} (shell active)");
                }
            }
            // Stage 1 (Mindfragger only) – rule #1
            else if (PsychicBuffManager.MindfraggerResearchCompleted &&
                     target.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
            {
                var wp = target.CharacterStats.WillPoints;
                float boost = wp.Max * 0.5f;
                _targetWpBackup[target] = (wp.Value, wp.Max);
                wp.Set(wp.Value + boost, false);
                wp.Set(wp.Max + boost, false);
                Debug.Log($"[AAP] Defensive WP boost (+50%) for {target.DisplayName}: {wp.Value}/{wp.Max}");
            }

            return true;
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

    // --- Sonic Blast negation + probability (rule #5) ---
    [HarmonyPatch(typeof(ApplyEffectAbility), "TargetFilterPredicate")]
    public static class SonicBlastDefense_Patch
    {
        private static readonly System.Random Rng = new System.Random();

        static bool Prefix(ApplyEffectAbility __instance, TacticalActorBase targetActor, ref bool __result)
        {
            string defName = __instance.TacticalAbilityDef.name;
            // Detect Sonic Blast ability – try known def name pattern
            if (!defName.Contains("SonicBlast") && !defName.Contains("Sonic_Blast"))
                return true;

            TacticalActor target = targetActor as TacticalActor;
            TacticalActor caster = __instance.TacticalActor;
            if (target == null || caster == null) return true;
            if (target.TacticalFaction != target.TacticalLevel.View.ViewerFaction ||
                caster.TacticalFaction.GetRelationTo(target.TacticalFaction) != FactionRelation.Enemy)
                return true;

            // Only Stage 2 (PsychicInfluences)
            if (!PsychicBuffManager.PsychicInfluencesCompleted)
                return true;

            float enemyMaxWP = caster.CharacterStats.WillPoints.Max;

            // Rule #5a: negate entirely if Max WP ≤ 56
            if (enemyMaxWP <= 56f)
            {
                __result = false;
                Debug.Log($"[AAP] Sonic Blast negated (shell): {defName} from {caster.DisplayName} (WPmax≤56) against {target.DisplayName}");
                return false;
            }

            // Rule #5b: probability roll (linear 0‑75% from 56 to 66 Max WP)
            float probability = 0.75f * (enemyMaxWP - 56f) / (66f - 56f);
            probability = Mathf.Clamp01(probability);
            float roll = (float)Rng.NextDouble();

            if (roll > probability)
            {
                __result = false;
                Debug.Log($"[AAP] Sonic Blast daze roll failed (prob {probability:P1}): {defName} from {caster.DisplayName} to {target.DisplayName}");
                return false;
            }

            Debug.Log($"[AAP] Sonic Blast daze roll succeeded (prob {probability:P1}): {defName} from {caster.DisplayName} to {target.DisplayName}");
            return true;  // allow the normal dazing calculation
        }
    }

    // --- Psychic damage defense (Scream / Mind Crush) – rules #2, #6, #7, #8 ---
    [HarmonyPatch(typeof(DamageAccumulation), "GenerateStandardDamageTargetData")]
    public static class PsychicDamage_Defense
    {
        static void Prefix(DamageAccumulation __instance, IDamageReceiver target)
        {
            var actor = target.GetActor() as TacticalActor;
            if (actor == null || actor.TacticalFaction != actor.TacticalLevel.View.ViewerFaction)
                return; // only protect Phoenix soldiers

            string effectName = __instance.DamageEffectDef?.name ?? "";
            bool isScream = effectName.Contains("PsychicScream");
            bool isCrush = effectName.Contains("MindCrush");
            if (!isScream && !isCrush) return;

            var caster = TacUtil.GetSourceTacticalActorBase(__instance.Source) as TacticalActor;
            if (caster == null) return;
            if (caster.TacticalFaction.GetRelationTo(actor.TacticalFaction) != FactionRelation.Enemy)
                return; // only defend against enemies

            float casterMaxWP = caster.CharacterStats.WillPoints.Max;

            // Stage 2 (PsychicInfluences) – rules #6, #7, #8
            if (PsychicBuffManager.PsychicInfluencesCompleted)
            {
                // Rule #6: block if caster Max WP ≤ 56
                if (casterMaxWP <= 56f)
                {
                    __instance.Amount = 0f;
                    Debug.Log($"[AAP] Psychic damage blocked (shell): {effectName} from {caster.DisplayName} (WPmax≤56) to {actor.DisplayName}");
                    return;
                }

                // Rule #7: exhaustion halving if caster Current WP ≤ 20
                if (caster.CharacterStats.WillPoints.Value <= 20f)
                {
                    __instance.Amount *= 0.5f;
                    Debug.Log($"[AAP] Psychic damage halved (exhaustion): {effectName} from {caster.DisplayName} to {actor.DisplayName}");
                    return;
                }

                // Rule #8: full damage (no modification)
                return;
            }
            // Stage 1 (Mindfragger only) – rule #2 (Scream only)
            else if (PsychicBuffManager.MindfraggerResearchCompleted &&
                     isScream &&
                     actor.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
            {
                __instance.Amount *= 0.5f;
                Debug.Log($"[AAP] Psychic damage halved (Stage1 Scream): {effectName} to {actor.DisplayName}");
            }
        }
    }
}