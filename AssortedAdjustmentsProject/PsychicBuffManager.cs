// PsychicBuffManager.cs (Final – Approved design, 2026‑04‑27, with WP‑loss defence v3 and MC block)
using Base.Core;
using Base.Defs;
using Base.Entities.Statuses;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Tactical;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Effects;
using PhoenixPoint.Tactical.Entities.Statuses;
using PhoenixPoint.Tactical.Levels;
using PhoenixPoint.Tactical.View;
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

    // ================================================================
    // TAG INHERITANCE
    // ================================================================
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

    // ================================================================
    // STAGE 2 – OFFENSIVE SWITCHEROO
    // ================================================================
    [HarmonyPatch(typeof(TacticalAbility), "ApplyCosts")]
    public static class OffensivePsychicApplyCosts_Patch
    {
        static bool Prefix(TacticalAbility __instance)
        {
            if (!PsychicBuffManager.PsychicInfluencesCompleted) return true;
            if (!IsOffensivePsychic(__instance.TacticalAbilityDef.name)) return true;
            var caster = __instance.TacticalActor;
            if (caster == null || !caster.GameTags.Contains(PsychicBuffManager.PsychicInfluencesTag)) return true;

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
            if (!PsychicBuffManager.offensiveSwitcheroo.TryGetValue(__instance, out var saved)) return;
            var wp = __instance.TacticalActor.CharacterStats.WillPoints;
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
    // DEFENSIVE PATCHES (MC / Panic target filter)
    // ================================================================

    // Original patch for ApplyEffectAbility.TargetFilterPredicate (may still apply to Induce Panic)
    [HarmonyPatch(typeof(ApplyEffectAbility), "TargetFilterPredicate")]
    public static class DefensiveTargetFilter_Patch
    {
        static bool Prefix(ApplyEffectAbility __instance, TacticalActorBase targetActor, ref bool __result)
        {
            string defName = __instance.TacticalAbilityDef.name;
            if (!defName.Contains("InducePanic") && !defName.Contains("MindControl")) return true;

            TacticalActor target = targetActor as TacticalActor;
            TacticalActor caster = __instance.TacticalActor;
            if (target == null || caster == null) return true;
            if (target.TacticalFaction != target.TacticalLevel.View.ViewerFaction ||
                caster.TacticalFaction.GetRelationTo(target.TacticalFaction) != FactionRelation.Enemy)
                return true;

            if (PsychicBuffManager.PsychicInfluencesCompleted)
            {
                if (caster.CharacterStats.WillPoints.Max <= 56f)
                {
                    __result = false;
                    Debug.Log($"[AAP] Defensive block (shell): {defName} from {caster.DisplayName} (WPmax≤56) against {target.DisplayName}");
                    return false;
                }
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

    // ===== FIXED: MindControlAbility.Activate prefix (null‑safe) =====
    [HarmonyPatch(typeof(MindControlAbility), "Activate")]
    public static class DefensiveTargetFilter_MindControl_Patch
    {
        static bool Prefix(MindControlAbility __instance)
        {
            if (!PsychicBuffManager.PsychicInfluencesCompleted)
                return true;

            TacticalActor caster = __instance.TacticalActor;
            if (caster == null) return true;

            // null safety for the whole view chain
            TacticalLevelController level = caster.TacticalLevel;
            if (level == null) return true;
            TacticalView view = level.View;
            if (view == null) return true;
            TacticalFaction viewerFaction = view.ViewerFaction;
            if (viewerFaction == null) return true;

            if (caster.TacticalFaction.GetRelationTo(viewerFaction) != FactionRelation.Enemy)
                return true;

            float casterMaxWP = caster.CharacterStats.WillPoints.Max;
            if (casterMaxWP <= 56f)
            {
                Debug.Log($"[AAP] Defensive block (shell) – {caster.DisplayName} (WPmax={casterMaxWP}) cannot use Mind Control.");
                return false; // skip the original Activate
            }

            return true;
        }
    }

    // ===== NEW: AI no longer tries the blocked ability =====
    [HarmonyPatch(typeof(MindControlAbility), "GetDisabledStateInternal")]
    public static class MindControl_DisableForAI_Patch
    {
        static void Postfix(MindControlAbility __instance, ref AbilityDisabledState __result)
        {
            if (!PsychicBuffManager.PsychicInfluencesCompleted) return;

            TacticalActor caster = __instance.TacticalActor;
            if (caster == null) return;
            TacticalLevelController level = caster.TacticalLevel;
            if (level == null || level.View == null) return;
            TacticalFaction viewer = level.View.ViewerFaction;
            if (viewer == null) return;
            if (caster.TacticalFaction.GetRelationTo(viewer) != FactionRelation.Enemy) return;

            float casterMaxWP = caster.CharacterStats.WillPoints.Max;
            if (casterMaxWP <= 56f)
            {
                __result = AbilityDisabledState.NotEnoughWillPoints;
            }
        }
    }

    // ================================================================
    // SONIC BLAST DEFENSE
    // ================================================================
    [HarmonyPatch(typeof(ApplyEffectAbility), "TargetFilterPredicate")]
    public static class SonicBlastDefense_Patch
    {
        private static readonly System.Random Rng = new System.Random();

        static bool Prefix(ApplyEffectAbility __instance, TacticalActorBase targetActor, ref bool __result)
        {
            string defName = __instance.TacticalAbilityDef.name;
            if (!defName.Contains("SonicBlast") && !defName.Contains("Sonic_Blast")) return true;

            TacticalActor target = targetActor as TacticalActor;
            TacticalActor caster = __instance.TacticalActor;
            if (target == null || caster == null) return true;
            if (target.TacticalFaction != target.TacticalLevel.View.ViewerFaction ||
                caster.TacticalFaction.GetRelationTo(target.TacticalFaction) != FactionRelation.Enemy)
                return true;
            if (!PsychicBuffManager.PsychicInfluencesCompleted) return true;

            float enemyMaxWP = caster.CharacterStats.WillPoints.Max;
            if (enemyMaxWP <= 56f)
            {
                __result = false;
                Debug.Log($"[AAP] Sonic Blast negated (shell): {defName} from {caster.DisplayName} (WPmax≤56) against {target.DisplayName}");
                return false;
            }

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
            return true;
        }
    }

    // ================================================================
    // PSYCHIC DAMAGE DEFENSE (health damage – Mind Crush)
    // ================================================================
    [HarmonyPatch(typeof(DamageAccumulation), "GenerateStandardDamageTargetData")]
    public static class PsychicDamage_Defense
    {
        static void Prefix(DamageAccumulation __instance, IDamageReceiver target)
        {
            var actor = target.GetActor() as TacticalActor;
            if (actor == null || actor.TacticalFaction != actor.TacticalLevel.View.ViewerFaction) return;

            string effectName = __instance.DamageEffectDef?.name ?? "";
            if (!effectName.Contains("MindCrush") && !effectName.Contains("PsychicScream")) return;

            var caster = TacUtil.GetSourceTacticalActorBase(__instance.Source) as TacticalActor;
            if (caster == null) return;
            if (caster.TacticalFaction.GetRelationTo(actor.TacticalFaction) != FactionRelation.Enemy) return;

            float casterMaxWP = caster.CharacterStats.WillPoints.Max;
            if (PsychicBuffManager.PsychicInfluencesCompleted)
            {
                if (casterMaxWP <= 56f)
                {
                    __instance.Amount = 0f;
                    Debug.Log($"[AAP] Psychic damage blocked (shell): {effectName} from {caster.DisplayName} (WPmax≤56) to {actor.DisplayName}");
                    return;
                }
                if (caster.CharacterStats.WillPoints.Value <= 20f)
                {
                    __instance.Amount *= 0.5f;
                    Debug.Log($"[AAP] Psychic damage halved (exhaustion): {effectName} from {caster.DisplayName} to {actor.DisplayName}");
                    return;
                }
            }
            else if (PsychicBuffManager.MindfraggerResearchCompleted &&
                     effectName.Contains("PsychicScream") &&
                     actor.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
            {
                __instance.Amount *= 0.5f;
                Debug.Log($"[AAP] Psychic damage halved (Stage1 Scream): {effectName} to {actor.DisplayName}");
            }
        }
    }

    // ================================================================
    // WILLPOINT‑LOSS DEFENSE (Scream / Mind Crush WP drain) – v3 FIXED
    // ================================================================
    [HarmonyPatch(typeof(StatusStat), "ApplyStatModification")]
    public static class PsychicWillpointLoss_Defense_V3
    {
        static void Prefix(StatusStat __instance, ref StatModification statMod)
        {
            if (__instance.Name != "WillPoints") return;
            if (statMod.Value >= 0f) return;

            var actor = __instance.Owner as TacticalActor;
            if (actor == null || actor.TacticalFaction != actor.TacticalLevel.View.ViewerFaction)
                return;

            TacticalActor caster = null;
            object source = statMod.Source;

            if (source is TacStatus status)
                caster = status.Source as TacticalActor;
            else if (source is TacticalAbility ability)
                caster = ability.TacticalActor;
            else if (source is DamageAccumulation dmg)
                caster = TacUtil.GetSourceTacticalActorBase(dmg.Source) as TacticalActor;

            if (caster == null && actor.Status != null)
            {
                foreach (var s in actor.Status.Statuses.OfType<TacStatus>())
                {
                    if (s.TacStatusDef.EffectName?.Contains("PsychicScream") == true ||
                        s.TacStatusDef.EffectName?.Contains("MindCrush") == true)
                    {
                        caster = s.Source as TacticalActor;
                        break;
                    }
                }
            }

            if (caster == null) return;
            if (caster.TacticalFaction.GetRelationTo(actor.TacticalFaction) != FactionRelation.Enemy)
                return;

            float casterMaxWP = caster.CharacterStats.WillPoints.Max;
            float casterCurWP = caster.CharacterStats.WillPoints.Value;

            if (PsychicBuffManager.PsychicInfluencesCompleted)
            {
                if (casterMaxWP <= 56f)
                {
                    statMod.Value = 0f;
                    Debug.Log($"[AAP] Psychic WP loss blocked (shell): from {caster.DisplayName} (WPmax≤56) to {actor.DisplayName}");
                    return;
                }
                if (casterCurWP <= 20f)
                {
                    statMod.Value *= 0.5f;
                    Debug.Log($"[AAP] Psychic WP loss halved (exhaustion): from {caster.DisplayName} to {actor.DisplayName}");
                }
            }
            else if (PsychicBuffManager.MindfraggerResearchCompleted &&
                     actor.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
            {
                statMod.Value *= 0.5f;
                Debug.Log($"[AAP] Psychic WP loss halved (Stage1 Scream): to {actor.DisplayName}");
            }
        }
    }
}