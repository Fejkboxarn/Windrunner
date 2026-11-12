# Migration progress

Baseline: `7bfdfe5e` (5P), `production-refactor`, 2026-11-12.

## Current handoff

Resumed after usage-limit interruption at `67938aea`. The uncommitted profiling/warning stage was preserved and inspected. `resume-profile-stage-EditMode` **26/26** and `resume-profile-stage-PlayMode` **14/14** passed, exit 0. Two earlier rendered profiling runs each passed 1/1; native frame allocation calibration succeeded. BuildWarnings.md/JSON classify all 21 original RC1 build warnings. Current code has not yet been packaged as RC2.

Final automated suites: **26/26 EditMode** (`candidate-final`) and **10/10 PlayMode** (`input-lifecycle-after`), both Unity exit 0. Ordered movement, nested time ownership, ability composition, decorative references, typed UI binding and input teardown are implemented. Windows candidate packaging is recorded below. Overall production architecture migration remains incomplete; manual acceptance and the next implementation scope are at the end of this file. TestResults.json preserves actual runs, including failures, and source hashes.

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

1. Explicit per-player bindings for camera/input/presentation, eliminating cross-player fallback searches with multiple-composition coverage. Current scene composition is verified; global services still assume one local player.
2. Further separate Sling presentation orchestration while preserving synchronous motor -> Sling (300) -> swimming (1000) steps. Expand water overlap, obstruction, destroyed-target and device-removal/reconnection coverage.
3. Author/test portable action mappings (all five currently -1; auto-start false). Keep legacy humanoid fallback until visual and gameplay parity passes. Do not invent mappings or remove a functioning writer without an authored replacement.
4. Representative rendered player CPU/GPU/GC captures, node-count scaling, FEEL pool saturation and large-selection Inspector measurement. Current microbenchmarks support a small UI simplification only; allocation counter failed calibration.
5. Scene/prefab composition and proven obsolete-code removal after the above dependencies; final production architecture sign-off.

Known risks: distributed presentation and service discovery, unassigned animation actions, missing broad device/water/obstruction coverage, inherited vendor/API/shader warnings. Vendor/demo source was inventoried, not exhaustively code-reviewed. Zero broken references applies to enabled gameplay scenes and owned prefabs, not every vendor demo. Headless PlayMode does not verify visual quality, audio output or gameplay feel.

Required in-engine acceptance: full movement/jump/momentum comparison, manual and reward dash, normal/perfect Sling and cast, collision/occlusion/cancellation, water traversal, camera/FOV/bank/sensitivity, pause during hit-stop/impact hold, rebinding/gamepad/device loss, audible/visible FEEL effects and animation action transitions. Do not label this production-ready until these and the remaining stages pass.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-UnityTests.ps1 -Label <fresh-label>` and again with `-Platform PlayMode`, with no Editor running. Run `python Tools/verify_serialized_contracts.py`. Detailed results/source fingerprints are in `TestResults.json`; full XML/logs remain locally under `Logs/ProductionMigration/`.
- Remaining work and manual acceptance matrix: `ProductionMigration.md`.
