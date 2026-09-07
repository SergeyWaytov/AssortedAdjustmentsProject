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
        // AAP U1: live lookups (typed) replace the Traverse chain that no-op'd
        // against nonexistent members. Facility.name/.Name/.FacilityDef returned
        // ERR:member; transform is a property; GetHealingPerDay / GetVehicleRepairPerDay
        // / GeoPhoenixBase.Soldiers / GeoCharacter.IsHealing do not exist.
        // New body uses GeoPhoenixBase.SoldiersInBase / Stats.HealSoldiersHP /
        // Stats.RepairAircraftHP / VehiclesAtBase (all verified via TFTV use).
        [HarmonyPatch(
            typeof(PhoenixPoint.Geoscape.View.ViewControllers.PhoenixBase.UIFacilityInfoPopup),
            "Show")]
        [HarmonyPostfix]
        public static void Show_Postfix(UIFacilityInfoPopup __instance, GeoPhoenixFacility facility)
        {
            try
            {
                GeoPhoenixBase pxBase = facility?.PxBase;
                Text anchor = __instance?.Description;
                if (pxBase == null || anchor == null) return;

                Transform parent = anchor.transform.parent;
                Transform old = parent.Find("AAP_BaseDetails");
                if (old != null) UnityEngine.Object.Destroy(old.gameObject);

                Text details = UnityEngine.Object.Instantiate(anchor, parent);
                details.name = "AAP_BaseDetails";
                Localize localize = details.GetComponent<Localize>();
                if (localize != null) UnityEngine.Object.Destroy(localize);

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

                details.text = string.Join("\n", lines);
                details.gameObject.SetActive(true);
                Debug.Log($"[AAP] ExtendedBaseInfo: {lines.Count} lines added");
            }
            catch (Exception e) { Debug.LogError($"[AAP] ExtendedBaseInfo failed: {e}"); }
        }
    }
}
