# Windrunner cinematic UI

## Baseline and scope

Dedicated `codex/cinematic-ui` branch from clean `5b743597` on `feature/zone01-world-blockout`. The original world branch and `codex/dagger-pack-animation` are unchanged. No merge. The world branch has the portable production character and existing posed Arsenal portrait, but does not include later animation-branch authoring work. No new character, animation, terrain or quest content is imported.

## Reference and original direction

Research sources: [official PlayStation game page](https://www.playstation.com/en-us/games/marvels-wolverine/), [official gameplay trailer](https://www.youtube.com/watch?v=s3pDMUWlA6I), [official features article](https://blog.playstation.com/?p=422270). The official page exposes gameplay screenshots, character presentation, equipment and accessibility information. Available source text establishes features, **not exact menu geometry, focus timing or transition choreography**. Embedded footage was not frame-analyzed; image fetches were incomplete. Secondary equipment-screen descriptions were useful leads, not evidence of directly observed motion. No proprietary image, logo, font, icon, sound or code was downloaded into the project.

The following are Windrunner design decisions/inferences, not claims of a literal Wolverine reconstruction: strong left navigation; large branding and negative space; central character study separated from choice and detail columns; a single restrained focus accent; short unscaled motion; stable controller focus; reduced information density. Windrunner's original palette uses slate, pale text and wind-blue. A lightweight generated ridge silhouette and drifting wind lines provide configurable placeholder atmosphere. These remain placeholder art, not final painted background production assets.

## UI inventory and ownership

| Surface | Existing technology/authority | Presentation change |
| --- | --- | --- |
| Title, Pause, confirmation | Runtime uGUI, SessionMenu/MenuLayout, existing input host | Left primary navigation, branding, geometric background, focus accents, safe-area layout, page entrance and responsive exit |
| Continue/New/Save/Load | Existing PlayerSession and PlaythroughSlots | Wide multiline rows; existing identity, zone, completed-quest count, timestamp/current marker, recovery and error text retained |
| Settings | Existing DesktopPreferences and input service | Shared category layout, real keyboard/gamepad/pointer volume sliders, focus memory, reduced-motion and menu scale preferences |
| Arsenal | Existing equipment service, DaggerPreview posed mesh snapshot | Dedicated left/center/right regions, actual character/weapons, preserved rotate/equip/unequip behavior |
| Journal | Existing IMGUI and progression/input/time leases | Navigation, objectives, connections/cinematics columns; shared palette/font; safe-area fit |
| Quest HUD/prompts/notifications | Existing journal projection | Smaller tracked panel, context-only prompts, unified colors |
| AirPower, reserve, Cast/Sling charge | Existing runtime material lease and UI adapters | Shared runtime-only meter palette; reserve text/font/flash identity retained; authored shader materials and timing untouched |
| Target indicators/world feedback | Existing production targeting/feedback | Existing silhouettes and ownership retained for high-speed readability; no changes to perfect windows, targeting, suppression or FEEL |

No duplicate save, input, equipment or animation authority. Existing UI frameworks remain. Portrait is a baked snapshot, not an extra Animator or live gameplay clone. Optional focus/confirm sounds create a single menu AudioSource only when a configured clip is used. No sound clips are assigned by default. The existing production female placeholder rig remains visible; a final male assassin model is an authored-content dependency, outside this UI task.

## Interaction behavior

Buttons use short color fades, a growing accent line and small label movement. Page entrance uses unscaled opacity/translation. Closing releases input immediately and leaves a brief noninteractive visual fade; reopening cancels that exit. Reduced motion removes these movements and backdrop drift. Transitions never defer save/equipment operations. Screen readers and a complete accessibility system are not implemented.

Per-page selection index survives nested navigation and same-page refresh. Disabled rows are skipped. Long Controls and save lists retain the existing ScrollRect and reveal selected rows. Confirmations initially choose Cancel. Footer labels derive from the active keyboard/gamepad bindings; changing input source updates labels. They are text labels, not proprietary controller glyphs. Journal keeps its existing progression actions/category controls and keyboard/gamepad binding labels. The existing page-scroll actions scroll both reading panes so overflowed relationship/cinematic details remain accessible on controller.

Volume sliders preserve arbitrary existing values; keyboard/gamepad adjustments use the same ten-percent increments (pointer dragging is continuous) and existing immediate persistence/mixer routing. Display changes retain Apply/Keep/Cancel and timeout recovery. No save schema changes. Menu scale 100/110/120% applies to uGUI session menus; Journal uses an independent safe-area-fitted 1280x720 canvas, and gameplay HUD keeps its established size. Do not imply this option scales every in-world label.

## Customization

1. Select `Assets/WindrunnerProduction/Runtime/Resources/WindrunnerUITheme.asset` in the Inspector, outside Play Mode.
2. Edit palette, body/heading/brand sizes, row spacing, focus/entrance duration, optional font and sound slots. Keep text/background contrast high. Leave optional sounds empty when suitable authored assets are unavailable.
3. Reopen menus or restart Play Mode to rebuild styles. This is an authored theme, not an implicit live-preview persistence system. Journal caches styles until reload; runtime meter materials copy the theme when their lease is created.
4. Retain the Resources name/location used by WindrunnerUITheme.Current. The fallback prevents missing-theme failure, but a duplicate theme is not automatically selected.
5. Use Settings > Accessibility for player reduced motion/menu scale. These use presentation-only PlayerPrefs keys and never enter playthrough records.
6. Run the rendered screen test below after changing text sizes, font, margins or transition times. The generated background contains no imported texture and can be replaced later inside the existing presentation component.

## Reproduction

Open the existing MainMenu scene and enter Play Mode. Continue requires a valid save; New game opens the unchanged production Zone 01. Pause opens Save/Load, Settings and Arsenal. Open Journal with its current binding. No fixtures are added to Zone 01.

`Tools/Run-UnityTests.ps1 -Platform PlayMode -Rendered -Filter CinematicScreensRenderWithNavigationAndPortrait -Label <fresh-label>` uses disposable slots and captures real production screens. It restores presentation preferences and temporary Game View sizes. Screenshot paths are `Logs/ProductionMigration/cinematic-<screen>-<width>x<height>.png`; rerunning replaces only this generated capture set. Full suites exclude this Profiling-category capture test by default. The ordinary suite includes the focus/slider/reduced-motion regression.

## Manual acceptance still required

- Judge the original art direction, pacing and contrast on the intended display; generated silhouettes and built-in font are configurable placeholders.
- Traverse naturally at high speed and judge Cast/Sling perfect feedback and both resource meters against bright/dark terrain.
- Try physical controller layouts and mouse/controller switching, rebind navigation, long saves and every nested Back path.
- Open/close Pause/Arsenal rapidly during gameplay; no fading control may accept input or keep ownership.
- Inspect actual final character art when available. This task does not replace the production rig or animation content.
- Validate chosen focus/confirm audio only after clips are authored/assigned.

Exact executed results and screenshots are recorded below after validation. Compilation alone is not visual acceptance.

## Rendered screen index

Local generated evidence (not shipped as game assets):

- [cinematic-accessibility-1920x1080](../Logs/ProductionMigration/cinematic-accessibility-1920x1080.png)
- [cinematic-arsenal-equipped-1920x1080](../Logs/ProductionMigration/cinematic-arsenal-equipped-1920x1080.png)
- [cinematic-arsenal-farseer-1280x720](../Logs/ProductionMigration/cinematic-arsenal-farseer-1280x720.png)
- [cinematic-arsenal-farseer-1920x1080](../Logs/ProductionMigration/cinematic-arsenal-farseer-1920x1080.png)
- [cinematic-arsenal-training-1920x1080](../Logs/ProductionMigration/cinematic-arsenal-training-1920x1080.png)
- [cinematic-confirmation-1920x1080](../Logs/ProductionMigration/cinematic-confirmation-1920x1080.png)
- [cinematic-controls-1920x1080](../Logs/ProductionMigration/cinematic-controls-1920x1080.png)
- [cinematic-display-1920x1080](../Logs/ProductionMigration/cinematic-display-1920x1080.png)
- [cinematic-gallery-1920x1080](../Logs/ProductionMigration/cinematic-gallery-1920x1080.png)
- [cinematic-hud-1920x1080](../Logs/ProductionMigration/cinematic-hud-1920x1080.png)
- [cinematic-journal-1280x720](../Logs/ProductionMigration/cinematic-journal-1280x720.png)
- [cinematic-journal-1920x1080](../Logs/ProductionMigration/cinematic-journal-1920x1080.png)
- [cinematic-load-1920x1080](../Logs/ProductionMigration/cinematic-load-1920x1080.png)
- [cinematic-main-1280x720](../Logs/ProductionMigration/cinematic-main-1280x720.png)
- [cinematic-main-1600x900](../Logs/ProductionMigration/cinematic-main-1600x900.png)
- [cinematic-main-1920x1080](../Logs/ProductionMigration/cinematic-main-1920x1080.png)
- [cinematic-main-2560x1080](../Logs/ProductionMigration/cinematic-main-2560x1080.png)
- [cinematic-main-2560x1440](../Logs/ProductionMigration/cinematic-main-2560x1440.png)
- [cinematic-new-game-1920x1080](../Logs/ProductionMigration/cinematic-new-game-1920x1080.png)
- [cinematic-pause-1920x1080](../Logs/ProductionMigration/cinematic-pause-1920x1080.png)
- [cinematic-reduced-motion-large-text-1280x720](../Logs/ProductionMigration/cinematic-reduced-motion-large-text-1280x720.png)
- [cinematic-save-1280x720](../Logs/ProductionMigration/cinematic-save-1280x720.png)
- [cinematic-save-1920x1080](../Logs/ProductionMigration/cinematic-save-1920x1080.png)
- [cinematic-save-copy-1920x1080](../Logs/ProductionMigration/cinematic-save-copy-1920x1080.png)
- [cinematic-settings-1280x720](../Logs/ProductionMigration/cinematic-settings-1280x720.png)
- [cinematic-settings-1600x900](../Logs/ProductionMigration/cinematic-settings-1600x900.png)
- [cinematic-settings-1920x1080](../Logs/ProductionMigration/cinematic-settings-1920x1080.png)
- [cinematic-settings-2560x1080](../Logs/ProductionMigration/cinematic-settings-2560x1080.png)
- [cinematic-settings-2560x1440](../Logs/ProductionMigration/cinematic-settings-2560x1440.png)
- [cinematic-sound-1920x1080](../Logs/ProductionMigration/cinematic-sound-1920x1080.png)

Baseline title comparison: `Logs/ProductionMigration/main-menu-1920-639305955805431973.png` from the passing `ui-before` run. The final 720p/ultrawide inspection covers portrait columns, Journal objectives, save-row metadata and title navigation. Long Controls lists intentionally scroll; they now disclose that more rows exist. No screenshot is a substitute for physical-controller or motion/contrast acceptance.

## Validation evidence

- Unity 6000.6.2f1 compilation succeeded through tests and the Windows Development build.
- Full Play Mode: **57/57**, no failures/skips (`ui-release-play`). Includes new disabled-focus, nested-page memory, repeat destructive-dialog Cancel focus, slider move/identity and reduced-motion checks; existing save transitions, device/rebinding, Journal/progression, ability, equipment, target suppression and lifecycle tests remain passing.
- Full Edit Mode: **146/146**, no failures/skips (`ui-release-edit`), on the final source.
- Rendered capture: **1/1** (`ui-screens-accepted`), 30 images indexed above. Actual production data, equipped/unequipped Arsenal states, portrait texture and generated backdrop mesh checked. Default-scale title bounds asserted at all five resolutions. Minor subsequent slider precision and Journal scrolling changes do not alter the settled screenshot layout; final full suites exercise the final source.
- Serialized contracts: **67/67**, no failures (`ui-release-serialized.json`); no duplicate GUIDs in Assets. `ui-preservation.json` records unchanged original branch heads and protected paths. No accepted scenes/prefabs, TerrainData, roads, input assets, assemblies, settings or packages are part of the change.
- Windows Development build: **Succeeded**, **0 errors / 24 warnings**, **32.86 s**. `Builds/cinematic-ui-candidate/Windrunner.exe` includes MainMenu, Zone 01 and StartingWorld. Full warning text: `Logs/ProductionMigration/cinematic-ui-candidate-build.json`.
- Rendered Windows startup: **15 s**, no matched errors. Startup/idle observation only, not a gameplay, frame-budget or GPU-performance claim. Evidence: `cinematic-ui-candidate-rendered-player.json`.

Warning origins are unchanged assets/source: mesh Read/Write and collider pre-bake settings; three existing FindFirstObjectByType uses; stripped Core debug shaders; optional FEEL third-party resources; INab progress-bar, Toony Colors Pro and Raygeas shader compatibility warnings. No warning names a new UI implementation file. These warnings remain, and this pass does not silently repair unrelated vendor/terrain content.

Earlier failures are retained: the capture fixture initially used an obsolete CanvasRenderer overload, then exposed a missing CanvasRenderer on the custom graphic; an optional unused AudioSource changed the existing audio-route count; the new fixture needed Unity's EntityId API and a title refresh after assigning its disposable save directory. Each was corrected. Screenshot inspection additionally caught initial white button transitions and ultrawide title clipping. Passing initial compilation was not treated as visual proof.
