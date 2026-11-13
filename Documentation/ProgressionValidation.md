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

Existing production suites cover movement, abilities, water/device/input, configuration hardening, portable animation and source/serialized integrity. New tests do not substitute for their gameplay-feel acceptance. Fixture Timeline tests verify control/state lifetime, not final cinematic visuals/audio. Test scene uses a separate engineering save slot; automated lifecycle checks inject a memory store and do not overwrite that slot.

Additional final work: expanded reference audit, performance samples, integrated reruns and Windows build/startup. The feature is not yet declared complete.
