using System;
using System.Collections.Generic;
using System.Linq;
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
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Plentiful item drops, ported to native Workshop infrastructure from Mad's
    /// AssortedAdjustments (Modnix era). AAP 1.1 NOTE: the old version tried to
    /// write loot settings (AlwaysRecoverAllItemsFromTacticalMissions & co.) onto
    /// a scanned ScriptableObject or an unrelated Ambush def - those fields do
    /// not exist on any game class, so it never did anything. The real mechanics
    /// are driven by DieAbility at actor death; these Harmony patches implement:
    ///   - weapons: destroy chance 30 flat, or health-based (100 - health%)
    ///   - armor:   dropped on death with 70% destruction chance instead of never
    ///   - other:   10% destruction chance
    /// plus duplicate-prevention for dead squad members' armour.
    /// Values follow the AAP Workshop feature list; toggle in mod options.
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

        public static void Prefix(DieAbility __instance, TacticalItem item)
        {
            try
            {
                if (item?.TacticalItemDef == null) return;

                if (item.TacticalItemDef is WeaponDef)
                {
                    if (LootMechanics.HealthBasedWeaponDestruction)
                    {
                        float currentHealth = item.GetHealth().IntValue;
                        float maxHealth = item.GetHealth().IntMax;
                        int healthPercent = maxHealth > 0 ? (int)((currentHealth / maxHealth) * 100) : 0;
                        item.TacticalItemDef.DestroyOnActorDeathPerc = Mathf.Clamp(100 - healthPercent, 0, 100);
                    }
                    else
                    {
                        item.TacticalItemDef.DestroyOnActorDeathPerc = LootMechanics.FlatWeaponDestructionChance;
                    }
                }
                else
                {
                    item.TacticalItemDef.DestroyOnActorDeathPerc = LootMechanics.ItemDestructionChance;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AAP] ShouldDestroyItem patch failed: {e.Message}");
            }
        }
    }

    // Drop armour too
    [HarmonyPatch(typeof(DieAbility), "DropItems")]
    public static class DieAbility_DropItems_Patch
    {
        public static bool Prepare()
        {
            return ModMain.Cfg?.EnablePlentifulDrops != false;
        }

        public static void Postfix(DieAbility __instance)
        {
            try
            {
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
                        int randomPercent = UnityEngine.Random.Range(0, 101);
                        bool willDrop = randomPercent > LootMechanics.FlatArmorDestructionChance;
                        if (willDrop)
                        {
                            item.Drop(sharedData.FallDownItemContainerDef, actor);
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
                // Armour of dead squaddies was already dropped in tactical
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
