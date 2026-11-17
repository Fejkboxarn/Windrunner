# Windrunner Movement Lab

Open `Assets/MovementLab/MovementGym.unity` and enter Play Mode, or choose **Tools → Windrunner → Movement Lab → Open Movement Gym**. The gym uses the production player, animation graph, Air Dash and Sling, orbit camera, input host, AirPower, FEEL and feedback wiring. Its scene derives from the production composition with unrelated scenery removed. All lab geometry, experimental settings and input additions live under `Assets/MovementLab`.

## Play the redesigned gym

1. Open **Tools > Windrunner > Movement Lab > Open Movement Gym**, enter Play Mode and click the Game view. Press the station key directly. The camera faces the runway. Follow the start stripe and distance signs to the finish. **F12** cancels traversal, restores AirPower and restarts the selected station. The backquote key toggles the trajectory and extra diagnostics. Use **Escape > Controls** for binding overrides. The HUD reads the active gameplay bindings.
2. **F1 Slopes** has a joined incline, crest and descent. Walk up, turn across the crest, then descend. **F2 Jumps** starts with a 2 m gap. Run with W and press Space just before the stripe. **F3 Dash** starts with a 36 m straight. Hold W and Shift, check the power cost, then coast. Medium rises, Long and Extreme add a change of heading at the lip.
3. **F4 Sling** has a gold node 16 m beyond the baseline lip. Face it, hold and release the Sling input (right mouse by default), then carry the launch onto the gold deck. Look for a grounded finish. **F5 Aerial Steering** has a landing offset to the right. Jump at the lip and steer right in the air. **F6 Momentum** compares running, holding Dash and releasing Dash while still steering.
4. **F7 Wind** opens on a dry track. Run straight and note entry, highest speed inside, and first speed after exit. Press **V** to restart on the continuous track, then again for the impulse track. The three tracks share a deck. Teal marks the continuous volume and gold marks the impulse gates. Reverse through a gate to check re-entry. Standard uses 12 m/s² with a 1.25 s ramp and 22 m/s ceiling for sustained wind, and a single 6 m/s burst with a 3 m/s entry threshold, 23 m/s ceiling and 0.5 s rearm for impulse.
5. **F8 HighSpeed** gives a long straight and braking deck. **F9 Overdrive** uses W and Q, or left shoulder on a gamepad. Release Q and watch the meter and speed. **F10 Combined** defaults to Long with a jump lip, hills, Sling relays and both wind modes. Try the route with Dash, jump and Sling first. Overdrive is optional. Extreme is a separate challenge.
6. **F11** switches to a collision-free inspection camera in the Editor or Development player. Use WASD relative to view, mouse to look, Space/Ctrl vertically and Shift for fast flight. F11 returns the production character to its safe station pad with its stored AirPower. F12 explicitly refills. The inactive character cannot tick movement, spend power or receive collision contacts during inspection. The gym build command sets the inspection compile symbol for this Development player only.
7. In Edit Mode, use the Movement Lab window to select a station and preset, then adjust its geometry, slope angles, node offset or tunnel dimensions. **Regenerate selected layout** reapplies the current fields. **Validate station pads** raycasts all starts and finishes. Undo restores edits. The window also offers three wind strength presets, individual wind settings, Overdrive tuning and inspection speeds. Changes here belong to the gym. Long and Extreme jump gaps are challenges. Try higher presets deliberately and record the sequence used.

## Movement and resource ownership

The opt-in motor callback runs after the existing planar/jump/gravity calculation and before the existing CharacterController move. It returns a velocity increment which enters the same simulation state and collision response. The lab adapter is installed only on the gym player. Direct repositioning is confined to intentional station changes and fall recovery. The orbit camera provides its own teleport reset. The original input asset, player tuning and progression content remain at baseline.

Wind samples oriented box coordinates in the motor step. It uses the strongest overlapping wind contribution, applies a per-effect speed limit, and reserves the remaining combined cap for Overdrive. Existing overspeed is preserved. Continuous wind can engage when movement reaches its minimum inside the volume. Entry impulse requires a valid crossing and delivers its configured amount once per visit, optionally over a ramp. The vertical weight controls rising/falling amplification; grounded movement excludes ground-stick velocity from direction and entry eligibility. A segment test across the preceding actual displacement catches complete crossings of thin volumes between samples. It can credit a transit on the following motor tick. Sling ownership suspends both effects and Overdrive requires a new press.

The gym's cloned gameplay template preserves production binding IDs and adds the dedicated Overdrive/navigation actions. Both Dash and Overdrive use the existing AirPower depletion/recovery policy. Overdrive converts only the paid part of a final partial tick to acceleration. Diagnostics use a bounded trajectory buffer and update text at a lower rate than the motor. The `MovementLab.ResolveInfluences` profiler marker covers the influence path, including its synchronous resource call.

## Redesign measurements

Unity 6000.6.2f1 exercised the production player with keyboard and gamepad input on the saved scene. The authored default is Short on F1 through F9 and Long on F10. All ten reached the finish while grounded on the destination deck. The finish test also ran the dry, continuous and impulse tracks independently. The Long combined route covered 187.67 m horizontally from spawn, rose 23.00 m, charged and traversed a Sling node, spent real AirPower and landed without Overdrive. The Short Sling run covered 88.73 m, rose 13.64 m and completed a post-launch landing. Its first design failed the landing test; the current deck extends 100 m past the node.

| Standard F7 track | Entry m/s | Highest inside m/s | First speed after exit m/s |
|---|---:|---:|---:|
| Dry | 7.50 | 7.50 | 7.50 |
| Continuous | 7.50 | 16.68 | 16.15 |
| Entry impulse | 7.50 | 13.50 | 11.28 |

Each wind type has independent Subtle, Standard and Strong controls. Continuous acceleration is 9 / 12 / 18 m/s² with 2.00 / 1.25 / 0.75 s ramps. Impulse adds 3 / 6 / 10 m/s once above its 1 / 3 / 6 m/s entry threshold. Separate deterministic tests cover the entry curve, no repeated burst inside a volume, rearm interval and stacking policy. The 30 / 60 / 120 simulated FPS wall probe peaked at 169.76 m/s and stopped approximately 399.45 m forward at the 400 m wall at all three rates. This measures straight-ahead collision on the tested wall.

The Extreme probes also finished Slopes (119.52 m horizontally from spawn, 18.34 m rise), angled Dash (136.39 / 6.00 m), Sling (119.73 / 16.64 m), Momentum (225.70 / 0 m), the dry Wind track (225.64 / 0 m), HighSpeed (528.35 / 0 m) and Overdrive (228.44 / 0 m). Two single-jump probes fell below the recovery plane before landing. Extreme Jump reached 27.89 m from spawn and 2.15 m above its starting height at the 12 m gap. Extreme Aerial Steering reached 26.56 m from spawn and 2.15 m above its starting height at the 10 m offset gap. Those distances include the approach. Both recovered to the correct pad.

The 60-second Extreme combined probe reached its grounded finish after 520.72 m horizontal excursion from spawn, 99.26 m maximum rise and a 170.00 m/s peak. It took the first available relay using a real charge and release. The earlier 40-second observation stopped short of the finish.

The Editor bake raycast each start and finish against its assigned collider. A file-ID inventory found all 1,551 original gym objects retained, with 40 new serialized objects for eight solid course sections. Only the experimental gym scene, lab input template and lab profile changed among existing serialized game assets. F1 and F2 are also bound on the progression fixture map; its guide/tool listeners are absent from the gym. F3 previously toggled diagnostics and now selects Dash.

Manual acceptance still includes camera comfort, flight sensitivity, readability in motion, controller ergonomics, turning across the incline, glancing collisions and whether the new wind levels feel appropriately distinct at the keyboard. Medium/Long custom edits need their own run when tuning distances, angles or power costs.

### Validation artifacts

| Run | Executed result |
|---|---|
| `gym-redesign-verified-EditMode` | 55/55, including wind presets and loaded scene/prefab references |
| `gym-redesign-checkpoint-PlayMode` | 32/32, including input navigation, rebinding, flight ownership, resource retention, all ten authored routes, grounded Extreme combined finish and barrier contacts |
| `gym-redesign-visual-verified-PlayMode` | 1/1 rendered test, fixed 1280×720, 1600×900 and 1920×1080 Game views with F10/F12 text and measured HUD bounds |
| `movement-lab-validated` | Windows x64 Development build succeeded, 0 errors, 28 warning occurrences, 402,926,550 bytes. Packaged only MovementGym.unity. Inspection rig was present in the compiled player assembly |
| `movement-lab-validated-rendered` | Player initialized and ran for 15 seconds with zero matched startup errors |
| Production serialized contracts | Zero failures against 7bfdfe5e |

The final build reported six occurrences across three pre-existing prototype obsolete-API call sites, fourteen FEEL managed-code analyzer warnings, two stripped Core debug shaders, three optional FEEL integration resource warnings and three vendor shader warnings. A prior build attempt could not start Unity's IL postprocessor. A fresh guarded retry succeeded. Logs, XML and screenshots are in `Logs/ProductionMigration`; the executable is `Builds/movement-lab-validated/Windrunner.exe`.

## Original gym evidence (before redesign)

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

## Original integrated verification, 17 November 2026

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
