using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Base.Core;
using Base.Defs;
using Base.UI;
using PhoenixPoint.Common.View.ViewModules;
using PhoenixPoint.Geoscape.Entities.Research;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// LORE SYSTEM (cleaned up in the Major Cleanup pass):
    ///
    /// The research def's own CompleteText bind is swapped to our imported
    /// AAP_*_FULL term (vanilla text + lore, EN and RU, with real newlines -
    /// the I2 CSV parser supports multi-line quoted fields). Every surface
    /// that renders a completed research - the Researches screen tooltip
    /// (GetTextByState(Completed)), the research-complete modal, and the
    /// Phoenixpedia entry - reads this one bind, so the lore persists
    /// everywhere in the player's language. The vanilla localization term
    /// is never touched.
    ///
    /// Removed in this cleanup:
    ///   * ResearchCompletePopupPatch (Harmony postfix on UIModuleModal.Show) -
    ///     it appended the AAP_*_LORE term to the completion popup. Now that
    ///     the AAP_*_FULL term itself carries the lore, that append was
    ///     redundant and would have doubled the lore on the popup.
    ///   * LegacyLoreCleanup - it stripped any text after the
    ///     "<color=#A0A0FF>-" marker from the research's CompleteText term.
    ///     That routine was written for an older design that baked lore into
    ///     the VANILLA terms; under the current design (AAP_*_FULL terms) it
    ///     was stripping the mod's OWN lore out of the mod's OWN terms on
    ///     every geoscape load. Gone.
    ///
    /// What remains here is the entire lore system: ResearchLoreBinds.Apply.
    /// </summary>
    internal static class ResearchLoreBinds
    {
        internal static void Apply()
        {
            try
            {
                int swapped = 0;
                swapped += Swap("PX_Alien_Mindfragger_ResearchDef", "AAP_MINDFRAGGER_FULL");
                swapped += Swap("PX_PyschicAttack_ResearchDef", "AAP_PSYCHIC_INFLUENCES_FULL");
                Debug.Log($"[AAP] Research lore binds: {swapped}/2 research CompleteText binds point at AAP full-text terms.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Research lore binds failed: {e.Message}");
            }
        }

        private static int Swap(string researchDefName, string fullTermKey)
        {
            var research = ModMain.DefCache?.GetDef<ResearchDef>(researchDefName);
            if (research?.ViewElementDef == null)
            {
                Debug.LogWarning($"[AAP] Research lore binds: {researchDefName} not found.");
                return 0;
            }

            // Only swap if our composed term actually imported - otherwise the
            // screen would show a missing-key placeholder.
            bool termImported = false;
            foreach (var source in I2.Loc.LocalizationManager.Sources)
            {
                if (source.GetTermData(fullTermKey) != null) { termImported = true; break; }
            }
            if (!termImported)
            {
                Debug.LogWarning($"[AAP] Research lore binds: term '{fullTermKey}' not imported - keeping vanilla text.");
                return 0;
            }

            if (research.ViewElementDef.CompleteText?.LocalizationKey == fullTermKey)
                return 1; // already swapped (idempotent re-apply)

            research.ViewElementDef.CompleteText = new LocalizedTextBind(fullTermKey);
            Debug.Log($"[AAP] Research lore binds: {researchDefName}.CompleteText -> {fullTermKey}.");
            return 1;
        }
    }
}
