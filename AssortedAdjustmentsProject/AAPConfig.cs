using PhoenixPoint.Modding;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Mod settings editable from the in-game Mod Options screen (main menu).
    /// Native replacement for the Modnix-era settings files of the original
    /// Assorted Adjustments / Limited War mods these features were ported from.
    /// Def-level changes re-apply through OnConfigChanged and are idempotent
    /// (relative changes are computed from values captured on first apply).
    /// </summary>
    /// <remarks>
    /// LOCALIZED MOD OPTIONS (v1.2):
    /// The original [ConfigField(text:..., description:...)] attributes were
    /// displayed verbatim by the framework's Mod Options UI and bypassed
    /// I2.Loc, so the panel was English-only regardless of game locale.
    /// We now override GetConfigFields() and return ModConfigField entries
    /// whose GetText/GetDescription delegates resolve through I2.Loc, the
    /// same pattern TFTV uses. Keys: AAP_CFG_&lt;Field&gt; (label) and
    /// AAP_CFG_&lt;Field&gt;_DESC (tooltip), defined in AAP_Localization.csv
    /// (English + Russian shipped). Field defaults + serialization are
    /// unaffected; only the panel presentation is localized.
    /// </remarks>
    public class AAPConfig : ModConfig
    {
        // ── Core toggles ──────────────────────────────────────────────

        public bool DisableRightClickMove = true;

        public bool EnableSmartEvacuation = true;

        public bool EnablePlentifulDrops = true;

        // ── Tunable values ────────────────────────────────────────────

        public int PersonalAbilitiesCount = 5;

        public float FrenzySpeedCoefficient = 1.5f;

        public float VehicleAmmoMultiplier = 1.5f;

        public int DeploymentCap = 16;

        // ── Festering Skies (DLC3) ────────────────────────────────────

        public bool EnableFesteringSkiesTweaks = true;

        public int BehemothSpeed = 6;

        public int AlienFlyerSpeedPercent = 10;

        // ── Corrupted Horizons (DLC4) ─────────────────────────────────

        public bool EnableCorruptedHorizonsTweaks = true;

        public int AcheronReinforceWPCost = 20;

        // ── Limited War (ported from Sheepy's Limited War / Mad's adaptation) ──
        // Changes take effect after a game restart.

        public bool EnableLimitedWar = true;

        public bool LWZonedFactionAttacks = true;

        public bool LWZonedPandoranAttacks = false;

        public bool LWAttacksRaiseAlertness = true;

        public bool LWStopOneSidedWar = true;

        public int LWGlobalAttackLimit = 3;

        public int LWFactionAttackLimit = 2;

        public int LWSiegeProtectionLimit = 1;

        public bool LWDisablePandoranBaseAttacks = false;

        // ── Localized Mod Options ─────────────────────────────────────

        /// <summary>
        /// Backing list of ModConfigField entries returned to the framework.
        /// Populated once by PopulateConfigFields() (called from OnModEnabled
        /// after ImportLocalization, so the AAP_CFG_ keys resolve).
        /// </summary>
        internal List<ModConfigField> modConfigFields = new List<ModConfigField>();

        /// <summary>
        /// Builds the ModConfigField list with localized GetText/GetDescription
        /// delegates for every public instance field above. Mirrors the TFTV
        /// pattern. Idempotent (Clears first). Safe to re-call.
        /// </summary>
        public void PopulateConfigFields()
        {
            try
            {
                modConfigFields.Clear();

                var fields = GetType()
                    .GetFields(BindingFlags.Instance | BindingFlags.Public);

                foreach (var fieldInfo in fields)
                {
                    // Skip our own backing list - it is not a user setting.
                    if (fieldInfo.Name == nameof(modConfigFields)) continue;

                    // C# 5+ foreach closures capture the per-iteration variable,
                    // but capturing into a local makes the intent unambiguous.
                    var captured = fieldInfo;

                    object sample = captured.GetValue(this);
                    Type fieldType = sample != null ? sample.GetType() : captured.FieldType;

                    modConfigFields.Add(new ModConfigField(captured.Name, fieldType)
                    {
                        GetValue = () => captured.GetValue(this),
                        SetValue = (o) => captured.SetValue(this, o),
                        GetText = () => LocalizedOption("CFG_" + captured.Name, captured.Name),
                        GetDescription = () => LocalizedOption("CFG_" + captured.Name + "_DESC", null),
                    });
                }

                Debug.Log($"[AAP] Mod Options: {modConfigFields.Count} fields wired to localized labels/descriptions (AAP_CFG_* keys).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] PopulateConfigFields failed: {e}");
            }
        }

        /// <summary>
        /// Framework entry point: the Mod Options screen asks the ModConfig
        /// for its field list. We return our pre-built, localized list.
        /// Lazy-populates if asked before OnModEnabled ran (the delegates are
        /// lazy, so labels still resolve correctly once localization imports).
        /// </summary>
        public override List<ModConfigField> GetConfigFields()
        {
            if (modConfigFields == null)
                modConfigFields = new List<ModConfigField>();

            if (modConfigFields.Count == 0)
                PopulateConfigFields();

            return modConfigFields;
        }

        /// <summary>
        /// Resolves a Mod Options string from I2.Loc (AAP_ namespace) with a
        /// graceful fallback so a missing key never blanks the panel: labels
        /// fall back to the raw field name, descriptions fall back to empty.
        /// </summary>
        private static string LocalizedOption(string key, string fallback)
        {
            try
            {
                string s = ModMain.Localize(key);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            catch
            {
                // Swallow - never break the Mod Options screen over a lookup.
            }
            return fallback ?? string.Empty;
        }
    }
}
