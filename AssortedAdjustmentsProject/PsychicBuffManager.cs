// PsychicBuffManager.cs (fixed – defensive only vs enemy attacks)
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
        // Force ON for testing – MUST be replaced with research gating before release
        public static bool MindfraggerResearchCompleted = true;
        public static bool PsychicInfluencesCompleted = true;

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

    // =============== TAG INHERITANCE ON SPAWN (adds tags to player soldiers) ===============
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

    // =============== OFFENSIVE JUGGLING (for player psychic attacks) ===============
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
            Debug.Log($"[AAP] Offensive WP boost: {__instance.TacticalAbilityDef.name} cast by {caster.DisplayName} (WP {savedCurrent}/{savedMax} → {wp.Value}/{wp.Max})");
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
                Debug.Log($"[AAP] Offensive WP restored: cost = {costPaid}, WP now {wp.Value}/{wp.Max}");
            }
        }
        private static bool IsOffensivePsychic(string n) =>
            n.Contains("MindControl") || n.Contains("Panic") ||
            n.Contains("PsychicScream") || n.Contains("MindCrush") ||
            n.Contains("InstilFrenzy");
    }

    // =============== DEFENSIVE TARGET FILTER (now only vs enemy attacks!) ===============
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

            // ONLY defend when the CASTER is an enemy of the target (i.e., enemy attack on our soldier)
            if (caster.TacticalFaction.GetRelationTo(target.TacticalFaction) != FactionRelation.Enemy)
                return true;   // allow friendly / self cast without interference

            // --- Stage 2 shell (Psychic Influences) ---
            if (PsychicBuffManager.PsychicInfluencesCompleted)
            {
                if (caster.CharacterStats.WillPoints.Max <= 56f)
                {
                    __result = false;
                    Debug.Log($"[AAP] Defensive block (shell): {defName} from {caster.DisplayName} (WPmax≤56) against {target.DisplayName}");
                    return false;
                }
            }

            // --- WP inflation (both stages) ---
            if (PsychicBuffManager.PsychicInfluencesCompleted)
            {
                if (caster.CharacterStats.WillPoints.Value > 20f)
                {
                    var wp = target.CharacterStats.WillPoints;
                    _targetWpBackup[target] = (wp.Value, wp.Max);
                    wp.Set(56f, false);
                    wp.Set(56f, false);
                    Debug.Log($"[AAP] Defensive WP inflated to 56 for {target.DisplayName} (shell active)");
                }
            }
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

    // =============== PSYCHIC DAMAGE DEFENSE (only when damage source is an enemy) ===============
    [HarmonyPatch(typeof(DamageAccumulation), "GenerateStandardDamageTargetData")]
    public static class PsychicDamage_Defense
    {
        static void Prefix(DamageAccumulation __instance, IDamageReceiver target)
        {
            var actor = target.GetActor() as TacticalActor;
            if (actor?.TacticalFaction != actor.TacticalLevel.View.ViewerFaction) return; // only player soldiers

            string effectName = __instance.DamageEffectDef?.name ?? "";
            if (!effectName.Contains("MindCrush") && !effectName.Contains("PsychicScream"))
                return;

            var caster = TacUtil.GetSourceTacticalActorBase(__instance.Source) as TacticalActor;
            if (caster == null) return;

            // Only defend if the CASTER is an enemy of the target
            if (caster.TacticalFaction.GetRelationTo(actor.TacticalFaction) != FactionRelation.Enemy)
                return;

            if (PsychicBuffManager.PsychicInfluencesCompleted && caster.CharacterStats.WillPoints.Max <= 56f)
            {
                __instance.Amount = 0f;
                Debug.Log($"[AAP] Psychic damage blocked (shell): {effectName} from {caster.DisplayName} to {actor.DisplayName}");
                return;
            }

            bool hasStage1 = PsychicBuffManager.MindfraggerResearchCompleted &&
                             effectName.Contains("PsychicScream") &&
                             actor.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag);
            bool stage2Exhausted = PsychicBuffManager.PsychicInfluencesCompleted &&
                                   (actor.CharacterStats.WillPoints.Value - caster.CharacterStats.WillPoints.Value > 7f);

            if (hasStage1 || stage2Exhausted)
            {
                __instance.Amount *= 0.5f;
                Debug.Log($"[AAP] Psychic damage halved ({(hasStage1 ? "Stage1" : "exhaustion")}): {effectName} to {actor.DisplayName}");
            }
        }
    }
}