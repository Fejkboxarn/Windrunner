# Unified dagger integration candidate

## Branch and runtime ownership

The clean starting tips were `cc31b179` (codex/unified-development) and `f7f9552d` (feature/dagger-equipment). Their merge base was `cc31b179`. The local codex/dagger-integration branch fast-forwarded through `915b2984`, `476c9342`, `bc230e82` and `f7f9552d`. Git produced no content conflicts. The production, progression, Movement Lab, main menu, Zone 01 and Terrain Studio tips are all ancestors of the candidate.

AirPowerState still owns spending, main capacity, recovery and reward splitting. The Air Node hit and Sling Node death return streams submit one-shot reward tickets. DaggerEquipment listens for the excess on the assigned player, checks the equipped definition at delivery, and writes the pair's balance through ProgressionService and the active playthrough store. The default reserve is 50. Transfer consumes the entire balance, accepts up to the missing main capacity and discards the remainder. It has no cooldown. Full time freezes and cinematic gameplay blocks suppress input. A positive perfect-timing slowdown still permits normal gameplay input.

Comparing both source tips found 28 new unique feature metadata GUIDs. Every existing metadata file survived unchanged. Removing the dagger adapter and its two wiring records reproduces both original production world scenes. The input templates retain every other action and binding after accounting for the two dagger actions and the retired gym Overdrive binding. The integration fixes required no scene, prefab, profile, terrain, package, assembly-definition or build-list edits.

## Reliability corrections

**Reloading the active slot.** Destination validation happened before the outgoing world had finished delivering rewards. In the reproduction, reserve was 9 at preflight, a return added 4 before unload, and the disk record held 13. The arriving service still held 9 and an older store revision. Same-slot arrival now reopens the committed slot and checks the world identity before registering its service. Preflight validation and the store's concurrent-write protection remain active.

**Arsenal presentation.** Equip and Unequip rebuilt the portrait's selected models and rendered the same selection again while refreshing the button states. The menu now refreshes the controls while retaining that portrait. Selection changes and rotation still render. A regression assertion checks the preview model's object identity across Equip.

## Rendered lifecycle and timing

The test opened the production menu from Zone 01, selected and rotated Farseer's, equipped it, resumed gameplay, delivered four rewards to an on-disk disposable slot, transferred energy, and equipped Training. It performed four warm-ups followed by 40 measured cycles. Every cycle verified one portrait camera and target while open, zero after close, one current attachment per hand, retirement of previous attachments, the reserve HUD state, and restoration of the gameplay camera. Burst objects were gone after their configured lifetime.

| Windows Editor measurement | Median | 95th percentile |
| --- | ---: | ---: |
| Open, select, rotate and equip | 18.63 ms | 25.73 ms |
| Four immediate reward deliveries with slot writes | 32.73 ms | 39.21 ms |

The pre-presentation-fix run measured 19.64/27.81 ms for the menu sequence and 29.56/39.61 ms for the reward sequence. These are two runs of the test harness, including reflection and synthetic UI submission. The object-identity test establishes the eliminated rebuild. The timing difference alone does not establish a general speedup.

The per-thread allocation API reported zero for a known 32 KiB allocation and was discarded. Unity's whole-frame GC recorder detected a retained 1 MiB calibration allocation. Median observed frames reported 1,507,997 bytes for the menu workload and 727,031 bytes for the reward workload, including other Editor and test activity. The immediate reward path includes save validation, serialization, file writes, notifications and HUD refresh. Bursts of synchronous rewards remain a frame-time risk; these measurements do not isolate the cost of an individual subsystem.

| Memory sampled before / during / after the workload | Baseline | Sampled peak | After cleanup |
| --- | ---: | ---: | ---: |
| Unity total allocated memory | 1,037.66 MiB | 1,041.91 MiB | 1,037.68 MiB |
| Managed heap reported by GC | 48.13 MiB | 69.83 MiB | 47.03 MiB |

Cleanup included deferred destruction, effect expiry, unused-asset unloading and a collection. Loaded meshes returned to 25 and materials to 175, their baseline counts. The workload found no accumulating portrait targets or cameras. These observations cover the sampled Editor session.

## Candidate verification

Runtime checkpoint `a3191ffc` contains the two integration fixes. Checkpoint `81dbef4a` contains the regressions and stress fixture. Unity 6000.6.2f1 executed **137 distinct passing test cases**, including the full standard suites (86 Edit Mode and 40 Play Mode), three additional Edit Mode profiling cases and eight additional rendered Play Mode cases. The uncapped scene profile and targeted flow reruns are repeats. The serialized-contract gate checked 65 source contracts with zero failures. Loaded-reference tests covered the production worlds, gym, progression fixture and owned prefabs. [Exact test identities, XML hashes and build evidence](DaggerIntegrationResults.json)

The integration flows used unique temporary slot directories. They exercised the following production paths:

- An Air Node return delivered after switching to Training filled main AirPower and left the bank alone. Consecutive tickets, duplicated deliveries, main capacity changes through an actual Character Tuning draft, full-meter rejection, and whole-bank discharge were verified.
- During an active Dash, a return filled the main meter, swapping pairs preserved Dash, and the rebound dagger action discharged the reserve under positive slowdown. Full freeze and the production cinematic camera/control lease rejected the press and suppressed a held press across recovery. Other tests ran the full authored Cast and Sling reward sequences, cancelled traversal, reconnected devices, and checked input override persistence.
- A late return during reload survived the next save. Three rounds overwrote a separate destination, checked the source world ID on the copy, created and saved a fresh playthrough, returned to title, and loaded the copy. Source-only progression facts and equipment changes stayed in the source. Existing compatibility tests covered missing definitions, old cooldown records, idempotent unlocks and partial reserve restoration.

| Windows Development target | Errors | Warnings | Rendered startup |
| --- | ---: | ---: | --- |
| Main menu, Zone 01 and StartingWorld | 0 | 24 | Initialized and survived 15 s, zero matched errors |
| Standalone Movement Gym | 0 | 25 | Initialized and survived 15 s, zero matched errors |

Neither target emitted a new warning headline or occurrence relative to its previous dagger build. Production reported 14 fewer FEEL analyzer occurrences on this build; those diagnostics remain visible in the gym build. Remaining warnings include mesh import and collision bake settings, obsolete project object lookups, optional FEEL assets, and Core, INab, Toony Colors and Raygeas shader diagnostics. Windows smoke tests cover initialization and idle. Interactive flow validation ran in Unity Play Mode.

The known integration reproduction now passes. Historical saves with an already incorrect world ID still require a known-good source or backup. The rapid-reward frame cost and physical-device gameplay acceptance remain open.

## Manual acceptance

1. Start a disposable slot in the Windows development player. Open Arsenal from gameplay and pause. Navigate and rotate with keyboard and gamepad. Judge focus, framing, both grips and camera recovery.
2. Equip Farseer's, earn node returns, Dash and release Second Wind. Check transfer feel at different speeds, numeric HUD readability, short VFX and audio during Cast and Sling. Repeat with Training equipped.
3. Save a partially charged pair, copy into a disposable slot, return to the title and reload both. Check the pair, reserve, world arrival and journal state.
4. Play the separate gym candidate and check wind, traversal, animation and input feel on target hardware. Human gameplay and artistic acceptance remain open.
