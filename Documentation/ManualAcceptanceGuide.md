# RC3 manual acceptance: practical Unity guide

Status: the user reports testing 8 of 11 areas without significant movement/ability/camera/animation differences. The identities of those eight and the remaining three were not supplied. Do not infer individual passes below. Waterfall audio is a reported regression; its fix still needs an audible retest. This guide expands the seven grouped ReleaseCandidate bullets into eleven practical areas; it does not invent a new acceptance result.

## Preparation and controls

1. Open this project with Unity **6000.6.2f1**. In Project, open `Assets/AirflowPrototype/Generated/Batch1_Playground.unity`. Save your own work before testing. Open **Window > General > Console** and clear it.
2. Click **Play**, then click the **Game** view. Default controls: WASD/arrows move, mouse looks, Space jumps/swims up, Left Ctrl swims down, Left Shift dashes, left mouse charges/releases Cast, right mouse charges/releases Sling, Escape opens/closes pause. Gamepad: left/right sticks, south button jump, right trigger Dash, left trigger Cast, right shoulder Sling, east button swim down, Start pause. Your saved rebindings may differ: inspect the pause menu first.
3. **Tools > Windrunner > Acceptance** provides selection, audio reporting, live portable takeover/restoration and gameplay reload commands. They never save assets or scene changes. Playback/reload commands do nothing outside Play Mode. Reload resets transient progress; use only while intentionally testing this scene. Stop Play to discard runtime component/transform changes. Do not edit ScriptableObject assets to perform these tests: their edits can persist during Play Mode.
4. To inspect the running player, press Escape, use **Select player**, then inspect its components. In Scene view press F to frame the selected object. Scene-view navigation does **not** move the Game camera/AudioListener. Click Game and resume to continue playing.
5. Record each area as **not tested / pass / fail**, with checkpoint, device, steps and observations. Do not apply this guide's suggested runtime edits to saved scenes. Do not start batch-mode tests while the Editor is open.

## 1. Ground movement, jumping and airborne momentum

- **Where/actions:** Play the playground, find open ground. Hold W until full speed, release, then repeat and press S abruptly. Strafe while looking around. Jump from rest and while running. Run off an edge; try Space just before leaving and just after leaving. Press Space shortly before landing, and again while already airborne.
- **Expected:** familiar acceleration/friction/reversal and air momentum; jump/buffer/coyote timing matches the prototype. There is no newly added free second airborne jump. The existing reward flip is a separate animation/event.
- **Failure:** instant unrelated velocity reset, unexpected extra jump, sticky controls, changed jump height, sinking or snapping on landing, repeated Console errors.
- **Setup:** none. Use a safe ledge and reload after falling. Automated rule/parity tests supplement this; they do not certify feel. Compare with your remembered/recorded prototype behavior, not simultaneous Editors on this project.

## 2. Manual Dash, depletion and reward recovery

- **Where/actions:** On open ground/air, use Left Shift; watch the AirPower meter. Repeat/hold as the configured Dash permits until depleted. Hit/traverse reward nodes using the existing abilities, then try Dash again. Pause during a Dash and resume.
- **Expected:** same direction, speed and end behavior as RC3; depletion prevents unaffordable activation, rewards restore availability, pause prevents movement and resume does not create a second Dash.
- **Failure:** free unlimited Dash, rewards affecting the wrong player/resource, persistent boosted speed after interruption, resource charged twice.
- **Setup:** reset via the Play Mode reload menu if nodes/resources are exhausted. No tuning changes needed.

## 3. Air Sling charge, perfect timing, traversal and cancellation

- **Where/actions:** Aim at a Sling node until its UI appears. Hold right mouse, release once early and once in the displayed perfect window. Follow the curved traversal through impact/launch. Repeat while moving, and with line of sight obstructed. Pause during charge and during the impact hold; resume.
- **Expected:** original target selection, charge/perfect feedback, path, impact, launch, reward and camera behavior. A canceled/invalid target leaves ordinary movement available; cancellation does not secretly award a launch/reward.
- **Failure:** stranded external velocity, stuck charge/camera bank, duplicate impact/reward, altered perfect window or path, firing a paid release after device loss.
- **Special setup for target-loss:** in Play Mode press Escape, select the targeted Sling node in Hierarchy, disable its GameObject checkbox, return to Game and resume. Do this once while charging and once during traversal; reload to restore it. Disabling an object during pause is a controlled interruption check, not a normal gameplay sequence. Do not save it.

## 4. Air Cast execution, perfect release and target loss

- **Where/actions:** Aim at an Air Node; hold left mouse and release early, normally, then in the perfect timing window. Observe projectile travel/hit and resource/reward UI. Repeat with a blocked view or no valid target. Pause while charging, then resume/release.
- **Expected:** familiar charge/target-lock timing, projectile trajectory, perfect feedback and one reward per successful interaction. Invalid/canceled execution recovers without a phantom projectile.
- **Failure:** double projectiles/rewards, stuck charge visuals, input firing on pause, or projectile behavior differing from the prototype.
- **Setup:** none for normal cases. For target loss, temporarily disable the selected Air Node in Play Mode as in test 3; reload afterward.

## 5. Water traversal and ability overlap

- **Where/actions:** Use Scene view to locate the authored river/water area, then move the player there through Game view. Enter slowly, then at running speed. Swim with movement, Space and Ctrl. Leave onto shore; jump/dash near the boundary. Try available Cast/Sling interactions from water, then pause/resume while swimming.
- **Expected:** established entry lift/buoyancy/drag and exit grace, no lingering swimming state on shore, no movement during pause. Ability-water overlap must retain the original displacement ordering and feel.
- **Failure:** endless upward drift, repeated entry boosts, sinking, frozen controls, abrupt momentum changes or exceptions.
- **Setup:** authored water meshes are required, already in this scene. Do not substitute an arbitrary plane and assume it is water. Automated fixtures cover removal/recovery; the real shoreline and collision combinations remain manual.

## 6. Camera movement, bank, FOV and sensitivity

- **Where/actions:** Mouse-look while idle, moving, dashing, Slinging and landing. Adjust sensitivity in the pause UI, resume, and compare. Move near walls/geometry. Turn rapidly and stop. Repeat with a controller if available.
- **Expected:** existing orbit, collision handling, speed/FOV response and bank; transient effects return to their ordinary state. Sensitivity changes affect the intended view, not movement physics.
- **Failure:** permanent FOV/bank offset, clipping introduced by the fix, view stealing, camera motion while the pause UI is active or sudden sensitivity resets.
- **Setup:** none; note your original sensitivity and restore it through the UI after testing.

## 7. Pause, hit-stop and impact-hold overlap

- **Where/actions:** Trigger a perfect Cast/Sling interaction that visibly slows/freezes time, press Escape during the effect, wait two real seconds, then resume. Repeat during Sling impact hold and rapid pause/resume.
- **Expected:** the menu owns the pause until dismissed; an expiring hit-stop cannot unpause gameplay. After all interruptions finish, normal speed and controls return exactly once, with no accumulating slow motion.
- **Failure:** motion under the menu, clock stuck slow/zero, unintended release on resume, or cursor locked while navigating the menu.
- **Setup:** timing the menu during a short effect may take several attempts. Use Console/Test Runner evidence from `TimeInterruptionTests` for deterministic overlap; a missed timing attempt is not a pass. No global Time Scale edits are needed.

## 8. Rebinding and physical device reconnection

- **Where/actions:** Open pause, locate the action binding rows, start rebinding Jump to an unused key, confirm and try it. Cancel a second rebind and verify the previous binding remains. Stop/restart Play to check saved overrides. With a real gamepad, charge Sling/Cast, disconnect it during charge/perfect timing, wait, reconnect, and try again; also disconnect while paused.
- **Expected:** cancellation preserves the old binding, confirmed overrides persist as designed, menu remains usable, device loss cancels charging without a paid release/projectile, reconnect restores fresh input without replaying stale held buttons.
- **Failure:** impossible-to-exit rebind/menu, accidental ability fire on disconnect, permanent dead input or missing persistence.
- **Setup:** real controller/USB or wireless disconnect is required for hardware acceptance. Synthetic Input System tests do not pass this hardware check. Record original bindings and restore using the rebinding UI. No controller available means **not tested** for that portion.

## 9. Portable animation takeover, actions and legacy restoration

- **Where/actions:** In Play Mode use **Tools > Windrunner > Acceptance > Select player**. Find **AirflowPortableAnimationBridge** in Inspector. Keep automatic startup off. Use **Start portable animation (Play Mode only)**; check Console for successful ownership. Run/stop/reverse, jump/fall/land, trigger Sling travel/impact/launch and a reward flip. Try Cast/Dash too. Use **Restore legacy animation (Play Mode only)** and repeat movement. Start portable again, then uncheck/recheck the bridge component's enabled box at runtime.
- **Expected:** one pose pipeline at a time; configured legacy writers suspend during takeover and restore on stop/disable. No root-motion displacement is introduced. Existing compatible clips play; missing Cast/Dash/Sling-charge slots safely fall back instead of inventing animations. Re-enabling with automatic startup off does not silently opt into takeover; start manually again.
- **Failure:** doubled/fighting poses, frozen Animator, permanent loss of legacy animation, wrong landing/action pose or gameplay rewards driven by animation. Visual differences in placeholders are acceptance observations, not automatically acceptable parity.
- **Setup:** valid Humanoid player already configured. See `AnimationWorkflow.md`. Do not enable automatic takeover permanently or delete legacy components on a passing compile. Clip/mask replacement needs separate authored-content approval; this test must not mutate profile assets.

## 10. Waterfall/audio, FEEL, art and UI lifecycle

- **Where/actions:** Use **Select waterfall sources**, then F in Scene view to locate them. Select `Audio/Water/Waterfall` in Hierarchy. In Inspector confirm Water_Waterfall_Big, Spatial Blend 1, volume .004, custom rolloff, min approximately 42.48, max 125, Waterfall mixer group. The four `Waterfall_Splashes_*` sources under Environment have **no clip**. Move the actual player/camera near the falls, then well beyond 125 world units; listen at the same system volume. Trigger normal/perfect abilities and check audible/visible feedback and target/resource UI. Use **Reload gameplay scene (Play Mode only)** twice, then repeat the listening check.
- **Expected:** local waterfall ambience with its original approximate near loudness and much quieter distance response; no four loud direct-output loops. The original custom curve has a small nonzero tail, so mathematical absolute silence beyond max distance is not the contract. Other ambient sounds, FEEL and gameplay audio remain unchanged. One active AudioListener, no growing volume/duplicate UI after reload. **Report loaded audio sources** writes a read-only snapshot to `Logs/ProductionMigration/waterfall-audio-audit.json`.
- **Failure:** scene-wide loud roar, changes to unrelated sounds, missing near ambience, duplicate listeners/sources, repeated feedback, magenta/incorrect shaders or accumulating UI rows. Check the dagger particle effect and waterfalls from multiple viewing angles.
- **Setup:** speakers/headphones, Game-view audio unmuted and reasonable fixed system volume. Audio Profiler (**Window > Analysis > Profiler**, Audio module) can show voices, but cannot certify perceived loudness. Scene-view audio preview should be off to avoid confusing it with the Game listener. Reload resets transient gameplay. No mixer/volume edits are needed.

## 11. Rendered Windows performance and sustained reload

- **Where/actions:** Use the newly fixed project in Editor for initial comparison; the old RC3 executable does not contain later fixes. Build a fresh Windows candidate when evaluating the shipping path. In Unity open **Window > Analysis > Profiler**, attach to Play Mode or a Development Build, enable CPU Usage, Memory, Rendering and Audio, warm up, then record movement/reversals/jumps, repeated Dash/Sling/Cast, water and scene reload. Use the same resolution/quality/device and route for two captures. Compare steady-state memory after several reloads, not just startup peaks.
- **Expected:** no new reproducible stalls attributable to this targeted fix, no unbounded growth in waterfall voices, listeners or retained scene objects. Existing measurements are baselines, not a promised FPS budget.
- **Failure:** reproducible spikes/new sustained regressions, growing retained source/UI/material counts or memory that keeps rising after repeated equivalent cycles. Separate initial shader/import work, Editor overhead and deep-profiling overhead from gameplay.
- **Setup:** representative target hardware and a rendered player are required for shipping performance acceptance; GPU timing availability depends on platform. Use a fresh output directory/build label. The CLI suites and two existing scenario captures supplement this test but do not establish your hardware's frame/GPU budget. Missing target hardware or absent GPU counters are **not tested**, not passes.

## Automated checks and reporting

With Unity open, use **Window > General > Test Runner**: PlayMode, select `WaterfallAudioRegressionTests`, Run Selected. It checks the actual scene's single configured ambience owner, spatial/mixer/volume/position/curve settings, one listener and unload/reload destruction. Run the full EditMode and PlayMode suites for broader regression evidence. It does not measure what you hear. The CLI harness checks for actual Editor processes and distinguishes Unity Hub's `unity.exe serve` helper; never launch a second Editor on this project.

For a failure record: area number, build/checkpoint, scene, exact actions, expected/actual result, whether repeatable, Console error text and (if useful) short capture. Stop Play before making unrelated edits. The architecture remains frozen while regression fixes are evaluated.
