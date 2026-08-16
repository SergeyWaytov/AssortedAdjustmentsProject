// PsychicResearchConditions.cs – Final version (no text injection)
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Geoscape.Entities.Research;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using System;
using System.Linq;
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

    /// <summary>
    /// OnResearchCompleted only fires at the moment of completion - on saves where
    /// the researches were finished in earlier sessions (or before the mod was
    /// installed) the psychic gate flags never set and both buff stages stayed
    /// inactive. This retroactively activates the gates from the faction's
    /// completed-research list every time a geoscape game loads.
    /// </summary>
    [HarmonyPatch(typeof(GeoLevelController), "OnLevelStart")]
    public static class PsychicRetroactiveGates_Patch
    {
        static void Postfix(GeoLevelController __instance)
        {
            try
            {
                var phoenix = __instance?.PhoenixFaction;
                if (phoenix?.Research == null) return;

                bool mindDone = phoenix.Research.GetResearchesBy(r =>
                    r.State == ResearchState.Completed &&
                    r.ResearchDef != null &&
                    r.ResearchDef.name == PsychicResearchConditions.MindfraggerResearch).Any();

                bool psychicDone = phoenix.Research.GetResearchesBy(r =>
                    r.State == ResearchState.Completed &&
                    r.ResearchDef != null &&
                    r.ResearchDef.name == PsychicResearchConditions.PsychicAttackResearch).Any();

                if (mindDone && !PsychicBuffManager.MindfraggerResearchCompleted)
                {
                    PsychicBuffManager.MindfraggerResearchCompleted = true;
                    Debug.Log("[AAP] Psychic buff: Mindfragger research detected as already completed (retroactive activation).");
                }
                if (psychicDone && !PsychicBuffManager.PsychicInfluencesCompleted)
                {
                    PsychicBuffManager.PsychicInfluencesCompleted = true;
                    Debug.Log("[AAP] Psychic buff: Psychic Attack research detected as already completed (retroactive activation).");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] Psychic retroactive gates failed: {e.Message}");
            }
        }
    }
}
