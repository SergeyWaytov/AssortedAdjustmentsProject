# AAP 1.1 — Assorted Adjustments Project, all-DLC edition

**Folder purpose.** `E:\PP-mods\AAP_1_1` hosts everything related to (a) verifying that
every existing AAP feature actually works, (b) fixing everything that silently didn't,
and (c) adding native Festering Skies / Corrupted Horizons / Kaos Engines support, so a
save with all DLCs enabled runs alongside the Workshop collection
(https://steamcommunity.com/sharedfiles/filedetails/?id=3716582831) without reinventing
wheels the dependency mods already cover.

AAP's mission statement (unchanged): port the good parts of the **Modnix-era** mods
(Mad's AssortedAdjustments, Limited War, TechStrike, the Slowdown/Nerf JSON modlets)
to the **native Steam Workshop infrastructure** (`PhoenixPoint.Modding.ModMain`,
HarmonyLib 2.x, DefRepository patching, native `ModConfig`) without Modnix and without
conflicts. Reference code is always *adapted*, never copied verbatim.

---

## Layout

```
AAP_1_1/
├── AssortedAdjustmentsProject/     ← the mod source (git repo, branch AAP_1_1)
│   ├── AssortedAdjustmentsProject/ ← C# project (build: dotnet build -c Release)
│   ├── ModSDK/                     ← game reference DLLs
│   └── Dist/                       ← build output (auto-deployed to WorkshopTool/TestMod)
├── _reference_mods/                ← MadAA, TFTV, SuperCheatsModPlus, PPWorkshopTool,
│                                      + extracted Nexus archives (LimitedWar, TechStrike,
│                                      SlowdownAlienFlyers, NerfAcheron)
├── _decompiled_full/               ← full ilspycmd decompile of Assembly-CSharp.dll
│                                      (used to verify every field/method name)
├── _history/                       ← files recovered from git history (old enforcers)
└── AAP_1_1_README.md               ← this file
```

---

## Audit findings & fixes (why 1.0 features silently didn't work)

Every finding below was verified against the decompiled game assembly
(`_decompiled_full`), not guessed. The recurring root cause: **Traverse.Property() on
things that are fields** — Harmony's Traverse silently no-ops in that case.

| Feature | 1.0 state | Root cause | 1.1 fix |
|---|---|---|---|
| Vanish 0 AP / Manual Control 0.25 / Rally 1 AP 5 WP | no-op | `ActionPointCost`/`WillPointCost` are **fields** on `TacticalAbilityDef` | direct field assignment |
| Rally restores +1 AP / +1 WP | no-op | `ActionPointRestoration`/`WillPointRestoration` don't exist anywhere in game code | ensure `StatModification` entries (`ActionPoints`/`WillPoints`, Add +1) on the status def if it has a `StatModifications` list |
| Stimpack 0.25 AP / heal all parts | no-op | same field-vs-property issue | direct fields on `HealAbilityDef` |
| Poison rework (-50% Acc, -3 WP) | no-op + crash risk | poison def is a `DamageOverTimeStatusDef` — has no StatModifications; old code wrote a wrong-shaped array back into a `List<StatModification>` field | runtime enforcer: `Status.OnApply`/`OnUnapply` postfixes add/remove stat mods with the poison def as source |
| Frenzy +75% speed | "worked too well" (cross-map rushes — Workshop report) | `FrenzyStatusDef` fields were set correctly | speed coefficient now config, default **1.5** (toned down per dev response); Willpower/Damage stay 1.5 |
| Personal abilities 5 | stuck at 3 | `PersonalAbilitiesCount` is a field on `BaseStatSheetDef` (default 3); old code patched one def via property setter | set field on **all** stat sheets + ported MadAA's `FactionCharacterGenerator.GeneratePersonalAbilities` regeneration fix + recruit-list icon cloning (display of >3 abilities) |
| Armor stat retune (stealth/acc/speed/perc) | only Armor applied | those stats live on `BodyPartAspectDef` (`TacticalItemDef.BodyPartAspectDef`), not the item def | set via the aspect def; Armor stays on the item def |
| Vehicle ammo 1.5× | unchanged (Workshop report) | filter required `"GroundVehicle"` in def name; real names are e.g. `NJ_Armadillo_Gauss_Turret_GroundVehicleWeaponDef`, `Scarab_Missile_Turret`, `Aspida_Arms` | matched by proven turret substrings + `Kaos_Buggy` (KE); base charges captured once so config re-applies don't compound |
| Squad evac | crash (Workshop report) | patched `TacticalAbility.Activate` and force-activated every soldier's ExitMission with no target/zones check | replaced with MadAA's SmartEvacuation: UI-level hook (`TacticalView.OnAbilityExecuted`), asks confirmation, only offers squad evac when every active member has a valid exit target, activates with proper targets |
| Access Lift protection | still demolishable | real flag is `PhoenixFacilityDef.CannotDemolish`; old code guessed property names that don't exist | `CannotDemolish = true` |
| Loot settings | no-op | `AlwaysRecoverAllItemsFromTacticalMissions` & co. exist on **no game class**; writing them onto an Ambush def did nothing | ported MadAA's proven `DieAbility.ShouldDestroyItem`/`DropItems` patches + `GeoMission.GetDeadSquadMembersArmour` dupe-prevention (weapons 30% or health-based, armor drops at 70% destroy chance, other 10%) |
| Repair costs (bionic 1 / mutation 0) | no-op | `BionicRepairCostPerHP` doesn't exist; real mechanism is `GeoscapeSettingsDef.AllItemRepairCost` + per-tag `ItemTypeSettings.RepairCost` | set via `SharedData.GeoscapeSettingsDef` (mutations free, bionics normal) |
| Tyr-1 / Vidar GL | "defs not found" | names were already correct (`FS_Autocannon_WeaponDef`, `FS_AssaultGrenadeLauncher_WeaponDef`, verified against TFTV) — the defs are simply absent when the KE DLC isn't installed | kept (null-safe) + DLC presence probes logged at startup |
| Right-click move disable | awkward with vehicles (report) | — | now a config toggle (default on) |
| Deployment cap 16 | worked | — | value now configurable |
| Mist Repeller / Satellite Uplink ranges | commented out on purpose | delegated to the **Range Control** mod from the collection (mist 2250 / radar 3000) | unchanged — do NOT re-implement here |

Untouched because already working: Precision Shot, Psychic stages 1–2, research lore
popups, Jacob sniper conversion, ambush disabler, "Nothing Found" disabler, Return Fire
cover cancel, Scrap Aircraft, Smart Base Selection, facility popup/recruit tooltips,
Rage Burst retune, deployment UI text, agenda tracker (excavation timers, click-focus,
hover; defence/repair trackers remain intentionally disabled — they were CTD sources).

---

## New: DLC support (the "all-DLC" build)

### Festering Skies (`FesteringSkiesAdjustments.cs`)
Native port of the Slowdown Alien Flyers/Behemoth JSON modlet, looked up **by GUID** so
it's a silent no-op without the DLC:
- Behemoth (`GeoBehemothActorDef`, root-level `Speed`): 60 → config `BehemothSpeed` (default **6**)
- Flyers small/medium/large (`GeoVehicleDef.BaseStats.Speed`): scaled to config
  `AlienFlyerSpeedPercent` of captured vanilla values (250/350/300; default **10%** → 25/35/30,
  exactly the reference mod's values)

### Corrupted Horizons (`CorruptedHorizonsAdjustments.cs`)
Native port of the Nerf Acheron JSON modlet, looked up **by def name**:
- Call Reinforcements: 0.75 AP + `AcheronReinforceWPCost` WP (default **20**), AI weight 50
- Clouds (Corruption/Corrosive/Blindness/Paralytic/Pepper/Cure): 0 WP but **1 use/turn**
- Leap / Restore Armor / Paralytic Spray: 0 WP (no WP competition with clouds)

### Kaos Engines
Operative weapons were already patched in `WeaponPatches.cs` under the correct
`FS_*` names (Tyr-1 Autocannon, Vidar GL, Slamstrike, Light Sniper Rifle); the
`Kaos_Buggy` weapons are now included in the vehicle ammo multiplier. Startup logging
reports exactly which KE defs were found (`[AAP] DLC probe: …`), so a missing DLC is
visible in Player.log instead of mysterious.

### TechStrike (ported)
The Modnix-era TechStrike mod reworked the NJ Technician Mech Arms into a real weapon.
Ported into `WeaponPatches.cs`: payload replaced with Damage 10 / Piercing 40 /
Paralyse 18 (exact reference values), on top of AAP's 8 charges.
Log line: `[AAP] Mech Arms (Tech Strike): Damage 10, Pierce 40, Paralyze 18, charges 8.`

### Limited War (ported)
Sheepy's Modnix-era Limited War (the attached Nexus DLL), ported via Mad's updated
adaptation — every patch target re-verified against the current game assembly.
`LimitedWarAdjustments.cs`, gated by mod options (restart game after changing):
- **Zoned attacks**: lost haven defenses against other factions destroy only the
  attacked zone (recruit removed if it was a recruitment zone), log entries renamed
  to "Haven (Zone)". Pandoran zoned attacks off by default.
- **Attack limits**: no one-sided wars (same faction can't attack twice in a row),
  max 3 simultaneous faction attacks map-wide, max 2 per faction, no new wars while
  defending own havens against a Pandoran siege (limit 1). Phoenix is never limited.
- **Alertness**: a faction that loses a haven/zone raises alertness on all its havens.
- **Defense multipliers** (Mad's defaults, hardcoded): alert ×1.2, high alert ×1.1,
  attacker-Pandoran ×1.2, defender Anu ×1.2, defender Synedrion ×1.2.
- Optional: disable Pandoran attacks on Phoenix bases entirely (off by default).
Startup log line: `[AAP][LW] Limited War enabled: …`; runtime events log as
`[AAP][LW] …` (attack cancellations, zone conversions, defense boosts).

---

## Localization (fixed in 1.1 — the "EN stub hardcode" is gone)

Root causes found (two):
1. **The game never imports mod CSVs automatically.** `Assets/Localization/*.csv`
   must be imported manually into `LocalizationManager.Sources[0]` via
   `Import_CSV(..., AddNewTerms)` — same approach TFTV uses. `ModMain.ImportLocalization()`
   now does this at enable time; `ModMain.Localize(key)` reads `AAP_<key>` terms.
2. **The CSV header must use full language names** — `Key,Type,Desc,English,Russian`.
   The old `en,ru` columns would have been registered as *phantom languages* named
   "en"/"ru" that the running game (English/Russian) never displays. (Verified in the
   decompiled I2.Loc `SetupLanguages`/`GetLanguageIndexFromCode`.)

Also fixed: `new LocalizedTextBind("text", true)` — the bool is **doNotLocalize**, which
is exactly the English-hardcode stub. Precision Shot now binds keys
(`AAP_PRECISION_SHOT`/`_DESC`), the research lore lives in the CSV (EN + full RU
translation) under `AAP_MINDFRAGGER_LORE` / `AAP_PSYCHIC_INFLUENCES_LORE`, and the
squad-evac prompt / scrap-aircraft texts are localized too.
Multi-paragraph texts are single-line in the CSV using literal `\n` escapes, converted
by `ModMain.Localize` — avoids CSV parser edge cases.

Verify in game: RU locale shows «Точный выстрел» on the ability and Russian lore in the
research popups; Player.log has
`[AAP] Localization: imported N terms from AAP_Localization.csv.`

---

## Updating the mod on Steam Workshop (PPWorkshopTool)

One-time prep already done on this machine: the WorkshopTool registry
(`%USERPROFILE%\AppData\LocalLow\Snapshot Games Inc\Phoenix Point\Steam\WorkshopTool\Data.xml`,
backup: `Data.xml.backup-AAP11`) now points workshop item **3716593118** at
`E:\PP-mods\AAP_1_1\AssortedAdjustmentsProject` instead of the old AAP_Final path.

Steps to publish a new version:

1. **Build**: `dotnet build -c Release` in
   `AAP_1_1\AssortedAdjustmentsProject\AssortedAdjustmentsProject` (or F7 in VS).
   The build also mirrors the output to `...\Steam\WorkshopTool\TestMod` for local testing.
2. **Test locally**: launch Phoenix Point (WorkshopTool menu: *Project → (Re)Start the
   game*), enable the mod, load a save, check Player.log against the checklist above.
   .NET can't hot-reload — restart the game after every rebuild.
3. **Close the game** (upload while the game is closed).
4. Start **Phoenix Point Workshop Tool** from Steam (Steam client must be running and
   logged into the account that owns item 3716593118). The project list should show
   *Assorted Adjustments Project* pointing at the AAP_1_1 folder.
5. Select the project → **Workshop → Upload Data to Workshop…**
6. Confirm, then enter the change log message (this text becomes the Workshop change
   note — e.g. "1.1.0: silent no-op fixes, mod options, Festering Skies / Corrupted
   Horizons support, proper EN-RU localization"). Everything currently in the project's
   **Dist** folder gets uploaded.
7. Suggested: also update the Workshop page Description (workshop page → Edit) to mention
   DLC compatibility and the new settings, and keep the pinned discussion feature list
   in sync.

Notes:
- Never change `meta.json` "ID" — dependencies track it.
- Version lives in `meta.json` ("Version": "1.1.0" currently) — bump per release.
- Thumbnail can only be set from the Workshop web page after first upload.

## Mod options (native ModConfig — main menu → Mods → AAP → Configure)

| Setting | Default | Notes |
|---|---|---|
| Disable right-click move | on | the 1.0 behavior, now toggleable |
| Smart squad evacuation | on | confirmation-prompted, zone-checked |
| Plentiful item drops | on | loot rework |
| Personal abilities count | 5 | 1–7 |
| Frenzy speed coefficient | 1.5 | was effectively 1.75 in 1.0 |
| Vehicle weapon ammo multiplier | 1.5 | turrets + Kaos Buggy |
| Deployment cap | 16 | 8–32 |
| Festering Skies tweaks | on | + Behemoth speed, flyer % |
| CH: enable / reinforce WP cost | on / 20 | Acheron nerf |

Def-level changes re-apply idempotently on config change (`OnConfigChanged`).

---

## Build & deploy

```
cd AAP_1_1\AssortedAdjustmentsProject\AssortedAdjustmentsProject
dotnet build -c Release
```
- Output: `..\Dist\SergeyWaytov_AssortedAdjustmentsProject.dll` (+ meta.json, Assets).
- The csproj PostBuild automatically mirrors `Dist` into
  `%USERPROFILE%\AppData\LocalLow\Snapshot Games Inc\Phoenix Point\Steam\WorkshopTool\TestMod`
  (verified working on this machine).
- Upload to Steam Workshop with PPWorkshopTool (`_reference_mods/PPWorkshopTool`, or the
  in-game WorkshopTool UI: TestMod → Upload).
- Git: work is committed on branch **AAP_1_1** inside `AssortedAdjustmentsProject/`
  (full history from AAP_Final preserved, including the old WIP branches).

## In-game verification checklist

### The companion collection (AAP_Modlist_Workshop, id 3716582831 — 13 mods)
vehicle resize to 2 · Grenade Throw Range Fix · Edit Soldier Backpack Slots ·
Free Ammo, Medkits And Grenades · Pistol Proficiency Mod · Facility Adjustments ·
XP Overflow · Full Augmentations · More Haven Defenders · Storage/Living Quarters Buff ·
Skip Intro · No Blocked Tiles on Bases · **Range Control** (set Mist 2250 / Radar 3000
per the Workshop discussion). AAP deliberately does not duplicate anything these mods
do — that is why the Mist Repeller / Satellite Uplink code stays commented out.

Enable the mod + the collection's dependency mods, load the KE+FS+CH save, then check
`Player.log` (see `AAP_Cumulative_Backup25.txt` §6 for the filtering command). Expect:

- `[AAP] Vanish AP cost set to 0.` / Manual Control / Rally lines
- `[AAP] Personal abilities limit set to 5 on N stat sheets.`
- `[AAP] Frenzy coefficients set: Speed 1.5 (config) …`
- `[AAP] <piece>: Armor X->Y, Stealth … (aspect: E_BodyPartAspect […])`
- `[AAP] <turret> ammo: N -> M (x1.5).` incl. Kaos_Buggy weapons
- `[AAP] Access Lift: CannotDemolish = true.`
- `[AAP] DLC probe: FS_Autocannon_WeaponDef = found` (KE installed) + `Tyr‑1 Autocannon patched.`
- `[AAP][FS] …Speed … -> 6` and flyer lines (FS installed)
- `[AAP][CH] Acheron_CallReinforcements_AbilityDef: AP 0.75, WP 20.` (CH installed)
- In tactical: poison an enemy → `[AAP] Poison debuff applied …`; evac zone → squad prompt.
