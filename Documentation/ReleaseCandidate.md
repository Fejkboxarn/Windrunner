# Windrunner migration validation candidate

Built 2026-11-12 with Unity 6000.6.2f1 on `production-refactor`. Local Windows x64 Mono executable: `Builds/migration-rc1-20261112/Windrunner.exe`. This integrates the tested migration work; it is **not final production architecture acceptance**. Do not distribute as a signed-off release.

| Validation | Actual result |
|---|---|
| Final Edit Mode (`candidate-final`) | 26 passed, zero failed, Unity exit 0 |
| Final Play Mode (`input-lifecycle-after`) | 10 passed, zero failed, Unity exit 0 |
| Windows packaging | Succeeded, zero errors, 21 warnings, 25.153 seconds |
| Player startup | 15 seconds headless, initialized, no matched exception/error/crash/load failure; harness stopped process |
| Serialized-contract comparison to 5P | Passed; only exact reviewed decorative repairs permitted |
| Loaded gameplay scenes / owned prefabs | Zero broken references and missing scripts |
| Full rendered player profiling | Not performed |
| Gameplay-feel / visual / audio acceptance | Not performed |

Tests cover deterministic motor/target/path/charge/resource rules; real keyboard-driven jump/landing/reversal; normal/perfect Sling execution and cancellation; Cast projectile hit/return/reward; Dash depletion/reactivation; camera/FOV pause; overlapping hit-stop and pause; portable graph takeover/restoration; UI material lifetime; feedback ordering/failure isolation; input rebind cancellation and disposal. This does not imply every combination of these systems is covered. The baseline has no double-jump mechanic; tests require the second airborne jump to be rejected.

Intentional corrections requiring acceptance: pause freezes ability/camera and interruption recovery; releasing a charge during pause cancels it on resume; Sling honors configured input; missing waterfall audio uses the bundled small-waterfall clip. No movement tuning, scene reserialization, GUID replacement or automatic portable-action activation was performed.

The UI resource callback microbenchmark improved from 4.7852 to 2.6680 ms per 100,000 warmed reads. This is not an FPS claim. The allocation counter failed calibration, so native/player allocation profiling remains required. See PerformanceFindings.md.

Outstanding architectural work: explicit per-player service binding and removal of scene fallback discovery; Sling presentation separation; authored portable action mappings and legacy retirement; water/obstruction/device-disconnection coverage; representative CPU/GPU/GC and pool stress measurements; subsequent scene/prefab composition and proven obsolete-code cleanup. Five action mappings remain intentionally unassigned and portable auto-start remains disabled. These gates prevent claiming the entire migration is finished.

Manual acceptance: compare ground/air momentum and steering to 5P; jump/coyote/buffer timing; dash and reward chains; normal/perfect/blocked/canceled Sling and Cast; swimming and ability overlap; camera bank/FOV/sensitivity; pause/rebind/controller disconnect; animation action transitions; audible FEEL/audio and waterfall mix; visual effects and scene reload. Shader/API warnings from inherited assets remain and must be evaluated in a rendered build.

Evidence: TestResults.json contains actual run summaries (including failures), source/XML/log hashes and build/startup reports. Raw logs/XML are in `Logs/ProductionMigration`; they and local builds are ignored artifacts. Reproduce with fresh labels using Tools/Run-UnityTests.ps1, Tools/Build-UnityCandidate.ps1 and Tools/Smoke-UnityCandidate.ps1, then Tools/verify_serialized_contracts.py. Never run a second Editor against this project.
