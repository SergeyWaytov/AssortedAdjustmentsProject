using System.Linq;
using System.Reflection;
using Base.Core;
using Base.Defs;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public static class DefNameScanner
    {
        public static void Run()
        {
            var repo = GameUtl.GameComponent<DefRepository>();
            var allDefs = repo.GetAllDefs<BaseDef>();
            string[] keywords = { "Sniper", "Class", "BaseStatSheet", "PsychicResistance", "Frenzy", "Stimpack", "HealAbility", "Precision", "PersonalAbility" };

            Debug.Log("[AAP SCAN] === Scanning def names ===");
            foreach (var def in allDefs)
            {
                foreach (var kw in keywords)
                {
                    if (def.name.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.Log($"[AAP SCAN] {def.GetType().Name} : {def.name}  (GUID: {def.Guid})");
                        break;
                    }
                }
            }
            Debug.Log("[AAP SCAN] === Done ===");
        }
    }
}