using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Modding;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Geoscape.Entities.Research;   // for ResearchDef
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using I2.Loc;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public class ModMain : PhoenixPoint.Modding.ModMain
    {
        private Harmony harmony;

        // Static reference so Harmony patches can reach the cache
        public static DefCache DefCache { get; private set; }

        // Mod settings (native ModConfig; cached statically for the static modules)
        private static AAPConfig _config;
        public new AAPConfig Config
        {
            get { _config = (AAPConfig)base.Config; return _config; }
        }
        public static AAPConfig Cfg => _config;

        // Fixed campaign template for Jacob – used by the runtime tutorial fix
        public static TacCharacterDef JacobsFixedTemplate { get; private set; }
        //public static bool DiagnosticsEnabled = false;   // turn on only when needed
        public override void OnModEnabled()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Debug.LogError("[AAP] UNHANDLED EXCEPTION: " + (args.ExceptionObject as Exception)?.ToString() ?? args.ExceptionObject.ToString());
            };
            Debug.Log("=========================================");
            Debug.Log("[AAP] FULL MOD: OnModEnabled called.");
            Debug.Log("=========================================");

            var _ = Config;   // cache settings before modules read them
            ImportLocalization();   // AAP_ keys must exist before anything localizes
            DefCache = new DefCache();
            InjectLoreIntoResearchDescriptions();   // lore into vanilla research terms (popup + archive)



            //DefNameScanner.Run();

            AbilityAdjustments.Apply(DefCache);
            RageBurstPatch.Apply(DefCache);
            PrecisionShot.Apply(DefCache);
            PsychicBuffManager.Init();
            WeaponAdjustments.Apply(DefCache);
            ArmorAdjustments.Apply(DefCache);
            GeoscapeFacilitiesAdjustments.Apply(DefCache);
            VehicleAdjustments.Apply(DefCache);

            LootMechanics.Apply(DefCache);
            RepairCosts.Apply(DefCache);

            // DLC adjustments (silently skip when the DLC is not installed)
            FesteringSkiesAdjustments.Apply(DefCache);
            CorruptedHorizonsAdjustments.Apply(DefCache);

            // Limited War (ported from Sheepy's/Mad's Modnix-era mod; Harmony patches
            // activate via Prepare() according to the mod options)
            LimitedWar.Apply(DefCache);

            // Update soldier templates and create the fixed Jacob reference
            FixSoldierTemplates();

            harmony = new Harmony("SergeyWaytov_AssortedAdjustmentsProject");
            harmony.PatchAll();   // Applies all patches, including TutorialJacobFixPatch
            I2.Loc.LocalizationManager.LocalizeAll(true);

            Debug.Log("[AAP] Precision Shot name: " + ModMain.Localize("PRECISION_SHOT"));
            Debug.Log("[AAP] Precision Shot desc: " + ModMain.Localize("PRECISION_SHOT_DESC"));

            Debug.Log("[AAP] Mod initialization complete.");
        }

        /// <summary>
        /// Re-applies def-level adjustments when the player changes settings in
        /// the main menu. Safe to re-run: relative changes recompute from base
        /// values captured on first apply, everything else sets absolute values.
        /// </summary>
        public override void OnConfigChanged()
        {
            try
            {
                var _ = Config;
                Debug.Log("[AAP] Config changed - re-applying def adjustments.");
                AbilityAdjustments.Apply(DefCache);
                VehicleAdjustments.Apply(DefCache);
                FesteringSkiesAdjustments.Apply(DefCache);
                CorruptedHorizonsAdjustments.Apply(DefCache);
                Debug.Log("[AAP] Config re-apply finished.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] OnConfigChanged failed: {e}");
            }
        }

        public override void OnModDisabled()
        {
            harmony?.UnpatchAll("SergeyWaytov_AssortedAdjustmentsProject");
            Debug.Log("[AAP] Mod disabled.");
        }

        /// <summary>
        /// Imports the mod's localization CSV into the game's I2 Loc source.
        /// The game does NOT auto-load mod CSVs - this manual import (same
        /// approach as TFTV) is what makes AAP_ localization keys resolve in
        /// every language column of the CSV. Without it, Localize() returns
        /// nothing and texts fall back to hardcoded English.
        /// NOTE: Assembly.Location is unreliable inside the Unity player
        /// (log showed "Invalid path") - use the mod's registered directory
        /// from the modding API instead, with the assembly path as fallback.
        /// </summary>
        private void ImportLocalization()
        {
            try
            {
                string modDir = null;
                try { modDir = Instance?.Entry?.Directory; } catch { }
                if (string.IsNullOrEmpty(modDir) || !System.IO.Directory.Exists(modDir))
                {
                    try { modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
                    catch { modDir = null; }
                }
                if (string.IsNullOrEmpty(modDir))
                {
                    Debug.LogWarning("[AAP] Localization: could not resolve mod directory.");
                    return;
                }

                string csvPath = Path.Combine(modDir, "Assets", "Localization", "AAP_Localization.csv");
                if (!File.Exists(csvPath))
                {
                    Debug.LogWarning($"[AAP] Localization CSV not found: {csvPath}");
                    return;
                }

                string csv = File.ReadAllText(csvPath);
                if (!csv.EndsWith("\n")) csv += "\n";

                var source = I2.Loc.LocalizationManager.Sources[0];
                int before = source.mTerms.Count;
                source.Import_CSV(string.Empty, csv, I2.Loc.eSpreadsheetUpdateMode.AddNewTerms, ',');
                int added = source.mTerms.Count - before;
                Debug.Log($"[AAP] Localization: imported {added} terms from AAP_Localization.csv.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Localization import failed: {e}");
            }
        }

        /// <summary>
        /// Appends the psychic bonus lore to the VANILLA research text terms
        /// (English + Russian). All three display surfaces render the SAME
        /// field for a finished research - CompleteText (Researches screen via
        /// GetTextByState(Completed), the research-complete modal, and the
        /// Phoenixpedia entry built in GeoPhoenixpedia) - so appending there
        /// makes the lore visible everywhere at once. The popup text-scan
        /// patch stays as a fallback and skips itself once the lore is present.
        /// </summary>
        internal static void InjectLoreIntoResearchDescriptions()
        {
            try
            {
                int injected = 0;
                injected += InjectResearchLore("PX_Alien_Mindfragger_ResearchDef", "MINDFRAGGER_LORE");
                injected += InjectResearchLore("PX_PyschicAttack_ResearchDef", "PSYCHIC_INFLUENCES_LORE");
                Debug.Log($"[AAP] Research lore: injected into {injected} research descriptions.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Research lore injection failed: {e}");
            }
        }

        private static int InjectResearchLore(string researchDefName, string loreKey)
        {
            var research = DefCache?.GetDef<ResearchDef>(researchDefName);
            if (research?.ViewElementDef == null)
            {
                Debug.LogWarning($"[AAP] Research lore: research def not found: {researchDefName}.");
                return 0;
            }

            // What completed researches display on every screen. Fallbacks kept
            // in case a future game version shuffles the fields.
            var textBind = research.ViewElementDef.CompleteText
                           ?? research.ViewElementDef.UnlockText
                           ?? research.ViewElementDef.Description;
            string descKey = textBind?.LocalizationKey;
            if (string.IsNullOrEmpty(descKey))
            {
                Debug.LogWarning($"[AAP] Research lore: no CompleteText key for {researchDefName}.");
                return 0;
            }

            string lore = Localize(loreKey);
            if (string.IsNullOrEmpty(lore) || lore.Contains("MISSING KEY"))
            {
                Debug.LogWarning($"[AAP] Research lore: key {loreKey} did not resolve - skipping.");
                return 0;
            }

            int changed = 0;
            foreach (var source in I2.Loc.LocalizationManager.Sources)
            {
                var term = source.GetTermData(descKey);
                if (term == null) continue;

                bool alreadyPresent = false;
                foreach (string language in new[] { "English", "Russian" })
                {
                    int idx = source.GetLanguageIndex(language);
                    if (idx < 0) continue;
                    string current = term.GetTranslation(idx);
                    bool hasLore = current != null &&
                        (current.Contains("— Whispers from the Silver") || current.Contains("— Шёпот сереброглазого") ||
                         current.Contains("Psychic Influences breakthrough") || current.Contains("Прорыв: Психические влияния"));
                    if (hasLore)
                    {
                        alreadyPresent = true;
                        continue;
                    }
                    if (string.IsNullOrEmpty(current)) continue;
                    term.SetTranslation(idx, current + lore, null);
                    changed++;
                }

                // Diagnostic: show what the term actually contains right now, so a
                // source rebuild that reverts the injection is visible in the log.
                int enIdx = source.GetLanguageIndex("English");
                int ruIdx = source.GetLanguageIndex("Russian");
                string enNow = enIdx >= 0 ? (term.GetTranslation(enIdx) ?? "") : "";
                string ruNow = ruIdx >= 0 ? (term.GetTranslation(ruIdx) ?? "") : "";
                Debug.Log($"[AAP] Research lore check '{descKey}': EN ends '...{enNow.Substring(Math.Max(0, enNow.Length - 40)).Replace("\n", " ")}', " +
                          $"RU ends '...{ruNow.Substring(Math.Max(0, ruNow.Length - 40)).Replace("\n", " ")}' (alreadyPresent={alreadyPresent}).");

                if (changed > 0) break;
            }

            Debug.Log(changed > 0
                ? $"[AAP] Research lore: appended to '{descKey}' ({researchDefName}, CompleteText) in {changed} languages."
                : $"[AAP] Research lore: could not patch '{descKey}' ({researchDefName}).");
            return changed > 0 ? 1 : 0;
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
            // "\n" sequences in CSV values are literal escapes - convert them
            // so multi-paragraph lore texts stay single-line in the CSV.
            return I2.Loc.LocalizationManager.GetTranslation("AAP_" + key)?.Replace("\\n", "\n");
        }

        
    }
}