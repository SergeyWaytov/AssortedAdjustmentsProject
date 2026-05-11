using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Base.Core;
using Base.Defs;
using PhoenixPoint.Common.View.ViewModules;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class ResearchCompletePopupPatch
    {
        // ⚠️ Replace these with your actual lore text
        private const string MindfraggerExtra =
    "\n\n<color=#A0A0FF>— Whispers from the Silver‑Eyed Man —</color>\n" +
    "Not long after Phoenix Point's beacon blazed back to life and the first Manticore scouts " +
    "returned, a stranger appeared at our makeshift gate. No aircraft, no escort – just a figure " +
    "who walked out of the mist and knocked. His eyes, a disquieting silver‑steel, seemed to hold " +
    "the weight of centuries. He offered no name, only a cryptic congratulations: " +
    "\"The ashes have stirred. Good. You'll need this.\"\n\n" +
    "The \"gift\" was a dense chemical formula scrawled on decaying paper, a recipe for something " +
    "he called the Mind Awakening Elixir. It promised to fortify the human psyche against the " +
    "alien corruption that would soon pour from the nests. Our best scientists tried to replicate " +
    "it, but the compound refused to stabilise. The promise of psychic armour crumbled into another " +
    "false hope, and the project was quietly shelved – no sense in dangling empty miracles before " +
    "the troops.\n\n" +
    "Then, during the autopsy of a captured Mindfragger, a bio‑chemist noticed a pattern. The " +
    "parasitic interface the creature used to dominate its victims shared an uncanny molecular " +
    "signature with the stranger's formula. A cross‑referencing frenzy ensued. The alien's own " +
    "biology held the missing key. From the marriage of human desperation and pandoran physiology, " +
    "a watered‑down but functional dietary supplement was born. It won't turn anyone into a " +
    "telepath, but it does thicken the mind's walls – a little harder to panic, a little harder " +
    "to control.";

        private const string PsychicInfluencesExtra =
            "\n\n<color=#A0A0FF>— Psychic Influences breakthrough —</color>\n" +
            "The original elixir, combined with the knowledge ripped from alien nerve‑clusters, finally " +
            "yielded a stable gene therapy. The silver‑eyed stranger's formula was never meant to be " +
            "swallowed – it was a blueprint for rewriting the human mind's architecture.\n\n" +
            "The treatment permanently elevates a soldier's psychic resilience to near‑superhuman levels, " +
            "forming an invisible shield that only the most cataclysmic mental assaults can breach. As an " +
            "unexpected side‑effect, operatives already gifted with outward psychic abilities report their " +
            "mind‑control efforts becoming eerily more potent – as if the aliens suddenly find their own " +
            "tricks turned against them with familiar ease.\n\n" +
            "The stranger was never seen again, but his gift now hums quietly in the blood of every " +
            "Phoenix operative, a silent thank‑you to a messenger who knew more than he ever told.";

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
}