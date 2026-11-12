# Migration progress

Baseline: `7bfdfe5e` (5P), `production-refactor`, 2026-11-12.

- Repository/source inventory and migration plan created. No 5Q implementation identified.
- Stage 1 complete: executable baseline, inventory, documentation and content-debt gate.
- Stage 1 checkpoint: `71f3f62`.
- Material ownership checkpoint: `4a1503bb`.
- Current: validate motor simulation extraction; explicit player composition remains outstanding.
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
- Remaining work and manual acceptance matrix: `ProductionMigration.md`.
