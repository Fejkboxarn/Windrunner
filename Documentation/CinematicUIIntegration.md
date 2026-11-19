# Cinematic UI and four-quest Wayfinder integration

## Open the unified project

Use `D:/Fork/Windrunner`, branch `feature/zone01-world-blockout`. Open the existing Main Menu to use saved playthroughs, or production `World_Zone01.unity` for unsaved development. Development version is **0.5.0**. WINDRUNNER remains a configurable temporary title; subtitle/tagline are empty. No final identity or lore was invented.

## Source and conflict decisions

Destination started clean at `3cdafc41`. Recovery tag: `checkpoint/zone01-before-cinematic-ui-3cdafc41`. Source latest verified checkpoint was `3d85030e`, including the glyph-only, transparent Wayfinder correction.

| Source | Destination transfer | Feature |
|---|---|---|
| d4a78df0 | 0c8a408b | Shared cinematic menus/HUD, focus, transitions, portrait |
| fcc43a3b | d952e92a | Configurable temporary branding |
| 0a820fd2 | fa5406a0 | Permanent crosshair removal |
| 0d6b08a4 | 02ec52de | Wayfinder, interface preference, placeholder warning, 0.5.0 |
| 3d85030e | 898b1dfd | Transparent Wayfinder with glyph outlines |

Controlled cherry-picks, not a whole-branch merge. Progress-document append conflicts retain both histories; source legacy punctuation encoding was normalized to UTF-8. Scene/catalog conflicts were resolved from the destination's four-quest content, adding only the UI data. Newer ranks, quest IDs, challenge definitions and encounter placements were not replaced by the older two-quest source. No animation/performance branch was imported. Existing worktrees and environment packages remain intact.

## Guidance ownership and configuration

The existing ProgressionJournal remains the only objective-presentation owner. It selects the tracked quest's open objective through the existing progression service. Nine explicit bindings are serialized on the production Journal:

| Quest/phase | Anchor |
|---|---|
| Hunt | Existing approximate North Basin area |
| Hunt turn-in | Waykeeper |
| Retrieval | Existing approximate East overlook; current retrieval destination while carrying |
| Retrieval turn-in | Waykeeper |
| Relay | Existing ZoneQuestActivity provider: first currently valid relay in authored binding order; retry station if no relay is available |
| Ground pursuit | Existing ZoneQuestActivity provider: live ground target |
| Air pursuit | Existing ZoneQuestActivity provider: live airborne target |
| Relay/pursuit turn-in | Waykeeper (one binding per quest) |

A small `IObjectiveAnchorProvider` contract bridges the portable progression assembly and scene activity adapter. It has no timer, quest state, movement or save authority. Each frame resolves the live Transform, so a reversal/elevation change does not leave a marker at an old route point. Relay choices come from the existing session's valid-node list; locked/activated nodes are not selected. The current course is sequential; branching courses retain their authored valid-node order rather than introducing a new routing algorithm. Approximate hunt/retrieval disclosure remains unchanged.

Inactive, destroyed and cross-scene targets hide; a destroyed provider does not fall back to a stale static Transform. Objective changes, retries and reloads rebind through the Journal's existing refresh cycle. Ground capture advances to the air objective; both captures advance to NPC turn-in. Retry does not reset saved progression. Failed pursuits have no live target guidance until retry; their labeled retry stations remain in the world.

Settings > Interface > Objective Wayfinder toggles the existing presentation preference. Pause, Settings, Save/Load, Arsenal, Journal and cinematics suppress it through existing blocking ownership. No extra pause/input lease is introduced by guidance. There is no permanent center reticle. Existing ability target rings, charge feedback and highlights remain distinct and enabled.

The Wayfinder draws only its diamond/arrow, optional label and distance with individual glyph outlines. There is no background texture, panel or surrounding frame. The separate upper-right quest-summary card intentionally retains its background and scrolling content.

## Cinematics and other UI

All four Zone 01 milestone definitions are explicitly `isPlaceholder: true`. The existing cinematic owner displays the red X and CUTSCENES ARE NOT FINAL only for a currently playing flagged definition. Unflagged/final content retains the default false flag. Skip, normal completion, replay, disabling and reload use the existing lifecycle. No Timeline content or camera choreography changed.

Main Menu, Pause, Save/Load, Settings, Arsenal/portrait, Journal, HUD styling and shared presentation assets are the source implementations. Save Copy/Overwrite/recovery, equipment, rewards and quest transaction authorities remain the destination implementations. Gallery remains available; future video playback requires actual authored video content and none was fabricated.

## Preservation evidence

Compared against `3cdafc41`: all **712** existing scene records remain; only Journal record **863015129** gains its anchor list. No transforms, object placement, components, routes or scene GUIDs were replaced. The catalog differs only by four explicit placeholder flags. All **39** other tracked world files, including TerrainData and activity profiles/timelines, compare byte-for-byte. Terrain heights/paint, roads, overrides, vegetation, Air Nodes, relay and pursuit locations remain unchanged. No package reimport or deletion was requested; incidental Unity importer normalization is restored before the final checkpoint.

## Validation and screenshots

See `CinematicUIIntegrationEvidence.json` for actual test/build results, hashes and capture inventory. Tests use disposable saves. The four-quest flow exercises real production Cast and movement callbacks, relay expiry/retry and next-node guidance, ground near-miss/reversal, airborne route movement/height change, capture, NPC turns, old saves, partial/reloaded saves, completed restoration, menu suppression and the persisted preference. Existing source suites cover hunt/retrieval destination changes and timeout, finalized versus placeholder warning behavior, UI focus, crosshair absence and save/equipment lifecycle.

Final four-quest UI captures: `Logs/CinematicUIIntegration/rendered-639306832052238797/` (15 PNGs plus contact sheet). Includes Main Menu, Pause, Settings, Save, Arsenal portrait, Journal before/after all four quests, HUD, relay and both pursuit guides, and all four placeholder warnings. Contrast captures: `Logs/ProductionMigration/floating-wayfinder-639306831394109275/` (open terrain, offscreen, sky and disposable dark-forest backdrop). These are rendered Editor Game View captures produced by command-line tests, not desktop automation or manual acceptance. Diagnostic camera placement is temporary; captured distance remains measured from the actual player, not the camera. The forest/sky fixtures exist only in the test session; no authored terrain or vegetation is saved.

The initial moving-target capture test missed from a trailing approach. The final test approaches a close interception point and lands a real Cast without changing projectile assistance, speed, hit radius or pursuit settings. This is not a gameplay-balance fix or proof that every moving-target shot should hit.

## Remaining manual acceptance

- Natural navigation, controller feel and high-speed camera readability across the four quest route areas.
- Gameplay difficulty of airborne interception and ground near misses.
- Readability at the user's preferred resolution/UI scale. Current inspected 1080p captures show readable floating glyphs and no Wayfinder panel. The long pursuit summary uses its existing scroll area; full objective text is available in the Journal.
- Previously documented source limitations remain: AirPower clipping at 2560x1080 and skip-hint overlap at 720p were not reworked by this integration. No new multi-resolution acceptance is claimed.
- Placeholder art/cinematics remain intentionally unfinished. Static capture inspection does not establish animation/artistic acceptance or target-hardware frame-time performance.

## Executed completion results

Full rendered Edit Mode **189/189**, full rendered Play Mode **68/68**; zero failures/skips. Serialized contracts **67**, zero failures; GUID audit **180**, zero missing or duplicate transferred GUIDs. Dedicated final four-quest capture passed, and the contrast fixture passed. Windows Development build **0 errors / 24 existing warnings**, version **0.5.0**; rendered 15-second startup passed. Candidate: `Builds/Windrunner_0_5_0_Zone01_Cinematic_Integration/Windrunner.exe`. Existing builds are not overwritten. Detailed warnings, failed intermediate evidence and result hashes are retained in the evidence JSON.
