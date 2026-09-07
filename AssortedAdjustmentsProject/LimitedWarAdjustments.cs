using Base.UI;
using HarmonyLib;
using I2.Loc;
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
    /// AAP G5-A: 0 = strictest (NormalizeLimit maps 0 -> 1; -1 stays as the
    /// off-switch). Descriptions in AAPConfig.cs already advertise this.
    /// AAP G6: both DestroySite patches check defense.Site == __instance
    /// identity, so a stray DestroySite call (e.g. world generation cleanup)
    /// doesn't run the zoned-destruction path against the wrong site.
    /// AAP G7: GetSiteVehicleDestinationWeight uses Prefix+Finalizer instead
    /// of Prefix+Postfix so the weight multiplier is restored even on throw.
    /// AAP G8: Map_SiteMissionStarted postfix uses typed GeoHavenDefenseMission
    /// access (no Store.DefenseMission null check needed).
    /// AAP G9-A: LastAttacker persists across save/load via the EventSystem
    /// variable AAP_LW_LastAttackerFactionIndex (1-based; 0 = no last attacker).
    /// AAP G11: char.ToUpperInvariant (culture-stable) and site.SiteName.Localize()
    /// for already-localized strings.
    /// AAP B5: GameDifficulty field + assignment + log portion deleted (was
    /// written and never read).
    /// </summary>
    internal static class LimitedWar
    {
        internal static bool Enabled => ModMain.Cfg?.EnableLimitedWar == true;
        internal static bool ZonedFactionAttacks => ModMain.Cfg?.LWZonedFactionAttacks == true;
        internal static bool ZonedPandoranAttacks => ModMain.Cfg?.LWZonedPandoranAttacks == true;
        internal static bool RaiseAlertness => ModMain.Cfg?.LWAttacksRaiseAlertness == true;
        internal static bool StopOneSidedWar => ModMain.Cfg?.LWStopOneSidedWar == true;
        internal static bool DisablePandoranBaseAttacks => ModMain.Cfg?.LWDisablePandoranBaseAttacks == true;

        // AAP G5-A: 0 is the strictest positive limit (NormalizeLimit maps
        // 0 -> 1). -1 is the off-switch (preserved as -1). See AAPConfig
        // tooltips for the player-facing contract.
        private static int NormalizeLimit(int value) => value == 0 ? 1 : value;
        internal static int GlobalAttackLimit => NormalizeLimit(ModMain.Cfg?.LWGlobalAttackLimit ?? 3);
        internal static int FactionAttackLimit => NormalizeLimit(ModMain.Cfg?.LWFactionAttackLimit ?? 2);
        internal static int SiegeProtectionLimit => NormalizeLimit(ModMain.Cfg?.LWSiegeProtectionLimit ?? 1);
        internal static bool ZoningActive => ZonedFactionAttacks || ZonedPandoranAttacks;
        internal static bool AttackLimitsActive => StopOneSidedWar || GlobalAttackLimit >= 0 || FactionAttackLimit >= 0 || SiegeProtectionLimit >= 0;

        // Mad's defense multiplier defaults (kept in code to avoid flooding the options screen)
        internal static readonly float DefMultAlert = 1.2f;
        internal static readonly float DefMultHighAlert = 1.1f;
        internal static readonly float DefMultAttackerPandora = 1.2f;
        internal static readonly float DefMultDefenderAnu = 1.2f;
        internal static readonly float DefMultDefenderSynedrion = 1.2f;

        // AAP G9-A: persistent attacker memory across save/load. The variable
        // is 1-based (0 = "no last attacker"); factions are stored as
        // (IndexOf(faction) + 1) so it stays valid even if the Factions list
        // is reordered on a later save.
        private const string LastAttackerVariable = "AAP_LW_LastAttackerFactionIndex";

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
            // AAP B5: GameDifficulty field deleted (was written, never read).
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

        // AAP G11: char.ToUpperInvariant (culture-stable); the original
        // char.ToUpper was culture-sensitive (Turkish locale would mangle I).
        internal static string ToTitleCase(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
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
                    // AAP G6: identity guard. The original `if (Store.DefenseMission == null) return true;`
                    // fired the zoned-destruction path on ANY DestroySite call (e.g. world cleanup)
                    // once a defense mission had been recorded. New guard ties the path to the
                    // specific site under attack.
                    GeoHavenDefenseMission defense = Store.DefenseMission;
                    if (defense == null || defense.Site != __instance) return true;

                    IGeoFactionMissionParticipant attacker = defense.GetEnemyFaction();
                    if (Resolver.CanDestroyHavens(attacker)) return true;

                    GeoHavenZone zone = defense.AttackedZone;
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
        // AAP G8: typed access via the live `mission` parameter (cast to
        // GeoHavenDefenseMission). The original postfix rejected the live
        // mission unless Store.DefenseMission was non-null, but that store
        // is only populated in the resolution patch -- so the log entry was
        // never renamed. New body uses `mission` directly.
        [HarmonyPatch(typeof(GeoscapeLog), "Map_SiteMissionStarted")]
        public static class GeoscapeLog_Map_SiteMissionStarted_Patch
        {
            public static bool Prepare() => Enabled && ZoningActive;

            [HarmonyPostfix]
            public static void Postfix(GeoSite site, GeoMission mission,
                List<GeoscapeLogEntry> ____entries, GeoFaction ____faction)
            {
                try
                {
                    if (!(mission is GeoHavenDefenseMission defense)) return;
                    if (!site.GetInspected(____faction)) return;
                    IGeoFactionMissionParticipant attacker = defense.GetEnemyFaction();
                    if (Resolver.CanDestroyHavens(attacker)) return;
                    LocalizedTextBind zoneName = defense.AttackedZone?.Def?.ViewElementDef?.DisplayName1;
                    if (zoneName == null || ____entries == null || ____entries.Count == 0) return;
                    GeoscapeLogEntry entry = ____entries[____entries.Count - 1];
                    if (entry.Parameters == null || entry.Parameters.Length == 0) return;
                    // AAP G11: site.SiteName.Localize() (already-localized),
                    // not site.Name (frozen-in-English fallback).
                    string siteName = site.SiteName.Localize();
                    entry.Parameters[0] = new LocalizedTextBind(
                        $"{siteName} ({ToTitleCase(zoneName.Localize())})", true);
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
                            // AAP G11: same site.SiteName.Localize() composition here.
                            new LocalizedTextBind($"{site.SiteName.Localize()} ({ToTitleCase(zoneName.Localize())})", true),
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
                    // AAP G6: identity guard for the alertness postfix too.
                    GeoHavenDefenseMission defense = Store.DefenseMission;
                    if (defense == null || defense.Site != __instance) return;

                    GeoHaven haven = defense.Haven;
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

        // ── Attack limits: restore last attacker on level start (G9-A) ───
        [HarmonyPatch(typeof(GeoLevelController), "OnLevelStart")]
        public static class GeoLevelController_OnLevelStart_Patch
        {
            public static bool Prepare() => Enabled && AttackLimitsActive;

            public static void Postfix(GeoLevelController __instance)
            {
                try
                {
                    // AAP G9-A: restore LastAttacker from the EventSystem
                    // variable so "no two attacks in a row" survives save/load.
                    // Variable is 1-based; 0 means no last attacker recorded.
                    int stored = __instance.EventSystem.GetVariable(LastAttackerVariable, 0);
                    Store.LastAttacker = stored > 0 && stored <= __instance.Factions.Count
                        ? __instance.Factions[stored - 1] : null;
                    // AAP B5: removed the GameDifficulty assignment + log portion.
                    Log($"Loaded: last attacker = {Store.LastAttacker?.GetPPName() ?? "(none)"} (EventSystem var {LastAttackerVariable}={stored}).");
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
                    // AAP G9-A: persist LastAttacker across save/load via
                    // EventSystem so "no two attacks in a row" survives a reload.
                    Store.LastAttacker = vehicle.Owner;
                    int index = ____level.Factions.IndexOf(vehicle.Owner);
                    ____level.EventSystem.SetVariable(LastAttackerVariable, index >= 0 ? index + 1 : 0);
                    return true;
                }
                catch (Exception e) { LogError(e); return true; }
            }
        }

        // ── Attack limits: discourage war navigation ─────────────────
        // AAP G7: Prefix + Finalizer (replaces Prefix + Postfix). The
        // postfix would skip on throw, leaving the multiplier at -2f and
        // permanently discouraging navigation. Finalizer always restores.
        [HarmonyPatch(typeof(VehicleFactionController), "GetSiteVehicleDestinationWeight")]
        public static class VehicleFactionController_GetSiteVehicleDestinationWeight_Patch
        {
            public static bool Prepare() => Enabled && AttackLimitsActive;

            public static void Prefix(VehicleFactionController __instance, ref float? __state)
            {
                __state = null;
                try
                {
                    if (Resolver.ShouldCancelAttack(__instance.Vehicle?.GeoLevel, __instance.Vehicle?.Owner))
                    {
                        __state = __instance.ControllerDef.FactionInWarWeightMultiplier;
                        __instance.ControllerDef.FactionInWarWeightMultiplier = -2f;
                    }
                }
                catch (Exception e) { LogError(e); }
            }

            [HarmonyFinalizer]
            public static Exception Finalizer(VehicleFactionController __instance, float? __state, Exception __exception)
            {
                if (__state.HasValue)
                    __instance.ControllerDef.FactionInWarWeightMultiplier = __state.Value;
                return __exception;
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
