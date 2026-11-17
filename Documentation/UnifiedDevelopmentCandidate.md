# Unified Windrunner development candidate

Branch `codex/unified-development` starts at audited Terrain Studio `a7ff50c3` through its documentation-only child `19e0cd87`. Checkpoint `2954a3d7` fixes both audit failures. The two-parent integration checkpoint `c3fab8d6` joins that fix and `feature/movement-lab` at `0fab74bd`, including all four divergent commits. No changes were made to main or to the original feature tips.

## Session and input behavior

* **Persistent play.** New Game creates a `zone-01` record. The record opened by the slot store supplies both its progression and its world ID. PlayerSession maps the two supported IDs to their full authored scene paths, chooses the corresponding catalog, and verifies scene and definitions on arrival. Current save, autosave and return-to-title flush the bound store. Copy and confirmed overwrite snapshot the active service and pass the source session’s world explicitly. Load and Continue use the saved route. The gallery uses the inspected slot’s catalog.
* **Direct play.** Direct entry into either registered gameplay scene uses a memory store and can create an explicit copy with that world’s ID. An unregistered disposable preview can run in memory but cannot save a copy under an invented route. The existing arrival checkpoint policy restores progress at the authored safe spawn with fresh traversal state.
* **Compatibility.** The envelope version, checksums, revision checks, atomic replacement and legacy `starting-world` records are preserved. Unknown IDs are rejected. There is no bulk migration. An old save already mislabeled by the former bug still routes according to its recorded ID. Recover such a slot from a known-good original or backup; an empty or ambiguous snapshot cannot determine its world.
* **Binding ownership.** Both templates retain the existing PlayerPrefs key and Input System v1 binding-ID format. On load the input service applies only bindings present in its runtime template. Before persisting a rebind it reads the latest preference document, overlays just the locally changed binding records and orders the result by ID. Unseen IDs, empty paths, interactions and processors survive. Shared IDs follow the player across templates. Reset clears every binding in the current template (including shared bindings) and preserves IDs exclusive to other templates. Input device handlers and pause/interrupt lifetimes remain owned by the same service.

## Merge resolutions

| File | Resolution |
| --- | --- |
| `Assets/AirflowPrototype/Editor/WindrunnerCandidateBuild.cs` | Production uses the explicit title, Zone 01 and legacy scene manifest. Gym selects just MovementGym, Development mode and the gym-only flight define. The fixture target and default enabled-scene behavior are retained. An explicit Development option produces the production-route debug candidate without gym flight. Conflicting scene selectors are rejected. |
| `Tools/Build-UnityCandidate.ps1` | Combined selectors and retained the closed-Editor guard, matching Unity version, fresh output label and build-report verification. Tested conflicting menu/gym selectors before launching Unity. |
| `Documentation/Progress.md` | Kept both branches’ histories, appending the gym section from its divergence point. |
| `Documentation/TestResults.json` | Merged by evidence identity with collision checks, preserving 23 additional lab test runs, three builds, two smoke runs and 39 additional log hashes. Retained lab source hashes under their originating checkpoint and kept the baseline’s hashes. New measurements are in [UnifiedCandidateResults.json](UnifiedCandidateResults.json). |

The whole MovementLab asset folder and metadata compare byte-for-byte with `0fab74bd`. The two production frontend scenes, Zone 01 scene/objects/quests/TerrainData, terrain presets, package manifests and ProjectSettings compare byte-for-byte with `a7ff50c3`. The two lab changes to the shared motor/camera are the incoming per-player influence and teleport reset. Production scenes have no gym influence provider. The existing production portable animation, Character Tuning Studio and progression services remain the owners of their respective systems.

## Launch and acceptance

* **Production-facing.** Start `Builds/unified-windows-dev/Windrunner.exe` for the Windows Development candidate. Main Menu, settings, slots, Zone 01, its authored quests and legacy loading are available. `Builds/unified-production/Windrunner.exe` is the non-Development build of those same routes.
* **Development-only.** Start `Builds/unified-gym/Windrunner.exe` as an independent process, or open the gym through Movement Lab’s Editor tools. It has its own persistent root, mapping and experimental equipment. No main-menu destination connects it to a running production session. Terrain and character authoring tools remain Editor-only. Fixture and painted preview are separate validation builds.
* **Manual acceptance still open.** (1) On a named test slot, complete a Zone 01 objective, pause, save a copy, overwrite a separate slot, quit, reload each and Continue; check the journal, dialogue, arrival and gallery. Repeat with a legacy slot. (2) Rebind Jump on production and Reset in the gym; restart each, reconnect a controller and check both mappings, pause and scoped reset. (3) Compare production ground/air movement, Sling, Dash, Cast, swim, camera, animation and waterfall attenuation with the accepted baseline. (4) In the independent gym try F1–F10, continuous and impulse wind, Overdrive depletion, F11 inspection flight and F12 reset; then close the gym and start production normally. (5) On a disposable terrain copy inspect protected artwork, road intersections, seams and wheel wear in motion. Check Character Tuning preview/discard against a clean asset diff.

## Validation on Unity 6000.6.2f1

| Check | Executed result |
| --- | --- |
| Standard Edit Mode | 82/82 passed |
| Standard Play Mode | 38/38 passed |
| Additional rendered/profiling cases | 3/3 Edit Mode and 7/7 Play Mode passed |
| Separate uncapped profile | 1/1 passed |
| Source serialization contracts | Zero failures |
| Loaded scene references | Gym, Main Menu, Zone 01, legacy world and progression fixture tests passed |

The merged runs executed 130 distinct test cases and one additional uncapped repeat. Before the merge, both original defect reproductions and the real menu transition control also passed at the defect-fix checkpoint. The inert audit probe templates reproduce the historical baseline; the integrated suite uses the checked-in production and gym input assets directly. It covers bidirectional persistence, deterministic resave and reset scope alongside the interactive input and device lifecycle tests.

| Windows build | Errors | Warnings | Rendered startup |
| --- | ---: | ---: | --- |
| Production routes, Development | 0 | 38 | Passed, 15 s |
| Production routes, non-Development | 0 | 24 | Passed, 15 s |
| Standalone Movement Gym, Development | 0 | 25 | Passed, 15 s |
| Progression fixture | 0 | 5 | Passed, 15 s |
| Disposable painted basin | 0 | 8 | Passed, 15 s |

Each process initialized, survived its observation period and produced zero matched runtime errors. The painted build also verified inclusion of the working TerrainData and wheel mesh and exclusion of the authoring history. Gameplay and UI interaction evidence comes from the Unity suites; the executable checks exercised startup.

**Warnings and limits.** Against the prior 21-warning production build, this non-Development build also reported obsolete object searches at `AirChimeTrial.cs:45`, `AutumnPostProcessController.cs:70` and `PlayerWindFeedback.cs:41`. Those three source files match the baseline. Other diagnostics cover mesh read/write and collision bake settings, stripped Core debug shaders, optional FEEL resources, and Raygeas, progress-bar and Toony Colors shader diagnostics. Development builds also report FEEL reflection/serialization analyzer warnings. [UnifiedCandidateResults.json](UnifiedCandidateResults.json) records the exact target-specific messages, compiler diagnostics, test identities, evidence hashes, build scene lists and startup results.

Terrain Studio undo-memory behavior was left in place. The earlier 480-step investigation remains the long-session evidence. Human visual, audio and gameplay acceptance and recovery of any historically mistagged individual saves remain open.
