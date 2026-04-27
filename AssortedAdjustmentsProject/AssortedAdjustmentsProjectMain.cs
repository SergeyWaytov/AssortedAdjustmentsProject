using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Modding;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public class ModMain : PhoenixPoint.Modding.ModMain
    {
        private Harmony harmony;

        // Static reference so Harmony patches can reach the cache
        public static DefCache DefCache { get; private set; }

        // Fixed campaign template for Jacob – used by the runtime tutorial fix
        public static TacCharacterDef JacobsFixedTemplate { get; private set; }
        //public static bool DiagnosticsEnabled = false;   // turn on only when needed
        public override void OnModEnabled()
        {
            Debug.Log("=========================================");
            Debug.Log("[AAP] FULL MOD: OnModEnabled called.");
            Debug.Log("=========================================");

            DefCache = new DefCache();


            //DefNameScanner.Run();
           
            AbilityAdjustments.Apply(DefCache);
            //SniperPrecisionShotAbility.Apply(DefCache);
            PrecisionShot.Apply(DefCache);
            PsychicBuffManager.Init();
            WeaponAdjustments.Apply(DefCache);
            ArmorAdjustments.Apply(DefCache);
            GeoscapeFacilitiesAdjustments.Apply(DefCache);
            VehicleAdjustments.Apply(DefCache);
            
            LootMechanics.Apply(DefCache);
            RepairCosts.Apply(DefCache);

            // Update soldier templates and create the fixed Jacob reference
            FixSoldierTemplates();

            harmony = new Harmony("SergeyWaytov_AssortedAdjustmentsProject");
            harmony.PatchAll();   // Applies all patches, including TutorialJacobFixPatch
            I2.Loc.LocalizationManager.LocalizeAll(true);
            Debug.Log("[AAP] Mod initialization complete.");
        }

        public override void OnModDisabled()
        {
            harmony?.UnpatchAll("SergeyWaytov_AssortedAdjustmentsProject");
            Debug.Log("[AAP] Mod disabled.");
        }

        private void FixSoldierTemplates()
        {
            try
            {
                var repo = GameUtl.GameComponent<DefRepository>();

                string jacobGuid = "2f7a41a8-d68a-3374-1a13-16f18425d7bb";
                string irinaGuid = "e3c06e40-0543-fa04-5a9d-7ff43410b1e0";

                var jacobTemplate = repo.GetDef(jacobGuid) as TacCharacterDef;
                var irinaTemplate = repo.GetDef(irinaGuid) as TacCharacterDef;

                // Fix Jacob’s campaign template: class tag, gear, and Sniper base abilities
                if (jacobTemplate != null && irinaTemplate != null)
                {
                    jacobTemplate.Data.GameTags[0] = irinaTemplate.Data.GameTags[0];
                    jacobTemplate.Data.BodypartItems = irinaTemplate.Data.BodypartItems;
                    jacobTemplate.Data.EquipmentItems = irinaTemplate.Data.EquipmentItems;
                    jacobTemplate.Data.Abilites = irinaTemplate.Data.Abilites
                        .Select(a => a as TacticalAbilityDef)
                        .Where(a => a != null)
                        .ToArray();

                    // Store the fixed template for reference
                    JacobsFixedTemplate = jacobTemplate;
                }
                else
                {
                    Debug.LogWarning("[AAP] Jacob or Irina template not found – skipping class conversion.");
                }

                // Add personal abilities to all story soldiers (appends to existing abilities)
                AddPersonalAbilitiesToTemplate(repo, "Jacob", "Trooper_AbilityDef", "Cautious_AbilityDef", "Thief_AbilityDef");
                AddPersonalAbilitiesToTemplate(repo, "Sophia", "SelfDefenseSpecialist_AbilityDef", "Quarterback_AbilityDef", "Thief_AbilityDef");
                AddPersonalAbilitiesToTemplate(repo, "Omar", "CloseQuartersSpecialist_AbilityDef", "Reckless_AbilityDef", "Biochemist_AbilityDef");
                AddPersonalAbilitiesToTemplate(repo, "Takeshi", "CloseQuartersSpecialist_AbilityDef", "Reckless_AbilityDef", "Strongman_AbilityDef");
                AddPersonalAbilitiesToTemplate(repo, "Irina", "Farsighted_AbilityDef", "Bombardier_AbilityDef", "Healer_AbilityDef");

                Debug.Log("[AAP] Soldier templates updated. Jacob is now a Sniper.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Template fix failed: {e.Message}");
            }
        }

        private void AddPersonalAbilitiesToTemplate(DefRepository repo, string soldierName, string ability1, string ability2, string ability3)
        {
            var template = repo.GetDef(GetGuidForSoldier(soldierName)) as TacCharacterDef;
            if (template == null)
            {
                Debug.LogWarning($"[AAP] Template not found for {soldierName}.");
                return;
            }

            var abilitiesList = new List<TacticalAbilityDef>();
            foreach (var abilityName in new[] { ability1, ability2, ability3 })
            {
                var ability = DefCache.GetDef<TacticalAbilityDef>(abilityName);
                if (ability != null) abilitiesList.Add(ability);
            }

            if (abilitiesList.Count > 0)
            {
                // Append to existing abilities instead of overwriting
                var current = template.Data.Abilites?.ToList() ?? new List<TacticalAbilityDef>();
                current.AddRange(abilitiesList);
                template.Data.Abilites = current.Distinct().ToArray(); // avoid duplicates
                Debug.Log($"[AAP] {soldierName} received {abilitiesList.Count} personal abilities.");
            }
        }

        private string GetGuidForSoldier(string name)
        {
            switch (name)
            {
                case "Sophia": return "400f644c-41f2-c534-1b99-34d48400b7f7";
                case "Jacob": return "2f7a41a8-d68a-3374-1a13-16f18425d7bb";
                case "Omar": return "8c9986d9-d875-e0e4-8978-578af6eba952";
                case "Takeshi": return "d008b763-7eac-e7f4-e9c4-57eec8bb0c1e";
                case "Irina": return "e3c06e40-0543-fa04-5a9d-7ff43410b1e0";
                default: return null;
            }
        }

        public static string Localize(string key)
        {
            return I2.Loc.LocalizationManager.GetTranslation("AAP_" + key);
        }
    }
}