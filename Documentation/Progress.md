# Migration progress

## Progression framework continuation (2026-11-13)

Baseline aa9e9d58 verified clean; branch feature/progression-framework. Hardening work is retained. Stage 1 definitions/state/rules/persistence implemented; progression-foundation Edit Mode **37/37** and existing Play Mode **25/25**, exit 0. Serialized contract and whitespace checks passed. See ProgressionFramework.md for the staged scope; no complete-framework claim yet.

Stage 1 checkpoint 3ffce279. World integration: progression-world-compile and progression-world-final Edit Mode **40/40** each; progression-world and progression-world-final Play Mode **28/28** each, exit 0. Real physics checkpoint, range checks, quest evaluation, independent race/puzzle rules and cinematic nested-pause restoration execute. Initial progression-world compile failed on an incorrect test-only Input System named parameter; corrected, original log retained. Serialized gate passed; no existing scene/prefab/profile edits. Next: Studio/journal/fixtures and expanded integration validation.

Stage 2 checkpoint 769a1b00. Stage 3 adds searchable Progression Studio with Undo/validated save/remapped duplication, keyboard-first journal/HUD and explicit missing-content fallback, persistent-root input handoff and one registry-owned feedback observer. An isolated engineering fixture contains two independent chapter definitions, a race, switch puzzle and one-second marker Timeline, excluded from shipping build settings. progression-studio-final: **41/41 Edit Mode, 29/29 Play Mode**, exit 0. Initial scene-generation and fixture-lifecycle failures plus fixes are preserved in ProgressionValidation.md/TestResults.json. Existing serialized contracts passed. No existing gameplay scene, prefab or profile changed. Next: final robustness, profiling and Windows verification.

Stage 3 checkpoint f45cdb8b. Final framework candidate: **46/46 Edit Mode** (progression-candidate), **31/31 full Play Mode** (progression-notification-final), rendered motor-driven fixture **1/1**, command/storage profiling **1/1**; all exit 0. Later checks cover every objective kind, real projectile-to-mechanism execution, persistent collection presentation, whole-fixture serialized references, scene binding guards, frozen world-session definitions and cinematic save-failure control recovery. Scene reload does not replay historical journal notifications.

Final Windows main: Builds/progression-final-20261113/Windrunner.exe, **Succeeded, 0 errors, 21 reported warnings**, 24.2806404 s, 1,212,201,809 bytes. Final standalone engineering fixture: Builds/progression-final-fixture-20261113/Windrunner.exe, **Succeeded, 0 errors, 5 warnings**, 6.0804185 s, 113,727,421 bytes. Both contain the identical final progression assembly hash recorded in ProgressionReleaseCandidate.md. Main/fixture rendered startup and main headless startup each survived 15 s with initialization and no matched errors. Harness termination is not graceful-shutdown acceptance. The three old CS0618 diagnostics absent from the last incremental build report remain unfixed; no new progression warning subject observed.

Serialized gate: zero failures; no pre-existing scene/prefab/profile/meta changes from aa9e9d58. Code/documentation whitespace checks passed. Command CPU median/p95: 10 quests 0.1927/0.2570 ms; 100 quests 1.8436/4.9651 ms; separate small durable writes 3.0752/4.8330 ms. See ProgressionPerformance.md for actual scope; no new allocation/GPU claim.

Continuation boundary: requested single-local-player framework implementation is integrated as an opt-in engineering candidate, not final story content or production-readiness sign-off. Start with ProgressionReleaseCandidate.md and ProgressionAuthoring.md. Remaining work is authored content/recipes, the explicitly unaccepted manual checklist, large-save/burst and target-player profiling, and future external reward consumers when designed. Do not repeat Character Tuning hardening, delete legacy assets, or silently install fixture quests in the playground. Nothing pushed or merged.

Baseline: `7bfdfe5e` (5P), `production-refactor`, 2026-11-12.

## Current handoff

RC2 packaged successfully: `Builds/migration-rc2-20261112/Windrunner.exe`, Unity 6000.6.2f1, zero errors / 24 classified warnings, 26.164 seconds, 1,222,126,654 bytes. Its 15-second headless startup initialized successfully with no matched exception/error/crash/load-failure lines; harness terminated its own process afterward. This does not test rendered player quality or graceful shutdown. ArchitectureReview.md and ReleaseCandidate.md identify unfinished gates; no migration-complete claim.

Resumed after usage-limit interruption at `67938aea`. The uncommitted profiling/warning stage was validated and checkpointed at `63e3408c`, followed by animation ownership `4bb6c7e8`, scoped camera/rewards `9dda123d`, and scene lifetime `a15a886f`. Latest suites: **27/27 EditMode** (`scene-lifetime-final`) and **18/18 PlayMode** (`scene-lifetime-cursor`), exit 0. Two rendered profiling runs each passed 1/1 with native frame allocation calibration. BuildWarnings.md/JSON classify all 21 original RC1 build warnings. RC2 packaging follows below.

Overall production architecture migration remains incomplete; manual acceptance and the remaining scope are at the end of this file. TestResults.json preserves actual runs, including failures, source hashes, rendered profiles and build reports. Earlier counts below are historical stage evidence.

## Historical first-pass record

- Repository/source inventory and migration plan created. No 5Q implementation identified.
- Stage 1 complete: executable baseline, inventory, documentation and content-debt gate.
- Stage 1 checkpoint: `71f3f62`.
- Material ownership checkpoint: `4a1503bb`.
- Motor simulation checkpoint: `933d05f3`.
- Current: checkpoint validated feedback/animation lifetimes and composition checks. Overall migration is **not complete**.
- Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe`.
- Baseline Unity licensing and compilation confirmed by actual batch runs.
- `baseline-EditMode`: 4 passed / 11 failed (wrapper incorrectly rejected normal PASS logs). Corrected wrapper captures errors and requires a PASS message.
- `baseline-fixed-EditMode`: **15 passed / 0 failed**, exit 0.
- `baseline-content-EditMode`: compilation failed on obsolete Unity 6.6 `objectReferenceInstanceIDValue`; changed to entity ID API.
- `baseline-content-fixed-EditMode`: 15 passed / 1 failed, batch-mode empty scene restoration error; corrected cleanup.
- `baseline-content-final-EditMode`: 15 passed / 1 failed, revealing 10 existing broken references: four waterfall AudioSource resources and six bird particle sprite entries. No missing scripts reported. Test now checks exact baseline debt; it does not claim clean content.
- `baseline-PlayMode`: **1 passed / 0 failed**, exit 0. Loaded actual gameplay scene, found one motor and production input host, checked finite player position for 60 frames. This is startup smoke coverage, not gameplay-feel acceptance.
- Raw XML and logs: `Logs/ProductionMigration/` (ignored local artifacts). Baseline inventory includes serialized SHA-256 fingerprints and no duplicate Assets GUIDs.
- `baseline-recorded-EditMode`: **16 passed / 0 failed**, exit 0; integrity test requires the exact 10 baseline reference issues, with no additions.
- Baseline PlayMode changed two shader float values in vendor `Canvas 4.mat` via the resource bar. Restored the known test-induced changes from HEAD; no user changes were present. This is a real asset-ownership defect, so its fix precedes motor work. No scene/prefab migration has been performed.
- Material ownership: added a regular C# lease owned by AirPowerProgressBarUI. It clones the plugin material in Play Mode, binds its graphic/renderer, restores references and destroys the clone on teardown. Vendor code and serialized fields remain unchanged.
- `material-ownership-PlayMode`: **1 passed / 0 failed**, exit 0. Actual scene startup plus authored-material immutability, runtime instance ownership and destruction after scene unload verified.
- `material-ownership-EditMode`: **16 passed / 0 failed**, exit 0.
- Baseline serialized fingerprint comparison: **0 changed**; Git diff has no scene, prefab, vendor material or existing metadata changes.
- Motor simulation: regular C# owns velocity, jump/reward frame ordering, gravity, collision response, impact reset and external traversal velocity lifetime. PlayerMotor retains identical serialized fields/public API and performs the same CharacterController.Move plus facing update. No scripts/assets moved, no execution-order changes, no gameplay tuning changes.
- `motor-simulation-EditMode`: **17 passed / 0 failed**, exit 0. New frozen-5P integration-order oracle compared 60,000 frames at 30/60/144 Hz and 100 ms, with impulses, rewards, jumps, collision normals, landings, impact reset and external velocity lifetime. Existing individual-rule parity checks also passed. This does not prove full physics/gameplay-feel parity.
- `motor-simulation-PlayMode`: 1 passed / 1 failed: synthetic keyboard input did not reach the fixture. Isolated headless Input System routing and cleaned persistent project host between fixtures.
- `motor-simulation-isolated-PlayMode`: 1 passed / 1 failed: Input System destroyed temporary settings on replacement, causing teardown failure. Retained/restored the original temporary settings object explicitly.
- `motor-simulation-final-PlayMode`: **2 passed / 0 failed**, exit 0: scene/material lifecycle plus actual keyboard-driven CharacterController movement, jump, zero-timescale pause and landing.
- `python Tools/verify_serialized_contracts.py`: **passed**, no existing serialized/configuration content changes or removed/retargeted direct SerializeField declarations. Script GUIDs and scene/prefab content unchanged. `git diff --check` on changed code/docs passed (Unity-generated metadata's conventional trailing spaces excluded).
- `observer-lifetime-before-EditMode`: **18 passed / 2 failed**. Tests reproduced a throwing feedback listener suppressing later listeners and a disposed animation session accepting a new subscription.
- Observer lifetime fix: cached immutable subscriber snapshots preserve multicast ordering and subscription-change semantics without allocating an invocation list on every publish; each exception is logged independently. Disposed animation sessions reject rebinding. Normal gameplay event order and animation configuration are unchanged.
- `observer-lifetime-after-EditMode`: **20 passed / 0 failed**, exit 0.
- `animation-ownership-PlayMode`: **2 passed / 0 failed**, exit 0. In addition to movement/material checks, actually started the installed portable graph, checked legacy writers suspended, checked controller/fireEvents/rootMotion and writer states restored, restarted graph, disabled/re-enabled bridge and verified return to observation mode.
- `composition-final-EditMode`: **21 passed / 0 failed**, exit 0. Added gameplay-scene composition gate: one of each of 11 core components, same CharacterController for motor/Sling/swimming, and matching serialized input/resource/targeter/reward/animation references.

## Continuation

### Autonomous continuation — movement authority

- Read all handoff documents; starting branch clean at `8dfe6a68`.
- Writer inventory: `MovementAuthority.md`; raw source scan in `Logs/ProductionMigration/writers-scan.txt`.
- Added regular C# PlayerDisplacementAuthority as the single synchronous CharacterController.Move boundary. Motor, Sling path/impact/hold and swimming keep original call order, callbacks, displacement arithmetic and execution orders. Standalone swimming retains a local driver when no motor exists.
- `movement-authority-PlayMode`: 2 passed / 1 failed due to reflection selecting Activator's bool overload through Unity Object's implicit conversion. Corrected constructor argument array.
- `movement-authority-fixed-PlayMode`: **3/3 passed**, exit 0. New fixture compared all 360 intermediate displacement steps and collision flags against direct CharacterController.Move, including floor/wall contacts. Existing motor, material and animation takeover tests passed.
- Serialized-contract gate: passed; no serialized content or field changes.
- `movement-authority-EditMode`: **21/21 passed**, exit 0. Movement stage ready for local checkpoint; next is engine time ownership.
- Checkpoint: `5f4f6645`.

### Autonomous continuation — time ownership

- TimeScaleArbiter owns disposable nested pause and slowdown requests; UnityTimeAuthority is the single engine-clock writer in owned runtime. HitStopEnvelope preserves the original standalone quality/hold/recovery equations.
- Deliberate overlap correction: pause dominates slowdowns and freezes hit-stop/impact-hold clocks, ability execution, camera and FOV recovery. Releasing/disabling one owner cannot restore stale time or override another owner. Unpaused hit-stop presentation still uses original clocks.
- `time-authority-EditMode`: **24/24 passed**, exit 0; includes nested requests, fixed-step restoration and 400 legacy-envelope scenarios.
- `time-authority-PlayMode`: **4/4 passed**, exit 0; actual pause menu + two hit-stop sources + nested pause + source disable + recovery tested, alongside previous physics/animation/material gates.
- Serialized-contract gate passed; no scene/assets/serialized fields changed. See `TimeOwnership.md` for policy and limits.
- Checkpoint: `617cb5e8`.

### Autonomous continuation — ability composition

- Added AirCastSession and explicit IAirPowerBudget/AbilityResourcePolicy; moved Dash execution decisions into its session and routed Sling through PlayerInputReader's configured action. Preserve resource/depletion semantics, sticky targeting and event order. Destroyed Unity resource references are normalized at the adapter boundary.
- `ability-composition-EditMode`: **25/25 passed**, exit 0; includes 20,000 cast-session cases and payment/depletion/reward tests.
- `ability-execution-PlayMode`: 5/8 passed; fixture teardown reused an existing scene name, preventing later scenarios. Fixed cleanup reuse.
- `ability-execution-fixed-PlayMode`: 6/8 passed; Sling fixtures assumed pre-upgrade default settings and waited scaled frame counts for an unscaled impact hold. Made policy explicit and bounded waits by real time.
- `ability-execution-clocks-PlayMode`: **8/8 passed**, exit 0.
- `ability-edges-final-PlayMode`: **8/8 passed**, exit 0; added reward-to-Dash subscriber check, second airborne jump rejection, reversal, camera/FOV pause. Actual perfect/normal Sling and Cast execution, payment/feedback order and resource interruption validated. See `AbilityComposition.md`.
- `ability-final-EditMode`: **25/25 passed**, exit 0. Serialized-contract and code whitespace gates passed; ready for ability checkpoint.
- Checkpoint: `365d6aaf`.

### Autonomous continuation — decorative references

- Traced all 10 missing references to three source prefabs. Four scene AudioSource failures inherited one absent audio GUID from two waterfall splash prefabs. Six bird failures inherited two absent sprite GUIDs in a prefab configured for Grid texture-sheet mode (8 columns), where sprite-mode entries are unused.
- Reused bundled Water_Waterfall_Small.wav for the small-waterfall emitters; original missing clip cannot be recovered from repository/history. This restores audible ambience and needs human mix acceptance. Cleared only the unused bird sprite references; texture/material/grid animation unchanged.
- Four reference lines changed across three prefabs. No assets/components deleted, no GUID changes, no scene reserialization. Exact pre/post canonical hashes and rationale are in ContentRepairs.json; serialized gate now permits only these exact reviewed repairs.
- `content-repair-EditMode`: **25/25 passed**, exit 0. The integrity test now requires **zero** missing scripts/broken references in enabled gameplay scenes and owned prefabs; the ten-item exception list was removed.
- `content-repair-PlayMode`: **8/8 passed**, exit 0. Exact content-repair hash gate passed.
- Checkpoint: `fc550cfc`.

### Autonomous continuation — measured UI simplification

- `runtime-profile-before-EditMode`: **26/26 passed**, exit 0. Initial profiling exposed counter uncertainty.
- `runtime-profile-calibrated-EditMode`: **26/26 passed**, exit 0. Confirmed GC.GetAllocatedBytesForCurrentThread is unsupported here (8 KiB calibration reports zero). No zero-allocation claim is made.
- Calibrated UI resource callback: 4.7852 ms/100,000 reads versus typed getter control 1.6864 ms. Removed unnecessary guessed-member reflection from the sealed AirPower binding; preserved all serialized fields/plugin animation behavior.
- `runtime-profile-after-EditMode`: **26/26 passed**, exit 0. UI callback 2.6680 ms/100,000 reads; typed control 1.8505 ms; feedback dispatch 0.9616 ms. See PerformanceFindings.md for limits.
- `ui-profile-after-PlayMode`: **8/8 passed**, exit 0. Actual gameplay UI/material and all traversal regressions passed. Serialized-contract gate passed, including support for combined SerializeField attributes.
- Checkpoint: `27fa589d`.

### Autonomous continuation — input lifecycle and candidate verification

- Reproduced two input defects: canceling the sole System action's rebind left Pause disabled, and Dispose cancellation callbacks could start an orphaned rebind. Restore from the service's map policy and mark disposed before callbacks; finally guarantees asset cleanup even if a cancellation callback throws.
- `input-lifecycle-before-PlayMode`: **3/10 passed, 7 failed**, exit 2. The orphaned rebind also interfered with later input scenarios; all failures retained in evidence.
- `input-lifecycle-after-PlayMode`: **10/10 passed**, exit 0. New tests cover System restoration, menu-aware gameplay cancellation, reentrant disposal rejection and idempotent cleanup.
- `candidate-final-EditMode`: **26/26 passed**, exit 0. Serialized-contract gate passed; only the exact three documented decorative prefab repairs differ from baseline content.
- Initial Windows build `migration-20261112`: succeeded, **0 errors / 40 warnings**, 56.388 seconds, 1,222,122,846 bytes. A final candidate build follows to include input fixes. Build scripts never overwrite artifacts or run beside an existing Editor.
- Warning families include inherited FindFirstObjectByType calls, vendor BinaryFormatter/API warnings and vendor shader warnings. The retained hidden legacy resource fallback field also reports unused; no claim of warning-free compilation.
- Final Windows candidate `migration-rc1-20261112`: succeeded, **0 errors / 21 warnings**, 25.153 seconds, 1,222,122,846 bytes. Artifact: `Builds/migration-rc1-20261112/Windrunner.exe`. Incremental builds do not re-emit every earlier shader warning; a lower warning count is not evidence of fixes.
- Headless candidate startup: initialized Unity and remained running for **15 seconds**, no matched exception/error/crash/load-failure lines. Harness then stopped only its own process. This is not rendered gameplay or graceful-shutdown validation; graphics/audio/feel still require acceptance.
- `Tools/Record-MigrationResults.py` refreshes committed test/build summaries and SHA-256 source/XML/log evidence without discarding failed runs. Local raw logs, XML and build artifacts are ignored by Git.

## Next implementation scope and release gates

### Resumed: animation and feedback ownership

- Interrupted profiling/warning stage checkpoint: `63e3408c`.
- WindrunnerAnimationSession now accepts an explicit player source and ignores other players' and unowned semantic requests. A destroyed owner never becomes a broadcast listener. Existing three-argument callers retain observation compatibility; the installed bridge supplies its motor owner.
- Bridge and emitter expose non-owning BindFeedbackService APIs; explicit null means unbound, not global fallback. Emitter rebind/disable/re-enable disposes and recreates exactly one subscription set. No clips/mappings or graph startup settings changed.
- `animation-player-ownership-EditMode`: **27/27 passed**. `animation-player-ownership-PlayMode`: **15/15 passed**, both exit 0. Existing live graph takeover/restoration and traversal checks remain green. Serialized-contract gate passed.

### Resumed: camera and death-reward scope

- Animation ownership checkpoint: `4bb6c7e8`.
- Verified serialized camera and Sling death-reward references before modifying fallbacks (`explicit-scene-baseline-EditMode`: 27/27 passed). Targeters, Cast, Sling, hit/reward feedback now prefer their motor's view; a configured motor with no view no longer captures another player's MainCamera. Explicit serialized camera overrides remain authoritative.
- Death reward resolves missing resource only from its specified player, never an arbitrary scene resource. Two-player Play Mode delivery test confirms recipient isolation. Added destroyed-target cancellation during charge and paid traversal, including baseline no-refund semantics and external-velocity cleanup.
- `scoped-camera-rewards-PlayMode`: 16/16 passed; `scoped-ownership-final-PlayMode`: **17/17 passed**; `scoped-ownership-final-EditMode`: **27/27 passed**, all exit 0. Serialized-contract gate passed.

### Resumed: application lifetime across scene reload

- Camera/reward checkpoint: `9dda123d`.
- Reproduced persistent-root reload failure (`scene-reload-before-PlayMode`: 0/1, exit 2). The scene-authored duplicate now retires at frame end; existing application services remain canonical. The duplicate input adapter hands off the new scene's UI module before retirement. Persistent pause view, input service and pause state survive.
- Menu disposal releases generated binding rows. Camera startup respects an already-open pause menu instead of locking its cursor.
- `scene-reload-after-PlayMode`: 1/1 passed; `scene-lifetime-final-PlayMode`: 18/18 passed; `scene-lifetime-cursor-PlayMode`: **18/18 passed**; `scene-lifetime-final-EditMode`: **27/27 passed**, all exit 0. Full-scene reload verifies one root/host, same input service, live scene UI module, no duplicate rows, paused cursor and normal resume. Serialized-contract gate passed.
- ArchitectureReview.md distinguishes validated ownership from remaining authored-content, packaging, profiling and compatibility-adapter debt. No production-readiness sign-off.

### Post-RC1: scoped player bindings

- Added explicit non-owning input-service binding on PlayerInputReader. Disposed bindings remain inert; they never silently switch to the global player. Existing unbound scenes retain their compatibility host fallback.
- Camera sensitivity/cursor decisions use its serialized reader. Sling input uses the motor's actual configured reader; optional FOV effects resolve only on the player and camera kicks only on its assigned camera, removing scene-wide cross-player searches.
- `player-bindings-PlayMode`: **11/11 passed**, exit 0, including independent action overrides/menu state and disposed-binding isolation. `player-bindings-EditMode`: **26/26 passed**, exit 0. Serialized-contract gate passed; no serialized content changed.
- Remaining: explicit scene composition of the input binding and broader presentation/global-host discovery cleanup; this stage does not claim multiplayer support.

### Post-RC1: Sling presentation separation

- Scoped binding checkpoint: `de21c2c1`.
- AirSlingPresentation now owns 11 synchronous camera/FOV, bank, flip, VFX/audio and hit-stop commands. Controller retains gameplay decisions, serialized references and event ordering. No deferred event queue or additional MonoBehaviour was introduced. Both existing cleanup calls remain at their original positions.
- `sling-presentation-PlayMode`: **11/11 passed**, exit 0. `sling-presentation-EditMode`: **26/26 passed**, exit 0. Full charge/path/impact/launch and event-order regressions passed. Serialized content unchanged.
- Authored animation audit: WindrunnerActionClips.asset contains `actions: []`; five profile mappings remain -1. Existing locomotion clips do not establish equivalent action mappings. Activation/legacy retirement is blocked by missing authored action configuration; unrelated architecture/testing can continue.

### Post-RC1: water and interrupted input

- Sling presentation checkpoint: `154d0154`.
- Added real water mesh/swimming tests covering entry, pause, removed-water exit grace, recovery and disable. Device removal at a short charge initially passed (`water-device`: 13/13), but perfect-window removal reproduced an unintended paid Sling release (`device-perfect-before`: 12/13, exit 2).
- Input service now tracks participating devices and publishes an interruption generation on removal/disconnection/disable. Sling and Cast cancel charging before release processing when it changes. Subscriptions are removed on disposal. This is an intentional correction: device loss must not fire a perfect ability.
- `device-perfect-after`: 13/13 passed. `water-device-final-PlayMode`: **14/14 passed**, exit 0, including Cast cancellation without projectile and successful input after reconnection. `water-device-final-EditMode`: **26/26 passed**, exit 0. Serialized-contract gate passed.

### Post-RC1: profiling, warning audit and ownership cleanup

- Water/device checkpoint: `67938aea`.
- RenderedSceneProfileTests captures actual nonblack 1280x720 scene output on RTX 5070 Ti/DX11, with warmup and calibrated native frame allocations. Both runs passed. Median frame times 6.492/6.192 ms; p95 7.263/7.004 ms; frame allocations 4,596/3,570 bytes. Editor/Test Runner overhead prevents attribution or a shipping FPS claim. See PerformanceFindings.md.
- Exported the original RC1 BuildReport: all 21 warnings classified, including three missing-script vendor Resources not covered by gameplay scene integrity checks. Build tool now records exact warning messages. No vendor resources deleted.
- Reader binds the early production host once at Awake; swimming uses the motor's configured input/controller. Removed only unused nonserialized trial state; retained serialized UI legacy capacity with narrowly scoped warning suppression.
- Architecture inventory scans 205 owned runtime sources: 21 discovery sites, 8 global-service accesses, one physical Move boundary, and pose/material ownership candidates. These are review signals, not proof that components are obsolete.

1. Review remaining compatibility adapters outside the core scoped camera/input/feedback/reward paths. Explicit APIs and isolation tests exist; the application still has one local input host and no general multiplayer guarantee.
2. Expand broad water/ability collision overlap, obstruction and hardware reconnect acceptance. Sling presentation separation, basic swimming/device loss, target destruction and reward recipient tests are complete.
3. Author/test portable action mappings (all five currently -1; auto-start false). Keep legacy humanoid fallback until visual and gameplay parity passes. Do not invent mappings or remove a functioning writer without an authored replacement.
4. Representative player traversal CPU/GPU/GC captures, node-count scaling, FEEL saturation and large-selection Inspector measurement. Rendered Editor baseline and native frame allocation calibration now work; per-method attribution and shipping-player budgets remain unverified.
5. Scene/prefab composition and proven obsolete-code removal after the above dependencies; final production architecture sign-off.

Known risks: compatibility discovery and retained legacy presentation, unassigned animation actions, broad hardware/water/obstruction coverage, inherited vendor/API/shader/importer warnings and three missing-script optional FEEL Resources in packaged content. Vendor/demo source was inventoried, not exhaustively code-reviewed. Zero broken references applies to enabled gameplay scenes and owned prefabs. Rendered pixel validation does not establish visual quality, audio output or gameplay feel.

Required in-engine acceptance: full movement/jump/momentum comparison, manual and reward dash, normal/perfect Sling and cast, collision/occlusion/cancellation, water traversal, camera/FOV/bank/sensitivity, pause during hit-stop/impact hold, rebinding/gamepad/device loss, audible/visible FEEL effects and animation action transitions. Do not label this production-ready until these and the remaining stages pass.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-UnityTests.ps1 -Label <fresh-label>` and again with `-Platform PlayMode`, with no Editor running. Run `python Tools/verify_serialized_contracts.py`. Detailed results/source fingerprints are in `TestResults.json`; full XML/logs remain locally under `Logs/ProductionMigration/`.
- Remaining work and manual acceptance matrix: `ProductionMigration.md`.

### Placeholder action integration and graph ownership

- Extended the existing portable action graph with deterministic priority/interruption, phase groups, looping, speed, masks and cancellation. Added source-scoped Dash events and configurable Inspector workflows; no parallel animation framework.
- Reused audited compatible flip/Sling and start/stop/pivot/fall/landing clips. Cast, Dash and Sling-charge slots safely remain unassigned. Automatic takeover remains disabled for visual acceptance; legacy writers remain recoverable. See AnimationWorkflow.md and AnimationClipInventory.json. Three profile asset changes are hash-allowlisted; scene/prefab GUIDs unchanged.
- Edit Mode animation-transitions-final: **31/31 passed**. Play Mode animation-owner-registry: **18/18 passed**, exit 0. Includes live placeholder graph, missing actions, priority/cancel rules, independent profile overrides, transitions, duplicate portable-owner rejection and restoration. Serialized-contract audit passed.
- Preserved failed experiments: private legacy field compile failure; initial graph fan-out error; missing transition-slot test; Animator.hasBoundPlayables incorrectly rejecting the legacy controller. Fixed with actual clip collection, single layer mixer, compatible optional transitions, and weak per-Animator portable ownership registry. All results retained in TestResults.json.
- Remaining: representative scenario profiling, vendor/caller review and refreshed integrated Windows candidate; visual action/landing parity remains manual. Earlier empty-action-profile blocker statements are superseded by this stage.

### Presentation owner completion and representative measurements

- Animation checkpoint: `05ba46c1`. Added explicit camera views on Cast/Sling/targeting facades; charge UI, reticle, Cast presentation and legacy locomotion camera fallbacks follow their configured owner. Explicit serialized overrides remain authoritative; an unbound owner cannot capture another player's MainCamera. Scene/prefab assets unchanged.
- `presentation-owner-final-PlayMode`: **19/19 passed**, exit 0. `integrated-actions-final-EditMode`: **31/31 passed**, exit 0. Serialized-contract and whitespace checks passed.
- `traversal-scenarios` and repeat each **1/1 passed**: eight real movement, animation, UI reload, Sling, Dash, Cast and water scenarios. CPU/GC/memory results and limitations in PerformanceFindings.md; measurements do not establish a new attributable bottleneck or shipping FPS.
- CompatibilityDependencies.json records serialized users and source callers for retained adapters. All reviewed adapters still have scene references and installers/tests; none was removed solely for its prototype name. Three optional vendor Resources are noncritical unsupported integration templates (details in ArchitectureReview.md), not core architecture blockers.

### Integrated RC3 verification and acceptance handoff

- Presentation/profiling checkpoint: `9354f251`. Final Windows candidate `Builds/migration-rc3-final-20261112/Windrunner.exe`: succeeded, zero errors, 24 classified warnings, 25.77 s. Only post-suite source change is a corrected action restart tooltip, compiled into this candidate.
- Final full suites remain 31/31 Edit Mode and 19/19 Play Mode; two eight-scenario profiling passes. Final serialized audit and whitespace checks passed. TestResults.json records exact evidence, failed iterations and source hashes.
- ArchitectureReview.md and CompatibilityDependencies.json now distinguish critical ownership from supported legacy adapters and optional vendor templates. Missing authored clips have safe fallbacks and documented Inspector replacement workflows. No functional legacy component or important asset was removed.
- Current implementation is an integrated single-player architecture candidate. No known critical engineering defect remains in the executed audit scope; production readiness is **not** declared. Remaining work is explicit visual/gameplay/hardware acceptance, enabling portable takeover only after parity, acceptance-dependent legacy retirement and deeper target-player/GPU/stress profiling. No shipping FPS or allocation improvement is claimed from un-attributed Editor measurements.
- See ReleaseCandidate.md for the full inventory, warning/content categories, exact test/build evidence and manual checklist. Resume from that checklist; do not repeat completed architecture stages or delete compatibility components solely by name.

- Exact final-candidate startup: headless and DX11 each survived 15 seconds, initialization confirmed, zero matched errors. Rendered process aggregate CPU 10.53125 s; peak working set 1,012,662,272 bytes; ending private memory 2,609,991,680 bytes. Includes startup/idle, not frame timing.

### RC3 manual acceptance: targeted waterfall correction

- Starting checkpoint 28606d4f, clean working tree. User reports 8/11 manual areas exercised without significant gameplay differences, but no item identities supplied; no additional individual acceptance passes inferred. Architecture remains frozen.
- Root cause: fc550cfc repaired missing decorative waterfall clips with an audible substitute, activating four volume-1 direct-output loops alongside the original tuned .004 ambience. Spatial Blend was already 1. Neither scene lifecycle nor feedback pools introduced them. Scene, original ambience prefab and mixer match 5P byte-for-byte after newline normalization.
- Clear only the two substituted splash prefab clip references. Preserve all original ambience settings, mixer, transforms, source components and other sounds. Baseline silence is restored without broken GUIDs. ContentRepairs exact hashes updated.
- Added actual-scene configuration/reload/listener regression test; pre-fix test failed with five audible configured sources instead of one. Post-fix full Play Mode suite passed 20/20; final Edit Mode suite passed 31/31, both exit 0. Final broadened duplicate detection is included in the subsequent final run.
- Added ManualAcceptanceGuide.md (eleven numbered areas; no inferred passes), read-only audio audit/selection and Play-Mode-only acceptance helpers. Hub helper process is now distinguished from Editors by the test harness.
- WaterfallAudioRegression.md/json contain causal evidence and preserved settings. Audible near/far mix, unchanged other sounds and reload still require manual verification. No Windows rebuild was requested/performed; the existing RC3 executable remains unchanged.

- Final validation: waterfall-final-EditMode **31/31 passed**, waterfall-final-PlayMode **20/20 passed**, both Unity exit 0. Final serialized-contract audit has zero failures; git diff --check passed. Runtime duplicate detection covers both object and clip names.

### Character Tuning Studio: isolated profile authoring

- Started clean at 9a604f4a. No deletions in the initial dependency audit. Inventory and live composition are in LegacyRetirementInventory.json and CharacterCompositionAudit.json.
- Added Tools > Windrunner > Character Tuning, using 21 existing module profiles and 33 actual player/camera bindings. Drafts, search/categories, validation, baseline comparison, explicit Save, Undo/Revert, preset capture/application and independent profile duplication preserve existing runtime authorities. Petra preset indexes current assets without changing their values.
- Live preview allowlist covers movement/Dash/resources/camera, Sling/Cast/targeting/reward and water. Cached animation/pool settings require explicit save and restart. Other characters' authored references remain isolated.
- tuning-foundation Edit Mode: 31/32, test scene-restoration failure (not runtime parity); corrected test's empty-scene handling. tuning-scene-restore: 1/1 passed. tuning-live Play Mode: 21/21 passed. Full final Editor validation pending its result below.
- Next stage: explicit portable-by-default ownership and production-only diagnostics, then reference-safe retirement only where parity is proven.
- tuning-studio-final Edit Mode: **32/32 passed**, exit 0. Serialized contract check passed; no pre-existing gameplay asset changed in this stage.

### Production-only ownership and verified component retirement

- Studio checkpoint: fee61215. Enabled automatic portable ownership with productionOnly fail-closed stop behavior. Detached the player's legacy Animator controller; retained its Avatar and all node controllers. New character animation profiles default to portable production ownership.
- production-only-first Play Mode: **22/22 passed**, including runtime physical removal of legacy pose writers, startup, stop/re-enable, missing-clip fallback and real ability/resource/device/water tests. Retired ten disabled legacy/overlay components from the scene only after that evidence; cleared every incoming scene reference and refreshed Petra's preset. production-retired Play Mode: **22/22 passed**.
- Removed the completed WindrunnerPlaceholderActionSetup source/meta after zero-caller/GUID-reference audit. Legacy type definitions remain because historic installers, facade serialization and tests still depend on their APIs; exact retirement conditions are documented, not treated as active gameplay authorities.
- production-final and production-tuning-integrated Edit Mode: **32/32 passed** each. The first expanded Save/Undo check exposed the wrong integrated-weapon validator being used for a standalone ground profile; tuning-save-undo failed 0/1, then tuning-save-undo-fixed passed 1/1 after selecting AssertStandaloneValid. Actual save/Undo, numerical-copy parity, source isolation and no-op saving now execute.
- Next: final suites, representative rendered measurements, Windows candidate and startup validation. Production-only animation visual acceptance is a new manual gate; the user's earlier 8/11 RC3 observations do not automatically pass it.

- Final production-tuning-release suites: **32/32 Edit Mode**, **22/22 Play Mode**, both exit 0; serialized-contract and whitespace gates passed. Representative rendered profile and build verification follow.

- Rendered production-traversal-profile: 1/1 passed (eight actual gameplay/lifecycle scenarios). production-rendered-baseline: 1/1 passed, calibrated native allocations; 1280x720 DX11 median 6.1655 ms, p95 6.9400 ms, median frame allocation 2,664 bytes. Editor/test overhead applies; no shipping-FPS or causal optimization claim.
- Windows production-tuning-20261112: succeeded, 0 errors, 24 inherited/classified warnings, 24.36 s, 1,212,161,806 bytes. Headless and rendered DX11 startup both survived 15 seconds, initialized and logged no matched runtime errors.
- Tuning authoring sources grouped under Editor/CharacterTuning with all existing script GUIDs retained; no new runtime assembly or hot-path discovery introduced. Current composition export and full data-asset reference inventory recorded.

### Closed legacy source retirement and integrated candidate

- Starting checkpoint dac51246. Retired ten legacy runtime definitions and seventeen dependent historic installers/custom Editors after zero-GUID-user and caller-closure review. Removed their now-null bridge/movement-visual fields and matching scene keys. Required presentation events and bank/lean remain. Exact files/hashes/reasons are in SourceRetirements.json.
- Removed the detached Batch19_PlayerHumanoid controller after zero GUID/path/name callers; kept all clips, Avatar and node controllers. No third-party resource removed. The script/meta/content gate now checks exact reviewed retirements and field-removal contracts. Disabled rename inference in this gate because Git incorrectly paired two unrelated small meta files; actual GUIDs were unchanged.
- legacy-source-retirement: **32/32 Edit Mode and 22/22 Play Mode passed**, Unity exit 0. production-clean-references: **32/32 Edit Mode passed**, exit 0 after controller removal. Tests assert retired runtime types do not exist and production animation still owns startup/lifecycle. All earlier failed test iterations remain in evidence.
- Final rendered scenarios, Windows build/startup and evidence refresh follow below. No new gameplay numerical tuning or authored animation content introduced.

- production-clean-traversal rendered Play Mode: **1/1 passed**, eight scenarios. Final Windows production-clean-20261112: **Succeeded**, 0 errors, 24 warnings, 24.1062 s, 1,212,117,318 bytes. Warning subjects unchanged; three vendor shader variant detail blocks differ.
- Exact final candidate headless and DX11 startup each survived 15 seconds, initialized and logged no matched errors. Rendered process CPU 10.875 s, peak working set 987,619,328 bytes, ending private memory 2,579,845,120 bytes. Harness deliberately ends its process; no graceful-shutdown/gameplay claim.
- Final serialized gate and retired-GUID scan passed with zero failures/users. All numerical profile defaults and important assets retained. TestResults.json refreshed with actual results including failures. Final whitespace check passed.
- Engineering implementation for the requested Studio/production-only cleanup is integrated; retained live adapters/authoring data and uncertain vendor content have explicit retirement conditions. Manual ProductionOnlyAcceptance.md remains not tested; do not infer its completion from earlier RC3 observations. Target-player performance attribution and authored animation polish remain follow-up gates. Resume with that checklist, not repeated migration work.

### Reliability hardening: P1 boundary fixes (2026-11-13)

- Started clean at eeeb4ba4. Scope excludes IK, procedural foot placement, broad cleanup and gameplay retuning.
- Cast projectiles snapshot launch speed/lifetime/hit radius; preview disposal cannot strand them. Real Play Mode regression verifies two simultaneous hit/miss projectiles survive draft changes/destruction, deliver exactly one reward and clean up.
- Studio and session reject Play Mode saves when any restart-only module changed, before mutation. Nested profile validation remaps a proposed draft graph temporarily, supporting added slots and rejecting removed referenced slots. Save validates all entries first, groups Undo and restores memory and exact file bytes on failure.
- Edit Mode hardening-p1: **32/32 passed**, exit 0 (includes injected persistence-failure rollback, child removal/addition and actual Save/Undo). Play Mode hardening-p1: **23/23 passed**, exit 0, including active production graph remaining valid after rejected save.
- Initial launch was blocked by running Editors; user closed them before validation. No second Editor was launched. Next: P2 resource/preset/preview reliability and animation state recovery.

### Studio/resource reliability

- Capacity changes preserve absolute Air Power, clamp downward, publish capacity notifications and reconcile lockout. Restoring capacity never refunds energy. Ordinary unchanged-capacity behavior is preserved.
- Presets discover null profile slots, validate temporary complete compositions before committing, reject ambiguous/incompatible bindings, support grouped Undo and restore originals on rejection. Duplication validates drafts, refuses existing output assets and cleans only newly created output on failure.
- Preview validates on Undo/Redo and polls external draft edits while active. Restart-only controls are read-only during Play; session Save independently enforces the restriction. Scope foldout shows effective user-multiplied sensitivity and excluded encounter death-reward values; hand-trail scene overrides are available with standard Undo in Edit Mode. Dead Cast ring controls are hidden in the Studio, serialized fields retained.
- hardening-p2 Edit Mode **32/32 passed**. Initial Play Mode **21/23**: Undo fixture shared a previous record group, then its failed teardown contaminated the collision fixture. Isolated complete-object Undo snapshot; hardening-p2-undo Play Mode **23/23 passed**, including real resource clamp/restoration notifications and immediate invalid-Undo preview shutdown. No gameplay asset changes.

### Ability presentation recovery and measured hardening verification

- Added authoritative Sling presentation phase and direct animation-only recovery of Sling charge/delay/travel/impact, Cast charge and Dash. Graph recreation/service rebinding never publishes gameplay events or spends resources; stale sustained groups are cleared on recovery, while transient actions are not replayed. Existing compatible clips and production-only fallback remain.
- hardening-animation first compile failed for missing test animation assembly references; fixed explicitly. hardening-animation-refs Play Mode **24/24 passed**. Performance harness first compile failed from Unity namespace shadowing; fixed with global qualification. Both failed logs retained, with no false passing XML.
- hardening-final-compile and hardening-integrated Edit Mode **32/32 passed** each. Expanded configuration checks cover coherent slot reorder, incompatible reference changes, independent nested profile duplication, null-slot preset assignment, Undo and rejected-preset rollback. Persistent Save rejects temporary/scene object references.
- hardening-integrated and hardening-release Play Mode **25/25 passed** each. Added actual motor-speed response/restoration under preview, capacity notification/revert behavior, invalid Undo and automatic shutdown after external draft mutation, multiple projectiles after preview disposal and sustained Cast/Dash recovery. Final recovery reconciliation rerun is recorded below.
- Rendered hardening-capped, hardening-uncapped and hardening-hotspots: **1/1 passed each**. At 1280x720 DX11, median frame 6.4399 ms capped at 166 versus 5.8349 ms uncapped; GPU duration unavailable. Native allocation counter calibration passed. Target/water/projectile/resource batch results and scope limitations are in PerformanceFindings.md. No algorithm optimization or shipping-FPS improvement claimed.
- Remaining manual checks and explicit deferred boundaries: HardeningReport.md. No IK/procedural foot placement changes, asset deletion, scene/prefab changes or numerical retuning.

- hardening-recovery-final Play Mode **25/25 passed**, exit 0. Windows hardening-20261113 **Succeeded**, 0 errors, 24 unchanged warning subjects, 30.3081 s, 1,212,118,854 bytes. Both exact-candidate headless and DX11 startup survived 15 s, initialized and logged no matched errors. Harness terminates only its launched process; this is not graceful-shutdown/gameplay acceptance.
- Rendered startup aggregate CPU 10.546875 s, peak working set 987,512,832 bytes, ending private memory 2,592,919,552 bytes; includes startup/idle. The only post-build source change expands test-only capacity/depletion assertions; player code is unchanged.

- Final hardening-capacity-final Play Mode **25/25 passed**, exit 0, including exactly-once depletion recovery after capacity reduction and no refill on restoration. Final Edit Mode remains hardening-integrated **32/32 passed**. Final serialized contract/whitespace checks passed. No scene, prefab, authored profile or retained GUID changed from eeeb4ba4.
- Continuation: perform the focused manual checklist in HardeningReport.md; preserve current gameplay defaults. Deferred work is authored content, target-player GPU/long-run/stress profiling, hardware/visual/audible acceptance and optional reviewed cleanup. No P1 hardening defect remains known in the tested scope. Working implementation is not declared production-ready solely from automated tests.

### Progression fixture usability and real input correction (2026-11-13)

- Resumed the user-requested fixture/journal correction from clean checkpoint 5761575b on feature/progression-framework; preserved framework architecture and the accepted playground. User closed Unity before guarded validation; no concurrent Editor was opened.
- Fixed camera-ray self/volume obstruction and Update-versus-camera-LateUpdate targeting; retained occlusion, channel, distance and prerequisite checks. Added authoritative rejection diagnostics, binding-derived prompts, keyboard/gamepad selection/tracking/category/scroll/back controls, readable journal panes and nested cursor ownership shared with the existing pause controller.
- Added isolated colored/labeled stations, visible race arches/directions, collapsible authoritative field guide, visible fixture pause/rebinding panel, guarded reload/reset and cinematic interrupt tools. Five engineering station anchors were spread out; all original IDs, component references, physics values, race gates and interaction ranges remain. No production gameplay serialized assets changed.
- fixture-release-edit: 48/48 Edit Mode passed, exit 0. fixture-release-play: 31/31 Play Mode passed, exit 0 (final source compiled), including real station input, dynamic binding propagation, wall/range/unlock rejection, gamepad journal controls, nested pause, exactly-one subscription on re-enable, real motor race, independent turn-ins, real Timeline completion/skip/interruption and service reload.
- fixture-rendered-final: 1/1 passed, exit 0. Actual 1280x720, 1600x900, 1920x1080 screenshots plus long text/20 extra temporary entries inspected. Earlier screenshots exposed overlapping world labels; smaller proximity-limited signs and fixture spacing were corrected. Initial rendered cleanup loop failed and its exact test process was stopped; bounded cleanup succeeded on later runs.
- Existing serialized contract gate and new baseline-5761575b fixture gate passed: all 108 original blocks retained; 249 final scene blocks; no non-fixture serialized asset changes. The fixture loaded-reference audit also passed. Generated YAML keeps Unity's blank-value spacing; source/docs whitespace is checked independently.
- Read Documentation/ProgressionFixtureUsability.md for findings, exact failure history, numbered acceptance and remaining manual checks. No hardware/mouse ergonomics, final cinematic quality, final localization or production-readiness claim. Existing saves/stable IDs preserved; reset tests use temporary storage and prove normal-save isolation.

- Final navigation/lifecycle regression: `fixture-release-play` **31/31 passed**, exit 0. Final Windows fixture `progression-usability-fixture-20261113` **Succeeded**, 0 errors, 8 warnings, 18.441648 seconds, 113,801,939 bytes. Warnings are three existing FindFirstObjectByType deprecations (AirChimeTrial, PlayerWindFeedback, AutumnPostProcessController), two Core debug shader stripping messages and three retained optional FEEL resource missing-script warnings. None originates in the corrected progression UI code; unrelated source/vendor cleanup was not performed.
- Exact Windows fixture rendered startup survived 15 seconds, initialized and had no matched runtime errors. This is startup-only evidence, not mouse/gameplay/audio acceptance. Executable: `Builds/progression-usability-fixture-20261113/Windrunner.exe`. The main gameplay executable was not rebuilt for this targeted fixture correction.

- Post-packaging final Edit Mode: `fixture-validation-final` **48/48 passed**, exit 0; final source/docs whitespace and both serialized gates passed.

### Experimental movement gym, 17 November 2026

Feature branch feature/movement-lab starts at validated progression checkpoint 9e8f97c4. Checkpoint 9fe70b61 introduced the modular gym, direction-preserving wind influences and equipped Overdrive using the production motor and AirPower. The final continuation adds the dedicated gym composition, authored extreme relay course, collision/ability overlap probes and profiler evidence. No existing serialized assets, input templates, character tuning, progression definitions or shipping scene settings were changed.

Full gym-final Edit Mode 54/54 and Play Mode 32/32 passed with exit 0. Rendered gym-final-rendered passed 1/1, including production input, camera and Sling on the actual extreme section. Final straight runs at 30/60/120 simulated FPS peaked at 169.76 m/s and ended 399.45 m along the approach to the 400 m barrier. The automated extreme run reached 265.08 m from spawn and rose 26.13 m. Manual clearance, camera comfort, glancing collisions and hardware feel remain open. Both source contract and loaded gym reference checks passed. Windows movement-gym-20261117 succeeded with 0 errors and 11 warnings. Its rendered startup survived 15 seconds with no matched errors. See MovementLab.md for controls, authoring, measured limits, warning groups and exact validation scope.
