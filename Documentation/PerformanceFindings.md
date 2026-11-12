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
