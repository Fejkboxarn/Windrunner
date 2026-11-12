# Windrunner migration validation candidate

RC2 built 2026-11-12 with Unity 6000.6.2f1 on `production-refactor`, implementation checkpoint `a15a886f`. Local Windows x64 Mono executable: `Builds/migration-rc2-20261112/Windrunner.exe`. This integrates the tested migration work; it is **not final production architecture acceptance**. Do not distribute as a signed-off release.

| Validation | Actual result |
|---|---|
| Final Edit Mode (`scene-lifetime-final`) | 27 passed, zero failed, Unity exit 0 |
| Final Play Mode (`scene-lifetime-cursor`) | 18 passed, zero failed, Unity exit 0 |
| Windows packaging | Succeeded, zero errors, 24 warnings, 26.164 seconds; 1,222,126,654 bytes |
| Player startup | 15 seconds headless, initialized, no matched exception/error/crash/load failure; harness stopped process |
| Serialized-contract comparison to 5P | Passed; only exact reviewed decorative repairs permitted |
| Loaded gameplay scenes / owned prefabs | Zero broken references and missing scripts |
| Rendered Editor profiling | Two 1/1 passing captures, calibrated native frame allocations; shipping-player profiling still outstanding |
| Gameplay-feel / visual / audio acceptance | Not performed |

Tests cover deterministic motor/target/path/charge/resource rules; real keyboard-driven jump/landing/reversal; normal/perfect Sling execution and cancellation; Cast projectile hit/return/reward; Dash depletion/reactivation; camera/FOV pause; overlapping hit-stop and pause; portable graph takeover/restoration; UI material lifetime; feedback ordering/failure isolation; input rebind cancellation and disposal. This does not imply every combination of these systems is covered. The baseline has no double-jump mechanic; tests require the second airborne jump to be rejected.

Since RC1: explicit input/feedback bindings and animation source filtering; scoped core camera and death-reward resolution; synchronous Sling presentation separation; swimming/device-loss/target-loss and reward-recipient tests; canonical service ownership and paused UI rebinding across scene reload; native rendered profiling and complete RC1 warning classification. Checkpoints: de21c2c1, 154d0154, 67938aea, 63e3408c, 4bb6c7e8, 9dda123d, a15a886f. Full stage evidence is in Progress.md.

Intentional corrections requiring acceptance: pause freezes ability/camera and interruption recovery; releasing a charge during pause cancels it on resume; device loss cancels charged Cast/Sling without firing; Sling honors configured input; animation events affect only their source player; missing waterfall audio uses the bundled small-waterfall clip; scene reload preserves service ownership and paused cursor/UI. No movement tuning, scene reserialization, GUID replacement or automatic portable-action activation was performed.

The UI resource callback microbenchmark improved from 4.7852 to 2.6680 ms per 100,000 warmed reads. Rendered idle-scene Editor captures measured 6.492/6.192 ms median and 7.263/7.004 ms p95 at 1280x720 on RTX 5070 Ti/DX11. The replacement native counter passed 1 MiB calibration and measured 4,596/3,570 bytes per frame. Editor overhead remains included; no shipping FPS, zero-allocation or causal optimization claim follows. See PerformanceFindings.md.

Outstanding work: authored portable action mappings and legacy retirement; review of remaining compatibility adapters outside the scoped core; broad collision/water/hardware acceptance; shipping-player CPU/GPU/GC and pool stress measurements; vendor packaging/importer/shader debt and subsequent proven obsolete-code cleanup. The action-clip profile is empty, five mappings remain unassigned, and portable auto-start stays disabled. Three optional FEEL Resources still have missing scripts in the packaged build. These gates prevent claiming the migration is finished. See ArchitectureReview.md and BuildWarnings.md.

Manual acceptance: compare ground/air momentum and steering to 5P; jump/coyote/buffer timing; dash and reward chains; normal/perfect/blocked/canceled Sling and Cast; swimming and ability overlap; camera bank/FOV/sensitivity; pause/rebind/controller disconnect; animation action transitions; audible FEEL/audio and waterfall mix; visual effects and scene reload. Shader/API warnings from inherited assets remain and must be evaluated in a rendered build.

Evidence: TestResults.json contains actual run summaries (including failures), source/XML/log hashes and build/startup reports. Raw logs/XML are in `Logs/ProductionMigration`; they and local builds are ignored artifacts. Reproduce with fresh labels using Tools/Run-UnityTests.ps1, Tools/Build-UnityCandidate.ps1 and Tools/Smoke-UnityCandidate.ps1, then Tools/verify_serialized_contracts.py. Never run a second Editor against this project.
