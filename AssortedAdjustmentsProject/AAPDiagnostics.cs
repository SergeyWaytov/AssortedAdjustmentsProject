// AAPDiagnostics.cs -- AAP Phase 3 "SEEKER LINES" module.
// ----------------------------------------------------------------------------
// Purpose: the mod's runtime surfaces (poison debuffs, psychic WP defenses,
// squad evacuation, base-screen selection, facility popups) all interact with
// VANILLA systems whose exact member names / layout hierarchies we cannot
// inspect from the mod SDK. The seeker module pings those surfaces with
// DEDUPLICATED one-shot diagnostics so the next Player.log tells us exactly
// which hooks resolved, which didn't, and what the real layout/field names
// are -- without spamming the log on every frame.
//
// All seeker output is prefixed "[AAP][SEEKER]" and can be grepped with:
//     findstr /C:"[AAP][SEEKER]" Player.log
//
// Gating: the "Diagnostic logging" mod option (AAPConfig.VerboseDiagnostics,
// default ON). Error-level logs elsewhere in AAP are NOT gated -- only these
// discovery probes are.
//
// This file is NEW in Phase 3: add it to the AssortedAdjustmentsProject
// folder -- the updated AssortedAdjustmentsProject.csproj already includes
// it in its <Compile> list.
// ----------------------------------------------------------------------------
using Base.Defs;
using Base.Entities.Statuses;
using HarmonyLib;
using PhoenixPoint.Geoscape.View.ViewControllers.PhoenixBase;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Statuses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Central, deduplicated diagnostic helper. Everything routes through
    /// Log / LogOnce so a probe can fire hundreds of times per mission while
    /// the Player.log still only carries one line per unique discovery.
    /// </summary>
    internal static class Seeker
    {
        private static readonly HashSet<string> _seenKeys = new HashSet<string>();

        /// <summary>True when the Diagnostics mod option is on (default).</summary>
        public static bool Enabled => ModMain.Cfg?.VerboseDiagnostics != false;

        public static void Log(string message)
        {
            if (!Enabled) return;
            Debug.Log("[AAP][SEEKER] " + message);
        }

        /// <summary>
        /// Logs the message only the first time this key is seen this session.
        /// Use for discovery probes on hot paths (called every frame / every
        /// stat modification / every popup show).
        /// </summary>
        public static void LogOnce(string key, string message)
        {
            if (!Enabled) return;
            if (_seenKeys.Contains(key)) return;
            _seenKeys.Add(key);
            Debug.Log("[AAP][SEEKER] " + message);
        }

        /// <summary>Short human description of a stat-mod source object.</summary>
        public static string DescribeSource(object source)
        {
            if (source == null) return "(null)";
            BaseDef def = source as BaseDef;
            if (def != null) return $"{def.GetType().Name}({def.name})";
            TacStatus status = source as TacStatus;
            if (status != null) return $"TacStatus({status.TacStatusDef?.name ?? "?"})";
            return source.GetType().Name;
        }

        /// <summary>
        /// One-shot dump of an object's instance fields and properties (name,
        /// type) -- used when an expected member (e.g. the base-screen's
        /// "_base") turns out to have a different name in the live build, so
        /// the NEXT Player.log reveals the real one.
        /// </summary>
        public static void DumpMembersOnce(object instance, string context)
        {
            try
            {
                if (instance == null) { LogOnce("dump:" + context, context + ": instance is null."); return; }
                Type t = instance.GetType();
                string key = "dump:" + context + ":" + t.FullName;
                if (_seenKeys.Contains(key)) return;
                _seenKeys.Add(key);

                var sb = new StringBuilder();
                sb.Append(context).Append(" -- ").Append(t.FullName).AppendLine(" members:");
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    sb.Append("  field  ").Append(f.FieldType.Name).Append(' ').AppendLine(f.Name);
                foreach (PropertyInfo p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    sb.Append("  prop   ").Append(p.PropertyType.Name).Append(' ').AppendLine(p.Name);
                Debug.Log("[AAP][SEEKER] " + sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AAP][SEEKER] DumpMembersOnce(" + context + ") failed: " + e.Message);
            }
        }

        /// <summary>
        /// One-shot dump of a UI transform tree (name, anchored position,
        /// size, pivot) plus which children carry Text components -- used to
        /// discover the facility-inspection popup's real layout so the
        /// extended-info block can be positioned without overlapping the
        /// description (the reported RU text-on-top-of-text bug).
        /// </summary>
        public static void DumpHierarchyOnce(Component root, string context, int maxDepth)
        {
            try
            {
                if (root == null) { LogOnce("hier:" + context, context + ": root is null."); return; }
                string key = "hier:" + context;
                if (_seenKeys.Contains(key)) return;
                _seenKeys.Add(key);

                var sb = new StringBuilder();
                sb.Append(context).AppendLine(" -- UI hierarchy:");
                DumpNode(sb, root.transform, 0, maxDepth);
                Debug.Log("[AAP][SEEKER] " + sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AAP][SEEKER] DumpHierarchyOnce(" + context + ") failed: " + e.Message);
            }
        }

        private static void DumpNode(StringBuilder sb, Transform node, int depth, int maxDepth)
        {
            if (node == null || depth > maxDepth) return;
            string pad = new string(' ', depth * 2);
            RectTransform rt = node as RectTransform;
            Text text = node.GetComponent<Text>();
            if (rt != null)
            {
                string textNote = text != null
                    ? $" TEXT[{text.text?.Length ?? 0} chars, fontSize={text.fontSize}, h={text.preferredHeight:F0}]"
                    : "";
                sb.AppendLine($"{pad}{node.name} pos={rt.anchoredPosition} size={rt.rect.size} pivot={rt.pivot} anchorMin={rt.anchorMin} anchorMax={rt.anchorMax}{textNote}");
            }
            else
            {
                sb.AppendLine($"{pad}{node.name} (no RectTransform)");
            }
            for (int i = 0; i < node.childCount; i++)
                DumpNode(sb, node.GetChild(i), depth + 1, maxDepth);
        }
    }

    /// <summary>
    /// The seeker PROBES. Each is a small Harmony patch with a Prepare()
    /// gate on the Diagnostics option. They never change game behavior --
    /// they only observe and report. Turning "Diagnostic logging" off
    /// removes every probe at patch-install time.
    /// </summary>
    internal static class SeekerProbes
    {
        // ============================================================
        // PROBE 1: WillPoints / Accuracy stat-modification traffic.
        // This is the EXACT seam where the Poison rework and the psychic
        // WP defense (PsychicWillpointLoss_Defense_V3) meet: both sides
        // read or mutate negative WillPoints StatModifications. The probe
        // logs every UNIQUE (stat, source type, modification type, sign)
        // combination once, so one mission produces a complete map of who
        // is applying what -- including sources we never predicted.
        // ============================================================
        [HarmonyPatch(typeof(StatusStat), "ApplyStatModification")]
        public static class Seeker_StatModTraffic
        {
            public static bool Prepare() => Seeker.Enabled;

            private static readonly HashSet<string> _seen = new HashSet<string>();

            public static void Prefix(StatusStat __instance, StatModification statMod)
            {
                try
                {
                    // Only the two stats that the poison / psychic systems
                    // fight over; everything else would be noise.
                    string statName = __instance?.Name;
                    if (statName != "WillPoints" && statName != "Accuracy") return;

                    string key = statName + "|" + (statMod.Source?.GetType().FullName ?? "null")
                                         + "|" + (statMod.Value >= 0f ? "+" : "-");
                    if (_seen.Contains(key)) return;
                    _seen.Add(key);

                    // Identify the owning actor when reachable (helps tell
                    // player-side from AI-side traffic at a glance).
                    string owner = (__instance?.Owner as TacticalActor)?.DisplayName ?? "(non-actor)";

                    Debug.Log($"[AAP][SEEKER] Stat-mod traffic: stat={statName} owner={owner} " +
                              $"value={statMod.Value} source={Seeker.DescribeSource(statMod.Source)}");
                }
                catch { /* probes never throw upward */ }
            }
        }

        // ============================================================
        // PROBE 2: Facility-inspection popup layout (RU overlap bug).
        // Dumps the popup's UI hierarchy the first time a base facility
        // popup is shown, so the Player.log reveals the real structure the
        // extended-info block must fit into (text heights in the ACTIVE
        // language included -- RU strings are longer, which is what made
        // the old same-position clone overlap).
        // ============================================================
        [HarmonyPatch(
            typeof(PhoenixPoint.Geoscape.View.ViewControllers.PhoenixBase.UIFacilityInfoPopup),
            "Show")]
        public static class Seeker_FacilityPopupLayout
        {
            public static bool Prepare() => Seeker.Enabled;

            public static void Postfix(UIFacilityInfoPopup __instance)
            {
                try
                {
                    // Dump the POPUP's own subtree (not the whole UI canvas
                    // root) -- depth 6 covers the popup's structure.
                    Component root = __instance != null ? __instance.transform : null;
                    Seeker.DumpHierarchyOnce(root, "FacilityInfoPopup", 6);
                }
                catch { }
            }
        }

        // ============================================================
        // PROBE 3: Base-layout screen state members.
        // If SmartBaseSelection cannot find the field that stores the
        // selected base, this dump lists every field/property on
        // UIStatePhoenixBaseLayout so the next round can patch the REAL
        // member name instead of guessing.
        // ============================================================
        [HarmonyPatch]
        public static class Seeker_BaseScreenState
        {
            public static bool Prepare()
            {
                if (!Seeker.Enabled) return false;
                MethodBase m = TargetMethod();
                if (m == null)
                {
                    Debug.LogWarning("[AAP][SEEKER] UIStatePhoenixBaseLayout.EnterState could not be resolved -- base-screen probe skipped (SmartBaseSelection logs its own resolution status).");
                    return false;
                }
                return true;
            }

            [HarmonyTargetMethod]
            public static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("PhoenixPoint.Geoscape.View.ViewStates.UIStatePhoenixBaseLayout");
                return AccessTools.Method(type, "EnterState");
            }

            public static void Postfix(object __instance)
            {
                try { Seeker.DumpMembersOnce(__instance, "UIStatePhoenixBaseLayout(EnterState)"); }
                catch { }
            }
        }
    }
}
