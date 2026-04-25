using Base.Core;
using Base.Defs;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Tactical.Entities.Abilities;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class SniperPrecisionShotAbility
    {
        private const string PrecisionShotGuid = "e1e2a3b4-c5d6-4e7f-8901-234567abcdef";

        // Store as the correct type so we can instantiate a ShootAbility later
        public static ShootAbilityDef PrecisionShotDef { get; private set; }

        public static void Apply(DefCache cache)
        {
            var repo = GameUtl.GameComponent<DefRepository>();

            var shootAbility = cache.GetDef<TacticalAbilityDef>("Shoot_AbilityDef");
            if (shootAbility == null)
            {
                Debug.LogWarning("[AAP] Precision Shot: Shoot_AbilityDef not found.");
                return;
            }

            // Clone as ShootAbilityDef – CreateDefFromClone returns the same type
            PrecisionShotDef = Helpers.CreateDefFromClone(shootAbility, PrecisionShotGuid, "AAP_PrecisionShot_AbilityDef") as ShootAbilityDef;
            if (PrecisionShotDef == null)
            {
                Debug.LogWarning("[AAP] Precision Shot: clone creation failed.");
                return;
            }

            var t = Traverse.Create(PrecisionShotDef);
            t.Property("ActionPointCost")?.SetValue(0f);
            t.Property("WillPointCost")?.SetValue(3);
            t.Field("MaxUsesPerTurn")?.SetValue(1);

            // Rename for UI
            var view = PrecisionShotDef.ViewElementDef;
            if (view != null)
            {
                view.DisplayName1 = new LocalizedTextBind("AAP_PrecisionShot_DisplayName");
            }

            Debug.Log("[AAP] Precision Shot ability created (0 AP, 3 WP, once per turn).");
        }
    }
}