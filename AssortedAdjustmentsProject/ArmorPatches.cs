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
    /// </summary>
    public static class ArmorAdjustments
    {
        public static void Apply(DefCache cache)
        {
            var repo = GameUtl.GameComponent<DefRepository>();

            SetStatsForAllMatching(repo, "NJ_Heavy_Torso", 45, -0.20f, 0.01f, -1f, 0f);
            SetStatsForAllMatching(repo, "NJ_Heavy_Legs", 40, -0.25f, 0.03f, -1f, 0f);
            SetStatsForAllMatching(repo, "NJ_Jugg_BIO_Helmet", 30, -0.10f, 0.03f, 0f, 0f);

            SetStatsForAllMatching(repo, "NJ_Exo_BIO_Torso", 30, -0.15f, 0.08f, 0f, 0f);
            SetStatsForAllMatching(repo, "NJ_Exo_BIO_Helmet", 20, -0.05f, 0.12f, 0f, 5f);
            SetStatsForAllMatching(repo, "NJ_Exo_BIO_Legs", 20, -0.10f, 0f, 3f, 0f);

            SetStatsForAllMatching(repo, "SY_Assault_Torso", 22, 0.20f, 0f, 0f, 0f);
            SetStatsForAllMatching(repo, "SY_Assault_Helmet", 20, 0.10f, 0f, 0f, 0f);
            SetStatsForAllMatching(repo, "SY_Assault_Legs", 20, 0.20f, 0f, 1f, 0f);

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

            var tritonLeft = repo.GetDef("TritonElite_LeftArm_BodyPartDef") as TacticalItemDef;
            var tritonRight = repo.GetDef("TritonElite_RightArm_BodyPartDef") as TacticalItemDef;
            if (tritonLeft != null) { Debug.Log($"[AAP] Triton Left Arm HandsToUse: {tritonLeft.HandsToUse} -> 0"); tritonLeft.HandsToUse = 0; }
            if (tritonRight != null) { Debug.Log($"[AAP] Triton Right Arm HandsToUse: {tritonRight.HandsToUse} -> 0"); tritonRight.HandsToUse = 0; }

            var neuralTorso = repo.GetDef("Neural_Torso_BodyPartDef");
            var mountedTag = repo.GetDef("MountedWeapon_WeaponTagDef");
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

        private static void SetArmor(DefRepository repo, string defName, int armor)
        {
            var def = repo.GetDef(defName) as TacticalItemDef;
            if (def != null)
            {
                int old = (int)def.Armor;
                def.Armor = armor;
                Debug.Log($"[AAP] {defName}: Armor {old} -> {armor}");
            }
        }
    }
}
