using Base.Core;
using Base.Defs;
using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class ArmorAdjustments
    {
        public static void Apply(DefCache cache)
        {
            // ===== NJ Juggernaut Torso =====
            var juggTorso = cache.GetDef<BaseDef>("NJ_Heavy_Torso_BodyPartDef");
            if (juggTorso != null)
            {
                var t = Traverse.Create(juggTorso);
                t.Property("Stealth")?.SetValue(-0.2f);
                t.Property("Accuracy")?.SetValue(0.01f);
                t.Property("Speed")?.SetValue(-1f);
                t.Property("Perception")?.SetValue(0f);
                t.Property("Armor")?.SetValue(45f);
                Debug.Log("[AAP] NJ Juggernaut Torso patched.");
            }

            // ===== NJ Juggernaut Helmet =====
            var juggHelmet = cache.GetDef<BaseDef>("NJ_Jugg_BIO_Helmet_BodyPartDef");
            if (juggHelmet != null)
            {
                var t = Traverse.Create(juggHelmet);
                t.Property("Stealth")?.SetValue(-0.1f);
                t.Property("Accuracy")?.SetValue(0.03f);
                t.Property("Speed")?.SetValue(0f);
                t.Property("Perception")?.SetValue(0f);
                t.Property("Armor")?.SetValue(30f);
                Debug.Log("[AAP] NJ Juggernaut Helmet patched.");
            }

            // ===== NJ Juggernaut Legs =====
            var juggLegs = cache.GetDef<BaseDef>("NJ_Heavy_Legs_ItemDef");
            if (juggLegs != null)
            {
                var t = Traverse.Create(juggLegs);
                t.Property("Stealth")?.SetValue(-0.25f);
                t.Property("Accuracy")?.SetValue(0.03f);
                t.Property("Speed")?.SetValue(-1f);
                t.Property("Perception")?.SetValue(0f);
                t.Property("Armor")?.SetValue(40f);
                Debug.Log("[AAP] NJ Juggernaut Legs patched.");
            }

            // ===== NJ Juggernaut Arms =====
            var juggArmLeft = cache.GetDef<BaseDef>("NJ_Heavy_LeftArm_BodyPartDef");
            if (juggArmLeft != null) Traverse.Create(juggArmLeft).Property("Armor")?.SetValue(45f);
            var juggArmRight = cache.GetDef<BaseDef>("NJ_Heavy_RightArm_BodyPartDef");
            if (juggArmRight != null) Traverse.Create(juggArmRight).Property("Armor")?.SetValue(45f);

            // ===== NJ Exoskeleton Torso =====
            var exoTorso = cache.GetDef<BaseDef>("NJ_Exo_BIO_Torso_BodyPartDef");
            if (exoTorso != null)
            {
                var t = Traverse.Create(exoTorso);
                t.Property("Stealth")?.SetValue(-0.15f);
                t.Property("Accuracy")?.SetValue(0.08f);
                t.Property("Speed")?.SetValue(0f);
                t.Property("Perception")?.SetValue(0f);
                t.Property("Armor")?.SetValue(30f);
                Debug.Log("[AAP] NJ Exoskeleton Torso patched.");
            }

            // ===== NJ Exoskeleton Helmet =====
            var exoHelmet = cache.GetDef<BaseDef>("NJ_Exo_BIO_Helmet_BodyPartDef");
            if (exoHelmet != null)
            {
                var t = Traverse.Create(exoHelmet);
                t.Property("Stealth")?.SetValue(-0.05f);
                t.Property("Accuracy")?.SetValue(0.12f);
                t.Property("Speed")?.SetValue(0f);
                t.Property("Perception")?.SetValue(5f);
                t.Property("Armor")?.SetValue(20f);
                Debug.Log("[AAP] NJ Exoskeleton Helmet patched.");
            }

            // ===== NJ Exoskeleton Legs =====
            var exoLegs = cache.GetDef<BaseDef>("NJ_Exo_BIO_Legs_ItemDef");
            if (exoLegs != null)
            {
                var t = Traverse.Create(exoLegs);
                t.Property("Stealth")?.SetValue(-0.1f);
                t.Property("Accuracy")?.SetValue(0f);
                t.Property("Speed")?.SetValue(3f);
                t.Property("Perception")?.SetValue(0f);
                t.Property("Armor")?.SetValue(20f);
                Debug.Log("[AAP] NJ Exoskeleton Legs patched.");
            }

            // ===== NJ Exoskeleton Arms =====
            var exoArmLeft = cache.GetDef<BaseDef>("NJ_Exo_BIO_LeftArm_BodyPartDef");
            if (exoArmLeft != null) Traverse.Create(exoArmLeft).Property("Armor")?.SetValue(30f);
            var exoArmRight = cache.GetDef<BaseDef>("NJ_Exo_BIO_RightArm_BodyPartDef");
            if (exoArmRight != null) Traverse.Create(exoArmRight).Property("Armor")?.SetValue(30f);

            // ===== Synedrion Assault Torso =====
            var synTorso = cache.GetDef<BaseDef>("SY_Assault_Torso_BodyPartDef");
            if (synTorso != null)
            {
                var t = Traverse.Create(synTorso);
                t.Property("Stealth")?.SetValue(0.2f);
                t.Property("Accuracy")?.SetValue(0f);
                t.Property("Speed")?.SetValue(0f);
                t.Property("Perception")?.SetValue(0f);
                t.Property("Armor")?.SetValue(22f);
                Debug.Log("[AAP] SYN Torso patched.");
            }

            // ===== Synedrion Assault Helmet =====
            var synHelmet = cache.GetDef<BaseDef>("SY_Assault_Helmet_BodyPartDef");
            if (synHelmet != null)
            {
                var t = Traverse.Create(synHelmet);
                t.Property("Stealth")?.SetValue(0.1f);
                t.Property("Accuracy")?.SetValue(0f);
                t.Property("Speed")?.SetValue(0f);
                t.Property("Perception")?.SetValue(0f);
                t.Property("Armor")?.SetValue(20f);
                Debug.Log("[AAP] SYN Helmet patched.");
            }

            // ===== Synedrion Assault Legs =====
            var synLegs = cache.GetDef<BaseDef>("SY_Assault_Legs_ItemDef");
            if (synLegs != null)
            {
                var t = Traverse.Create(synLegs);
                t.Property("Stealth")?.SetValue(0.2f);
                t.Property("Accuracy")?.SetValue(0f);
                t.Property("Speed")?.SetValue(1f);
                t.Property("Perception")?.SetValue(0f);
                t.Property("Armor")?.SetValue(20f);
                Debug.Log("[AAP] SYN Legs patched.");
            }

            // ===== Synedrion Assault Arms =====
            var synArmLeft = cache.GetDef<BaseDef>("SY_Assault_LeftArm_BodyPartDef");
            if (synArmLeft != null) Traverse.Create(synArmLeft).Property("Armor")?.SetValue(22f);
            var synArmRight = cache.GetDef<BaseDef>("SY_Assault_RightArm_BodyPartDef");
            if (synArmRight != null) Traverse.Create(synArmRight).Property("Armor")?.SetValue(22f);

            // ===== Triton Elite Arms Fix =====
            var tritonLeft = cache.GetDef<BaseDef>("TritonElite_LeftArm_BodyPartDef");
            if (tritonLeft != null)
            {
                Traverse.Create(tritonLeft).Property("HandsToUse")?.SetValue(0);
                Debug.Log("[AAP] Triton Elite Left Arm fixed: does not occupy hands.");
            }
            var tritonRight = cache.GetDef<BaseDef>("TritonElite_RightArm_BodyPartDef");
            if (tritonRight != null)
            {
                Traverse.Create(tritonRight).Property("HandsToUse")?.SetValue(0);
                Debug.Log("[AAP] Triton Elite Right Arm fixed: does not occupy hands.");
            }

            // ===== Neural Torso Mounted Weapon Proficiency =====
            var neuralTorso = cache.GetDef<BaseDef>("Neural_Torso_BodyPartDef");
            if (neuralTorso != null)
            {
                var mountedTag = cache.GetDef<BaseDef>("MountedWeapon_WeaponTagDef");
                if (mountedTag != null)
                {
                    Helpers.AddDefToArrayField(neuralTorso, "WeaponProficiencies", mountedTag);
                    Debug.Log("[AAP] Neural Torso now grants Mounted Weapon proficiency.");
                }
            }
        }
    }
}