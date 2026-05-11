using Base.Entities.Statuses;
using HarmonyLib;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Statuses;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch(typeof(DamageOverTimeStatus), "StartTurn")]
    public static class PoisonStartTurnEnforcer
    {
        static void Postfix(DamageOverTimeStatus __instance)
        {
            if (__instance.TacStatusDef?.name != "Poison_DamageOverTimeStatusDef") return;

            var stats = __instance.TacticalActor?.CharacterStats;
            if (stats == null) return;

            // Force -50% accuracy
            var accStat = stats.TryGetStat(StatModificationTarget.Accuracy);
            if (accStat != null)
            {
                accStat.RemoveStatModificationsWithSource(__instance.TacStatusDef, true);
                accStat.AddStatModification(new StatModification(
                    StatModificationType.Multiply,
                    StatModificationTarget.Accuracy.ToString(),
                    0.5f,
                    __instance.TacStatusDef,
                    0f), true);
            }

            // Force -3 WP per turn
            var wpStat = stats.TryGetStat(StatModificationTarget.WillPoints);
            if (wpStat != null)
            {
                wpStat.RemoveStatModificationsWithSource(__instance.TacStatusDef, true);
                wpStat.AddStatModification(new StatModification(
                    StatModificationType.Add,
                    StatModificationTarget.WillPoints.ToString(),
                    -3f,
                    __instance.TacStatusDef,
                    0f), true);
            }
        }
    }
}