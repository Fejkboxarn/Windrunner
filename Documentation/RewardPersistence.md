# Reward persistence and slot recovery

This report records the synchronous baseline at `30752891`. The [same-frame batching report](RewardBatching.md) documents the subsequent approved policy and measurements.

Baseline `3b9bf18c` on `codex/dagger-integration`. Unity 6000.6.2f1, Windows x64, rendered Direct3D 11. The benchmark creates a fresh Zone 01 playthrough in a unique temporary directory. Each burst has four independent three-unit Air Node returns with full main AirPower and Farseer's equipped. The single-return case exercises one overflow. The main-only control spends five units before one three-unit return. Reset, transfer, menu navigation and reload occur outside the timed callbacks. A separate 120-frame traversal control moves the production character through the Input System with a virtual gamepad.

## Persistence decision

Each delivered node or sling ticket claims once. Main AirPower remains a transient movement resource. Only an eligible, balance-changing overflow updates the persisted dagger reserve. A successful Second Wind activation durably empties the reserve before restoring the amount the main meter can accept. Spending, an ordinary main-meter restore, and an already full reserve create no reserve transaction. A fresh scene initializes the main meter under the existing movement rules.

Every changed reserve still uses the progression service's private candidate, locked revision check, checksum, flushed staging file, atomic publication and previous-revision backup. The service publishes notifications after the commit. Four separately delivered rewards therefore produce four full writes. Manual Save, Return to Main Menu, orderly quit, copy and load see the latest committed state immediately. Same-slot scene admission still reopens the durable slot on arrival to capture a return delivered after load preflight. Quest, relationship and one-time reward commands keep their existing transaction boundaries.

Dirty-at-frame-end, periodic writes and an asynchronous writer would allow some observers to see reserve that exists only in RAM or require a second durable ordering protocol. No such recovery window was accepted. Changing the envelope or introducing a second on-disk authority would require substantially more recovery and compatibility work. Direct Windows native replacement was also trialled and discarded. The Editor burst median was 32.33 ms against 32.82 ms with managed replacement, while p95 increased from 38.61 to 40.47 ms.

## Implemented changes

- Update the single reserve entry on the service's already-private progression candidate, preserving commit and event order. This removes two JSON loadout copies per reserve gain or activation. Full equipment mutations retain the original API.
- Encode small, checksummed envelopes once for the staged write. Large strings above 16,384 UTF-16 code units stream through the original strict UTF-8 encoder, bounding temporary byte storage. A lock handle requires no read/write buffer. Both paths flush to disk before publication.
- Confirmed overwrite now publishes the primary before refreshing an existing backup. The old implementation could change the only valid backup even when publishing the replacement failed. A held primary reproduced this against a corrupt primary and healthy backup. A fresh slot still seeds its recovery copy first. If refreshing an existing backup fails **after** primary publication, the new primary stays committed, the previous recovery copy remains available and Unity reports a warning. A recovery during that interval can load the previous committed playthrough generation. Both staged copies are flushed before publishing.
- Development diagnostics time actual gameplay and storage calls on the main thread. The standalone benchmark confirms a pre-quit reserve of 13, lets Unity quit normally, then verifies both checksums, world, equipment, reserve and revision lineage from outside the process.

## Reproduction

1. Run `./Tools/Run-UnityTests.ps1 -Platform PlayMode -Rendered -Filter RenderedRewardPersistence -Label <fresh-label>` with the project closed in the Editor.
2. Build with `./Tools/Build-UnityCandidate.ps1 -MainMenu -Development -Label <fresh-label>`, then run `./Tools/Measure-RewardPersistence.ps1 -Label <same-label>`. The player uses a test directory and quits itself. The harness removes only the verified temporary fixture after both snapshots pass.
3. Read the `REWARD_TRAVERSAL` and `REWARD_PROFILE` lines in the run logs. Each reward scenario discards four warmups and measures 40 samples. Allocation is the calibrated whole-frame GC recorder, so it includes gameplay, rendering and Editor activity. Callback elapsed time includes I/O waits. Component medians are independent and need not sum to the median total. Reserve HUD timing includes immediate feedback listeners and optional effects. Deferred canvas rebuilds contribute to frame time. The flush and replacement measurements use the local temporary-directory volume.

## Measured results

Same production reward calls and allocation calibration on both sides. The control uses the reward and slot implementations from `3b9bf18c` with timing probes. The candidate uses `cf22d5bf` with the same harness. The earlier 32.73 ms figure mixed Arsenal activity and reflection with rewards. Its allocation count is not comparable to the isolated callback workload.

| Host and workload | Callback p50 ms, before → after | Callback p95 ms | Gameplay-thread CPU mean ms | Frame p50 / p95 ms, before → after | Frame allocation p50 bytes | Writes per sample |
|---|---:|---:|---:|---:|---:|---:|
| Editor, burst | 33.484 → 34.457 | 43.725 → 44.556 | 6.250 → 7.422 | 34.66 / 44.93 → 35.51 / 45.57 | 284,738 → 244,235 | 4 |
| Editor, single | 8.783 → 8.002 | 9.675 → 8.958 | 1.563 → 0.781 | 9.88 / 11.07 → 9.04 / 10.39 | 72,499 → 62,504 | 1 |
| Editor, main-only | 0.001 → 0.001 | 0.002 → 0.001 | 0.000 → 0.000 | 6.02 / 6.04 → 6.02 / 6.05 | 138,628 → 135,461 | 0 |
| Editor, traversal | — | — | — | 6.02 / 6.04 → 6.02 / 6.05 | 137,025 → 135,490 | — |
| Windows, burst | 34.722 → 35.002 | 37.893 → 42.898 | 6.250 → 6.250 | 35.48 / 38.71 → 35.94 / 43.64 | 280,870 → 240,411 | 4 |
| Windows, single | 9.057 → 7.988 | 9.992 → 9.811 | 1.953 → 1.563 | 9.97 / 10.78 → 8.89 / 10.70 | 70,545 → 61,587 | 1 |
| Windows, main-only | 0.001 → 0.001 | 0.001 → 0.001 | 0.000 → 0.000 | 6.02 / 6.03 → 6.02 / 6.02 | 3,880 → 1,828 | 0 |
| Windows, traversal | — | — | — | 6.02 / 6.02 → 6.02 / 6.02 | 410 → 410 | — |

Median elapsed milliseconds per four-return burst. The inspection column includes reading the previous envelope, checking its hash and validating the decoded records. Serialization covers encoding and hashing the outgoing envelope. The slot candidate copy/validation has its own column.

| Host / implementation | AirPower | Overflow rules | Progression listeners | Reserve HUD / feedback | Lock | Inspect | Candidate validation | Serialize | Stage write | Durable flush | Replace + backup |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Editor baseline | 0.002 | 0.060 | 0.493 | 0.172 | 0.861 | 0.817 | 0.083 | 0.092 | 0.920 | 9.646 | 19.534 |
| Editor candidate | 0.002 | 0.002 | 0.482 | 0.163 | 0.860 | 0.811 | 0.083 | 0.090 | 0.915 | 9.438 | 21.097 |
| Windows baseline | 0.002 | 0.051 | 0.432 | 0.121 | 0.812 | 0.738 | 0.074 | 0.082 | 0.908 | 9.632 | 21.084 |
| Windows candidate | 0.002 | 0.002 | 0.455 | 0.130 | 0.811 | 0.772 | 0.076 | 0.083 | 0.894 | 9.639 | 21.328 |

A preliminary per-thread allocation counter returned zero even for allocating work. A preliminary Development player also timed out because its compile variant omitted the benchmark entry point. The paired runs use a frame allocation recorder checked with a 1 MiB allocation and the profiler symbol present in the actual player. Neither preliminary run supplies figures in the comparison tables.

Time inside a durable flush and atomic publication dominates the burst on this machine. The allocation reduction repeats in the Editor and the player. The burst can still exceed a 16.7 ms frame budget. Storage timing varied between runs, and the CPU counter has coarse OS accounting resolution. A 0.000 reading does not establish zero CPU cost. Normal traversal is a short synthetic input route, not a complete player session. Results from a fresh slot on this machine do not establish timing on slower volumes or large endgame saves.

## Validation

- The failed-overwrite reproduction failed 1/1 before repair. The same case passed in the full 91/91 Edit Mode run, together with blocked publication and retry, failed post-commit backup refresh, missing/corrupt primary, stale temporary files, old formats, large Unicode payloads, stale revisions, independent slots, deletion, quest claims and relationship/cinematic progression.
- Full Play Mode passed 41/41. Three selected rendered cases passed 3/3, including the 9→13 same-slot reload, repeated copy/overwrite/world transitions, and a save and copy between a Sling return and a later Node return. The source spent its reserve while the copy reopened with Farseer’s and nine units. The additional paired Editor benchmark passed on both runs.
- The serialized-contract gate checked 65 source contracts against `7bfdfe5e` with zero failures. Edit Mode also audited authored scene and prefab references.
- The final three-scene Windows Development build completed with 0 errors and 24 warnings. Warning headlines and occurrence counts match the previous unified dagger build. Both benchmark players exited normally. External checksum verification confirmed the pre-quit 13-unit reserve and equipped pair in the backup and the same values in a newly published primary revision.
- Faults were injected with Windows file-sharing locks and disposable disk snapshots. A real power cut, device firmware failure and manual traversal feel were not exercised. A failure before commit leaves the previous acknowledged snapshot intact. A transient gameplay return interrupted by an OS error has no persistent retry queue. Both unreadable copies remain a reported load error.

Exact measurements, build warning comparison, test XML hashes and quit records are in [the result manifest](RewardPersistenceResults.json).

## Manual check

1. In a disposable Windows playthrough, equip Farseer's. Spend some AirPower and collect a Sling return followed closely by Air Node returns. Watch the main meter fill first, then the reserve. Compare camera and movement responsiveness at collection.
2. Save with a partial reserve and copy into a second disposable slot. Spend main AirPower and activate Second Wind. Check that the whole bank empties, the main meter caps, and a second press cannot repeat the transfer.
3. Return to title, load each slot, and compare the pair, reserve, world and journal with their values when saved. For recovery testing, use the automated disposable fixtures.
