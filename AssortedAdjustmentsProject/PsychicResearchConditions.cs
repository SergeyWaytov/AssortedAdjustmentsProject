// PsychicResearchConditions.cs – Final version (no text injection)
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Geoscape.Levels.Factions;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class PsychicResearchConditions
    {
        public const string MindfraggerResearch = "PX_Alien_Mindfragger_ResearchDef";
        public const string PsychicAttackResearch = "PX_PyschicAttack_ResearchDef";

        public const string MindfraggerExtra =
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

        public const string PsychicInfluencesExtra =
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

        [HarmonyPatch(typeof(GeoPhoenixFaction), "OnResearchCompleted")]
        [HarmonyPostfix]
        static void OnResearchCompleted(GeoPhoenixFaction __instance, object research)
        {
            if (__instance == null || research == null) return;

            BaseDef researchDef = Traverse.Create(research)
                .Property("ResearchDef").GetValue<BaseDef>();
            if (researchDef == null) return;

            if (researchDef.name == MindfraggerResearch)
            {
                PsychicBuffManager.MindfraggerResearchCompleted = true;
                Debug.Log("[AAP] Psychic buff: Mindfragger research completed.");
            }
            else if (researchDef.name == PsychicAttackResearch)
            {
                PsychicBuffManager.PsychicInfluencesCompleted = true;
                Debug.Log("[AAP] Psychic buff: Psychic Attack research completed.");
            }
        }
    }
}