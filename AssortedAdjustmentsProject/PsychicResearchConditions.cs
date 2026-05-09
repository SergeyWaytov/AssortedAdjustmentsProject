// PsychicResearchConditions.cs – Diagnostic Edition
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Geoscape.Levels.Factions;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class PsychicResearchConditions
    {
        private static readonly string MindfraggerResearch = "PX_Alien_Mindfragger_ResearchDef";
        private static readonly string PsychicAttackResearch = "PX_PyschicAttack_ResearchDef";   // your current spelling

        [HarmonyPatch(typeof(GeoPhoenixFaction), "OnResearchCompleted")]
        [HarmonyPostfix]
        static void OnResearchCompleted(GeoPhoenixFaction __instance, object research)
        {
            if (__instance == null || research == null) return;

            Debug.Log($"[AAP TRACE] OnResearchCompleted fired. research type = {research.GetType().FullName}");

            BaseDef researchDef = null;
            try
            {
                // The correct property name is "ResearchDef"
                researchDef = Traverse.Create(research).Property("ResearchDef").GetValue<BaseDef>();
                if (researchDef == null)
                    researchDef = Traverse.Create(research).Property("Def").GetValue<BaseDef>(); // fallback
            }
            catch (System.Exception e) { Debug.LogError($"[AAP TRACE] Error getting def: {e.Message}"); }

            if (researchDef == null)
            {
                Debug.Log("[AAP TRACE] Resolved Def is NULL. No match possible.");
                return;
            }

            string researchName = researchDef.name;
            Debug.Log($"[AAP TRACE] Research def name = `{researchName}`");

            if (researchName == MindfraggerResearch)
            {
                PsychicBuffManager.MindfraggerResearchCompleted = true;
                Debug.Log("[AAP] Psychic buff: Mindfragger research completed.");
            }
            else if (researchName == PsychicAttackResearch)
            {
                PsychicBuffManager.PsychicInfluencesCompleted = true;
                Debug.Log("[AAP] Psychic buff: Psychic Attack research completed.");
            }
        }
    }
}