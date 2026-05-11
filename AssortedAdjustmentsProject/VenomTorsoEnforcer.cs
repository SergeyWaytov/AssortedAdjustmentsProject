using HarmonyLib;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.DamageKeywords;
using PhoenixPoint.Tactical.Entities.Weapons;
using System.Linq;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class VenomTorsoEnforcer
    {
        // ---- Infinite ammo ----
        [HarmonyPatch(typeof(WeaponDef), "get_ChargesMax")]
        public static class ChargesMax
        {
            static void Postfix(WeaponDef __instance, ref int __result)
            {
                if (__instance.name == "AN_Berserker_Shooter_LeftArm_WeaponDef")
                    __result = 0;
            }
        }

        // ---- Spread ----
        [HarmonyPatch(typeof(WeaponDef), "get_SpreadDegrees")]
        public static class Spread
        {
            static void Postfix(WeaponDef __instance, ref float __result)
            {
                if (__instance.name == "AN_Berserker_Shooter_LeftArm_WeaponDef")
                    __result = 1.5f;
            }
        }

        // ---- Force the exact damage keywords (50 poison, 25 piercing) ----
        [HarmonyPatch(typeof(Weapon), "get_DamagePayload")]
        public static class DamageKeywords
        {
            static void Postfix(Weapon __instance, ref DamagePayload __result)
            {
                if (__instance.WeaponDef.name != "AN_Berserker_Shooter_LeftArm_WeaponDef")
                    return;

                if (__result == null) return;

                // Poison keyword
                var poison = __result.DamageKeywords.FirstOrDefault(k =>
                    k.DamageKeywordDef.name == "Poison_DamageKeywordDataDef");
                if (poison != null)
                    poison.Value = 50f;

                // Piercing keyword
                var pierce = __result.DamageKeywords.FirstOrDefault(k =>
                    k.DamageKeywordDef.name == "Piercing_DamageKeywordDataDef");
                if (pierce != null)
                    pierce.Value = 25f;
            }
        }
    }
}