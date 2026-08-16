using Base.Core;
using Base.Defs;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Corrupted Horizons (DLC4) adjustments, ported natively from the
    /// Modnix-era "Nerf Acheron" JSON modlet:
    ///   - Call Reinforcements: 0.75 AP and a real WP cost (config, default 20)
    ///     plus AI action weight 50, so it stops chain-summoning.
    ///   - Cloud abilities (Corruption/Corrosive/Blindness/Paralytic/Pepper/...):
    ///     0 WP but limited to one use per turn each.
    ///   - Leap: 0 WP (keeps mobility, removes WP competition with clouds).
    /// All lookups are by def name and silently skip when the DLC is absent.
    /// </summary>
    public static class CorruptedHorizonsAdjustments
    {
        public static void Apply(DefCache cache)
        {
            var cfg = ModMain.Cfg;
            if (cfg == null || !cfg.EnableCorruptedHorizonsTweaks) return;

            int found = 0;

            // ── Call Reinforcements: make it expensive ────────────────
            var reinforce = cache.GetDef<TacticalAbilityDef>("Acheron_CallReinforcements_AbilityDef");
            if (reinforce != null)
            {
                reinforce.ActionPointCost = 0.75f;
                reinforce.WillPointCost = cfg.AcheronReinforceWPCost;
                found++;
                Debug.Log($"[AAP][CH] {reinforce.name}: AP 0.75, WP {cfg.AcheronReinforceWPCost}.");
            }

            var reinforceAI = cache.GetDef<Base.AI.Defs.AIActionDef>("Acheron_CallReinforcements_AIActionDef");
            if (reinforceAI != null)
            {
                reinforceAI.Weight = 50f;
                found++;
                Debug.Log($"[AAP][CH] {reinforceAI.name}: AI weight 50.");
            }

            // ── Clouds: free but once per turn ────────────────────────
            found += SetOncePerTurn(cache, "Acheron_CorruptionCloud_AbilityDef");
            found += SetOncePerTurn(cache, "Acheron_CorrosiveCloud_AbilityDef");
            found += SetOncePerTurn(cache, "Acheron_BlindnessCloud_AbilityDef");
            found += SetOncePerTurn(cache, "Acheron_ParalyticCloud_AbilityDef");
            found += SetOncePerTurn(cache, "Acheron_PepperCloud_ApplyStatusAbilityDef");
            found += SetOncePerTurn(cache, "Acheron_CureCloud_ApplyEffectAbilityDef");

            // ── Other abilities: remove WP competition ─────────────────
            found += SetFreeWP(cache, "Acheron_Leap_AbilityDef");
            found += SetFreeWP(cache, "Acheron_RestorePandoranArmor_AbilityDef");
            found += SetFreeWP(cache, "Acheron_ParalyticSpray_AbilityDef");

            if (found == 0)
                Debug.Log("[AAP][CH] Acheron defs not found - DLC not installed. Skipped (no error).");
            else
                Debug.Log($"[AAP][CH] Corrupted Horizons adjustments applied to {found} defs.");
        }

        private static int SetOncePerTurn(DefCache cache, string defName)
        {
            var def = cache.GetDef<TacticalAbilityDef>(defName);
            if (def == null) return 0;
            def.WillPointCost = 0f;
            def.UsesPerTurn = 1;
            Debug.Log($"[AAP][CH] {def.name}: WP 0, 1 use/turn.");
            return 1;
        }

        private static int SetFreeWP(DefCache cache, string defName)
        {
            var def = cache.GetDef<TacticalAbilityDef>(defName);
            if (def == null) return 0;
            def.WillPointCost = 0f;
            Debug.Log($"[AAP][CH] {def.name}: WP 0.");
            return 1;
        }
    }
}
