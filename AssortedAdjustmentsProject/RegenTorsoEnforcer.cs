using HarmonyLib;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    // Forces the regen torso to always be allowed to heal even when the actor is off‑map (in a vehicle)
    [HarmonyPatch(typeof(PassiveModifierAbility), "CanApplyToOffMapTarget", MethodType.Getter)]
    public static class RegenTorso_CanHealInVehicle
    {
        static void Postfix(PassiveModifierAbility __instance, ref bool __result)
        {
            if (__instance.TacticalAbilityDef?.name == "Regeneration_Torso_Passive_AbilityDef")
                __result = true;
        }
    }
}