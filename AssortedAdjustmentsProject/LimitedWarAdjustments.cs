using Base.UI;
using HarmonyLib;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Entities.Missions;
using PhoenixPoint.Geoscape.Entities.Sites;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Limited War, ported to native Workshop infrastructure from Sheepy's
    /// Modnix-era Limited War (the attached Nexus DLL) via Mad's AssortedAdjustments
    /// adaptation (updated patch targets, verified against the current game
    /// assembly). All patch targets and member names were re-verified in the
    /// decompiled Assembly-CSharp before porting.
    ///
    /// Effects (each gated by mod options, which require a game restart):
    ///  - Zoned attacks: a lost haven defense destroys only the attacked zone
    ///    instead of the whole haven (faction attacks by default; Pandorans
    ///    optionally), with geoscape log entries renamed accordingly.
    ///  - Attack limits: stop one-sided wars (same faction attacking twice in a
    ///    row), per-faction and global concurrent attack limits, and no new
    ///    attacks while already defending against a Pandoran siege.
    ///  - Alertness: lost havens (or their whole faction) raise alertness.
    ///  - Defense multipliers: Mad's defaults - alert x1.2, high alert x1.1,
    ///    attacker Pandoran x1.2, defender Anu x1.2, defender Synedrion x1.2.
    ///  - Optionally disable Pandoran attacks on Phoenix bases entirely.
    /// Phoenix itself is never limited - only AI-vs-AI warring is curbed.
    /// </summary>
    internal static class LimitedWar
    {
        internal static bool Enabled => ModMain.Cfg?.EnableLimitedWar == true;
        internal static bool ZonedFactionAttacks => ModMain.Cfg?.LWZonedFactionAttacks == true;
        internal static bool ZonedPandoranAttacks => ModMain.Cfg?.LWZonedPandoranAttacks == true;
        internal static bool RaiseAlertness => ModMain.Cfg?.LWAttacksRaiseAlertness == true;
        internal static bool StopOneSidedWar => ModMain.Cfg?.LWStopOneSidedWar == true;
        internal static bool DisablePandoranBaseAttacks => ModMain.Cfg?.LWDisablePandoranBaseAttacks == true;
        internal static int GlobalAttackLimit => ModMain.Cfg?.LWGlobalAttackLimit ?? 3;
        internal static int FactionAttackLimit => ModMain.Cfg?.LWFactionAttackLimit ?? 2;
        internal static int SiegeProtectionLimit => ModMain.Cfg?.LWSiegeProtectionLimit ?? 1;
        internal static bool ZoningActive => ZonedFactionAttacks || ZonedPandoranAttacks;
        internal static bool AttackLimitsActive => StopOneSidedWar || GlobalAttackLimit >= 0 || FactionAttackLimit >= 0 || SiegeProtectionLimit >= 0;

        // Mad's defense multiplier defaults (kept in code to avoid flooding the options screen)
        internal static readonly float DefMultAlert = 1.2f;
        internal static readonly float DefMultHighAlert = 1.1f;
        internal static readonly float DefMultAttackerPandora = 1.2f;
        internal static readonly float DefMultDefenderAnu = 1.2f;
        internal static readonly float DefMultDefenderSynedrion = 1.2f;

        public static void Apply(DefCache cache)
        {
            if (!Enabled)
            {
                Debug.Log("[AAP][LW] Limited War disabled in mod options.");
                return;
            }
            Debug.Log($"[AAP][LW] Limited War enabled: zoned attacks (faction={ZonedFactionAttacks}, pandoran={ZonedPandoranAttacks}), " +
                      $"one-sided war stop={StopOneSidedWar}, global limit={GlobalAttackLimit}, faction limit={FactionAttackLimit}, " +
                      $"siege protection={SiegeProtectionLimit}, alertness raise={RaiseAlertness}, " +
                      $"pandoran base attacks disabled={DisablePandoranBaseAttacks}.");
        }

        private static void Log(string msg) => Debug.Log($"[AAP][LW] {msg}");
        private static void LogError(Exception e) => Debug.LogError($"[AAP][LW] {e}");

        // ── Shared state ─────────────────────────────────────────────
        internal static class Store
        {
            internal static int GameDifficulty = 1; // Veteran fallback
            internal static IGeoFactionMissionParticipant LastAttacker;
            internal static GeoHavenDefenseMission DefenseMission;
        }

        // ── Resolver ─────────────────────────────────────────────────
        internal static class Resolver
        {
            internal static bool IsAlien(IGeoFactionMissionParticipant f) => f is GeoAlienFaction;
            internal static bool IsPhoenix(IGeoFactionMissionParticipant f) => f is GeoPhoenixFaction;
            internal static bool IsAlienOrPhoenix(IGeoFactionMissionParticipant f) => IsAlien(f) || IsPhoenix(f);

            internal static bool IsLimitedToZoneDamage(IGeoFactionMissionParticipant attacker)
            {
                return !IsPhoenix(attacker) &&
                       ((ZonedPandoranAttacks && IsAlien(attacker)) || (ZonedFactionAttacks && !IsAlien(attacker)));
            }

            internal static bool CanDestroyHavens(IGeoFactionMissionParticipant attacker) => !IsLimitedToZoneDamage(attacker);

            internal static bool HasReachedAttackLimits(GeoLevelController geoLevel, IGeoFactionMissionParticipant attacker)
            {
                try
                {
                    if (geoLevel?.Map == null || IsAlienOrPhoenix(attacker)) return false;

                    int havensUnderAttackByFactions = 0;
                    int ownHavensUnderAttackByPandorans = 0;
                    int havensUnderAttackByOwnFaction = 0;

                    foreach (GeoSite geoSite in geoLevel.Map.AllSites)
                    {
                        if (geoSite.ActiveMission is GeoHavenDefenseMission defense)
                        {
                            IGeoFactionMissionParticipant enemy = defense.GetEnemyFaction();
                            if (IsAlien(enemy))
                            {
                                if (geoSite.Owner == attacker) ownHavensUnderAttackByPandorans++;
                            }
                            else
                            {
                                havensUnderAttackByFactions++;
                            }
                            if (enemy == attacker) havensUnderAttackByOwnFaction++;
                        }
                    }

                    if (SiegeProtectionLimit >= 0 && ownHavensUnderAttackByPandorans >= SiegeProtectionLimit)
                    {
                        Log($"Siege protection: {attacker.GetPPName()} defends {ownHavensUnderAttackByPandorans} own haven(s) against Pandorans - attack cancelled.");
                        return true;
                    }
                    if (GlobalAttackLimit >= 0 && havensUnderAttackByFactions >= GlobalAttackLimit)
                    {
                        Log($"Global attack limit reached ({havensUnderAttackByFactions}/{GlobalAttackLimit}) - {attacker.GetPPName()} attack cancelled.");
                        return true;
                    }
                    if (FactionAttackLimit >= 0 && havensUnderAttackByOwnFaction >= FactionAttackLimit)
                    {
                        Log($"Faction attack limit reached ({havensUnderAttackByOwnFaction}/{FactionAttackLimit}) - {attacker.GetPPName()} attack cancelled.");
                        return true;
                    }
                    return false;
                }
                catch (Exception e) { LogError(e); return false; }
            }

            internal static bool ShouldCancelAttack(GeoLevelController geoLevel, IGeoFactionMissionParticipant attacker)
            {
                try
                {
                    if (StopOneSidedWar && Store.LastAttacker != null && attacker == Store.LastAttacker)
                    {
                        Log($"One-sided war stopped: {attacker.GetPPName()} was the most recent aggressor.");
                        return true;
                    }
                    return HasReachedAttackLimits(geoLevel, attacker);
                }
                catch (Exception e) { LogError(e); return false; }
            }
        }

        internal static string ToTitleCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        // ── Store mission for other patches ──────────────────────────
        [HarmonyPatch(typeof(GeoHavenDefenseMission), "UpdateGeoscapeMissionState")]
        public static class GeoHavenDefenseMission_UpdateGeoscapeMissionState_Patch
        {
            public static bool Prepare() => Enabled;

            public static void Prefix(GeoHavenDefenseMission __instance) => Store.DefenseMission = __instance;
            public static void Postfix() => Store.DefenseMission = null;
        }

        // ── Zoned attacks: convert haven destruction to zone destruction ──
        [HarmonyPatch(typeof(GeoSite), "DestroySite")]
        public static class GeoSite_DestroySite_Patch_ConvertDestruction
        {
            public static bool Prepare() => Enabled && ZoningActive;

            public static bool Prefix(GeoSite __instance)
            {
                try
                {
                    if (Store.DefenseMission == null) return true;

                    IGeoFactionMissionParticipant attacker = Store.DefenseMission.GetEnemyFaction();
                    if (Resolver.CanDestroyHavens(attacker)) return true;

                    GeoHavenZone zone = Store.DefenseMission.AttackedZone;
                    zone.AddDamage(zone.Health.IntValue);
                    zone.AddProduction(0);
                    GeoHaven haven = zone.Haven;
                    Log($"Fall of {__instance.Name} converted to '{zone.Def.ViewElementDef.DisplayName1.LocalizeEnglish()}' destruction.");

                    if (haven != null)
                    {
                        if ((zone.Def.ProvidesRecruitment || zone.Def.ProvidesEliteRecruitment) && haven.AvailableRecruit != null)
                        {
                            haven.RemoveRecruit();
                        }
                        haven.ZonesStats.UpdateZonesStats();
                    }

                    __instance.RefreshVisuals();
                    return false;
                }
                catch (Exception e) { LogError(e); return true; }
            }
        }

        // ── Zoned attacks: expand haven name with zone name in log ───
        [HarmonyPatch(typeof(GeoscapeLog), "Map_SiteMissionStarted")]
        public static class GeoscapeLog_Map_SiteMissionStarted_Patch
        {
            public static bool Prepare() => Enabled && ZoningActive;

            public static void Postfix(GeoSite site, GeoMission mission, List<GeoscapeLogEntry> ____entries)
            {
                try
                {
                    if (!(mission is GeoHavenDefenseMission) || Store.DefenseMission == null) return;

                    IGeoFactionMissionParticipant attacker = Store.DefenseMission.GetEnemyFaction();
                    if (Resolver.CanDestroyHavens(attacker)) return;

                    LocalizedTextBind zoneName = Store.DefenseMission.AttackedZone?.Def?.ViewElementDef?.DisplayName1;
                    if (zoneName == null || ____entries == null || ____entries.Count < 1) return;

                    GeoscapeLogEntry entry = ____entries[____entries.Count - 1];
                    entry.Parameters[0] = new LocalizedTextBind($"{site.Name} ({ToTitleCase(zoneName.Localize())})", true);
                    Log("Invasion log entry renamed to zone invasion.");
                }
                catch (Exception e) { LogError(e); }
            }
        }

        // ── Zoned attacks: mission-end log entry + no destruction sound ──
        [HarmonyPatch(typeof(GeoscapeLog), "Map_SiteMissionEnded")]
        public static class GeoscapeLog_Map_SiteMissionEnded_Patch
        {
            public static bool Prepare() => Enabled && ZoningActive;

            public static bool Prefix(GeoscapeLog __instance, GeoSite site, GeoMission mission,
                GeoLevelController ____level, GeoscapeLogMessagesDef ____messagesDef, GeoFaction ____faction)
            {
                try
                {
                    if (!site.GetInspected(____faction)) return false;
                    if (!(mission is GeoHavenDefenseMission defense) || Store.DefenseMission == null) return true;

                    IGeoFactionMissionParticipant attacker = ____level.GetFactionMissionParticipant(defense.AttackerFaction);
                    if (Resolver.CanDestroyHavens(attacker)) return true;

                    LocalizedTextBind zoneName = defense.AttackedZone?.Def?.ViewElementDef?.DisplayName1;
                    if (zoneName == null) return true;

                    bool attackersWon = defense.Status == GeoscapeMissionStatus.AttackersWon;
                    GeoscapeLogEntry entry = new GeoscapeLogEntry
                    {
                        Text = attackersWon ? ____messagesDef.HavenDestroyedMessage : ____messagesDef.HavenRepelledAttackMessage,
                        Parameters = new LocalizedTextBind[]
                        {
                            new LocalizedTextBind($"{site.Name} ({ToTitleCase(zoneName.Localize())})", true),
                            attacker.ParticipantName
                        }
                    };
                    typeof(GeoscapeLog).GetMethod("AddEntry", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.Invoke(__instance, new object[] { entry, site });
                    return false;
                }
                catch (Exception e) { LogError(e); return true; }
            }
        }

        // ── Alertness: raise after lost havens ───────────────────────
        [HarmonyPatch(typeof(GeoSite), "DestroySite")]
        public static class GeoSite_DestroySite_Patch_RaiseAlertness
        {
            public static bool Prepare() => Enabled && RaiseAlertness;

            public static void Postfix(GeoSite __instance)
            {
                try
                {
                    GeoHaven haven = Store.DefenseMission?.Haven;
                    GeoFaction owner = haven?.Site?.Owner;
                    if (haven == null || owner == null || Resolver.IsAlienOrPhoenix(owner)) return;

                    // Raise alertness across the whole losing faction
                    foreach (GeoHaven h in owner.Havens)
                    {
                        h.IncreaseAlertness();
                    }
                    Log($"{haven.Site.Name} has lost. Alertness raised for all {owner.GetPPName()} havens.");
                }
                catch (Exception e) { LogError(e); }
            }
        }

        // ── Attack limits: track difficulty + reset last attacker ────
        [HarmonyPatch(typeof(GeoLevelController), "OnLevelStart")]
        public static class GeoLevelController_OnLevelStart_Patch
        {
            public static bool Prepare() => Enabled && AttackLimitsActive;

            public static void Postfix(GeoLevelController __instance)
            {
                try
                {
                    Store.GameDifficulty = __instance.DynamicDifficultySystem.DifficultyLevels.ToList().IndexOf(__instance.CurrentDifficultyLevel);
                    Store.LastAttacker = null;
                    Log($"Last attacker reset. Difficulty level index: {Store.GameDifficulty}.");
                }
                catch (Exception e) { LogError(e); }
            }
        }

        // ── Attack limits: block forbidden attacks ───────────────────
        [HarmonyPatch(typeof(GeoFaction), "AttackHavenFromVehicle")]
        public static class GeoFaction_AttackHavenFromVehicle_Patch
        {
            public static bool Prepare() => Enabled && AttackLimitsActive;

            public static bool Prefix(GeoFaction __instance, GeoVehicle vehicle, GeoSite site, GeoLevelController ____level)
            {
                try
                {
                    if (Resolver.ShouldCancelAttack(____level, vehicle?.Owner))
                    {
                        Log($"{__instance.Name.Localize()} attack on {site.Name} prevented.");
                        return false;
                    }
                    Store.LastAttacker = vehicle.Owner;
                    return true;
                }
                catch (Exception e) { LogError(e); return true; }
            }
        }

        // ── Attack limits: discourage war navigation ─────────────────
        [HarmonyPatch(typeof(VehicleFactionController), "GetSiteVehicleDestinationWeight")]
        public static class VehicleFactionController_GetSiteVehicleDestinationWeight_Patch
        {
            public static bool Prepare() => Enabled && AttackLimitsActive;

            public static void Prefix(VehicleFactionController __instance, ref float? __state)
            {
                try
                {
                    if (Resolver.ShouldCancelAttack(__instance.Vehicle?.GeoLevel, __instance.Vehicle?.Owner))
                    {
                        __state = __instance.ControllerDef.FactionInWarWeightMultiplier;
                        __instance.ControllerDef.FactionInWarWeightMultiplier = -2f;
                    }
                    else
                    {
                        __state = null;
                    }
                }
                catch (Exception e) { LogError(e); }
            }

            public static void Postfix(VehicleFactionController __instance, float? __state)
            {
                if (__state.HasValue)
                {
                    __instance.ControllerDef.FactionInWarWeightMultiplier = __state.Value;
                }
            }
        }

        // ── Defense multipliers ──────────────────────────────────────
        [HarmonyPatch(typeof(GeoHavenDefenseMission), "GetDefenseDeployment")]
        public static class GeoHavenDefenseMission_GetDefenseDeployment_Patch
        {
            public static bool Prepare() => Enabled;

            public static void Postfix(GeoHavenDefenseMission __instance, ref int __result, GeoHaven haven)
            {
                try
                {
                    if (haven == null) return;

                    GeoFaction attacker = __instance.GetEnemyFaction() is GeoSubFaction sub ? sub.BaseFaction : __instance.GetEnemyFaction() as GeoFaction;
                    GeoFaction defender = haven.Site.Owner;

                    float multiply = 1f;
                    if (haven.AlertLevel == GeoHaven.HavenAlertLevel.Alert) multiply *= DefMultAlert;
                    else if (haven.AlertLevel == GeoHaven.HavenAlertLevel.HighAlert) multiply *= DefMultHighAlert;

                    GeoLevelController geoLevel = haven.Site.GeoLevel;
                    if (Resolver.IsAlien(attacker)) multiply *= DefMultAttackerPandora;
                    if (defender == geoLevel.AnuFaction) multiply *= DefMultDefenderAnu;
                    else if (defender == geoLevel.SynedrionFaction) multiply *= DefMultDefenderSynedrion;

                    if (multiply != 1f)
                    {
                        int before = __result;
                        __result = (int)Math.Round(__result * multiply);
                        Log($"{haven.Site.Name} defense strength {before} x {multiply} -> {__result} (attacker: {attacker?.GetPPName()}).");
                    }
                }
                catch (Exception e) { LogError(e); }
            }
        }

        // ── Optionally disable Pandoran attacks on Phoenix bases ────
        [HarmonyPatch(typeof(GeoAlienFaction), "AttackPhoenixBase")]
        public static class GeoAlienFaction_AttackPhoenixBase_Patch
        {
            public static bool Prepare() => Enabled && DisablePandoranBaseAttacks;
            public static bool Prefix() => false;
        }

        [HarmonyPatch(typeof(GeoAlienFaction), "StartPhoenixBaseAssault")]
        public static class GeoAlienFaction_StartPhoenixBaseAssault_Patch
        {
            public static bool Prepare() => Enabled && DisablePandoranBaseAttacks;
            public static bool Prefix() => false;
        }
    }
}
