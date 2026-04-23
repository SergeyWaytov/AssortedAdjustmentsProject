using Base.Core;
using Base.UI.MessageBox;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class SquadEvacPatch
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("PhoenixPoint.Tactical.View.TacticalView");
            return AccessTools.Method(type, "OnAbilityExecuted");
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance, object ability)
        {
            try
            {
                // Check if the executed ability is ExitMission
                var abilityName = Traverse.Create(ability).Field("name").GetValue<string>();
                if (abilityName != "ExitMission_AbilityDef")
                    return true;

                var traverse = Traverse.Create(__instance);
                var actor = Traverse.Create(ability).Property("Actor").GetValue<object>();
                var squad = traverse.Property("CurrentSquad").GetValue<IEnumerable<object>>();

                if (squad == null)
                    return true;

                // Show confirmation dialog (optional)
                var messageBox = GameUtl.GetMessageBox();
                messageBox.ShowSimplePrompt(
                    "Evacuate entire squad?",
                    MessageBoxIcon.Question,
                    MessageBoxButtons.YesNo,
                    result =>
                    {
                        if (result.DialogResult == MessageBoxResult.Yes)
                        {
                            foreach (var member in squad)
                            {
                                var exitAbility = Traverse.Create(member).Method("GetAbility", new[] { typeof(string) })
                                    .GetValue<object>("ExitMission_AbilityDef");
                                if (exitAbility != null)
                                {
                                    Traverse.Create(exitAbility).Method("Activate").GetValue();
                                }
                            }
                        }
                    },
                    null
                );

                return false; // Skip original individual evacuation
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AAP] SquadEvacPatch failed: {e.Message}");
                return true;
            }
        }
    }
}