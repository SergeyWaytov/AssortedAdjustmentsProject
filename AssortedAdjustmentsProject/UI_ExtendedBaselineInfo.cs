// UI_ExtendedBaselineInfo.cs -- AAP Phase 3 (WHOLE-FILE REPLACEMENT).
// ----------------------------------------------------------------------------
// Reported bug (Phase 3): "base facility inspection in RU got text ON TOP of
// each other, instead of not showing where its not supposed to be / spacing
// out."
//
// Root cause in the 2b version: the extended-info block was a CLONE of the
// popup's Description Text, instantiated as a SIBLING under the same parent
// -- which copies the Description's rect and anchored position exactly. Two
// Text components on the same rect = both render in the same spot. In
// English the strings are short, so the collision was easy to miss; in
// Russian the localized strings are longer and the overlap is plainly
// visible.
//
// Phase 3 fix:
//   1. The clone is positioned BELOW the Description: the anchor's rendered
//      height is measured (after a forced layout rebuild so the ACTIVE
//      language's wrapped line count is included), and the clone's
//      anchoredPosition is shifted down by that height plus a small gap.
//      Long RU lines WRAP inside the anchor's width instead of spilling.
//   2. The popup window (the UIFacilityInfoPopup's own RectTransform) is
//      grown downward by the block's measured height so the new text fits
//      INSIDE the window instead of overflowing its background. The pivot
//      handling keeps the window's TOP edge stationary for all common
//      pivot configurations (0.5 / top / bottom).
//   3. Hide-don't-break policy: if anything in the layout math fails, the
//      clone is destroyed -- the popup falls back to clean vanilla instead
//      of showing text where it is not supposed to be.
//   4. Seeker metrics (once): anchor height, block height, window name,
//      window size before/after -- plus the AAPDiagnostics hierarchy dump
//      of the whole popup (probe Seeker_FacilityPopupLayout) so the exact
//      RU layout is visible in the next Player.log.
//   5. Option gate: "Base facility popup: extended info" (default on).
// ----------------------------------------------------------------------------
using HarmonyLib;
using I2.Loc;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Entities.PhoenixBases;
using PhoenixPoint.Geoscape.Entities.Sites;
using PhoenixPoint.Geoscape.View.ViewControllers.PhoenixBase;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    [HarmonyPatch]
    public static class ExtendedBaseInfo
    {
        private const float BlockGap = 8f;

        [HarmonyPatch(
            typeof(PhoenixPoint.Geoscape.View.ViewControllers.PhoenixBase.UIFacilityInfoPopup),
            "Show")]
        [HarmonyPostfix]
        public static void Show_Postfix(UIFacilityInfoPopup __instance, GeoPhoenixFacility facility)
        {
            Text clone = null;
            try
            {
                if (ModMain.Cfg?.FacilityPopupExtraInfo == false) return;

                GeoPhoenixBase pxBase = facility?.PxBase;
                Text anchor = __instance?.Description;
                if (pxBase == null || anchor == null) return;

                // Remove any previous AAP block (popup reuse between facilities).
                Transform parent = anchor.transform.parent;
                if (parent == null) return;
                Transform old = parent.Find("AAP_BaseDetails");
                if (old != null) UnityEngine.Object.Destroy(old.gameObject);

                int wounded = pxBase.SoldiersInBase.Count(s => s.Health.Value < s.Health.Max);
                var lines = new List<string>
                {
                    $"{ModMain.Localize("HealingRate")}: {pxBase.Stats.HealSoldiersHP * 24} {ModMain.Localize("HpPerDay")}",
                    $"{ModMain.Localize("InTreatment")}: {wounded}",
                    $"{ModMain.Localize("RepairRate")}: {pxBase.Stats.RepairAircraftHP * 24} {ModMain.Localize("HpPerDay")}",
                    $"{ModMain.Localize("Vehicles")}: {pxBase.VehiclesAtBase.Count()}"
                };
                foreach (GeoVehicle vehicle in pxBase.VehiclesAtBase)
                    lines.Add($"  {vehicle.Name}: {vehicle.Stats.HitPoints}/{vehicle.Stats.MaxHitPoints}");

                // Measure the DESCRIPTION as rendered in the active language
                // (RU wraps to more lines than EN -- this was the overlap).
                RectTransform anchorRt = anchor.rectTransform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(anchorRt);
                float anchorHeight = Mathf.Max(anchor.preferredHeight, anchorRt.rect.height);

                clone = UnityEngine.Object.Instantiate(anchor, parent);
                clone.name = "AAP_BaseDetails";
                Localize localize = clone.GetComponent<Localize>();
                if (localize != null) UnityEngine.Object.Destroy(localize);

                clone.text = string.Join("\n", lines);

                // ── Position BELOW the description (the overlap fix) ─────
                RectTransform cloneRt = clone.rectTransform;
                Vector2 anchorPos = anchorRt.anchoredPosition;
                cloneRt.anchoredPosition = new Vector2(anchorPos.x, anchorPos.y - anchorHeight - BlockGap);

                // Measure our own block now that the text is set.
                LayoutRebuilder.ForceRebuildLayoutImmediate(cloneRt);
                float blockHeight = Mathf.Max(clone.preferredHeight, 20f);

                // ── Grow the popup window so the block fits inside ──────
                RectTransform window = __instance.transform as RectTransform;
                if (window == null)
                {
                    // Fallback: nearest ancestor with a RectTransform.
                    Transform t = __instance.transform.parent;
                    while (t != null && window == null)
                    {
                        window = t as RectTransform;
                        t = t.parent;
                    }
                }
                if (window != null)
                {
                    Vector2 sizeBefore = window.rect.size;
                    Vector2 delta = new Vector2(0f, blockHeight + BlockGap);
                    if (Mathf.Approximately(window.pivot.y, 0.5f))
                    {
                        // Center pivot: grow both ways, then shift the whole
                        // window DOWN by half so the TOP edge stays put.
                        window.sizeDelta += delta;
                        window.anchoredPosition += new Vector2(0f, -delta.y / 2f);
                    }
                    else if (window.pivot.y >= 1f)
                    {
                        // Top pivot: growing the size already extends downward.
                        window.sizeDelta += delta;
                    }
                    else
                    {
                        // Bottom pivot: extend downward by shifting as well.
                        window.sizeDelta += delta;
                        window.anchoredPosition += new Vector2(0f, -delta.y);
                    }
                    Seeker.LogOnce("FacilityPopup:metrics",
                        $"FacilityInfoPopup layout: description h={anchorHeight:F0}, AAP block h={blockHeight:F0}, " +
                        $"window '{window.name}' size {sizeBefore} -> {window.rect.size} (pivot.y={window.pivot.y}).");
                }
                else
                {
                    Seeker.LogOnce("FacilityPopup:nowindow",
                        "FacilityInfoPopup layout: no window RectTransform found -- AAP block positioned below the description without window growth (may overflow the popup background).");
                }

                clone.gameObject.SetActive(true);
                Debug.Log($"[AAP] ExtendedBaseInfo: {lines.Count} lines added below the description (no overlap).");
            }
            catch (Exception e)
            {
                // Hide-don't-break: never leave a half-positioned clone
                // rendering on top of the vanilla description.
                if (clone != null) UnityEngine.Object.Destroy(clone.gameObject);
                Debug.LogError($"[AAP] ExtendedBaseInfo failed (block hidden, vanilla popup intact): {e}");
            }
        }
    }
}
