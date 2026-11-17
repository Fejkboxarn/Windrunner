# Same-frame traversal reward persistence

Windrunner started this change at checkpoint `30752891` on the unified dagger integration branch. The Windows control reproduced four durable publications per four eligible overflow deliveries. The previous investigation documents why a staging-file flush and atomic replacement dominate this workload. [Previous measurements](RewardPersistence.md)

## Live and durable state

Only a disk-backed player session opens a collection frame on its existing progression service. Air Node and Sling Node tickets still claim once, fill main AirPower first and present each eligible reserve delta immediately. The fourth balance-changing delivery publishes the accumulated absolute balance. The session closes the window in LateUpdate and flushes even during pause or hit-stop. A later same-frame delivery publishes synchronously. A new frame cannot extend a pending batch. Memory previews and standalone progression fixtures keep synchronous behavior.

Manual Save writes one snapshot. Copy first flushes the source and then copies its world and progression. Load, Continue, New Game, active overwrite, Return to Main Menu, suspension and orderly quit enforce the source barrier. The outgoing session switches to synchronous collection through handoff. A same-slot destination reopens the latest revision on arrival. A successful active-slot replacement retires the earlier writer. The slot lock, checksummed envelope, staged flush, primary publication and protected recovery copy use the existing file format and writer.

All critical commands build their candidate from the latest live progression. A successful chapter turn-in can commit its occurrence/claim records, relationship rank, permanent entitlement and pending balance in one write. A rejected command leaves the pending deadline alone. An activation durably commits zero reserve before restoring main AirPower. Failed activation grants no energy. Retry writes only the absolute candidate state. It never calls a gameplay reward handler. The main meter and a traversal ticket's one-shot claim remain transient, as before.

## Notifications and failure handling

The reserve HUD and charge feedback listen to the immediate reserve delta. The progression feedback observer compares completed quest and permanent claim counts after durable commits. Journal, fact/unlock views, relationship contacts, timed retrieval and the fixture guide refresh from durable Changed notifications. The dagger player queues a synchronization check on that event. None of these listeners grants the reserve. Tests record three immediate HUD deltas, publication on the fourth return, and its HUD delta. Activation publishes the debit before presenting the transfer. End-of-frame publication emits no second reserve delta.

A failed publication retains the credited balance and existing bound revision, reports a save error and pauses gameplay. Subsequent real return streams wait with their original tickets before claiming them. Retry runs on the main thread using unscaled delays of 250 ms, one second, then five seconds. A manual save can retry immediately. An external revision conflict stops automatic retries. No automatic reload or rebase can copy the stale state over a different revision. During a late-return failure the session holds scene activation until the outgoing slot publishes; the failed-write handoff test observes both disk and live balances. Normal quit can be vetoed on a failed save.

An abrupt exit can lose positive reserve earned after the last durable publication, normally up to one unfinished frame and at most the four credits in that batch. A long frame, slow storage, suspend or I/O failure can extend elapsed time. While storage is unavailable an unflushed reserve is recoverable in the live session; process loss can still discard it. A permanently conflicting revision requires resolving the slot conflict. Power cuts and hardware cache failure were not simulated.

## Matched performance

Four warmups and 40 measured samples per workload, with frame allocation calibrated against a retained 1 MiB allocation. Fresh and populated slots exercise the same production player, HUD, progression listeners and local temporary-directory volume. Populated slots contain 128 extra durable gameplay facts. A spread sample distributes four returns across four consecutive frames. Its callback duration is their sum, its affected-frame duration and allocation are the worst of the four, and the following sample is the next frame. For other workloads there is one delivery frame per sample. Reset and transfer occur after following-frame measurement. Thread CPU uses coarse Windows kernel/user time inside the callbacks only. Timed write phases include late-frame persistence. Independent medians need not add to the median total.

| Windows workload | Writes per sample | Callback p50 / p95 ms | Affected frame p50 / p95 ms | Following frame p50 / p95 ms | Affected frame GC p50 bytes |
|---|---:|---:|---:|---:|---:|
| burst | 4 → 1 | 27.09 / 28.41 → 9.97 / 12.00 | 27.98 / 29.22 → 10.83 / 12.82 | 6.02 / 6.03 → 6.02 / 6.03 | 240,411 → 59,584 |
| single | 1 → 1 | 7.29 / 7.73 → 0.01 / 0.02 | 8.01 / 8.60 → 9.66 / 10.52 | 6.02 / 6.03 → 6.02 / 6.03 | 60,578 → 60,793 |
| spread | 4 → 4 | 27.63 / 28.94 → 0.09 / 0.10 | 8.23 / 8.97 → 10.65 / 19.34 | 6.02 / 6.03 → 6.02 / 6.03 | 63,039 → 60,083 |
| populated-burst | 4 → 1 | 48.34 / 50.55 → 12.77 / 14.12 | 49.24 / 51.46 → 13.50 / 14.93 | 6.03 / 6.03 → 6.02 / 6.03 | 4,166,599 → 948,525 |
| populated-single | 1 → 1 | 12.37 / 13.06 → 0.01 / 0.02 | 13.22 / 14.22 → 12.78 / 13.85 | 6.02 / 6.03 → 6.02 / 6.03 | 1,043,445 → 948,709 |
| populated-spread | 4 → 4 | 47.19 / 49.14 → 0.09 / 0.10 | 13.29 / 13.98 → 12.93 / 14.06 | 6.02 / 6.04 → 6.02 / 6.03 | 1,043,792 → 948,946 |
| main-only | 0 → 0 | 0.00 / 0.00 → 0.00 / 0.00 | 6.02 / 6.05 → 6.02 / 6.02 | 6.02 / 6.02 → 6.02 / 6.02 | 1,845 → 1,688 |


| Four-return workload | Callback thread CPU mean ms | Slot inspection p50 ms | Candidate validation p50 ms | Envelope serialization p50 ms | Staged write p50 ms | Durable flush p50 ms | Publication with backup p50 ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| burst | 6.641 → 2.344 | 0.753 → 0.211 | 0.077 → 0.022 | 0.082 → 0.022 | 0.898 → 0.242 | 2.146 → 2.751 | 21.006 → 5.947 |
| populated-burst | 23.438 → 6.25 | 5.967 → 1.567 | 2.022 → 0.389 | 1.99 → 0.392 | 1.95 → 0.384 | 2.381 → 1.452 | 23.689 → 6.246 |

Windows traversal control covered 120 frames and 3.35 m. Median / p95 frame duration was 6.02 / 6.02 ms before and 6.02 / 6.02 ms after. The rendered Editor candidate recorded 40 writes across 40 fresh-slot bursts. Baseline and candidate Windows runs were separate guarded builds and used the same expanded measurement workload. Timing fluctuates between runs and machines.

A second run used the same executables in reverse order. Whole affected frames are shown here to expose run-to-run variation, particularly for isolated and spread-out rewards.

| Repeat workload | Control p50 / p95 ms | Candidate p50 / p95 ms |
|---|---:|---:|
| burst | 27.70 / 29.12 | 9.87 / 10.94 |
| single | 8.10 / 9.22 | 9.78 / 10.63 |
| spread | 8.10 / 8.85 | 9.66 / 11.73 |
| populated-burst | 48.97 / 51.83 | 15.19 / 17.50 |
| populated-single | 12.54 / 13.77 | 14.18 / 15.60 |
| populated-spread | 12.46 / 13.43 | 13.99 / 14.95 |
| main-only | 6.02 / 6.02 | 6.02 / 6.02 |

Only same-frame bursts eliminate publications. Single returns still pay the full storage cost. In the first fresh-slot run the spread workload’s worst-frame p95 increased from 8.97 to 19.34 ms. Neither single nor spread workloads have a demonstrated general frame-time improvement. The fresh-slot isolated frame was slower in both run orders. Following frames stayed near 6 ms. Storage timing and these samples do not establish performance on other hardware.

## Verification and reproduction

Implementation checkpoint `78c95b37` passed 95 ordinary Edit Mode and 45 ordinary Play Mode cases. The selected rendered run passed 4/4. The remaining opt-in suites passed 3/3 Edit Mode and 8/8 Play Mode. Together these runs exercised 152 distinct cases. The serialized gate retained all 65 source contracts. Edit Mode also audited production scenes and prefabs. No authored scene, prefab, profile, input map or TerrainData was changed. Exact result counts, file hashes, Windows builds and warnings, crash and quit snapshots, and all phase timings are in the [evidence manifest](RewardBatchingResults.json).

The external harness terminated disposable Windows players while their game thread was parked before LateUpdate, after the fourth publication, and after a pending gain had been spent. It verified the slot identity, checksum, previously committed fact and equipped pair in each case. Recovered balances were 0, 12 and 0 respectively. A separate graceful quit published 13 and preserved the pre-quit revision as backup. Fault tests exercised file locks, retries during pause, scene activation, slot changes, stale writers, missing/corrupt saves and the recovered-slot overwrite regression.

Use `Tools/Run-UnityTests.ps1` with the EditMode and PlayMode platforms for the ordinary suites and pass `-Rendered -Filter RenderedRewardPersistence` for the benchmark. Build with `./Tools/Build-UnityCandidate.ps1 -MainMenu -Development -Label <fresh-label>`. Run `Tools/Measure-RewardPersistence.ps1` and `Tools/Test-RewardInterruption.ps1` with that label. For a repeat with the same build, supply a fresh `-RunLabel` to the measurement script and use `-SynchronousControl` for the instrumented pre-batching executable. The interruption harness operates only on the development processes and disposable slots it creates.

## Manual acceptance

1. In a disposable Windows slot, equip Farseer's. Collect several close-together Node and Sling returns while moving. Check the main meter, reserve HUD and charge effects for each delivery.
2. Spend main AirPower and activate with a partial reserve. Check the transfer, then save, return to title and load. Verify that the spent reserve stays empty.
3. Copy a partially charged slot and play both independently. Compare their reserve, equipped pair, journal and world on reload. Judge perceptible stalls on target hardware, especially with a long-running save.
