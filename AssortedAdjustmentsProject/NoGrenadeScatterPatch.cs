using Base.Core;
using Base.Defs;
using HarmonyLib;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Tactical.Entities.Weapons;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// [AAP] Removes the aim-point scatter of hand-thrown grenades, and of nothing else.
    ///
    /// Seam analysis and design by external auditor; AAP adaptation notes:
    ///   - Reads ModMain.Cfg live (same idiom as DeploymentCap_IncreaseMaxUnits), so there
    ///     are NO static-flag sync points: OnModEnabled needs no wiring (PatchAll applies
    ///     this attribute-annotated patch automatically), OnConfigChanged flips take effect
    ///     immediately, and OnModDisabled's UnpatchAll stands the patch down entirely.
    ///
    /// Seam: Weapon.GetWeaponSpread
    ///   public float GetWeaponSpread(AttackType attackType, float spreadMultiplier,
    ///                                float actorAccuracyMultiplier, float targetDistance = -1f)
    ///   A hand throw goes through it as AttackType.Regular via DefaultShootAbility (there is
    ///   no separate throw ability in the assembly). Firing, the AI and the UI aim preview all
    ///   read this one method, so one Postfix keeps prediction and reality in sync: the aim
    ///   preview circle collapses in step with the real behaviour.
    ///
    /// Selection is by item tag, not by delivery type: Tags is a GameTagsList on AddonDef,
    /// inherited by WeaponDef, and TFTV picks hand grenades with exactly
    /// weaponDef.Tags.Contains(grenadeTag). Filtering on DamageDeliveryType would also catch
    /// launchers, rockets and mortars.
    ///
    /// GrenadeItem_TagDef alone is slightly too wide: 13 vanilla WeaponDefs carry it, and two
    /// of them (Crabman_LeftHand_Grenade_WeaponDef, Crabman_LeftHand_Acid_Grenade_WeaponDef)
    /// are biological grenade ARMS, not carried items. All 11 real hand grenades also carry
    /// StandaloneItem_TagDef, and neither Crabman arm does, so the two tags together are the
    /// exact "hand-thrown grenade item" set.
    /// </summary>
    [HarmonyPatch(typeof(Weapon), nameof(Weapon.GetWeaponSpread))]
    internal static class NoGrenadeScatterPatch
    {
        // GrenadeItem_TagDef and StandaloneItem_TagDef. Resolved once, on first use, via
        // DefRepository.GetDef(string guid) (DefRepository.cs:70); the repo is a game
        // component (GameUtl.GameComponent<DefRepository>()).
        private const string GrenadeTagGuid = "318dd3ff-28f0-1bb4-98bc-39164b7292b6";
        private const string StandaloneTagGuid = "a6b28f12-9ddf-c2f4-4aba-021d89c51b78";
        private static GameTagDef _grenadeTag;
        private static GameTagDef _standaloneTag;
        private static bool _tagLookupDone;

        private static void ResolveTags()
        {
            if (_tagLookupDone) return;
            _tagLookupDone = true;
            DefRepository repo = GameUtl.GameComponent<DefRepository>();
            _grenadeTag = repo?.GetDef(GrenadeTagGuid) as GameTagDef;
            _standaloneTag = repo?.GetDef(StandaloneTagGuid) as GameTagDef;
            if (_grenadeTag == null || _standaloneTag == null)
            {
                // Either tag missing (unexpected game version) -> patch stays a no-op.
                // Never widen the filter. Logged once so Player.log diagnostics can see it.
                Debug.LogWarning("[AAP] NoGrenadeScatter: tag lookup failed - patch stays inactive "
                    + "(GrenadeItem/StandaloneItem tag defs not found in this game version).");
            }
            else
            {
                Debug.Log("[AAP] NoGrenadeScatter: tags resolved - patch active (config: "
                    + (ModMain.Cfg?.DisableGrenadeScatter == true ? "ON" : "OFF") + ").");
            }
        }

        // Priority.Last: another mod's Postfix on the same method can also write __result, and
        // the last one to run wins. Running last keeps the zero. (Caveat, same as the original
        // audit: an equal-priority postfix, or one ordered explicitly with HarmonyAfter, can
        // still run later and change the result.)
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Weapon __instance, ref float __result)
        {
            try
            {
                // Live config read (DeploymentCap idiom): default off = vanilla scatter.
                // Flipping the checkbox applies immediately - no restart, no reload.
                if (ModMain.Cfg?.DisableGrenadeScatter != true || __result == 0f) return;

                ResolveTags();
                // Either tag missing (unexpected game version) -> do nothing. Never widen.
                if (_grenadeTag == null || _standaloneTag == null) return;

                WeaponDef def = __instance.WeaponDef;
                if (def?.Tags != null && def.Tags.Contains(_grenadeTag) && def.Tags.Contains(_standaloneTag))
                {
                    __result = 0f;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AAP] NoGrenadeScatterPatch failed: {e.Message}");
            }
        }
    }
}
