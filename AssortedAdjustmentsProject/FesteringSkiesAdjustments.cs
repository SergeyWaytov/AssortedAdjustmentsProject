using Base.Core;
using Base.Defs;
using PhoenixPoint.Geoscape.Entities;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Festering Skies (DLC3) adjustments, ported natively from the Modnix-era
    /// "Slowdown Alien Flyers / Behemoth" JSON modlet:
    ///   - Behemoth geoscape speed & range
    ///   - Pandoran geoscape flyer speeds (small/medium/large)
    /// Defs are looked up by GUID (the same GUIDs the JSON modlet patched),
    /// so this is naturally a no-op when the DLC is not installed.
    /// Relative changes (flyer percent) are computed from values captured on
    /// first apply so repeated applies stay idempotent.
    /// </summary>
    public static class FesteringSkiesAdjustments
    {
        // GUIDs from Slowdown_AlienFlyers_Behemoth.json
        private const string BehemothGuid = "5b499345-37cd-06a8-ee32-b5f2067b893c";
        private const string FlyerMediumGuid = "096ee7aa-4ca3-c3f4-baea-926d4e4a7c6a"; // ALN_GeoscapeFlyer_Medium (vanilla 350)
        private const string FlyerLargeGuid = "436396ce-aedb-5ba4-2b4b-bfce38d281a8";  // ALN_GeoscapeFlyer_Large  (vanilla 300)
        private const string FlyerSmallGuid = "c235f229-820f-cfa4-7b74-fa6329015aaa";  // ALN_GeoscapeFlyer_Small  (vanilla 250)

        private static bool _originalsCaptured;
        private static float _flyerSmallBase, _flyerMediumBase, _flyerLargeBase;

        public static void Apply(DefCache cache)
        {
            var cfg = ModMain.Cfg;
            if (cfg == null || !cfg.EnableFesteringSkiesTweaks) return;

            var repo = GameUtl.GameComponent<DefRepository>();
            int found = 0;

            // ── Behemoth (GeoBehemothActorDef: root-level Speed/MaxRange) ──
            var behemoth = repo.GetDef(BehemothGuid) as GeoBehemothActorDef;
            if (behemoth != null)
            {
                found++;
                float oldSpeed = behemoth.Speed.Value;
                behemoth.Speed.Value = cfg.BehemothSpeed;
                Debug.Log($"[AAP][FS] {behemoth.name}: Speed {oldSpeed} -> {cfg.BehemothSpeed} (vanilla 60).");
            }

            // ── Pandoran flyers ───────────────────────────────────────
            found += ApplyFlyer(repo.GetDef(FlyerSmallGuid) as GeoVehicleDef, "small", ref _flyerSmallBase, cfg);
            found += ApplyFlyer(repo.GetDef(FlyerMediumGuid) as GeoVehicleDef, "medium", ref _flyerMediumBase, cfg);
            found += ApplyFlyer(repo.GetDef(FlyerLargeGuid) as GeoVehicleDef, "large", ref _flyerLargeBase, cfg);

            _originalsCaptured = true;

            if (found == 0)
                Debug.Log("[AAP][FS] Festering Skies defs not found - DLC not installed. Skipped (no error).");
            else
                Debug.Log($"[AAP][FS] Festering Skies adjustments applied to {found} defs.");
        }

        private static int ApplyFlyer(GeoVehicleDef flyer, string size, ref float capturedBase, AAPConfig cfg)
        {
            if (flyer == null) return 0;
            if (!_originalsCaptured)
                capturedBase = flyer.BaseStats.Speed.Value;

            float target = capturedBase * Mathf.Clamp(cfg.AlienFlyerSpeedPercent, 5, 100) / 100f;
            float old = flyer.BaseStats.Speed.Value;
            flyer.BaseStats.Speed.Value = target;
            Debug.Log($"[AAP][FS] {flyer.name} ({size}): Speed {old} -> {target} (vanilla {capturedBase}, {cfg.AlienFlyerSpeedPercent}%).");
            return 1;
        }
    }
}
