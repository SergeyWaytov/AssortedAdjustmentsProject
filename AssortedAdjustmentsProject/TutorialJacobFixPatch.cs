using HarmonyLib;
using System;
using UnityEngine;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Common.Entities.GameTags;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    // AAP T9: this postfix must run BEFORE PrecisionShot_ApplyToPhoenixSnipers
    // (Priority.Low). Jacob's runtime class-tag swap creates the Sniper tag
    // that the PrecisionShot patch inspects. Priority.High.
    [HarmonyPatch(typeof(TacticalActor), "ProcessInstanceData")]
    public static class TutorialJacobFixPatch
    {
        // AAP Q4-A: this is the deliberate fallback path. It runs when the
        // Jacob template tag isn't already set at template-load time. Log
        // when the fallback triggers so it's visible in Player.log.
        [HarmonyPriority(Priority.High)]
        [HarmonyPostfix]
        public static void Postfix(TacticalActor __instance)
        {
            if (__instance.GameTags == null) return;

            bool isJacob = false, isAssault = false;
            foreach (var t in __instance.GameTags)
            {
                if (t.name == "Jacob_GameTagDef") isJacob = true;
                if (t.name == "Assault_ClassTagDef") isAssault = true;
            }
            if (!isJacob || !isAssault) return;

            var cache = ModMain.DefCache;
            if (cache == null) return;

            // Swap class tag
            var assaultTag = cache.GetDef<GameTagDef>("Assault_ClassTagDef");
            var sniperTag = cache.GetDef<GameTagDef>("Sniper_ClassTagDef");
            if (assaultTag != null)
            {
                var removeList = new GameTagsList();
                removeList.Add(assaultTag);
                __instance.RemoveGameTags(removeList);
            }
            if (sniperTag != null)
            {
                var addList = new GameTagsList();
                addList.Add(sniperTag);
                __instance.AddGameTags(addList);
            }

            // Q4-A: deliberate fallback for tutorial; logs when it acts.
            Debug.Log($"[AAP] Tutorial fallback fired: swapped Jacob's class tag from Assault to Sniper for actor {__instance.DisplayName} (template-load swap unavailable).");
        }
    }
}
