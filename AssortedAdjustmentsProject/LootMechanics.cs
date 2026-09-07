using Base;
using Base.Core;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsSharedData;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using PhoenixPoint.Tactical.Entities.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Plentiful item drops, ported to native Workshop infrastructure from Mad's
    /// AssortedAdjustments (Modnix era). AAP 1.1 NOTE: the old version tried to
    /// write loot settings (AlwaysRecoverAllItemsFromTacticalMissions &amp; co.) onto
    /// a scanned ScriptableObject or an unrelated Ambush def - those fields do
    /// not exist on any game class, so it never did anything. The real mechanics
    /// are driven by DieAbility at actor death; these Harmony patches implement:
    ///   - weapons: destroy chance 30 flat, or health-based (100 - health%)
    ///   - armor:   dropped on death with 70% destruction chance instead of never
    ///   - other:   10% destruction chance
    /// plus duplicate-prevention for dead squad members' armour.
    /// Values follow the AAP Workshop feature list; toggle in mod options.
    /// AAP R1 fix: per-item weapon destruction writes to the SHARED def
    /// (item.TacticalItemDef.DestroyOnActorDeathPerc) so the chance is
    /// computed per-call and never written back. The 100-healthPercent
    /// formula stays; only the write TARGET was wrong.
    /// AAP R2 (Option A): KIA armour is owned by the player -- the dead
    /// squad member's armour goes to the ground unconditionally. Vanilla
    /// recovery (GetDeadSquadMembersArmour) is blanked because the
    /// DropItems postfix already dropped it; this preserves dup-prevention.
    /// AAP R3: HulkDieAbility:DropItems is also patched (mounted inventory
    /// branch) so hulk deaths don't bypass the armour-drop path.
    /// AAP G1 (live-guard): Prepare() stays false when the toggle is off,
    /// AND the prefix bodies re-check the toggle so a config change does
    /// not require a restart (Prepare() latches the patch in).
    /// </summary>
    public static class LootMechanics
    {
        public const int ItemDestructionChance = 10;
        public const int FlatWeaponDestructionChance = 30;
        public const int FlatArmorDestructionChance = 70;
        public static readonly bool HealthBasedWeaponDestruction = true;

        public static void Apply(DefCache cache)
        {
            // Patches are applied via harmony.PatchAll(); config gate lives in Prepare().
            Debug.Log(ModMain.Cfg?.EnablePlentifulDrops != false
                ? "[AAP] Plentiful item drops enabled (weapons 30% or health-based, armor 70%, other 10%)."
                : "[AAP] Plentiful item drops disabled in config.");
        }
    }

    [HarmonyPatch(typeof(DieAbility), "ShouldDestroyItem")]
    public static class DieAbility_ShouldDestroyItem_Patch
    {
        public static bool Prepare()
        {
            return ModMain.Cfg?.EnablePlentifulDrops != false;
        }

        // AAP R1: rewrite the prefix so the chance is returned via __result
        // (return false) and the SHARED def's DestroyOnActorDeathPerc is never
        // written. The old code wrote 75->0 on four WeaponDefs at actor death,
        // permanently disabling destruction for every other actor in the save.
        [HarmonyPrefix]
        public static bool Prefix(DieAbility __instance, TacticalItem item, ref bool __result)
        {
            try
            {
                // AAP G1 (live-guard): re-check the toggle so changing it in
                // options doesn't require a restart (Prepare() latches).
                if (ModMain.Cfg?.EnablePlentifulDrops == false) return true;

                // Player-controlled actors never destroy their own gear.
                if (__instance.TacticalActor == null || __instance.TacticalActor.IsControlledByPlayer)
                {
                    __result = false;
                    return false;
                }
                if (item?.TacticalItemDef == null) return true;

                int chance;
                if (item.TacticalItemDef is WeaponDef)
                {
                    if (LootMechanics.HealthBasedWeaponDestruction)
                    {
                        float max = item.GetHealth().IntMax;
                        int healthPercent = max > 0f
                            ? (int)(item.GetHealth().IntValue / max * 100f)
                            : 0;
                        chance = Mathf.Clamp(100 - healthPercent, 0, 100);
                    }
                    else
                    {
                        chance = LootMechanics.FlatWeaponDestructionChance;
                    }
                }
                else
                {
                    chance = LootMechanics.ItemDestructionChance;
                }

                __result = __instance.TacticalActorBase.SharedData.Random.Range(0, 100) < chance;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] ShouldDestroyItem patch failed: {e}");
                return true;
            }
        }
    }

    // AAP R3: HulkDieAbility:DropItems is also patched, plus the standard
    // DieAbility:DropItems. The hulk death path otherwise bypasses the
    // armour-drop loop because hulk inventories live on the mount, not
    // the actor. The mounted-inventory branch moves items to the mount's
    // inventory when present, falls back to vanilla Drop otherwise.
    [HarmonyPatch]
    public static class DieAbility_DropItems_Patch
    {
        public static bool Prepare()
        {
            return ModMain.Cfg?.EnablePlentifulDrops != false;
        }

        // R3: target both DieAbility.DropItems and HulkDieAbility.DropItems.
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(DieAbility), "DropItems");
            MethodInfo hulk = AccessTools.Method(
                "PhoenixPoint.Tactical.Entities.Abilities.HulkDieAbility:DropItems");
            if (hulk != null) yield return hulk;
        }

        [HarmonyPostfix]
        public static void Postfix(DieAbility __instance)
        {
            try
            {
                // AAP G1 (live-guard): re-check the toggle.
                if (ModMain.Cfg?.EnablePlentifulDrops == false) return;

                TacticalActor actor = __instance.TacticalActor;
                if (actor == null) return;
                if (actor.DisplayName.IndexOf("decoy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (__instance.AbilityDef?.name?.IndexOf("decoy", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                {
                    return;
                }

                IEnumerable<TacticalItem> items = actor.BodyState?.GetArmourItems();
                if (items?.Any() != true)
                {
                    return;
                }

                SharedData sharedData = SharedData.GetSharedDataFromGame();
                SharedGameTagsDataDef sharedGameTags = sharedData.SharedGameTags;
                GameTagDef armor = sharedGameTags.ArmorTag, manufacturable = sharedGameTags.ManufacturableTag, mounted = sharedGameTags.MountedTag;

                int count = 0;
                foreach (TacticalItem item in items.ToList())
                {
                    TacticalItemDef def = item.TacticalItemDef;
                    GameTagsList tags = def?.Tags;
                    if (tags == null || tags.Count == 0 || !tags.Contains(manufacturable) || def.IsPermanentAugment)
                    {
                        continue;
                    }
                    if (tags.Contains(armor) || tags.Contains(mounted))
                    {
                        // AAP R2 (Option A): player-owned armour drops to the
                        // ground unconditionally; enemy armour still rolls 70%
                        // destruction. Player gear is thus guaranteed to reach
                        // GetItemsOnTheGround (vanilla recovery is blanked in
                        // the dead-squad patch below).
                        bool playerOwned = actor.IsControlledByPlayer;
                        bool willDrop = playerOwned ||
                            UnityEngine.Random.Range(0, 100) >= LootMechanics.FlatArmorDestructionChance;
                        if (willDrop)
                        {
                            // AAP R3: mounted inventory branch -- if the actor
                            // is a vehicle/mount, the item goes to the mount's
                            // inventory; otherwise vanilla Drop to the ground.
                            var mountInventory = __instance.TacticalActorBase.Mount?.TacticalActorBase?.Inventory;
                            if (mountInventory != null)
                            {
                                mountInventory.AddItem(item);
                            }
                            else
                            {
                                item.Drop(sharedData.FallDownItemContainerDef, actor);
                            }
                            count++;
                        }
                    }
                }

                if (count > 0)
                {
                    Debug.Log($"[AAP] Dropped {count} armour pieces from {actor.ViewElementDef?.Name}.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] DropItems patch failed: {e.Message}");
            }
        }
    }

    // Prevent dupes from squad member deaths
    [HarmonyPatch(typeof(GeoMission), "GetDeadSquadMembersArmour")]
    public static class GeoMission_GetDeadSquadMembersArmour_Patch
    {
        public static bool Prepare()
        {
            return ModMain.Cfg?.EnablePlentifulDrops != false;
        }

        // Override!
        public static bool Prefix(ref IEnumerable<GeoItem> __result)
        {
            try
            {
                // AAP G1 (live-guard): re-check the toggle.
                if (ModMain.Cfg?.EnablePlentifulDrops == false) return true;

                // R2-A: armour of dead squaddies was already dropped in tactical
                // (the DieAbility_DropItems postfix above moved player-owned
                // pieces to the ground unconditionally). Blank the vanilla
                // recovery list so it does not re-drop and duplicate.
                __result = Enumerable.Empty<GeoItem>();
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }
}
