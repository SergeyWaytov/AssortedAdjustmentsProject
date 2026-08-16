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
