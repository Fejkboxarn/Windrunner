# Migration progress

Baseline: `7bfdfe5e` (5P), `production-refactor`, 2026-11-12.

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

Completed this pass: structural repository audit and dependency graph; durable guidance/plan; executable baseline; immutable runtime UI material ownership; remaining motor simulation extraction; feedback failure isolation; animation subscription lifetime fix; scene composition/reference and animation takeover gates. No pushes, scene migrations, asset deletion, obsolete-code removal or automatic animation activation.

Next stage is explicit player composition and traversal orchestration, **not** deletion of legacy animation:

1. Extend the physics fixture to replay Sling charge/release/curved dash/impact hold/launch, water overlap and cast reward ordering. Preserve motor -> Sling (300) -> swimming (1000) movement order until traces justify consolidation.
2. Introduce explicit per-player bindings for input, camera, resources and presentation; eliminate cross-player scene searches with scene/prefab migration coverage. Current composition tests prove reference consistency, not exclusive physical ownership.
3. Extract Sling orchestration/resource decisions and route input through the central service. Its current raw Mouse polling bypasses rebinding/pause; any externally visible correction must be documented before activation.
4. Map portable actions deliberately (all five mappings currently -1; auto-start false). Keep legacy humanoid fallback until full action and gameplay-feel coverage passes.
5. Continue camera/FOV/audio/UI and time-scale arbitration, prefab organization, Editor profiling, measured runtime optimization, then proven obsolete-code removal and build verification.

Remaining risks: 10 known missing decorative audio/sprite references; three physics writers by design; pause/hit-stop ownership overlap; static service access/runtime discovery; full Sling/cast/reward/device-loss/rebind automation absent; animation actions not integrated; no measured CPU/GC/Inspector performance baseline or player build yet. Vendor/demo source was inventoried, not exhaustively code-reviewed. Headless PlayMode does not verify visual quality, audio output or gameplay feel.

Required in-engine acceptance: full movement/jump/momentum comparison, manual and reward dash, normal/perfect Sling and cast, collision/occlusion/cancellation, water traversal, camera/FOV/bank/sensitivity, pause during hit-stop/impact hold, rebinding/gamepad/device loss, audible/visible FEEL effects and animation action transitions. Do not label this production-ready until these and the remaining stages pass.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Run-UnityTests.ps1 -Label <fresh-label>` and again with `-Platform PlayMode`, with no Editor running. Run `python Tools/verify_serialized_contracts.py`. Detailed results/source fingerprints are in `TestResults.json`; full XML/logs remain locally under `Logs/ProductionMigration/`.
- Remaining work and manual acceptance matrix: `ProductionMigration.md`.
