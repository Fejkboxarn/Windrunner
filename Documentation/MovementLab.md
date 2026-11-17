# Windrunner Movement Lab

Open `Assets/MovementLab/MovementGym.unity` and enter Play Mode, or choose **Tools → Windrunner → Movement Lab → Open Movement Gym**. The gym uses the production player, animation graph, Air Dash and Sling, orbit camera, input host, AirPower, FEEL and feedback wiring. Its scene derives from the production composition with unrelated scenery removed. All lab geometry, experimental settings and input additions live under `Assets/MovementLab`.

## Play and tune

1. Focus the Game view and follow the white station sign and distance markers. By default Page Down and Page Up change sections, Home returns to the selected start and refills AirPower, and F3 toggles the measurements and trajectory. The HUD displays the current bindings. Escape opens the regular pause/rebind panel. Teleporting deliberately cancels Sling and automatic Dash, clears motor momentum, refills the actual player meter, aligns the camera and starts a fresh measurement session.
2. Try Slopes, Jumps, Dash, Sling, Aerial Steering and Momentum first. Use normal movement, jump, Dash and Sling controls. The teal ramp is the launch, the gold platform is the landing, and spherical Sling targets use production targeting and charge timing. Use the 1,000 × 1,000 m common floor for unrestricted ground runs. A fall ten metres below the lower platform in an elevated section returns you to its start. Leaving the common floor also leads to recovery.
3. At Wind, move through the teal continuous tunnel, then the gold entry tunnel. Turn sideways inside, jump through while rising, and enter while falling. Boost follows current velocity. Stand still inside the entry volume, move, exit and re-enter to compare an invalid entry with a valid one. Compare the HUD's active tunnel count, measured velocity, actual traveled distance and cumulative wind contribution. The last quantity sums admitted velocity increments before collision response.
4. At Overdrive, hold Q or gamepad left shoulder while moving, release, and compare with another run holding Dash. Use **Equip Overdrive pair** in the Movement Lab window during play to repeat with the pair unequipped. Both abilities debit the actual AirPower meter. A full depletion or interruption requires releasing and pressing again. Existing motor steering, braking and gravity continue on release and on leaving wind. Overdrive can be held at standstill and will consume AirPower while active. It has no independent direction to accelerate until movement begins.
5. At HighSpeed, use the long approach to compare coast, Dash, Overdrive and their combinations. At Combined, aim along the ramp and use the relay spheres to chain Dash, wind, jump, Sling and Overdrive. The Extreme preset has 350 m of horizontal launch-to-destination spacing, a 100 m rise and a 45° approach relative to the landing deck. The final deck reaches 300 m above gym origin. Ordinary Sling acquisition range still applies. The intermediate targets make the approach testable before the final destination is in range.
6. In Edit Mode, select a section in **Tools → Windrunner → Movement Lab**. Choose Short, Medium, Long or Extreme, or change its origin, run-up length, spacing, rise, approach heading, ramp slope, Sling target offset and tunnel dimensions. The window updates actual geometry and markers. Duplicate a section to compare variants. Undo restores the edited scene objects and bindings. Edit the dedicated profile below the section controls for wind mode, strength, cap, eligibility, edge fade, ramp, vertical response and Overdrive cost, threshold, cap, ground/air strength and ramp. Save the gym scene or lab profile to keep deliberate edits. In Play Mode use **Reset measurements** to start timing at the current position without moving or refilling the player.
7. Check gameplay feel on hardware. Compare steering and braking just after release, camera yaw/FOV and visual readability at peak speed, slope lips, thin barriers, glancing impacts, full depletion, controller disconnect, pause/resume and Sling transitions. Try several approach angles and advanced relay sequences. Record the section, preset, input sequence and observed peak before comparing runs. Completing the extreme lane by hand remains an open acceptance test.

## Movement and resource ownership

The opt-in motor callback runs after the existing planar/jump/gravity calculation and before the existing CharacterController move. It returns a velocity increment which enters the same simulation state and collision response. The lab adapter is installed only on the gym player. Direct repositioning is confined to intentional station changes and fall recovery. The orbit camera provides its own teleport reset. The original input asset, player tuning and progression content remain at baseline.

Wind samples oriented box coordinates in the motor step. It uses the strongest overlapping wind contribution, applies a per-effect speed limit, and reserves the remaining combined cap for Overdrive. Existing overspeed is preserved. Continuous wind can engage when movement reaches its minimum inside the volume. Entry impulse requires a valid crossing and delivers its configured amount once per visit, optionally over a ramp. The vertical weight controls rising/falling amplification; grounded movement excludes ground-stick velocity from direction and entry eligibility. A segment test across the preceding actual displacement catches complete crossings of thin volumes between samples. It can credit a transit on the following motor tick. Sling ownership suspends both effects and Overdrive requires a new press.

The gym's cloned gameplay template preserves production binding IDs and adds the dedicated Overdrive/navigation actions. Both Dash and Overdrive use the existing AirPower depletion/recovery policy. Overdrive converts only the paid part of a final partial tick to acceleration. Diagnostics use a bounded trajectory buffer and update text at a lower rate than the motor. The `MovementLab.ResolveInfluences` profiler marker covers the influence path, including its synchronous resource call.

## Test evidence

Unity 6000.6.2f1. Baseline `9e8f97c4`, first verified lab checkpoint `9fe70b61`. The protected branch heads were `7bfdfe5e` (main), `aa9e9d58` (production-refactor) and `9e8f97c4` (feature/progression-framework).

The flat probe uses the real gym player and a temporary straight deck, wide 0.3 m wall, sustained wind volume and synthetic production inputs. Timing and physics settings are restored after each test session. Rendered Game View run `gym-final-rendered` passed 1/1 at 1292 × 719 on DX11. Values below include Editor and test overhead. Capture FPS controls simulated delta, independently of the display cadence. Frame intervals cover the three-second sustained-use interval and are wall-clock milliseconds. Influence CPU is the median Unity marker sample.

| Simulated FPS | Fixed step (s) | Peak (m/s) | Forward distance (m) | Wind added (m/s) | Frame interval median / p95 (ms) | Influence CPU (µs) |
|---:|---:|---:|---:|---:|---:|---:|
| 30 | .03333 | 169.76 | 399.45 | 61.75 | 6.03 / 6.90 | 10.0 |
| 60 | .02000 | 169.76 | 399.45 | 61.54 | 6.01 / 6.46 | 9.8 |
| 120 | .01000 | 169.76 | 399.45 | 61.07 | 6.02 / 6.72 | 11.2 |

All three finished on the near side of the broad wall. The original narrow-wall probe finished beyond the end plane, and the wider probe showed substantial lateral sliding. A straight-on wall result does not establish universal collision stability at arbitrary thickness, speed or approach. Wind contribution varies with sampling near the boundary. The real extreme-section run with one input-driven Sling charge/release recorded 265.08 m maximum horizontal excursion **from spawn**, 26.13 m maximum rise from spawn, 169.92 m/s peak, and successful node flight. The automated route did not clear the full gap. The runway before the gap contributes to the excursion. No production physics or ability limits were retuned to force completion.

Edit Mode covers deterministic directional boosts, cap/overlap, stationary/pause, once-per-visit impulses at three step sizes, final partial payment, interruption/rearm, and an audit of loaded scene references. Play Mode exercises actual jump/Overdrive, unequipped gating, Dash/wind/Overdrive overlap, scaled time, menu interruption, sustained traversal, collision and real Sling takeover. Full-suite and build results are recorded below. Manual control ergonomics, camera comfort, trajectory feel and target hardware budgets remain open.

Earlier failures included an audit treating Unity's intentionally empty prefab bookkeeping fields as required references, the narrow-wall probe, and a profiler test namespace compile error. The final audit checks required lab bindings and missing referenced objects across the loaded gym. The compiler error was fixed by qualifying the Unity profiling namespace.

Reproduce with `Tools/Run-UnityTests.ps1 -Platform EditMode -Label <fresh-label>`, the corresponding PlayMode command, and `-Platform PlayMode -Rendered -Filter MovementGymIntegrationTests` for the rendered probe. Use `Tools/Build-UnityCandidate.ps1 -MovementGym -Label <fresh-label>` for the Windows gym. The scripts check for a running Editor before opening the project. Raw XML, logs, backend-specific probe CSVs, extreme-run measurements and the timestamped spawn capture are in `Logs/ProductionMigration`.

## Integrated verification, 17 November 2026

| Check | Executed result |
|---|---|
| Full Edit Mode and Unity compilation | `gym-final` passed 54/54, exit 0 |
| Full Play Mode | `gym-final` passed 32/32, exit 0 |
| Rendered real-player probe | `gym-final-rendered` passed 1/1, exit 0. Spawn view captured and inspected |
| Loaded gym serialization | Edit test verified the unique production motor, ten modules, required lab bindings, missing scripts and unresolved serialized object references |
| Asset contracts | Production contract script reported zero failures. Git comparison against 9e8f97c4 found zero modified/deleted pre-existing serialized assets, packages or project settings |
| Windows x64 gym build | Succeeded, 0 errors and 11 warnings, 21.216 seconds, 330,321,150 bytes. Only MovementGym.unity was packaged |
| Windows rendered startup | Initialized and survived the 15-second smoke run with zero matched errors |

The build warnings comprise three existing FindFirstObjectByType API deprecations, two stripped Core debug shaders, three optional FEEL resource binding warnings and three vendor shader compiler warnings. The executable is `Builds/movement-gym-20261117/Windrunner.exe`.

The first checkpoint added both prototypes, production integration and the authored gym. The final checkpoint adds thin-volume transit sampling, an isolated gym composition, duplicate-with-Undo modules, distance markers, extreme Sling relays, loaded-reference checks, the full regression results and rendered measurements.
