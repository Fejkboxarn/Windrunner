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
