using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities.Equipments;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// AAP 1.1 NOTE: Stealth/Accuracy/Speed/Perception on armour pieces live on the
    /// piece's BodyPartAspectDef (TacticalItemDef.BodyPartAspectDef), not on the
    /// item def itself - the old Traverse "_stealth"/"_accuracy" fallbacks were
    /// silent no-ops. Only Armor is a real field on the item def. Verified against
    /// the decompiled game assembly (BodyPartAspectDef.GetBaseStatModifications).
    /// AAP T2: SetArmor uses the DefCache (def-name lookup), not the bare
    /// DefRepository.GetDef(string) (GUID lookup) which silently returned null.
    /// AAP Q6-A: the NJ_Heavy_Torso StartsWith sweep is intentional; it covers
    /// all Heavy variants including the Jetpack torso (no aspect stats on
    /// Jetpack, only Armor=45 is set there).
    /// </summary>
    public static class ArmorAdjustments
    {
        public static void Apply(DefCache cache)
        {
            var repo = GameUtl.GameComponent<DefRepository>();

            // Q6-A: sweeping to all Heavy incl. Jetpack. StartsWith catches
            // _Jetpack_ variants (intentional). Aspect stats are skipped on
            // the Jetpack torso (no BodyPartAspectDef), Armor is still set.
            SetStatsForAllMatching(repo, "NJ_Heavy_Torso", 45, -0.20f, 0.01f, -1f, 0f);
            SetStatsForAllMatching(repo, "NJ_Heavy_Legs", 40, -0.25f, 0.03f, -1f, 0f);
            SetStatsForAllMatching(repo, "NJ_Jugg_BIO_Helmet", 30, -0.10f, 0.03f, 0f, 0f);

            SetStatsForAllMatching(repo, "NJ_Exo_BIO_Torso", 30, -0.15f, 0.08f, 0f, 0f);
            SetStatsForAllMatching(repo, "NJ_Exo_BIO_Helmet", 20, -0.05f, 0.12f, 0f, 5f);
            SetStatsForAllMatching(repo, "NJ_Exo_BIO_Legs", 20, -0.10f, 0f, 3f, 0f);

            SetStatsForAllMatching(repo, "SY_Assault_Torso", 22, 0.20f, 0f, 0f, 0f);
            SetStatsForAllMatching(repo, "SY_Assault_Helmet", 20, 0.10f, 0f, 0f, 0f);
            SetStatsForAllMatching(repo, "SY_Assault_Legs", 20, 0.20f, 0f, 1f, 0f);

            // T2: SetArmor now uses DefCache (name-keyed), not DefRepository.GetDef(string) (GUID).
            SetArmor(cache, "NJ_Heavy_LeftArm_BodyPartDef", 45);
            SetArmor(cache, "NJ_Heavy_RightArm_BodyPartDef", 45);
            SetArmor(cache, "NJ_Exo_BIO_LeftArm_BodyPartDef", 30);
            SetArmor(cache, "NJ_Exo_BIO_RightArm_BodyPartDef", 30);
            SetArmor(cache, "SY_Assault_LeftArm_BodyPartDef", 22);
            SetArmor(cache, "SY_Assault_RightArm_BodyPartDef", 22);
            SetArmor(cache, "SY_Assault_LeftArm_Neon_BodyPartDef", 22);
            SetArmor(cache, "SY_Assault_RightArm_Neon_BodyPartDef", 22);
            SetArmor(cache, "SY_Assault_LeftArm_WhiteNeon_BodyPartDef", 22);
            SetArmor(cache, "SY_Assault_RightArm_WhiteNeon_BodyPartDef", 22);

            // FIXME T2: def TritonElite_LeftArm_BodyPartDef not found in current build
            // (per audit T2 caveat). Cache lookup returns null gracefully; verify in-game.
            var tritonLeft = cache.GetDef<TacticalItemDef>("TritonElite_LeftArm_BodyPartDef");
            // FIXME T2: def TritonElite_RightArm_BodyPartDef not found in current build.
            var tritonRight = cache.GetDef<TacticalItemDef>("TritonElite_RightArm_BodyPartDef");
            if (tritonLeft != null) { Debug.Log($"[AAP] Triton Left Arm HandsToUse: {tritonLeft.HandsToUse} -> 0"); tritonLeft.HandsToUse = 0; }
            if (tritonRight != null) { Debug.Log($"[AAP] Triton Right Arm HandsToUse: {tritonRight.HandsToUse} -> 0"); tritonRight.HandsToUse = 0; }

            // FIXME T2: def Neural_Torso_BodyPartDef not found in current build.
            // Also: T2 corrects MountedWeapon tag name (was MountedWeapon_WeaponTagDef,
            // now MountedWeapon_TagDef -- audit T2 spec line 74).
            var neuralTorso = cache.GetDef<BaseDef>("Neural_Torso_BodyPartDef");
            var mountedTag = cache.GetDef<BaseDef>("MountedWeapon_TagDef");
            if (neuralTorso != null && mountedTag != null)
                Helpers.AddDefToArrayField(neuralTorso, "WeaponProficiencies", mountedTag);

            Debug.Log("[AAP] ArmorAdjustments applied (armor via item def, stats via BodyPartAspectDef).");
        }

        private static void SetStatsForAllMatching(DefRepository repo, string nameStartsWith,
            int armor, float stealth, float accuracy, float speed, float perception)
        {
            foreach (var def in repo.GetAllDefs<TacticalItemDef>()
                         .Where(d => d.name.StartsWith(nameStartsWith)))
            {
                int oldArmor = (int)def.Armor;
                def.Armor = armor;

                var aspect = def.BodyPartAspectDef;
                if (aspect != null)
                {
                    Debug.Log($"[AAP] {def.name}: Armor {oldArmor}->{armor}, " +
                              $"Stealth {aspect.Stealth}->{stealth}, Acc {aspect.Accuracy}->{accuracy}, " +
                              $"Spd {aspect.Speed}->{speed}, Perc {aspect.Perception}->{perception} (aspect: {aspect.name}).");
                    aspect.Stealth = stealth;
                    aspect.Accuracy = accuracy;
                    aspect.Speed = speed;
                    aspect.Perception = perception;
                }
                else
                {
                    Debug.Log($"[AAP] {def.name}: Armor {oldArmor}->{armor} (no BodyPartAspectDef - stat mods skipped).");
                }
            }
        }

        // T2: cache-keyed lookup so the def name resolves. DefRepository.GetDef(string)
        // expects a GUID; passing a def name silently returned null for every arm.
        private static void SetArmor(DefCache cache, string defName, int armor)
        {
            TacticalItemDef def = cache.GetDef<TacticalItemDef>(defName);
            if (def == null) { Debug.LogWarning($"[AAP] Armor def not found: {defName}"); return; }
            int old = (int)def.Armor;
            def.Armor = armor;
            Debug.Log($"[AAP] {defName}: Armor {old} -> {armor}");
        }
    }
}
