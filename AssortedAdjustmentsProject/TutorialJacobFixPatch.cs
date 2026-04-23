using HarmonyLib;
using System;
using UnityEngine;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Common.Entities.GameTags;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch(typeof(TacticalActor), "ProcessInstanceData")]
    public static class TutorialJacobFixPatch
    {
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

            Debug.Log("[AAP] Swapped Jacob's class tag to Sniper.");
        }
    }
}