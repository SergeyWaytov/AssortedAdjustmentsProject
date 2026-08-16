using HarmonyLib;
using PhoenixPoint.Geoscape.Entities.Research;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View.ViewControllers.Research;
using PhoenixPoint.Geoscape.View.ViewModules;
using System;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Appends the psychic bonus lore at the DISPLAY layer - after the vanilla
    /// UI has set its text - instead of mutating shared I2 localization terms.
    /// The term-edit approach proved fragile: the game rebuilds language
    /// sources on save load, term tails came back truncated mid-lore, and a
    /// single Localize() result (current language) risked being written into
    /// both language columns. Patching the render points is deterministic:
    ///   - ResearchTooltip.Init  → research screen tooltip (Completed state)
    ///   - UIModulePhoenixpedia.SelectEntry → Phoenixpedia page
    ///   - ResearchCompletePopupPatch (existing) → completion popup
    /// </summary>
    internal static class LoreDisplay
    {
        private static string LoreFor(ResearchDef researchDef)
        {
            if (researchDef == null) return null;
            switch (researchDef.name)
            {
                case "PX_Alien_Mindfragger_ResearchDef":
                    return ModMain.Localize("MINDFRAGGER_LORE");
                case "PX_PyschicAttack_ResearchDef":
                    return ModMain.Localize("PSYCHIC_INFLUENCES_LORE");
                default:
                    return null;
            }
        }

        private static void AppendLore(string lore, Action<string> setText, Func<string> getText)
        {
            try
            {
                if (string.IsNullOrEmpty(lore) || lore.Contains("MISSING KEY")) return;
                string current = getText() ?? "";
                if (current.Contains("Silver-Eyed Man") || current.Contains("Шёпот сереброглазого") ||
                    current.Contains("Psychic Influences breakthrough") || current.Contains("Прорыв: Психические влияния"))
                    return; // already showing the lore
                setText(current + lore);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Lore display append failed: {e.Message}");
            }
        }

        // ── Legacy cleanup ────────────────────────────────────────────
        // Earlier builds appended lore directly into the vanilla I2 terms;
        // those edits could persist (and arrived truncated) in game runs made
        // with those builds. Strip any such leftovers so the vanilla text is
        // clean before the render-layer append does its job.
        private static readonly string[] LegacyLoreMarkers =
        {
            "\n\n<color=#A0A0FF>— Whispers from the Silver-Eyed Man",
            "\n\n<color=#A0A0FF>— Шёпот сереброглазого человека",
            "\n\n<color=#A0A0FF>— Psychic Influences breakthrough",
            "\n\n<color=#A0A0FF>— Прорыв: Психические влияния"
        };

        internal static void CleanupLegacyTermLore()
        {
            try
            {
                var cache = ModMain.DefCache;
                if (cache == null) return;
                string[] researchDefs = { "PX_Alien_Mindfragger_ResearchDef", "PX_PyschicAttack_ResearchDef" };

                foreach (string defName in researchDefs)
                {
                    var research = cache.GetDef<ResearchDef>(defName);
                    string key = research?.ViewElementDef?.CompleteText?.LocalizationKey;
                    if (string.IsNullOrEmpty(key)) continue;

                    foreach (var source in I2.Loc.LocalizationManager.Sources)
                    {
                        var term = source.GetTermData(key);
                        if (term == null) continue;
                        for (int i = 0; i < term.Languages.Length; i++)
                        {
                            string text = term.Languages[i];
                            if (string.IsNullOrEmpty(text)) continue;
                            foreach (string marker in LegacyLoreMarkers)
                            {
                                int pos = text.IndexOf(marker);
                                if (pos >= 0)
                                {
                                    term.Languages[i] = text.Substring(0, pos);
                                    Debug.Log($"[AAP] Research lore: stripped legacy injected text from '{key}' (language index {i}).");
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Research lore legacy cleanup failed: {e.Message}");
            }
        }

        // ── Research screen tooltip (hover on a research, incl. Completed) ──
        [HarmonyPatch(typeof(ResearchTooltip), "Init")]
        public static class ResearchTooltip_Init_Patch
        {
            static void Postfix(ResearchTooltip __instance, ResearchElement research)
            {
                string lore = LoreFor(research?.ResearchDef);
                if (lore == null) return;
                AppendLore(lore,
                    text => __instance.Description.text = text,
                    () => __instance.Description.text);
                Debug.Log($"[AAP] Research lore appended to tooltip for {research.ResearchDef.name}.");
            }
        }

        // ── Phoenixpedia entry page ───────────────────────────────────
        [HarmonyPatch(typeof(UIModulePhoenixpedia), "SelectEntry")]
        public static class UIModulePhoenixpedia_SelectEntry_Patch
        {
            static void Postfix(UIModulePhoenixpedia __instance, PhoenixpediaEntry entry)
            {
                try
                {
                    if (!(__instance.EntryDescriptionText?.gameObject.activeSelf ?? false)) return;
                    string lore = LoreFor(entry?.Source as ResearchDef);
                    if (lore == null) return;
                    AppendLore(lore,
                        text => __instance.EntryDescriptionText.Text = text,
                        () => __instance.EntryDescriptionText.Text);
                    Debug.Log($"[AAP] Research lore appended to Phoenixpedia page for {((ResearchDef)entry.Source).name}.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AAP] Phoenixpedia lore append failed: {e.Message}");
                }
            }
        }
    }
}
