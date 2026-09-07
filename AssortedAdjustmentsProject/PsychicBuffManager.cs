using Base.Core;
using Base.Defs;
using Base.Entities.Statuses;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsSharedData;
using PhoenixPoint.Common.Entities.GameTagsTypes;
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
        public static bool MindfraggerResearchCompleted = false;
        public static bool PsychicInfluencesCompleted = false;

        public static Dictionary<TacticalAbility, (float savedMax, float savedCurrent)> offensiveSwitcheroo
            = new Dictionary<TacticalAbility, (float savedMax, float savedCurrent)>();

        public static void Init() => CreateTags();

        // AAP P2 (Phase 2b.3 fix): the audit recommended cloning from
        // "Manufacturable_TagDef" via DefCache.GetDef<GameTagDef>(name),
        // but that NAME is not resolvable via DefCache in the live build --
        // the tag exists as a PROPERTY (ManufacturableTag) on the
        // SharedGameTagsDataDef singleton, accessed via SharedData.
        // The Phase 2b CreateTags used DefCache.GetDef("Manufacturable_TagDef")
        // which returned null, logging "[AAP] Psychic marker source is
        // missing or is a ClassTagDef" and leaving MindfraggerBonusTag /
        // PsychicInfluencesTag null -- which silently inactivated P1/P3/P4
        // (the psychic WP buffs never matched any actor).
        //
        // TFTV confirms the correct access pattern at 7 sites, e.g.
        // TFTV/Vehicles/Ammo/MissionEndReplenish.cs:334:
        //   GameTagDef manufacturableTag =
        //     GameUtl.GameComponent<SharedData>().SharedGameTags.ManufacturableTag;
        // AAP's equivalent (already used in LootMechanics.cs:117) is:
        //   SharedData.GetSharedDataFromGame()?.SharedGameTags?.ManufacturableTag
        //
        // This fix tries the TFTV-confirmed SharedData path first, then
        // falls back to DefCache candidates (defensive, in case SharedData
        // isn't ready at Init time). Rejects ClassTagDef subclasses at any
        // level (the audit's P2 invariant). Logs which source worked so
        // the next Player.log confirms the fix.
        private static void CreateTags()
        {
            GameTagDef baseTag = null;
            string usedSource = "(none)";

            // Primary: TFTV-confirmed access pattern via SharedData singleton.
            try
            {
                baseTag = SharedData.GetSharedDataFromGame()?.SharedGameTags?.ManufacturableTag;
                if (baseTag != null && baseTag is ClassTagDef)
                {
                    Debug.LogWarning("[AAP] Psychic marker: SharedGameTags.ManufacturableTag is a ClassTagDef subclass (unexpected); falling back to DefCache candidates.");
                    baseTag = null;
                }
                else if (baseTag != null)
                {
                    usedSource = "SharedData.SharedGameTags.ManufacturableTag";
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AAP] Psychic marker: SharedData access failed (" + e.Message + "); falling back to DefCache candidates.");
                baseTag = null;
            }

            // Fallback: try known-good plain GameTagDef names from TFTV source.
            if (baseTag == null)
            {
                string[] candidates = {
                    "Manufacturable_TagDef",      // audit's recommendation (may not resolve via DefCache)
                    "GunWeapon_TagDef",          // TFTV VehiclesAmmoMain.cs:449 clone source
                    "PhoenixPoint_UniformTagDef", // TFTV TFTVHints.cs:730
                };
                foreach (var name in candidates)
                {
                    var candidate = ModMain.DefCache.GetDef<GameTagDef>(name);
                    if (candidate != null && !(candidate is ClassTagDef))
                    {
                        baseTag = candidate;
                        usedSource = name + " (DefCache fallback)";
                        break;
                    }
                }
            }

            if (baseTag == null)
            {
                Debug.LogError("[AAP] Psychic marker: no plain GameTagDef source found. P1/P3/P4 patches will be inactive (no crash, but psychic WP buffs won't fire). Report this with your build version.");
                return;
            }

            if (MindfraggerBonusTag == null)
            {
                MindfraggerBonusTag = Helpers.CreateDefFromClone(
                    baseTag, "a1b2c3d4-e5f6-4789-0abc-def012345678",
                    "AAP_MindfraggerBonus_Tag") as GameTagDef;
            }
            if (PsychicInfluencesTag == null)
            {
                PsychicInfluencesTag = Helpers.CreateDefFromClone(
                    baseTag, "b2c3d4e5-f6a7-4b89-1bcd-ef0123456789",
                    "AAP_PsychicInfluences_Tag") as GameTagDef;
            }

            Debug.Log($"[AAP] Psychic marker tags created from '{usedSource}' (MindfraggerBonus={MindfraggerBonusTag?.name}, PsychicInfluences={PsychicInfluencesTag?.name}).");
        }

        // AAP P3 helper: maximum-first ordering. Set() clamps Value to the
        // existing Max, so raising Value past Max requires raising Max first.
        // Every WP inflation / restoration in this file routes through this.
        // Public because the [HarmonyPatch] classes below (Offensive/Defensive
        // shells) call into PsychicBuffManager.SetWillPoints from outside
        // this class's own scope.
        public static void SetWillPoints(StatusStat wp, float current, float maximum)
        {
            wp.SetMax(maximum, false);
            wp.Set(current, false);
        }
    }

    // ================================================================
    // TAG INHERITANCE
    // ================================================================
    // AAP T9: this postfix is Priority.Low so Jacob's runtime class-tag
    // swap (TutorialJacobFixPatch, High) lands first; this patch only
    // inspects the result, never depends on Jacob's tag.
    [HarmonyPatch(typeof(TacticalActor), "ProcessInstanceData")]
    public static class PsychicTagInheritance_Patch
    {
        [HarmonyPriority(Priority.Low)]
        [HarmonyPostfix]
        public static void Postfix(TacticalActor __instance)
        {
            try
            {
                // AAP P1: the old postfix tried to fetch the geo-level char
                // by GeoUnitId from a GeoLevelController that is null by
                // definition in tactical, so geoChar was always null and the
                // gate returned before applying tags. New body uses the
                // tactical actor's TacticalFaction == ViewerFaction check,
                // which is the actual "is this a player's actor" signal.
                if (__instance == null ||
                    __instance.TacticalFaction != __instance.TacticalLevel?.View?.ViewerFaction)
                {
                    return;
                }
                if (PsychicBuffManager.MindfraggerResearchCompleted &&
                    PsychicBuffManager.MindfraggerBonusTag != null &&
                    !__instance.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
                {
                    __instance.AddGameTags(new GameTagsList { PsychicBuffManager.MindfraggerBonusTag });
                }
                if (PsychicBuffManager.PsychicInfluencesCompleted &&
                    PsychicBuffManager.PsychicInfluencesTag != null &&
                    !__instance.GameTags.Contains(PsychicBuffManager.PsychicInfluencesTag))
                {
                    __instance.AddGameTags(new GameTagsList { PsychicBuffManager.PsychicInfluencesTag });
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] PsychicTagInheritance_Patch failed: {e}");
            }
        }
    }

    // ================================================================
    // STAGE 2 – OFFENSIVE SWITCHEROO
    // ================================================================
    [HarmonyPatch(typeof(TacticalAbility), "ApplyCosts")]
    public static class OffensivePsychicApplyCosts_Patch
    {
        static bool Prefix(TacticalAbility __instance)
        {
            try
            {
                if (!PsychicBuffManager.PsychicInfluencesCompleted) return true;
                if (!IsOffensivePsychic(__instance.TacticalAbilityDef.name)) return true;
                var caster = __instance.TacticalActor;
                if (caster == null || !caster.GameTags.Contains(PsychicBuffManager.PsychicInfluencesTag)) return true;

                var wp = caster.CharacterStats.WillPoints;
                float savedMax = wp.Max, savedCurrent = wp.Value;
                PsychicBuffManager.offensiveSwitcheroo[__instance] = (savedMax, savedCurrent);
                // AAP P3: maximum-first ordering. The old code called Set() twice
                // (current first, max second), but Set() clamps to the existing Max
                // so the +30 current never landed until the (now-raised) Max took
                // effect -- which it never did because SetMax was never called.
                PsychicBuffManager.SetWillPoints(wp, savedCurrent + 30f, savedMax + 30f);
                Debug.Log($"[AAP] Offensive WP boost: {__instance.TacticalAbilityDef.name} cast by {caster.DisplayName} (WP {savedCurrent}/{savedMax} → {wp.Value}/{wp.Max})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] OffensivePsychicApplyCosts_Patch.Prefix failed: {e.Message}");
                return true;
            }
        }

        static void Postfix(TacticalAbility __instance)
        {
            try
            {
                if (!PsychicBuffManager.offensiveSwitcheroo.TryGetValue(__instance, out var saved)) return;
                var wp = __instance.TacticalActor.CharacterStats.WillPoints;
                // AAP P3: costPaid = savedCurrent + 30 - wp.Value (the WP we
                // actually spent during ApplyCosts while the +30 buffer was live).
                // The old formula `savedCurrent - (wp.Value - 30f)` failed when
                // ApplyCosts deducted more than 30 (yielded negative costPaid
                // that was then clamped to 0, hiding the over-spend).
                float costPaid = Mathf.Max(0f, saved.savedCurrent + 30f - wp.Value);
                // AAP P3: maximum-first restore (SetMax(saved.savedMax), then Set).
                PsychicBuffManager.SetWillPoints(wp, Mathf.Max(0f, saved.savedCurrent - costPaid), saved.savedMax);
                PsychicBuffManager.offensiveSwitcheroo.Remove(__instance);
                Debug.Log($"[AAP] Offensive WP restored: cost = {costPaid}, WP now {wp.Value}/{wp.Max}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] OffensivePsychicApplyCosts_Patch.Postfix failed: {e.Message}");
            }
        }

        private static bool IsOffensivePsychic(string name) =>
            name.Contains("MindControl") || name.Contains("InducePanic") ||
            name.Contains("PsychicScream") || name.Contains("MindCrush") ||
            name.Contains("InstilFrenzy");
    }

    // ================================================================
    // DEFENSIVE PATCHES (MC / Panic target filter)
    // ================================================================

    [HarmonyPatch(typeof(ApplyEffectAbility), "TargetFilterPredicate")]
    public static class DefensiveTargetFilter_Patch
    {
        private static Dictionary<TacticalActor, (float cur, float max)> _targetWpBackup
            = new Dictionary<TacticalActor, (float cur, float max)>();

        // AAP P4: acquire-once backup. Nested predicate calls used to overwrite
        // the baseline; exceptions skipped the postfix entirely. TryBackup
        // returns true only when this invocation acquired the backup, so the
        // restore in Finalizer runs exactly once per target.
        private static bool TryBackup(
            TacticalActor target,
            Dictionary<TacticalActor, (float cur, float max)> backups)
        {
            if (target == null || backups.ContainsKey(target)) return false;
            StatusStat wp = target.CharacterStats.WillPoints;
            backups.Add(target, (wp.Value, wp.Max));
            return true;
        }

        private static void Restore(
            TacticalActor target,
            Dictionary<TacticalActor, (float cur, float max)> backups)
        {
            if (target == null || !backups.TryGetValue(target, out var saved)) return;
            StatusStat wp = target.CharacterStats.WillPoints;
            // P3: maximum-first restore inside Restore too.
            wp.SetMax(saved.max, false);
            wp.Set(saved.cur, false);
            backups.Remove(target);
        }

        static bool Prefix(ApplyEffectAbility __instance, TacticalActorBase targetActor, ref bool __result)
        {
            try
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
                        // AAP P4: inflate only when this invocation acquired the
                        // backup. The Finalizer (not the postfix) owns cleanup.
                        if (TryBackup(target, _targetWpBackup))
                        {
                            // AAP P3: 56 shell uses maximum-first ordering.
                            PsychicBuffManager.SetWillPoints(target.CharacterStats.WillPoints, 56f, 56f);
                            Debug.Log($"[AAP] Defensive WP inflated to 56 for {target.DisplayName} (shell active)");
                        }
                    }
                }
                else if (PsychicBuffManager.MindfraggerResearchCompleted &&
                         target.GameTags.Contains(PsychicBuffManager.MindfraggerBonusTag))
                {
                    // AAP P4: acquire-once backup here too.
                    if (TryBackup(target, _targetWpBackup))
                    {
                        var wp = target.CharacterStats.WillPoints;
                        // AAP P3: +50% shell uses maximum-first ordering.
                        float oldMax = wp.Max;
                        float boost = oldMax * 0.5f;
                        PsychicBuffManager.SetWillPoints(wp, wp.Value + boost, oldMax + boost);
                        Debug.Log($"[AAP] Defensive WP boost (+50%) for {target.DisplayName}: {wp.Value}/{wp.Max}");
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] DefensiveTargetFilter_Patch.Prefix failed: {e.Message}");
                return true;
            }
        }

        // AAP P4: Finalizer owns cleanup. The separate postfix restoration
        // was removed because exceptions skipped it (leaving the 56 shell
        // permanently inflated) and nested predicate calls overwrote the
        // baseline before the postfix could see the right value.
        [HarmonyFinalizer]
        public static Exception Finalizer(TacticalActorBase targetActor, Exception __exception)
        {
            Restore(targetActor as TacticalActor, _targetWpBackup);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(MindControlAbility), "Activate")]
    public static class DefensiveTargetFilter_MindControl_Patch
    {
        static bool Prefix(MindControlAbility __instance)
        {
            try
            {
                if (!PsychicBuffManager.PsychicInfluencesCompleted) return true;

                TacticalActor caster = __instance.TacticalActor;
                if (caster == null) return true;

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
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] MindControl_Patch.Prefix failed: {e.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(TacticalAbility), "GetDisabledStateInternal")]
    public static class MindControl_DisableForAI_Patch
    {
        static void Postfix(TacticalAbility __instance, ref AbilityDisabledState __result,
            IgnoredAbilityDisabledStatesFilter filter)
        {
            try
            {
                if (!PsychicBuffManager.PsychicInfluencesCompleted) return;
                if (!(__instance is MindControlAbility)) return;

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
            catch (Exception e)
            {
                Debug.LogError($"[AAP] MindControl_DisableForAI_Patch failed: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(ApplyEffectAbility), "TargetFilterPredicate")]
    public static class SonicBlastDefense_Patch
    {
        private static readonly System.Random Rng = new System.Random();

        static bool Prefix(ApplyEffectAbility __instance, TacticalActorBase targetActor, ref bool __result)
        {
            try
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
            catch (Exception e)
            {
                Debug.LogError($"[AAP] SonicBlastDefense_Patch.Prefix failed: {e.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(DamageAccumulation), "GenerateStandardDamageTargetData")]
    public static class PsychicDamage_Defense
    {
        static void Prefix(DamageAccumulation __instance, IDamageReceiver target)
        {
            try
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
            catch (Exception e)
            {
                Debug.LogError($"[AAP] PsychicDamage_Defense failed: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(StatusStat), "ApplyStatModification")]
    public static class PsychicWillpointLoss_Defense_V3
    {
        static void Prefix(StatusStat __instance, ref StatModification statMod)
        {
            try
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
            catch (Exception e)
            {
                Debug.LogError($"[AAP] PsychicWillpointLoss_Defense_V3 failed: {e.Message}");
            }
        }
    }
}
