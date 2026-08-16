using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Tactical.Entities.DamageKeywords;
using PhoenixPoint.Tactical.Entities.Weapons;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class WeaponAdjustments
    {
        public static void Apply(DefCache cache)
        {
            // ── LOGGING HELPER ──────────────────────────────
            void Log(string msg) => Debug.Log($"[AAP] {msg}");

            // ===== Siren Injector =====
            var sirenInjector = cache.GetDef<WeaponDef>("Siren_Arms_Injector_WeaponDef");
            if (sirenInjector != null)
            {
                ModifyDamageKeyword(sirenInjector, "Viral_DamageKeywordDataDef", 3f);
                Log("Siren Injector viral damage set to 3.");
            }

            // ===== NJ Technician Mech Arms ("Tech Strike" rework) =====
            // Ported from the Modnix-era TechStrike mod: the Mech Arms get a real
            // attack payload (10 damage / 40 piercing / 18 paralysing), replacing
            // their vanilla keyword list. AAP adds 8 charges on top.
            var mechArms = cache.GetDef<WeaponDef>("NJ_Technician_MechArms_WeaponDef");
            if (mechArms != null)
            {
                var damageKeyword = cache.GetDef<DamageKeywordDef>("Damage_DamageKeywordDataDef");
                var pierceKeyword = cache.GetDef<DamageKeywordDef>("Piercing_DamageKeywordDataDef");
                var paralyseKeyword = cache.GetDef<DamageKeywordDef>("Paralysing_DamageKeywordDataDef");
                if (mechArms.DamagePayload != null && damageKeyword != null && pierceKeyword != null && paralyseKeyword != null)
                {
                    mechArms.DamagePayload.DamageKeywords = new List<DamageKeywordPair>
                    {
                        new DamageKeywordPair { DamageKeywordDef = damageKeyword, Value = 10f },
                        new DamageKeywordPair { DamageKeywordDef = pierceKeyword, Value = 40f },
                        new DamageKeywordPair { DamageKeywordDef = paralyseKeyword, Value = 18f },
                    };
                }
                mechArms.ChargesMax = 8;
                Log("Mech Arms (Tech Strike): Damage 10, Pierce 40, Paralyze 18, charges 8.");
            }

            // ===== Laser Array =====
            var laserArray = cache.GetDef<WeaponDef>("PX_LaserArrayPack_WeaponDef");
            if (laserArray != null)
            {
                ModifyDamageKeyword(laserArray, "Normal_DamageKeywordDataDef", 80f);
                laserArray.ChargesMax = 30;
                Log("Laser Array damage 80, charges 30.");
            }

            // ===== Acid Assault Rifle =====
            var acidAR = cache.GetDef<WeaponDef>("PX_AcidAssaultRifle_WeaponDef");
            if (acidAR != null)
            {
                ModifyDamageKeyword(acidAR, "Damage_DamageKeywordDataDef", 40f);
                ModifyDamageKeyword(acidAR, "Acid_DamageKeywordDataDef", 10f);
                var payload = acidAR.DamagePayload;
                if (payload != null)
                {
                    payload.StopOnFirstHit = false;
                    payload.AutoFireShotCount = 7;
                    Log($"Acid AR payload updated: Stop={payload.StopOnFirstHit}, Shots={payload.AutoFireShotCount}");
                }
                acidAR.SpreadDegrees = 1.1f;
                Log("Acid Assault Rifle patched.");
            }

            // ===== Poison Machinegun =====
            var poisonMG = cache.GetDef<WeaponDef>("PX_PoisonMachineGun_WeaponDef");
            if (poisonMG != null)
            {
                ModifyDamageKeyword(poisonMG, "Normal_DamageKeywordDataDef", 35f);
                ModifyDamageKeyword(poisonMG, "Poison_DamageKeywordDataDef", 10f);
                var payload = poisonMG.DamagePayload;
                if (payload != null)
                {
                    payload.StopOnFirstHit = false;
                    payload.AutoFireShotCount = 16;
                    Log($"Poison MG payload updated: Stop={payload.StopOnFirstHit}, Shots={payload.AutoFireShotCount}");
                }
                poisonMG.SpreadDegrees = 2.227f;
                Log("Poison Machinegun patched.");
            }

            // ===== Obliterator =====
            var obliterator = cache.GetDef<WeaponDef>("KS_Obliterator_WeaponDef");
            if (obliterator != null)
            {
                Traverse.Create(obliterator).Field("_effectiveRange").SetValue(40);
                ModifyDamageKeyword(obliterator, "Shredding_DamageKeywordDataDef", 2f);
                AddDamageKeyword(obliterator, "Paralysing_DamageKeywordDataDef", 2f, cache);
                Log("Obliterator patched: range 40, Shred 2, Paralyze 2.");
            }

            // ===== Redemptor =====
            var redemptor = cache.GetDef<WeaponDef>("KS_Redemptor_WeaponDef");
            if (redemptor != null)
            {
                var payload = redemptor.DamagePayload;
                if (payload != null) payload.AutoFireShotCount = 10;
                Log($"Redemptor: AutoFireShotCount set to {redemptor.DamagePayload?.AutoFireShotCount}");
            }

            // ===== Subjector =====
            var subjector = cache.GetDef<WeaponDef>("KS_Subjector_WeaponDef");
            if (subjector != null)
            {
                Traverse.Create(subjector).Field("_effectiveRange").SetValue(61);
                ModifyDamageKeyword(subjector, "Poison_DamageKeywordDataDef", 60f);
                Log("Subjector patched: range 61, Poison 60.");
            }

            // ===== Devastator =====
            var devastator = cache.GetDef<WeaponDef>("KS_Devastator_WeaponDef");
            if (devastator != null)
            {
                Traverse.Create(devastator).Field("_effectiveRange").SetValue(25);
                ModifyDamageKeyword(devastator, "Normal_DamageKeywordDataDef", 200f);
                ModifyDamageKeyword(devastator, "Shock_DamageKeywordDataDef", 200f);
                AddDamageKeyword(devastator, "Shredding_DamageKeywordDataDef", 20f, cache);
                Log("Devastator patched: range 25, Normal/Shock 200, Shred 20.");
            }

            // ===== Tormentor =====
            var tormentor = cache.GetDef<WeaponDef>("KS_Tormentor_WeaponDef");
            if (tormentor != null)
            {
                Traverse.Create(tormentor).Field("_effectiveRange").SetValue(29);
                ModifyDamageKeyword(tormentor, "Normal_DamageKeywordDataDef", 60f);
                ModifyDamageKeyword(tormentor, "Piercing_DamageKeywordDataDef", 20f);
                Log("Tormentor patched: range 29, Normal 60, Pierce 20.");
            }

            // ===== Venom Torso Bone Spike =====
            var boneSpike = cache.GetDef<WeaponDef>("AN_Berserker_Shooter_LeftArm_WeaponDef");
            if (boneSpike != null)
            {
                boneSpike.SpreadDegrees = 1.5f;
                boneSpike.ChargesMax = 0;
                ModifyDamageKeyword(boneSpike, "Piercing_DamageKeywordDataDef", 25f);
                ModifyDamageKeyword(boneSpike, "Poison_DamageKeywordDataDef", 50f);
                Log("Venom Torso bone spike patched.");
            }
            

            // ===== Slamstrike Shotgun =====
            var slamstrike = cache.GetDef<WeaponDef>("FS_SlamstrikeShotgun_WeaponDef");
            if (slamstrike != null)
            {
                Traverse.Create(slamstrike).Field("_effectiveRange").SetValue(22);
                ModifyDamageKeyword(slamstrike, "Damage_DamageKeywordDataDef", 160f);
                ModifyDamageKeyword(slamstrike, "Shredding_DamageKeywordDataDef", 10f);
                ModifyDamageKeyword(slamstrike, "Shock_DamageKeywordDataDef", 210f);
                var payload = slamstrike.DamagePayload;
                if (payload != null) payload.StopOnFirstHit = false;
                Log("Slamstrike Shotgun patched.");
            }

            // ===== Light Sniper Rifle =====
            var lightSniper = cache.GetDef<WeaponDef>("FS_LightSniperRifle_WeaponDef");
            if (lightSniper != null)
            {
                Traverse.Create(lightSniper).Field("_effectiveRange").SetValue(60);
                ModifyDamageKeyword(lightSniper, "Damage_DamageKeywordDataDef", 110f);
                Log("Light Sniper Rifle patched: Damage 110, Range 60.");
            }
            // ===== Tyr‑1 Autocannon (FS_Autocannon_WeaponDef) =====
            var tyr1 = cache.GetDef<WeaponDef>("FS_Autocannon_WeaponDef");
            if (tyr1 != null)
            {
                tyr1.APToUsePerc = 50;                      // 2 AP
                Traverse.Create(tyr1).Field("_effectiveRange").SetValue(25);
                ModifyDamageKeyword(tyr1, "Shredding_DamageKeywordDataDef", 10f);
                
                ModifyDamageKeyword(tyr1, "Damage_DamageKeywordDataDef", 60f);
                
                var payload = tyr1.DamagePayload;
                if (payload != null)
                {
                    payload.ObjectMultiplier = 2f;
                    payload.StopOnFirstHit = false;
                    Log($"Tyr‑1 payload: Multiplier={payload.ObjectMultiplier}, Stop={payload.StopOnFirstHit}");
                }
                Log("Tyr‑1 Autocannon patched.");
            }
            // ===== Vidar Grenade Launcher =====
            var vidar = cache.GetDef<WeaponDef>("FS_AssaultGrenadeLauncher_WeaponDef");
            if (vidar != null)
            {
                vidar.APToUsePerc = 50;                      // 2 AP for a soldier with 4 max AP
                ModifyDamageKeyword(vidar, "Shredding_DamageKeywordDataDef", 20f);
                AddDamageKeyword(vidar, "Acid_DamageKeywordDataDef", 10f, cache);
                Log("Vidar GL patched: Shred 20, Acid 10, AP 2.");
            }

            // ===== Mutoid Worm Launchers =====
            SetChargesMaxIfExists(cache, "Mutoid_Arm_AcidWorm_WeaponDef", 5);
            SetChargesMaxIfExists(cache, "Mutoid_Arm_PoisonWorm_WeaponDef", 5);

            // ===== Ammo Items =====
            SetChargesMaxIfExists(cache, "MechArms_AmmoClip_ItemDef", 8, typeof(ItemDef));
            SetChargesMaxIfExists(cache, "PX_LaserArray_AmmoClip_ItemDef", 30, typeof(ItemDef));

            // ===== DLC presence diagnostics (Kaos Engines & Festering Skies) =====
            // The FS_ prefix covers both KE operative weapons (Tyr-1 Autocannon,
            // Vidar GL, Slamstrike, Light Sniper Rifle - names verified against
            // TFTV) and is simply absent from the DefRepository when the DLC is
            // not installed. Log what is available so the Player.log always
            // shows whether the KE patches could apply.
            string[] dlcProbe =
            {
                "FS_Autocannon_WeaponDef",             // KE: Tyr-1 Autocannon
                "FS_AssaultGrenadeLauncher_WeaponDef", // KE: Vidar GL
                "FS_SlamstrikeShotgun_WeaponDef",      // KE: Slamstrike Shotgun
                "FS_LightSniperRifle_WeaponDef"        // KE: Light Sniper Rifle
            };
            int present = 0;
            foreach (string probe in dlcProbe)
            {
                bool found = cache.GetDef<WeaponDef>(probe) != null;
                if (found) present++;
                Log($"DLC probe: {probe} = {(found ? "found" : "NOT found")}");
            }
            Log($"Kaos Engines weapon defs present: {present}/{dlcProbe.Length}" +
                (present == 0 ? " - DLC not installed, KE weapon patches skipped (no error)." : "."));

            Log("WeaponAdjustments finished.");
        }

        // ── Helpers ─────────────────────────────────────────────
        private static void ModifyDamageKeyword(WeaponDef weapon, string keywordDefName, float newValue)
        {
            var payload = weapon.DamagePayload;
            if (payload == null) return;
            foreach (var kw in payload.DamageKeywords)
            {
                if (kw.DamageKeywordDef != null && kw.DamageKeywordDef.name == keywordDefName)
                {
                    var old = kw.Value;
                    kw.Value = newValue;
                    Debug.Log($"[AAP] {weapon.name} {keywordDefName}: {old} -> {newValue}");
                    return;
                }
            }
        }

        private static void AddDamageKeyword(WeaponDef weapon, string keywordDefName, float value, DefCache cache)
        {
            var payload = weapon.DamagePayload;
            if (payload == null) return;
            foreach (var kw in payload.DamageKeywords)
            {
                if (kw.DamageKeywordDef != null && kw.DamageKeywordDef.name == keywordDefName)
                    return;
            }
            var targetDef = cache.GetDef<DamageKeywordDef>(keywordDefName);
            if (targetDef == null) return;
            payload.DamageKeywords.Add(new DamageKeywordPair { DamageKeywordDef = targetDef, Value = value });
            Debug.Log($"[AAP] {weapon.name} added keyword {keywordDefName} = {value}");
        }

        private static void SetChargesMaxIfExists(DefCache cache, string defName, int charges, Type defType = null)
        {
            BaseDef def = null;
            if (defType == typeof(ItemDef))
                def = cache.GetDef<ItemDef>(defName);
            else
                def = cache.GetDef<WeaponDef>(defName);

            if (def != null)
            {
                if (def is WeaponDef wdef)
                    wdef.ChargesMax = charges;
                else if (def is ItemDef idef)
                    idef.ChargesMax = charges;
            }
        }



    }
}