using Base.Core;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Statuses;
using System.Linq;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch(typeof(TacticalAbility), "Activate")]
    public static class SquadEvacPatch
    {
        static void Prefix(TacticalAbility __instance)
        {
            if (__instance.TacticalAbilityDef.name != "ExitMission_AbilityDef")
                return;
            if (!__instance.TacticalActor.TacticalFaction.IsControlledByPlayer)
                return;

            var faction = __instance.TacticalActor.TacticalFaction;
            foreach (var actor in faction.GetOwnedActors<TacticalActor>().ToList())
            {
                if (actor == __instance.TacticalActor) continue;
                if (actor.IsMounted) continue;
                if (actor.Status.HasStatus<EvacuatedStatus>()) continue;

                // Try to get the evacuate ability by def, then activate it
                var exitAbility = actor.GetAbilities<TacticalAbility>()
                    .FirstOrDefault(ab => ab.TacticalAbilityDef.name == "ExitMission_AbilityDef");
                if (exitAbility != null && exitAbility.IsEnabled())
                    exitAbility.Activate();
            }
            // The original evac still happens after this prefix.
        }
    }
}