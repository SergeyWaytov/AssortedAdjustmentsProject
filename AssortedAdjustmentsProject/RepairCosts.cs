using Base.Core;
using Base.Defs;
using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class RepairCosts
    {
        public static void Apply(DefCache cache)
        {
            var repo = GameUtl.GameComponent<DefRepository>();
            var allDefs = repo.GetAllDefs<BaseDef>();

            // Try to find by type name substring first
            var economy = allDefs.FirstOrDefault(d =>
                d.GetType().Name.Contains("Economy") ||
                d.GetType().Name.Contains("Global") ||
                d.name.Contains("Economy") ||
                d.name.Contains("GlobalEconomy"));

            // If not found, search by property existence
            if (economy == null)
            {
                foreach (var def in allDefs)
                {
                    var prop = Traverse.Create(def).Property("BionicRepairCostPerHP");
                    if (prop.PropertyExists())
                    {
                        economy = def;
                        Debug.Log($"[AAP] Found economy settings via property detection: {def.GetType().Name} ({def.name})");
                        break;
                    }
                }
            }

            if (economy == null)
            {
                Debug.LogWarning("[AAP] Economy settings def not found! Repair costs unchanged.");
                return;
            }

            var t = Traverse.Create(economy);
            t.Property("BionicRepairCostPerHP")?.SetValue(1);
            t.Property("MutationRepairCostPerHP")?.SetValue(0);

            Debug.Log("[AAP] RepairCosts applied (Bionic: 1, Mutation: 0).");
        }
    }
}