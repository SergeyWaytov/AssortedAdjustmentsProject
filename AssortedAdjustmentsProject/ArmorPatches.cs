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

            Debug.Log("[AAP] ArmorAdjustments applied (all skins via Traverse).");
        }

        private static void SetStatsForAllMatching(DefRepository repo, string nameStartsWith,
            int armor, float stealth, float accuracy, float speed, float perception)
        {
            foreach (var def in repo.GetAllDefs<TacticalItemDef>()
                         .Where(d => d.name.StartsWith(nameStartsWith)))
            {
                float oldStealth = GetFloatValue(def, "Stealth");
                float oldAcc = GetFloatValue(def, "Accuracy");
                float oldSpeed = GetFloatValue(def, "Speed");
                float oldPerc = GetFloatValue(def, "Perception");
                int oldArmor = (int)def.Armor;

                def.Armor = armor;
                SetStatValue(def, "Stealth", stealth);
                SetStatValue(def, "Accuracy", accuracy);
                SetStatValue(def, "Speed", speed);
                SetStatValue(def, "Perception", perception);

                float newStealth = GetFloatValue(def, "Stealth");
                float newAcc = GetFloatValue(def, "Accuracy");
                float newSpeed = GetFloatValue(def, "Speed");
                float newPerc = GetFloatValue(def, "Perception");
                Debug.Log($"[AAP] {def.name}: Armor {oldArmor}->{armor}, Stealth {oldStealth}->{newStealth}, Acc {oldAcc}->{newAcc}, Spd {oldSpeed}->{newSpeed}, Perc {oldPerc}->{newPerc}");
            }
        }

        private static float GetFloatValue(object target, string propName)
        {
            var t = Traverse.Create(target);
            string fieldName = "_" + char.ToLower(propName[0]) + propName.Substring(1);
            var field = t.Field(fieldName);
            if (field != null && field.FieldExists()) return field.GetValue<float>();
            var prop = t.Property(propName);
            return (prop != null && prop.PropertyExists()) ? prop.GetValue<float>() : 0f;
        }

        private static void SetStatValue(object target, string propName, float value)
        {
            var t = Traverse.Create(target);
            string fieldName = "_" + char.ToLower(propName[0]) + propName.Substring(1);
            var field = t.Field(fieldName);
            if (field != null && field.FieldExists())
            {
                field.SetValue(value);
                return;
            }
            var prop = t.Property(propName);
            if (prop != null && prop.PropertyExists())
                prop.SetValue(value);
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