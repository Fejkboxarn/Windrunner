# Development 0.5.0: cinematic warnings and objective guidance

> Integrated into the normal `D:/Fork/Windrunner` project on `feature/zone01-world-blockout`. See [CinematicUIIntegration.md](CinematicUIIntegration.md) for current four-quest bindings, validation and captures. Source-branch evidence below is historical.

Starting point: 0a820fd2 on codex/cinematic-ui. No branch merge.

## Placeholder cinematics

Progression Studio > catalog > cutscenes now exposes `isPlaceholder`. It defaults to false. The two installed Zone 01 milestone sequences and the two engineering fixture sequences are explicitly marked true. The production cinematic owner draws the translucent red X and CUTSCENES ARE NOT FINAL only while its flagged session is active. Completion, skip, interruption and disable use the existing session cleanup. Timeline replay uses the same flag; future authored gallery videos also use that flag. No clips, camera tracks, quest transactions or input handlers were changed. Clear the flag when a sequence is final; it is never inferred from names.

## Objective Wayfinder

Settings > Interface > Objective Wayfinder: ON/OFF is shared by Main Menu and Pause. The presentation preference uses the existing PlayerPrefs convention (`Windrunner.UI.ObjectiveWayfinder`), defaults ON, applies without reload and never changes saved quest state. Distances are straight-line metres, changing to kilometres around 1000 m.

The existing ProgressionJournal owns presentation. It reads its existing service snapshot and `ObjectiveOpen`, with no independent progression store or tracker. The first open authored objective is selected; an unavailable anchor hides guidance instead of pointing at a later objective or world origin. For multiple bindings for the same objective, the first valid authored binding wins. Cross-scene and inactive/destroyed anchors hide. Selection refreshes on progression changes plus a 5 Hz validity check; carried retrieval destinations are resolved every frame, so pickup/expiry do not wait for a save notification. Distance text refreshes at 10 Hz. No per-frame scene discovery. The existing input/menu/cinematic gates suppress the marker; no input lease or camera authority is added.

On the production player's ProgressionJournal, `objectiveAnchors` explicitly binds quest ID, objective ID, phase, Transform, optional label, optional approximate-area disclosure ID and optional existing TimedRetrieval. Approximate anchors require the objective or quest's existing showArea guidance to match. Do not bind a hidden puzzle solution as an exact anchor. Missing location content is intentionally hidden. Current bindings use the existing North Basin area, East overlook area and Waykeeper Transform. Retrieval uses its existing destination while carrying; scene reload intentionally resets carried attempts under the unchanged rules.

Projection uses the camera and the shared safe-area canvas, with behind-camera edge handling, bounded motion, edge hysteresis and reserved quest/context/resource HUD space. The diamond/arrow is objective-based and is not a permanent center crosshair. There is no route planner or minimap. A marker may lie at screen center only when the objective is actually there.

## Version and preservation

PlayerSettings.bundleVersion is 0.5.0. Build metadata records this version. Two pre-existing local Windows Store defaults (package version 1.0.0.0 and tile short name Windrunner) are preserved. Terrain height/paint/roads, scene geometry, animation, movement and balancing are unchanged. The Zone scene diff adds only four explicit journal anchor records. Existing Timeline assets and GUIDs are untouched. Prior build files were inventoried before the build; the existing build pipeline refuses an existing output folder.

## Reproduce rendered player verification

Use the existing Tools/Build-UnityCandidate.ps1 with -MainMenu -Development and a NEW versioned label. Run that build with `-windrunnerWayfinderProbe <new-absolute-evidence-directory> -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile <new-log>`.

The opt-in development-only probe uses disposable save slots beneath its fresh output directory and restores the Wayfinder preference. It exercises the production menu buttons, quest service, item interaction, Timeline, save and load. It stages hunt facts to capture the turn-in transition; it is not evidence of manual combat acceptance. It never runs during ordinary gameplay. Screenshots and passed/failure evidence are written locally. The player window must be visible for real rendered captures; hidden launches produce black frames on this workstation. The fixture rejects black captures, records their actual dimensions and applies requested capture resolution without changing stored display preferences. Previous build folders are never reused.

## Remaining human acceptance

1. Main/Pause Settings > Interface: toggle off/on and verify the marker disappears/reappears without changing the quest.
2. Accept Clear the North Basin; traverse forward, backward and at speed. Check marker readability over sky/forest and edge stability. Complete actual Sling kills; verify guidance returns to the Waykeeper.
3. Turn in: verify the red X/text throughout the placeholder, then skip and replay. Warning must disappear after completion/skip.
4. Accept retrieval: search-area guidance switches to the Waykeeper on pickup, back to the area on expiry/reload. Verify no guide during Pause, Journal, Arsenal or cinematics.
5. Review composition and readability at 720p, 1080p, 1440p and ultrawide. Automated state assertions and inspected captures do not replace moving-camera/gamepad/artistic acceptance.

## Executed evidence

Final validation and build results are recorded below. Failed intermediate attempts remain in local logs: a void Save API call in the first capture fixture, synthetic quest authoring and untitled-scene fixture errors, two obsolete capture-helper API warnings, and black hidden-window screenshots. Visible screenshots additionally exposed a scaled arrow-pivot displacement and a nearby NPC-label overlap; both were corrected before the final candidate. These attempts are not counted as final passing evidence.


Final result: Unity 6000.6.2f1 compilation, **155/155 full Edit Mode**, **58/58 full rendered Play Mode**, **67/67 serialized contracts**. Evidence labels: wayfinder-render-fix-edit, wayfinder-render-fix-play, wayfinder-final-serialized. Zone quest test also ran separately (wayfinder-quest). All final suites have zero failures/skips. The world scene contains only the new journal binding block; local reference IDs and new metadata GUIDs were checked.

Final executable: `D:/Fork/Windrunner/Builds/Windrunner_0_5_0_Windows_Development_r2/Windrunner.exe`. Windows Development build succeeded in 24.72 seconds, **0 errors / 24 warnings**, same warning subjects as ui-identity-candidate: mesh import settings, existing deprecated lookups, Core stripping, optional FEEL resources and vendor shader warnings. The initial non-r2 build is retained but superseded by r2's visual corrections. The r2 player's Application.version assertion reports **0.5.0**.

Four real Windows-player runs passed with ten captures each: `Logs/ProductionMigration/wayfinder-r2-{1080,720,1440,ultrawide}/`. Actual dimensions are 1920x1080, 1280x720, 2560x1440 and 2560x1080. Each folder contains captures.txt and passed.txt; summary: wayfinder-r2-player-report.json. All 40 images passed non-black checks; the contact sheet and representative full-resolution images were inspected. Visible arrow attachment, nearby label separation, readable warning, menu suppression, OFF state and quest destination changes are confirmed in those captures. This is not a manual high-speed traversal sign-off.

Capture index (same filenames at every resolution):

1. `01-setting-on.png`: Main Menu Settings ON.
2. `02-setting-off.png`: Main Menu Settings OFF.
3. `03-hunt-world-distance.png`: actual North Basin area and distance.
4. `04-hunt-edge-distance.png`: offscreen/behind-camera direction and distance.
5. `05-pause-setting.png`: Pause Settings, no Wayfinder overlay.
6. `06-gameplay-wayfinder-off.png`: gameplay with guidance disabled, existing quest tracker remains.
7. `07-hunt-return.png`: completed hunt redirects to Waykeeper, clear of NPC label.
8. `08-placeholder-warning.png`: red X/text on the actual placeholder Timeline.
9. `09-retrieval-search.png`: East overlook search area after skip; warning gone.
10. `10-retrieval-return.png`: pickup changes destination while actual carry timer runs.

Previous builds remain intact: all **19,124** pre-task files have unchanged existence, byte length and modification timestamps. A second manifest also covers the first 0.5.0 build before r2; see wayfinder-build-preservation.json. No previous build was deleted, renamed or overwritten. Four incidental vendor importer keyword changes and the fixture scene's empty-array reserialization were restored after Editor shutdown.

Separate findings left explicit: the existing AirPower meter is clipped against the bottom at 2560x1080, and its existing cinematic skip hint overlaps it at 720p. Neither owner was modified by this change. Native Unity startup/capture logs contain two temporary-allocation lifetime notices per player run; there are no matched managed exception/error signatures. Those notices have not been diagnosed as leaks or attributed to a new gameplay feature. Future video Gallery playback still requires an actual authored video clip. Missing anchor content intentionally hides guidance rather than inventing destinations.

The Windows launcher executable retains Unity engine FileVersion metadata (6000.6.2); the game version is PlayerSettings/Application.version, verified as 0.5.0 by each running player.


## Floating presentation follow-up

The Wayfinder now has a fully transparent background, no rectangle and no surrounding frame. Only its existing icon/arrow, optional label and distance draw, with a restrained 0.75 canvas-unit black outline on each glyph. Their geometry and all behavior remain unchanged. The separate upper-right tracked-quest summary is not the Wayfinder and retains its existing card.

Final Editor Play Mode visual evidence: Logs/ProductionMigration/floating-wayfinder-639306046506960854/{01-open-terrain,02-offscreen,03-bright-sky,04-dark-forest-fixture}.png. The forest image uses temporary instances of the installed spruce prefab, resized for a foliage backdrop and darkened through runtime lighting only. The sky case uses a temporary world-space contrast anchor. Neither fixture alters authored content. All four images were inspected for background removal and glyph readability.

Validation: floating-wayfinder-edit (9/9); floating-wayfinder-play (2/2, including actual hunt/retrieval/expiry/retry/reload/menu/cinematic regressions); floating-wayfinder-capture-final (1/1 rerun after waiting for shader compilation); floating-wayfinder-serialized (67/67). Existing Windows executables predate this follow-up and were not rebuilt or changed.
