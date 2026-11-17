# Reward persistence and slot recovery

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

## Manual check

1. In a disposable Windows playthrough, equip Farseer's. Spend some AirPower and collect a Sling return followed closely by Air Node returns. Watch the main meter fill first, then the reserve. Compare camera and movement responsiveness at collection.
2. Save with a partial reserve and copy into a second disposable slot. Spend main AirPower and activate Second Wind. Check that the whole bank empties, the main meter caps, and a second press cannot repeat the transfer.
3. Return to title, load each slot, and compare the pair, reserve, world and journal with their values when saved. For recovery testing, use the automated disposable fixtures.
