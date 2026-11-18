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

        // Static self-reference so level-start patches can re-assert mod state
        // (localization import + lore binds) after the game rebuilds sources.
        internal static ModMain Self;

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

        // AAP T10: the mod mutates shared defs at startup; disabling it live
        // cannot undo those writes. Do not advertise live-disable as safe.
        public override bool CanSafelyDisable => false;

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
            Self = this;
            ImportLocalization();   // AAP_ keys must exist before anything localizes
            Config.PopulateConfigFields();   // wire Mod Options panel to AAP_CFG_ keys (EN+RU)
            DefCache = new DefCache();
            // Persistent lore: swap the two research defs' CompleteText to our
            // full-text AAP_*_FULL terms. The popup postfix and the legacy
            // lore-stripping pass were removed in the Major Cleanup - the
            // AAP_*_FULL terms now carry the full [vanilla + lore] text and
            // are rendered untouched by the game's own UI on every surface.
            ResearchLoreBinds.Apply();



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
                // AAP G2: Festering Skies / Corrupted Horizons adjustments cannot be
                // live-reversed (defs are mutated, not captured/restored). Removed
                // the no-op re-apply calls here; toggling these requires a game restart
                // (see the config field tooltips).
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
        internal void ImportLocalization()
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

                string csv = File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                if (!csv.EndsWith("\n")) csv += "\n";

                var source = I2.Loc.LocalizationManager.Sources[0];
                int before = source.mTerms.Count;
                // AAP L1 fix: Remove existing AAP_ terms first, then re-import in
                // AddNewTerms mode. The old Replace mode clobbered source 0's
                // language table (9 languages -> 2), making Russian AAP_ terms
                // unreachable (rendered as MISSING KEY). AddNewTerms preserves all
                // existing languages and the manual remove-then-add makes the
                // import idempotent across save/load cycles.
                for (int i = source.mTerms.Count - 1; i >= 0; i--)
                {
                    string term = source.mTerms[i]?.Term;
                    if (!string.IsNullOrEmpty(term) && term.StartsWith("AAP_", StringComparison.Ordinal))
                    {
                        source.RemoveTerm(term);
                    }
                }
                source.Import_CSV(string.Empty, csv, I2.Loc.eSpreadsheetUpdateMode.AddNewTerms, ',');
                source.UpdateDictionary(true);
                // AAP 1.2.2 fix: the Sep-23 game build can finish populating source 0's
                // language table AFTER our boot-time import, so the import may match
                // the CSV's English/Russian columns against codeless duplicate language
                // rows (which I2's translation lookup never reads - GetTranslation
                // falls back to English). Enforce the CSV's EN/RU values into the
                // canonical, code-keyed slots so every surface localizes correctly
                // regardless of import timing.
                EnforceCanonicalTranslations(source, ParseCsvRows(csv));
                LocalizationManager.LocalizeAll(true);
                int added = source.mTerms.Count - before;
                Debug.Log($"[AAP] Localization: CSV processed in AddNewTerms mode ({added} new term(s); existing AAP_ terms removed and re-added; all languages preserved) from AAP_Localization.csv.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Localization import failed: {e}");
            }
        }

        /// <summary>
        /// Re-writes every AAP_ term's English/Russian columns from the CSV we
        /// just imported into the CANONICAL language slots (resolved by language
        /// code, not name), so the values are readable no matter which slot the
        /// import happened to fill. Idempotent and cheap (~100 terms).
        /// </summary>
        private void EnforceCanonicalTranslations(I2.Loc.LanguageSourceData source, List<string[]> rows)
        {
            try
            {
                if (rows == null || rows.Count < 2) return;
                int ruIdx = source.GetLanguageIndexFromCode("ru", true, false);
                if (ruIdx < 0)
                {
                    Debug.Log("[AAP] Localization: canonical Russian slot not present yet (language table still populating); EN/RU re-asserted on menu/level start.");
                    return;
                }
                int enIdx = source.GetLanguageIndexFromCode("en", true, false);
                if (enIdx < 0) enIdx = source.GetLanguageIndex("English", true, false);

                var header = rows[0];
                int enCol = -1, ruCol = -1;
                for (int i = 1; i < header.Length; i++)
                {
                    if (string.Equals(header[i], "English", StringComparison.OrdinalIgnoreCase)) enCol = i;
                    else if (string.Equals(header[i], "Russian", StringComparison.OrdinalIgnoreCase)) ruCol = i;
                }
                if (ruCol < 0) return;

                int fixedRu = 0, fixedEn = 0;
                for (int r = 1; r < rows.Count; r++)
                {
                    var row = rows[r];
                    if (row == null || row.Length <= ruCol) continue;
                    string key = row[0];
                    if (string.IsNullOrEmpty(key) || !key.StartsWith("AAP_", StringComparison.Ordinal)) continue;
                    var td = source.GetTermData(key, false);
                    if (td == null) continue;
                    string ru = row[ruCol];
                    string curRu = ruIdx < td.Languages.Length ? td.Languages[ruIdx] : null;
                    if (!string.Equals(curRu, ru, StringComparison.Ordinal))
                    {
                        td.SetTranslation(ruIdx, ru, null);
                        fixedRu++;
                    }
                    if (enCol >= 0 && enIdx >= 0 && enIdx < td.Languages.Length)
                    {
                        string en = row[enCol];
                        if (string.IsNullOrEmpty(td.Languages[enIdx]) && !string.IsNullOrEmpty(en))
                        {
                            td.SetTranslation(enIdx, en, null);
                            fixedEn++;
                        }
                    }
                }
                Debug.Log($"[AAP] Localization: canonical slots enforced (ru idx {ruIdx}: {fixedRu} updated, en idx {enIdx}: {fixedEn} updated).");
            }
            catch (Exception e)
            {
                Debug.LogError("[AAP] Localization canonical enforcement failed: " + e);
            }
        }

        /// <summary>
        /// Minimal RFC-4180-style CSV row parser (quoted fields, doubled quotes,
        /// embedded commas). Rows in AAP_Localization.csv are single-line (lore
        /// newlines are literal backslash-n escapes), so no multi-line records.
        /// </summary>
        private static List<string[]> ParseCsvRows(string csv)
        {
            var rows = new List<string[]>();
            var cur = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQ = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (inQ)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQ = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') inQ = true;
                else if (c == ',') { cur.Add(sb.ToString()); sb.Length = 0; }
                else if (c == '\n') { cur.Add(sb.ToString()); sb.Length = 0; rows.Add(cur.ToArray()); cur = new List<string>(); }
                else if (c != '\r') sb.Append(c);
            }
            if (sb.Length > 0 || cur.Count > 0) { cur.Add(sb.ToString()); rows.Add(cur.ToArray()); }
            return rows;
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

                // Fix Jacob's campaign template: class tag, gear, and Sniper base abilities
                if (jacobTemplate != null && irinaTemplate != null)
                {
                    jacobTemplate.Data.GameTags[0] = irinaTemplate.Data.GameTags[0];
                    // AAP T8: copy by value, not by reference. Without ToArray(),
                    // Jacob shares Irina's mutable equipment arrays -- later
                    // edits to one template silently mutate the other.
                    jacobTemplate.Data.BodypartItems = irinaTemplate.Data.BodypartItems?.ToArray();
                    jacobTemplate.Data.EquipmentItems = irinaTemplate.Data.EquipmentItems?.ToArray();
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

                // AAP T7: Add personal abilities with fail-loud diagnostics.
                // The old version silently skipped missing defs; the audit found
                // that 5 of the requested names (Trooper_, Quarterback_, Farsighted_,
                // Bombardier_, Healer_) do not exist in this build. The conservative
                // call sites below remove the nonexistent args and defer Irina
                // (all 3 of her requested defs are absent).
                AddPersonalAbilitiesToTemplate(repo, "Jacob", "Cautious_AbilityDef", "Thief_AbilityDef");
                AddPersonalAbilitiesToTemplate(repo, "Sophia", "SelfDefenseSpecialist_AbilityDef", "Thief_AbilityDef");
                AddPersonalAbilitiesToTemplate(repo, "Omar", "CloseQuartersSpecialist_AbilityDef", "Reckless_AbilityDef", "Biochemist_AbilityDef");
                AddPersonalAbilitiesToTemplate(repo, "Takeshi", "CloseQuartersSpecialist_AbilityDef", "Reckless_AbilityDef", "Strongman_AbilityDef");
                Debug.LogWarning("[AAP] Irina personal abilities deferred: no selected defs exist in this build.");

                Debug.Log("[AAP] Soldier templates updated. Jacob is now a Sniper.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Template fix failed: {e.Message}");
            }
        }

        // AAP T7: params signature so each soldier can have a different count.
        // Logs a warning for each requested def that is not found (fail-loud).
        private void AddPersonalAbilitiesToTemplate(DefRepository repo, string soldierName, params string[] requestedNames)
        {
            var template = repo.GetDef(GetGuidForSoldier(soldierName)) as TacCharacterDef;
            if (template == null)
            {
                Debug.LogWarning($"[AAP] Template not found for {soldierName}.");
                return;
            }

            var resolved = new List<TacticalAbilityDef>();
            foreach (string name in requestedNames)
            {
                var ability = DefCache.GetDef<TacticalAbilityDef>(name);
                if (ability == null)
                    Debug.LogWarning($"[AAP] {soldierName}: ability def not found: {name}");
                else
                    resolved.Add(ability);
            }

            // Append to existing abilities instead of overwriting
            var current = template.Data.Abilites?.ToList() ?? new List<TacticalAbilityDef>();
            current.AddRange(resolved);
            template.Data.Abilites = current.Where(a => a != null).Distinct().ToArray();
            Debug.Log($"[AAP] {soldierName}: requested {requestedNames.Length}, resolved {resolved.Count}, total {template.Data.Abilites.Length}.");
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
