using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities.Equipments;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class ArmorAdjustments
    {
        public static void Apply(DefCache cache)
        {
            var repo = GameUtl.GameComponent<DefRepository>();

            // NJ Juggernaut (heavy)
            SetStatsForAllMatching(repo, "NJ_Heavy_Torso", 45, -0.20f, 0.01f, -1f, 0f);
            SetStatsForAllMatching(repo, "NJ_Heavy_Legs", 40, -0.25f, 0.03f, -1f, 0f);
            SetStatsForAllMatching(repo, "NJ_Jugg_BIO_Helmet", 30, -0.10f, 0.03f, 0f, 0f);

            // NJ Exoskeleton
            SetStatsForAllMatching(repo, "NJ_Exo_BIO_Torso", 30, -0.15f, 0.08f, 0f, 0f);
            SetStatsForAllMatching(repo, "NJ_Exo_BIO_Helmet", 20, -0.05f, 0.12f, 0f, 5f);
            SetStatsForAllMatching(repo, "NJ_Exo_BIO_Legs", 20, -0.10f, 0f, 3f, 0f);

            // Synedrion Assault – all skins
            SetStatsForAllMatching(repo, "SY_Assault_Torso", 22, 0.20f, 0f, 0f, 0f);
            SetStatsForAllMatching(repo, "SY_Assault_Helmet", 20, 0.10f, 0f, 0f, 0f);
            SetStatsForAllMatching(repo, "SY_Assault_Legs", 20, 0.20f, 0f, 1f, 0f);

            // Arms
            SetArmor(repo, "NJ_Heavy_LeftArm_BodyPartDef", 45);
            SetArmor(repo, "NJ_Heavy_RightArm_BodyPartDef", 45);
            SetArmor(repo, "NJ_Exo_BIO_LeftArm_BodyPartDef", 30);
            SetArmor(repo, "NJ_Exo_BIO_RightArm_BodyPartDef", 30);
            SetArmor(repo, "SY_Assault_LeftArm_BodyPartDef", 22);
            SetArmor(repo, "SY_Assault_RightArm_BodyPartDef", 22);
            SetArmor(repo, "SY_Assault_LeftArm_Neon_BodyPartDef", 22);
            SetArmor(repo, "SY_Assault_RightArm_Neon_BodyPartDef", 22);
            SetArmor(repo, "SY_Assault_LeftArm_WhiteNeon_BodyPartDef", 22);
            SetArmor(repo, "SY_Assault_RightArm_WhiteNeon_BodyPartDef", 22);

            // Triton Elite Arms fix
            var tritonLeft = repo.GetDef("TritonElite_LeftArm_BodyPartDef") as TacticalItemDef;
            var tritonRight = repo.GetDef("TritonElite_RightArm_BodyPartDef") as TacticalItemDef;
            if (tritonLeft != null) tritonLeft.HandsToUse = 0;
            if (tritonRight != null) tritonRight.HandsToUse = 0;

            // Neural Torso mounted weapon proficiency
            var neuralTorso = repo.GetDef("Neural_Torso_BodyPartDef");
            var mountedTag = repo.GetDef("MountedWeapon_WeaponTagDef");
            if (neuralTorso != null && mountedTag != null)
                Helpers.AddDefToArrayField(neuralTorso, "WeaponProficiencies", mountedTag);

            Debug.Log("[AAP] ArmorAdjustments applied (all skins included).");
        }

        private static void SetStatsForAllMatching(DefRepository repo, string nameStartsWith,
            int armor, float stealth, float accuracy, float speed, float perception)
        {
            foreach (var def in repo.GetAllDefs<TacticalItemDef>()
                         .Where(d => d.name.StartsWith(nameStartsWith)))
            {
                var t = Traverse.Create(def);
                t.Property("Armor")?.SetValue(armor);
                t.Property("Stealth")?.SetValue(stealth);
                t.Property("Accuracy")?.SetValue(accuracy);
                t.Property("Speed")?.SetValue(speed);
                t.Property("Perception")?.SetValue(perception);
            }
        }

        private static void SetArmor(DefRepository repo, string defName, int armor)
        {
            var def = repo.GetDef(defName) as TacticalItemDef;
            if (def != null) def.Armor = armor;
        }
    }
}