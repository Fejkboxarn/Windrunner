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
