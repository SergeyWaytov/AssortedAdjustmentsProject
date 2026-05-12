using HarmonyLib;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using PhoenixPoint.Tactical.Entities.Weapons;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    // ==================== WEAPON STATS ====================
    public static class WeaponStatEnforcer
    {
        [HarmonyPatch(typeof(WeaponDef), "get_ActionPointCost")]
        public static class Weapon_AP
        {
            static void Postfix(WeaponDef __instance, ref float __result)
            {
                if (__instance.name == "Tyr1_Autocannon_WeaponDef") __result = 2f;
                else if (__instance.name == "NJ_VidarGL_WeaponDef") __result = 2f;
            }
        }

        [HarmonyPatch(typeof(WeaponDef), "get_EffectiveRange")]
        public static class Weapon_Range
        {
            static void Postfix(WeaponDef __instance, ref float __result)
            {
                switch (__instance.name)
                {
                    case "Tyr1_Autocannon_WeaponDef": __result = 25f; break;
                    case "KS_Obliterator_WeaponDef": __result = 40f; break;
                    case "KS_Subjector_WeaponDef": __result = 61f; break;
                    case "KS_Devastator_WeaponDef": __result = 25f; break;
                    case "KS_Tormentor_WeaponDef": __result = 29f; break;
                    case "FS_SlamstrikeShotgun_WeaponDef": __result = 22f; break;
                    case "FS_LightSniperRifle_WeaponDef": __result = 60f; break;
                }
            }
        }

        [HarmonyPatch(typeof(ShootAbility), "GetBaseDamage")]
        public static class Weapon_Damage
        {
            static void Postfix(ShootAbility __instance, ref float __result)
            {
                var id = __instance.Weapon?.WeaponDef?.name;
                if (id == "PX_AcidAssaultRifle_WeaponDef") __result = 40f;
                else if (id == "PX_PoisonMachineGun_WeaponDef") __result = 35f;
                else if (id == "FS_LightSniperRifle_WeaponDef") __result = 110f;
                else if (id == "AN_Berserker_Shooter_LeftArm_WeaponDef") __result = 50f;
            }
        }
    }

    // ==================== ARMOUR & ITEM STATS ====================
    public static class ArmorStatEnforcer
    {
        [HarmonyPatch(typeof(TacticalItemDef), "get_Armor")]
        public static class BodyPart_Armor
        {
            static void Postfix(TacticalItemDef __instance, ref int __result)
            {
                switch (__instance.name)
                {
                    case "NJ_Jugg_BIO_Helmet_BodyPartDef": __result = 30; break;
                    case "NJ_Heavy_Torso_BodyPartDef": __result = 45; break;
                    case "NJ_Heavy_Legs_ItemDef": __result = 40; break;
                    case "NJ_Exo_BIO_Helmet_BodyPartDef": __result = 20; break;
                    case "NJ_Exo_BIO_Torso_BodyPartDef": __result = 30; break;
                    case "NJ_Exo_BIO_Legs_ItemDef": __result = 20; break;
                    case "SY_Assault_Helmet_BodyPartDef": __result = 20; break;
                    case "SY_Assault_Torso_BodyPartDef": __result = 22; break;
                    case "SY_Assault_Legs_ItemDef": __result = 20; break;
                }
            }
        }

        [HarmonyPatch(typeof(TacticalItemDef), "get_Stealth")]
        public static class BodyPart_Stealth
        {
            static void Postfix(TacticalItemDef __instance, ref float __result)
            {
                switch (__instance.name)
                {
                    case "NJ_Jugg_BIO_Helmet_BodyPartDef": __result = -0.1f; break;
                    case "NJ_Heavy_Torso_BodyPartDef": __result = -0.2f; break;
                    case "NJ_Heavy_Legs_ItemDef": __result = -0.25f; break;
                    case "NJ_Exo_BIO_Helmet_BodyPartDef": __result = -0.05f; break;
                    case "NJ_Exo_BIO_Torso_BodyPartDef": __result = -0.15f; break;
                    case "NJ_Exo_BIO_Legs_ItemDef": __result = -0.1f; break;
                    case "SY_Assault_Helmet_BodyPartDef": __result = 0.1f; break;
                    case "SY_Assault_Torso_BodyPartDef": __result = 0.2f; break;
                    case "SY_Assault_Legs_ItemDef": __result = 0.2f; break;
                }
            }
        }

        [HarmonyPatch(typeof(TacticalItemDef), "get_Accuracy")]
        public static class BodyPart_Accuracy
        {
            static void Postfix(TacticalItemDef __instance, ref float __result)
            {
                switch (__instance.name)
                {
                    case "NJ_Jugg_BIO_Helmet_BodyPartDef": __result = 0.03f; break;
                    case "NJ_Heavy_Torso_BodyPartDef": __result = 0.01f; break;
                    case "NJ_Heavy_Legs_ItemDef": __result = 0.03f; break;
                    case "NJ_Exo_BIO_Helmet_BodyPartDef": __result = 0.12f; break;
                    case "NJ_Exo_BIO_Torso_BodyPartDef": __result = 0.08f; break;
                }
            }
        }

        [HarmonyPatch(typeof(TacticalItemDef), "get_Speed")]
        public static class BodyPart_Speed
        {
            static void Postfix(TacticalItemDef __instance, ref float __result)
            {
                switch (__instance.name)
                {
                    case "NJ_Heavy_Torso_BodyPartDef": __result = -1f; break;
                    case "NJ_Heavy_Legs_ItemDef": __result = -1f; break;
                    case "NJ_Exo_BIO_Legs_ItemDef": __result = 3f; break;
                    case "SY_Assault_Legs_ItemDef": __result = 1f; break;
                }
            }
        }
    }
}