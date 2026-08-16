using System;
using System.Collections;
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
    /// Persistent lore, take three - and the clean one: instead of editing the
    /// game's localization TERMS (which arrived truncated / language-mixed and
    /// got reverted by source rebuilds), the research def's own CompleteText
    /// bind is swapped to our imported AAP_*_FULL term (vanilla text + lore,
    /// EN and RU, with real newlines - the I2 CSV parser supports multi-line
    /// quoted fields, verified in LocalizationReader.ReadCSV).
    /// Every surface that renders a completed research - the Researches screen
    /// tooltip (GetTextByState(Completed)), the research-complete modal, and
    /// the Phoenixpedia entry - reads this one bind, so the lore persists
    /// everywhere in the player's language. The vanilla term is never touched.
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

    [HarmonyPatch]
    public static class ResearchCompletePopupPatch
    {
        // Lore texts now live in Localization/AAP_Localization.csv (EN + RU columns)
        // and resolve through the mod's CSV importer - no more English hardcode stub.
        private static string MindfraggerExtra => ModMain.Localize("MINDFRAGGER_LORE");
        private static string PsychicInfluencesExtra => ModMain.Localize("PSYCHIC_INFLUENCES_LORE");

        // Vanilla description texts (RU + EN) – used to locate the body text
        private static readonly string[] MindfraggerVanillaTexts =
        {
            // Russian (from screenshot)
            "Отчет: промыватели — это биологическое оружие, предназначенное для контроля и, возможно, похищения людей. Голова существа отрывается от тела во время нападения, становясь постоянно связанной с его хозяином. К счастью, если убить прикрепившееся существо, то жертва безопасно вернется в сознательное состояние.",
            // English (from screenshot)
            "Summary: Mindfraggers are a bio-weapon designed to control, and possibly abduct human subjects. The creature’s head detaches from the body during an attack, becoming permanently linked to its host. Fortunately, killing the attached creature will remove it safely, restoring the victim to a normal state of mind."
        };

        private static readonly string[] PsychicInfluencesVanillaTexts =
        {
            // Russian (from screenshot)
            "Интенсивный анализ показал, что способность пандоронов прививать страх людям является прямым следствием сверхстимуляции миндалин через инфразвук, который действует на людей на психологическом уровне. Данные, основанные на текущих исследованиях, говорят, что только некоторые особи пандоронов могут оказать такой эффект. Хотя точный метод, с помощью которого они это делают, остается неизвестным. Чтобы свети на нет этот эффект, разработан демпфирующий модуль.",
            // English (from screenshot)
            "Intense analysis has shown that the Pandoran ability to instill fear in humans is a direct result of an overstimulation of the amygdala through a form of infrasound combined with some kind of psychic influence. Based on current observations, only certain Pandoran species are able to produce the effect, although the exact method through which they do so remains unknown. A dampening module has been developed to negate this type of effect."
        };

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            foreach (var m in typeof(UIModuleModal).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var p = m.GetParameters();
                if (m.Name == "Show" && p.Length == 3 && p[2].ParameterType == typeof(object))
                    return m;
            }
            Debug.LogError("[AAP] Could not find UIModuleModal.Show method.");
            return null;
        }

        [HarmonyPostfix]
        static void Postfix(object modalData, UIModuleModal __instance)
        {
            if (modalData == null || !modalData.GetType().Name.Contains("ResearchComplete"))
                return;

            object researchElement = Traverse.Create(modalData).Field("ResearchElement").GetValue();
            string defName = Traverse.Create(researchElement).Property("ResearchDef")
                .GetValue<BaseDef>()?.name;
            if (defName == null) return;

            string extraText = defName switch
            {
                "PX_Alien_Mindfragger_ResearchDef" => MindfraggerExtra,
                "PX_PyschicAttack_ResearchDef" => PsychicInfluencesExtra,
                _ => null
            };
            if (extraText == null) return;

            // Select the appropriate vanilla text array
            string[] vanillaTexts = defName switch
            {
                "PX_Alien_Mindfragger_ResearchDef" => MindfraggerVanillaTexts,
                "PX_PyschicAttack_ResearchDef" => PsychicInfluencesVanillaTexts,
                _ => null
            };
            if (vanillaTexts == null) return;

            __instance.StartCoroutine(InjectLoreAfterFrame(__instance, extraText, vanillaTexts));
        }

        static IEnumerator InjectLoreAfterFrame(UIModuleModal instance, string extraText, string[] vanillaTexts)
        {
            yield return null;
            yield return null; // wait two frames for UI to fully appear

            // --- ADD THESE 4 LINES ---
            if (instance == null || !instance.gameObject.activeInHierarchy)
            {
                Debug.Log("[AAP] InjectLore: UI instance null/inactive – aborting.");
                yield break;
            }
            // --- END OF ADDITION ---

            var allText = UnityEngine.Object.FindObjectsOfType<Text>();
            Debug.Log($"[AAP] InjectLore: scanning {allText.Length} active Text objects...");

            Text targetText = null;
            foreach (var t in allText)
            {
                string trimmed = t.text.Trim();
                foreach (string vanilla in vanillaTexts)
                {
                    if (trimmed.IndexOf(vanilla.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetText = t;
                        break;
                    }
                }
                if (targetText != null) break;
            }

            if (targetText == null)
            {
                Debug.Log("[AAP] InjectLore: vanilla text not found among active Text objects. Dumping first 5 candidates...");
                int count = 0;
                foreach (var t in allText)
                {
                    if (t.text.Length > 50 && count < 5)
                    {
                        Debug.Log($"[AAP]   '{t.name}' ({t.text.Length} chars): {t.text.Substring(0, Math.Min(t.text.Length, 100))}");
                        count++;
                    }
                }
                yield break;
            }

            // Strip any legacy lore text that earlier (term-injection era) builds
            // may have baked into the vanilla description - it could arrive
            // truncated and in the wrong language. Cut at our lore's colored
            // header, which only ever comes from AAP text.
            int legacyPos = targetText.text.IndexOf("<color=#A0A0FF>—", StringComparison.Ordinal);
            if (legacyPos >= 0)
            {
                Debug.Log($"[AAP] InjectLore: stripped {targetText.text.Length - legacyPos} chars of legacy lore text before appending.");
                targetText.text = targetText.text.Substring(0, legacyPos).TrimEnd();
            }

            if (!targetText.text.EndsWith(extraText))
            {
                targetText.text += extraText;
                Debug.Log($"[AAP] Lore appended successfully to '{targetText.name}'.");
            }
            else
            {
                Debug.Log("[AAP] Text already contains lore, skipping.");
            }
        }
    }

    /// <summary>
    /// Removes lore text that earlier builds appended directly into the vanilla
    /// localization terms (it could persist across sessions, truncated and in
    /// the wrong language). Runs at every geoscape level start so the Research
    /// screen, Phoenixpedia and any other term-driven surface show clean
    /// vanilla text again; the readable lore comes from the popup stub above.
    /// </summary>
    internal static class LegacyLoreCleanup
    {
        private const string LoreHeaderMarker = "<color=#A0A0FF>—";

        internal static void Run()
        {
            try
            {
                var cache = ModMain.DefCache;
                if (cache == null) return;
                string[] researchDefs = { "PX_Alien_Mindfragger_ResearchDef", "PX_PyschicAttack_ResearchDef" };

                foreach (string defName in researchDefs)
                {
                    var research = cache.GetDef<PhoenixPoint.Geoscape.Entities.Research.ResearchDef>(defName);
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
                            int pos = text.IndexOf(LoreHeaderMarker, StringComparison.Ordinal);
                            if (pos >= 0)
                            {
                                term.Languages[i] = text.Substring(0, pos);
                                Debug.Log($"[AAP] Research lore cleanup: stripped legacy text from '{key}' (language index {i}, {text.Length - pos} chars).");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Research lore cleanup failed: {e.Message}");
            }
        }
    }
}