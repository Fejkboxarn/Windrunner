# Progression validation evidence

Baseline aa9e9d58. Tests below are actual Unity executions; unexecuted work is listed separately. Raw logs/XML are retained under Logs/ProductionMigration and indexed by Documentation/TestResults.json.

| Stage/run | Edit Mode | Play Mode | Notes |
|---|---:|---:|---|
| progression-foundation | 37/37 | 25/25 | Transactional rules plus existing gameplay regression; checkpoint 3ffce279 |
| progression-world-compile | 40/40 | — | Corrected test-only Input System API parameter |
| progression-world | Compilation failed before XML | 28/28 | Initial compile log retained; no false passing compile result |
| progression-world-final | 40/40 | 28/28 | Sequential occurrence receipts, world sessions, nested control leases; checkpoint 769a1b00 |
| progression-authoring | 40/41 | 28/29 | Builder rejected additive untitled scene; fixture exposed retired input-host reference |
| progression-authoring-scene | 41/41 | — | Safe empty-scene generation and preserved existing fixture assets |
| progression-fixture-bindings | — | 0/1 | Input handoff fixed; test requested cinematic before motor settled |
| progression-fixture-safety | — | 1/1 | Waited for real safe-start condition; real Timeline completed and scene reload restored state |
| progression-studio-final | 41/41 | 29/29 | Integrated authoring, journal, real Timeline and scene lifecycle |
| progression-robustness | 44/44 | — | Every objective kind, filtering, feedback order/failure and immutable definitions |
| progression-rendered-fixture | — | 1/1 | Rendered, actual keyboard-driven PlayerMotor traverses both checkpoints |
| progression-profile | 1/1 | — | Reproducible command/durable write timings; no allocation/GPU claims |
| progression-final | 45/45 | 30/30 | Includes fixture reference audit and real Cast projectiles reaching mechanisms with disable/re-enable coverage |
| progression-scene-validation | 45/45 | — | Pre-Play interaction, checkpoint, puzzle and cinematic binding validation |
| progression-recovery-final | 46/46 | — | Injected cinematic save failure releases control and reconstructs pending |
| progression-release | 46/46 | — | Frozen race/puzzle definitions and strict cinematic interruption semantics |
| progression-candidate | 46/46 | 31/31 | Adds persistent collection presentation and re-enable/reload behavior |
| progression-notification-final | — | 31/31 | Full regression after suppressing historical journal completion notifications on reload |

Existing production suites cover movement, abilities, water/device/input, configuration hardening, portable animation and source/serialized integrity. New tests do not substitute for their gameplay-feel acceptance. Fixture Timeline tests verify control/state lifetime, not final cinematic visuals/audio. Test scene uses a separate engineering save slot; automated lifecycle checks inject a memory store and do not overwrite that slot.

Final serialized contract gate and code/documentation whitespace checks passed. Existing gameplay scenes/prefabs/profiles and GUIDs are unchanged; only new engineering fixture assets are added. Windows build/startup results are in ProgressionReleaseCandidate.md and TestResults.json. Performance scope and remaining scaling risks are in ProgressionPerformance.md. All passing runs exited 0; failed attempts above remain recorded rather than overwritten.

## Fixture usability continuation

See ProgressionFixtureUsability.md for implementation findings and practical in-Unity acceptance. `fixture-release-edit` passed 48/48; `fixture-release-play` passed 31/31, both exit 0. `fixture-rendered-final` passed 1/1 and captured the three required resolutions plus long-content/many-entry stress. New coverage expands the existing fixture case rather than inflating test counts: production input for offers/switch/race/puzzle/turn-ins, keyboard/gamepad journal controls, runtime rebinding propagation, actual occlusion/rejection, nested pause and UI subscription recovery. Reset isolation/backup uses real temporary files. The exact fixture button-to-disk restoration and hardware mouse/rebinding flow remain manual checks.

Historical failed labels in this continuation: fixture-usability-compile (TextMesh setup); fixture-input-integrated (test assembly compile reference); fixture-input-path and path2 (test sightline occluded by another real station); fixture-input-path3 (both turn-ins correctly yielded two cinematic records, invalidating the test's old Single assertion); fixture-rendered-layout (unbounded reflection cleanup, stopped exact test process without successful XML). Corrected runs remain separately recorded. No failure is presented as passing.

- Final navigation/lifecycle regression: `fixture-release-play` **31/31 passed**, exit 0. Final Windows fixture `progression-usability-fixture-20261113` **Succeeded**, 0 errors, 8 warnings, 18.441648 seconds, 113,801,939 bytes. Warnings are three existing FindFirstObjectByType deprecations (AirChimeTrial, PlayerWindFeedback, AutumnPostProcessController), two Core debug shader stripping messages and three retained optional FEEL resource missing-script warnings. None originates in the corrected progression UI code; unrelated source/vendor cleanup was not performed.
- Exact Windows fixture rendered startup survived 15 seconds, initialized and had no matched runtime errors. This is startup-only evidence, not mouse/gameplay/audio acceptance. Executable: `Builds/progression-usability-fixture-20261113/Windrunner.exe`. The main gameplay executable was not rebuilt for this targeted fixture correction.

- Post-packaging final Edit Mode: `fixture-validation-final` **48/48 passed**, exit 0; final source/docs whitespace and both serialized gates passed.
