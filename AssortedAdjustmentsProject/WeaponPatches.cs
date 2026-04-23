using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Tactical.Entities.DamageKeywords;
using PhoenixPoint.Tactical.Entities.Weapons;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class WeaponAdjustments
    {
        public static void Apply(DefCache cache)
        {
            // ===== Siren Injector =====
            var sirenInjector = cache.GetDef<WeaponDef>("Siren_Arms_Injector_WeaponDef");
            if (sirenInjector != null)
            {
                ModifyDamageKeyword(sirenInjector, "Viral_DamageKeywordDataDef", 3f);
                Debug.Log("[AAP] Siren Injector viral damage set to 3.");
            }

            // ===== NJ Technician Mech Arms =====
            var mechArms = cache.GetDef<WeaponDef>("NJ_Technician_MechArms_WeaponDef");
            if (mechArms != null)
            {
                var t = Traverse.Create(mechArms);
                t.Property("ChargesMax").SetValue(8);
                AddDamageKeyword(mechArms, "Paralysing_DamageKeywordDataDef", 18f, cache);
                Debug.Log("[AAP] Mech Arms: charges=8, Paralyze 18 added.");
            }

            // ===== Laser Array =====
            var laserArray = cache.GetDef<WeaponDef>("PX_LaserArrayPack_WeaponDef");
            if (laserArray != null)
            {
                ModifyDamageKeyword(laserArray, "Normal_DamageKeywordDataDef", 80f);
                Traverse.Create(laserArray).Property("ChargesMax").SetValue(30);
                Debug.Log("[AAP] Laser Array damage 80, charges 30.");
            }

            // ===== Acid Assault Rifle =====
            var acidAR = cache.GetDef<WeaponDef>("PX_AcidAssaultRifle_WeaponDef");
            if (acidAR != null)
            {
                ModifyDamageKeyword(acidAR, "Normal_DamageKeywordDataDef", 40f);
                ModifyDamageKeyword(acidAR, "Acid_DamageKeywordDataDef", 10f);
                var t = Traverse.Create(acidAR);
                var payload = t.Field("DamagePayload").GetValue<object>();
                if (payload != null)
                {
                    Traverse.Create(payload).Field("StopOnFirstHit").SetValue(false);
                    Traverse.Create(payload).Field("AutoFireShotCount").SetValue(7);
                }
                t.Field("SpreadDegrees").SetValue(1.1f);
                Debug.Log("[AAP] Acid Assault Rifle patched.");
            }

            // ===== Poison Machinegun =====
            var poisonMG = cache.GetDef<WeaponDef>("PX_PoisonMachineGun_WeaponDef");
            if (poisonMG != null)
            {
                ModifyDamageKeyword(poisonMG, "Normal_DamageKeywordDataDef", 35f);
                ModifyDamageKeyword(poisonMG, "Poison_DamageKeywordDataDef", 10f);
                var t = Traverse.Create(poisonMG);
                var payload = t.Field("DamagePayload").GetValue<object>();
                if (payload != null)
                {
                    Traverse.Create(payload).Field("StopOnFirstHit").SetValue(false);
                    Traverse.Create(payload).Field("AutoFireShotCount").SetValue(16);
                }
                t.Field("SpreadDegrees").SetValue(2.227f);
                Debug.Log("[AAP] Poison Machinegun patched.");
            }

            // ===== Obliterator =====
            var obliterator = cache.GetDef<WeaponDef>("KS_Obliterator_WeaponDef");
            if (obliterator != null)
            {
                var t = Traverse.Create(obliterator);
                t.Field("EffectiveRange").SetValue(40f);
                ModifyDamageKeyword(obliterator, "Shredding_DamageKeywordDataDef", 2f);
                AddDamageKeyword(obliterator, "Paralysing_DamageKeywordDataDef", 2f, cache);
                Debug.Log("[AAP] Obliterator patched: range 40, Shred 2, Paralyze 2.");
            }

            // ===== Redemptor =====
            var redemptor = cache.GetDef<WeaponDef>("KS_Redemptor_WeaponDef");
            if (redemptor != null)
            {
                var payload = Traverse.Create(redemptor).Field("DamagePayload").GetValue<object>();
                if (payload != null) Traverse.Create(payload).Field("AutoFireShotCount").SetValue(10);
                Debug.Log("[AAP] Redemptor patched.");
            }

            // ===== Subjector =====
            var subjector = cache.GetDef<WeaponDef>("KS_Subjector_WeaponDef");
            if (subjector != null)
            {
                var t = Traverse.Create(subjector);
                t.Field("EffectiveRange").SetValue(61f);
                ModifyDamageKeyword(subjector, "Poison_DamageKeywordDataDef", 60f);
                Debug.Log("[AAP] Subjector patched: range 61, Poison 60.");
            }

            // ===== Devastator =====
            var devastator = cache.GetDef<WeaponDef>("KS_Devastator_WeaponDef");
            if (devastator != null)
            {
                var t = Traverse.Create(devastator);
                t.Field("EffectiveRange").SetValue(25f);
                ModifyDamageKeyword(devastator, "Normal_DamageKeywordDataDef", 200f);
                ModifyDamageKeyword(devastator, "Shock_DamageKeywordDataDef", 200f);
                AddDamageKeyword(devastator, "Shredding_DamageKeywordDataDef", 20f, cache);
                Debug.Log("[AAP] Devastator patched: range 25, Normal/Shock 200, Shred 20.");
            }

            // ===== Tormentor =====
            var tormentor = cache.GetDef<WeaponDef>("KS_Tormentor_WeaponDef");
            if (tormentor != null)
            {
                var t = Traverse.Create(tormentor);
                t.Field("EffectiveRange").SetValue(29f);
                ModifyDamageKeyword(tormentor, "Normal_DamageKeywordDataDef", 60f);
                ModifyDamageKeyword(tormentor, "Piercing_DamageKeywordDataDef", 20f);
                Debug.Log("[AAP] Tormentor patched: range 29, Normal 60, Pierce 20.");
            }

            // ===== Venom Torso Bone Spike =====
            var boneSpike = cache.GetDef<WeaponDef>("AN_Berserker_Shooter_LeftArm_WeaponDef");
            if (boneSpike != null)
            {
                var t = Traverse.Create(boneSpike);
                t.Field("SpreadDegrees").SetValue(1.5f);
                t.Property("ChargesMax").SetValue(0);
                ModifyDamageKeyword(boneSpike, "Piercing_DamageKeywordDataDef", 25f);
                ModifyDamageKeyword(boneSpike, "Poison_DamageKeywordDataDef", 50f);
                Debug.Log("[AAP] Venom Torso bone spike patched.");
            }

            // ===== Tyr-1 Autocannon =====
            var tyr1 = cache.GetDef<WeaponDef>("Tyr1_Autocannon_WeaponDef");
            if (tyr1 != null)
            {
                var t = Traverse.Create(tyr1);
                t.Property("ActionPointCost")?.SetValue(2f);
                t.Field("EffectiveRange")?.SetValue(25f);
                ModifyDamageKeyword(tyr1, "Shredding_DamageKeywordDataDef", 10f);
                var payload = t.Field("DamagePayload").GetValue<object>();
                if (payload != null)
                {
                    Traverse.Create(payload).Field("ObjectMultiplier")?.SetValue(2f);
                    Traverse.Create(payload).Field("StopOnFirstHit")?.SetValue(false);
                }
                Debug.Log("[AAP] Tyr-1 Autocannon patched: Shred 10, AP 2, Range 25, ObjectDamage +100%, Penetration.");
            }

            // ===== Slamstrike Shotgun =====
            var slamstrike = cache.GetDef<WeaponDef>("FS_SlamstrikeShotgun_WeaponDef");
            if (slamstrike != null)
            {
                var t = Traverse.Create(slamstrike);
                t.Field("EffectiveRange")?.SetValue(22f);
                ModifyDamageKeyword(slamstrike, "Normal_DamageKeywordDataDef", 160f);
                ModifyDamageKeyword(slamstrike, "Shredding_DamageKeywordDataDef", 10f);
                ModifyDamageKeyword(slamstrike, "Shock_DamageKeywordDataDef", 210f);
                var payload = t.Field("DamagePayload").GetValue<object>();
                if (payload != null)
                    Traverse.Create(payload).Field("StopOnFirstHit")?.SetValue(false);
                Debug.Log("[AAP] Slamstrike Shotgun patched: Damage 160, Shred 10, Shock 210, Range 22, Penetration.");
            }

            // ===== Light Sniper Rifle =====
            var lightSniper = cache.GetDef<WeaponDef>("FS_LightSniperRifle_WeaponDef");
            if (lightSniper != null)
            {
                var t = Traverse.Create(lightSniper);
                t.Field("EffectiveRange")?.SetValue(60f);
                ModifyDamageKeyword(lightSniper, "Normal_DamageKeywordDataDef", 110f);
                Debug.Log("[AAP] Light Sniper Rifle patched: Damage 110, Range 60.");
            }

            // ===== Vidar GL =====
            var vidar = cache.GetDef<WeaponDef>("NJ_VidarGL_WeaponDef");
            if (vidar != null)
            {
                var t = Traverse.Create(vidar);
                t.Property("ActionPointCost")?.SetValue(2f);
                ModifyDamageKeyword(vidar, "Shredding_DamageKeywordDataDef", 20f);
                AddDamageKeyword(vidar, "Acid_DamageKeywordDataDef", 10f, cache);
                Debug.Log("[AAP] Vidar GL patched: Shred 20, Acid 10, AP 2.");
            }

            // ===== Mutoid Worm Launchers (Charge Limit) =====
            var acidWorm = cache.GetDef<WeaponDef>("Mutoid_Arm_AcidWorm_WeaponDef");
            if (acidWorm != null)
            {
                Traverse.Create(acidWorm).Property("ChargesMax")?.SetValue(5);
                Debug.Log("[AAP] Mutoid Acid Worm charges limited to 5.");
            }
            var poisonWorm = cache.GetDef<WeaponDef>("Mutoid_Arm_PoisonWorm_WeaponDef");
            if (poisonWorm != null)
            {
                Traverse.Create(poisonWorm).Property("ChargesMax")?.SetValue(5);
                Debug.Log("[AAP] Mutoid Poison Worm charges limited to 5.");
            }

            // ===== Ammo Items =====
            var mechArmsAmmo = cache.GetDef<ItemDef>("MechArms_AmmoClip_ItemDef");
            if (mechArmsAmmo != null)
            {
                Traverse.Create(mechArmsAmmo).Property("ChargesMax").SetValue(8);
                Debug.Log("[AAP] Mech Arms Ammo charges set to 8.");
            }

            var laserArrayAmmo = cache.GetDef<ItemDef>("PX_LaserArray_AmmoClip_ItemDef");
            if (laserArrayAmmo != null)
            {
                Traverse.Create(laserArrayAmmo).Property("ChargesMax").SetValue(30);
                Debug.Log("[AAP] Laser Array Ammo charges set to 30.");
            }
        }

        private static void ModifyDamageKeyword(WeaponDef weapon, string keywordDefName, float newValue)
        {
            var payload = weapon.DamagePayload;
            if (payload == null) return;
            foreach (var kw in payload.DamageKeywords)
            {
                if (kw.DamageKeywordDef != null && kw.DamageKeywordDef.name == keywordDefName)
                {
                    kw.Value = newValue;
                    return;
                }
            }
        }

        private static void AddDamageKeyword(WeaponDef weapon, string keywordDefName, float value, DefCache cache)
        {
            var payload = weapon.DamagePayload;
            if (payload == null) return;

            // Check if already exists
            foreach (var kw in payload.DamageKeywords)
            {
                if (kw.DamageKeywordDef != null && kw.DamageKeywordDef.name == keywordDefName)
                    return;
            }

            var targetDef = cache.GetDef<DamageKeywordDef>(keywordDefName);
            if (targetDef == null) return;

            var newPair = new DamageKeywordPair
            {
                DamageKeywordDef = targetDef,
                Value = value
            };

            // payload.DamageKeywords is a List<DamageKeywordPair>, so we can just Add
            payload.DamageKeywords.Add(newPair);
        }
    }
}