using PhoenixPoint.Modding;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    /// <summary>
    /// Mod settings editable from the in-game Mod Options screen (main menu).
    /// Native replacement for the Modnix-era settings files of the original
    /// Assorted Adjustments / Limited War mods these features were ported from.
    /// Def-level changes re-apply through OnConfigChanged and are idempotent
    /// (relative changes are computed from values captured on first apply).
    /// </summary>
    public class AAPConfig : ModConfig
    {
        // ── Core toggles ──────────────────────────────────────────────

        [ConfigField(text: "Disable right-click move",
            description: "Right-click no longer issues move orders; it closes open menus instead. Turn off if it feels awkward with vehicles.")]
        public bool DisableRightClickMove = true;

        [ConfigField(text: "Smart squad evacuation",
            description: "When a squad member moves into an exit zone, ask to evacuate the whole squad (only possible when everyone can evacuate).")]
        public bool EnableSmartEvacuation = true;

        [ConfigField(text: "Plentiful item drops",
            description: "Reduced item destruction chance and extra loot drops from fallen enemies.")]
        public bool EnablePlentifulDrops = true;

        // ── Tunable values ────────────────────────────────────────────

        [ConfigField(text: "Personal abilities count",
            description: "Personal ability slots per soldier (vanilla 3, max 7).")]
        public int PersonalAbilitiesCount = 5;

        [ConfigField(text: "Frenzy speed coefficient",
            description: "Frenzy speed multiplier. 1.5 = toned-down +50%% (vanilla value). The old 1.75 allowed cross-map rushes.")]
        public float FrenzySpeedCoefficient = 1.5f;

        [ConfigField(text: "Vehicle weapon ammo multiplier",
            description: "Ammo charges multiplier for ground vehicle turret weapons (Armadillo, Scarab, Aspida, Mutog, Kaos Buggy). 1.5 = +50%%.")]
        public float VehicleAmmoMultiplier = 1.5f;

        [ConfigField(text: "Deployment cap",
            description: "Maximum deployable units per mission (vanilla 8; 16 fits two full Thunderbirds).")]
        public int DeploymentCap = 16;

        // ── Festering Skies (DLC3) ────────────────────────────────────

        [ConfigField(text: "Festering Skies tweaks",
            description: "Slow down the Behemoth and Pandoran geoscape flyers so aircraft and bases have more reaction time.")]
        public bool EnableFesteringSkiesTweaks = true;

        [ConfigField(text: "FS: Behemoth speed (kph)",
            description: "Geoscape speed of the Behemoth. Vanilla 60; 6 matches the Slowdown Behemoth reference mod. Only used with Festering Skies tweaks enabled.")]
        public int BehemothSpeed = 6;

        [ConfigField(text: "FS: Alien flyer speed percent",
            description: "Geoscape speed of Pandoran flyers as percent of vanilla (vanilla small 250 / medium 350 / large 300). 10 = Slowdown Alien Flyers reference mod values.")]
        public int AlienFlyerSpeedPercent = 10;

        // ── Corrupted Horizons (DLC4) ─────────────────────────────────

        [ConfigField(text: "Corrupted Horizons tweaks",
            description: "Nerf the Acheron: Call Reinforcements gets a real WP cost, and its cloud abilities are limited to one use per turn.")]
        public bool EnableCorruptedHorizonsTweaks = true;

        [ConfigField(text: "CH: Acheron reinforcement WP cost",
            description: "Willpower cost of the Acheron's Call Reinforcements (vanilla 0 = free spam). 20 matches the Nerf Acheron mod.")]
        public int AcheronReinforceWPCost = 20;
    }
}
