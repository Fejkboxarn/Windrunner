# Progression performance evidence

Actual run: progression-profile Edit Mode, 1/1 passed, Unity 6000.6.2f1. Raw log/XML and timestamped progression-command-profile text are in Logs/ProductionMigration. Reproduce with Run-UnityTests.ps1 -Platform EditMode -Label <fresh> -Filter ProgressionCommandTimingBaseline.

Each event sample matches all active quests, updates one objective in each, snapshots state, serializes and commits to the memory store. Five warm-up samples and 20 measured samples; state grows as receipts are recorded. This measures command CPU time, not a frame/GPU/allocation budget.

| Workload | Median | p95 | Final serialized characters |
|---|---:|---:|---:|
| One event / 10 active quests | 0.1927 ms | 0.2570 ms | 4,117 |
| One event / 100 active quests | 1.8436 ms | 4.9651 ms | 39,577 |
| Small durable file replacement | 3.0752 ms | 4.8330 ms | Separate nearly empty save |

The file sample performs real flush and same-volume replacement in a unique temporary path; it does not measure the 100-quest payload. Command and file timings must not be combined into a claimed measured full workload. Synchronous durable commits can cause frame spikes during bursts; preserve atomic reward/progression semantics if future asynchronous/coalesced persistence is introduced. Current adapters emit meaningful interactions/results, not per-frame movement counts.

Occurrence receipts and successful challenge attempt IDs accumulate. Completed objectives/quests stop counting, but long-running active quests and frequent race replays need sustained save-growth measurements before large production content ships. The present demo-scale measurements are a baseline, not proof of unbounded scaling or zero allocations. No speculative optimization or allocation improvement is claimed.

progression-rendered-fixture Play Mode passed 1/1 with rendered Unity: actual input drives PlayerMotor through both course triggers, and real Timeline/journal/reload checks execute. This is rendered functional validation, not a visual-quality or frame-time capture. Existing capped/uncapped gameplay and calibrated allocation evidence remains in PerformanceFindings.md; it predates the progression UI and does not establish its shipping budget. Remaining profiling: dense authored journals, real save sizes, streaming composition and target-player CPU/GPU capture.
