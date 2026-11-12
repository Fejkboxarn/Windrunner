# Measured performance findings

Headless Unity 6000.6.2f1 Editor, warmed 100,000-call microbenchmarks; not a player frame-time/GPU profile.

| Path | Before (ms) | After (ms) |
|---|---:|---:|
| Resource UI normalized-value callback | 4.7852 | 2.6680 |
| Direct typed resource getter (control) | 1.6864 | 1.8505 |
| Stable feedback observer dispatch | 0.9828 | 0.9616 |

An earlier uncalibrated run measured the UI callback at 12.1263 ms, showing why one timing sample is not a frame-rate claim. The calibrated comparison supports removing redundant reflection over a sealed, concrete AirPower dependency. Typed binding also eliminates guessed member-name tables and fallback discovery logic while preserving serialized fields and plugin animation calls. This is a small local improvement, not evidence that UI was the game's main bottleneck.

**Allocation-counter limitation:** GC.GetAllocatedBytesForCurrentThread returned zero even for a retained 8 KiB calibration allocation in this Unity runtime. Reported zero byte counts are unsupported, not proof of zero allocations. The test explicitly checks calibration and only enforces an allocation budget when the counter is functional. Native Unity Profiler/player capture remains required for GC evidence.

Inspector review: the knife/arm custom inspectors already cache preset lists and refresh on EditorApplication.projectChanged, and dispose cached SerializedObjects. No per-repaint asset scan was found in those two inspectors; no speculative rewrite performed.

Remaining measurements: representative rendered traversal with node-count scaling, physics/LOS, cloth and animation graph evaluation, FEEL pool saturation, GPU/VFX cost, Inspector interaction with large selection sets, and player-build CPU/GC captures. Do not extrapolate these microbenchmarks to FPS or claim measured native allocation improvements.

## Rendered baseline and replacement allocation measurement

Executed twice in Unity Editor on NVIDIA GeForce RTX 5070 Ti / Direct3D11. Enabled gameplay scene, fixed 1280x720 camera RenderTexture, 90 warmup frames, verified nonblack pixels, 180 measured idle frames. Native ProfilerRecorder `GC Allocated In Frame` observed **1,054,140 bytes** during a retained 1 MiB calibration allocation in both runs. This replaces the unsupported per-thread counter for frame-level evidence; it does not provide per-method allocation attribution.

| Capture | Median frame ms | p95 frame ms | Median main-thread ms | Median GC bytes/frame |
|---|---:|---:|---:|---:|
| rendered-baseline | 6.492 | 7.263 | 6.480 | 4,596 |
| rendered-repeat | 6.192 | 7.004 | 6.189 | 3,570 |

Both profiling tests passed (1/1 each). Variation includes Editor/Test Runner work; these samples do not prove an optimization or establish a shipping FPS budget. No bottleneck was sufficiently attributed to justify a new performance rewrite. There is measurable frame allocation, not zero allocation. Full traversal, GPU timing, player builds and call-stack attribution remain outstanding.

Reproduce: `Tools/Run-UnityTests.ps1 -Platform PlayMode -Rendered -Filter RenderedSceneProfileTests -Label <fresh-label>`. Ordinary suites exclude the Profiling category. Actual capture JSON is included in TestResults.json and local Logs/ProductionMigration/rendered-profile-*.json.

## Traversal and lifecycle scenarios (two executed rendered runs)

Unity 6000.6.2f1, Intel Core Ultra 7 265KF, RTX 5070 Ti. `traversal-scenarios` and `traversal-scenarios-repeat` each passed 1/1, exercising eight actual regression scenarios with native Main Thread, GC Allocated In Frame and Total Used Memory counters. Nested test iterators are measured frame by frame. Fixed simulation time is used by ability fixtures; wall time comes from Stopwatch, not simulated delta time.

These include assertions/reflection, transient scene loading, Editor rendering and test infrastructure. Simple ability fixtures are not the full art scene. Full scene animation and reload are included separately. Rendering is enabled but only the earlier 1280x720 idle capture validates pixels. No GPU duration, standalone-player frame budget, leak conclusion, zero-allocation claim or causal optimization is inferred.

| Scenario | Samples | Median main ms | p95 main ms | Median GC bytes | p95 GC bytes | Peak process used MiB |
|---|---:|---:|---:|---:|---:|---:|
| cast-perfect-projectile-reward (09:52:37) | 45 | 6.021 | 6.036 | 133,784 | 145,126 | 3106.1 |
| cast-perfect-projectile-reward (09:53:36) | 45 | 6.019 | 6.059 | 135,319 | 142,994 | 3101.3 |
| dash-deplete-reward-reactivate-interrupt (09:52:37) | 28 | 6.020 | 6.060 | 129,179 | 141,135 | 3105.7 |
| dash-deplete-reward-reactivate-interrupt (09:53:36) | 28 | 6.021 | 6.030 | 132,539 | 141,749 | 3100.9 |
| live-animation-actions-restoration (09:52:27) | 239 | 6.022 | 6.819 | 121,584 | 137,636 | 3281.6 |
| live-animation-actions-restoration (09:53:25) | 239 | 6.022 | 6.523 | 123,733 | 138,480 | 3273.8 |
| movement-jump-reversal-pause (09:52:27) | 214 | 0.215 | 0.525 | 853 | 901 | 607.0 |
| movement-jump-reversal-pause (09:53:25) | 214 | 0.235 | 0.603 | 853 | 901 | 598.8 |
| scene-reload-ui-handoff (09:52:35) | 7 | 6.030 | 253.594 | 100,713 | 504,428 | 3604.1 |
| scene-reload-ui-handoff (09:53:33) | 7 | 8.386 | 231.229 | 101,026 | 504,725 | 3601.8 |
| sling-cancel-pause-normal-release (09:52:37) | 65 | 6.021 | 6.043 | 133,477 | 144,222 | 3105.7 |
| sling-cancel-pause-normal-release (09:53:35) | 65 | 6.021 | 6.044 | 134,688 | 144,529 | 3100.8 |
| sling-charge-perfect-traverse-impact-launch (09:52:36) | 65 | 6.021 | 6.051 | 130,714 | 143,608 | 3104.3 |
| sling-charge-perfect-traverse-impact-launch (09:53:35) | 65 | 6.021 | 6.054 | 134,398 | 142,687 | 3099.7 |
| water-swim-pause-exit-recovery (09:52:38) | 38 | 6.021 | 6.094 | 131,925 | 140,521 | 3106.7 |
| water-swim-pause-exit-recovery (09:53:36) | 38 | 6.021 | 6.035 | 129,793 | 142,687 | 3101.8 |

Reproduce with `Tools/Run-UnityTests.ps1 -Platform PlayMode -Rendered -Filter TraversalScenarioProfileTests -Label <fresh-label>`. Highest reload/animation spikes include import/loading and test work. The measured costs justify follow-up native allocation call-stack and player captures, not an unverified gameplay rewrite. Water samples show nonzero allocations; cached water mesh arrays and triangle traversal do not establish attribution to swimming. High node counts, pool saturation and target-hardware GPU budgets remain performance acceptance work.

Final RC3 standalone startup/idle (15 s, DX11/RTX 5070 Ti): aggregate process CPU 10.53125 s, peak working set 1,012,662,272 bytes, ending private memory 2,609,991,680 bytes. Run passed initialization/error checks. No input, pixel assertion, GPU timing or graceful-shutdown test; process totals include startup and do not establish gameplay frame budgets. Reproduce with Smoke-UnityCandidate.ps1 -Label <existing-build-label> -Rendered on fresh evidence paths.

## Production-only character and source retirement (2026-11-12)

The production-rendered-baseline test passed 1/1 with 180 samples at 1280x720, DX11/RTX 5070 Ti: median wall 6.165497 ms, p95 6.940002 ms, median main thread 6.1708 ms, median native frame GC 2,664 bytes. Its 1 MiB calibration recorded 1,054,260 bytes. This validates the counter, not zero allocations. Raw evidence: rendered-profile-20261112-115117.json.

After physically retiring the legacy runtime/source/controller group, production-clean-traversal passed 1/1 with the eight scenarios below. These are rendered Editor measurements including fixtures/assertions/loading; not isolated gameplay costs or player FPS. No causal optimization or visual parity is claimed by comparing different runs.

| Scenario | Samples | Median main ms | p95 main ms | Median GC bytes | p95 GC bytes |
|---|---:|---:|---:|---:|---:|
| cast-perfect-projectile-reward | 45 | 6.022 | 6.033 | 144,222 | 152,563 |
| dash-deplete-reward-reactivate-interrupt | 28 | 6.021 | 6.031 | 141,442 | 150,362 |
| live-animation-actions-restoration | 239 | 6.022 | 6.372 | 136,465 | 146,434 |
| movement-jump-reversal-pause | 214 | 0.203 | 0.389 | 853 | 889 |
| scene-reload-ui-handoff | 7 | 6.036 | 249.547 | 113,914 | 503,628 |
| sling-cancel-pause-normal-release | 65 | 6.023 | 6.032 | 146,678 | 152,818 |
| sling-charge-perfect-traverse-impact-launch | 65 | 6.021 | 6.038 | 139,003 | 146,968 |
| water-swim-pause-exit-recovery | 38 | 6.025 | 6.036 | 138,696 | 147,582 |

Reload spikes warrant player allocation/call-stack investigation, not an unmeasured gameplay rewrite. Remaining performance gates: target-player CPU/GPU budgets, sustained memory/reload traces, node/feedback saturation and large-selection Studio responsiveness.

Final production-clean-20261112 standalone DX11 startup/idle: survived 15 s, process CPU 10.875 s, peak working set 987,619,328 bytes, ending private memory 2,579,845,120 bytes. Headless also passed. These include initialization/idle and intentional harness termination, not gameplay frame or graceful-shutdown validation.
