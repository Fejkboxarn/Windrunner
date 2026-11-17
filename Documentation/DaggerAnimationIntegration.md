# Dagger Pack animation candidate

Use the [Animation Authoring Studio V2 guide](AnimationStudioV2.md) for current browser, mask, timing and draft-lane controls.


Local feature branch `codex/dagger-pack-animation` starts at the integrated acceptance checkpoint `3feecb35`. The production model is the existing Humanoid Character instance of `Assets/AirflowPrototype/VampGirl_V2/Vamgirl_Windrunner.fbx`. The desired male assassin is the animation direction. This pass used the installed character and Avatar.

## Current context-sensitive candidate

This section supersedes the earlier candidate mappings below. The clip inventory CSV lists each pack asset and its current production slots. Standing_Throw_1 is outside that pack. The production ground and action profiles in Assets/WindrunnerProduction/Animation/Generated are the authority for current assignments.

### Air Cast

| Phase | Stationary | Grounded moving / base fallback | Airborne |
| --- | --- | --- | --- |
| Charge, held until release or cancellation | Block_Loop, full body, 85% | Block_Loop, UpperBody mask, 60% | Same masked base; no dedicated aerial windup assigned |
| Release and recovery | DarkSorcerer_AnimSet/Animations/Humanoid/Inplace/Standing_Throw_1, full body, 100% | Dagger pack Combo_Attack_Air_Wave_01, UpperBody, 80% | Dagger pack Air_throw_01, full body, 80%, blended over the aerial base |

The stationary throw has an authored 2.167 s standing gesture. The aerial candidate has 95 Humanoid channels, zero net root translation and a 0.917 s duration. Air_throw_02 travels 3.586 source units and is a comparison candidate only. Both masks on the stationary assignments are deliberately empty. The release envelope uses the 0.917 s base clip at 1.1× (0.833 s), with 0.04 s entry and 0.14 s recovery. Each variant samples the same normalized phase, so the stationary gesture is compressed to that envelope. Charge loops at 1× with 0.09/0.14 s fades. The existing charge and release gameplay events retain sole authority for consumption, projectile emission and rewards. Recovery uses the authored release tail and a fade to the current base. No dedicated recovery clip or invented melee action was added.

An airborne motor sample selects Airborne immediately. Grounded speed at or below 0.15 m/s enters Stationary; once stationary, speed must exceed 0.45 m/s to leave. Landing above the entry threshold selects moving. A context change moves approximately 95% toward the new layer mixture in 0.12 s. It never requests the action again. A disabled, absent, legacy or incompatible override falls back to the base clip **and its mask and weight**. A missing base action stays unavailable. Cast release priority is 12, below Dash (15), Sling travel/charge (20), Sling impact (30) and flip (40). All other actions retain their authored base mappings. Imported events and root motion are disabled on the production Animator.

### Idle, running and turn choices

| Slot | Candidate assignment | Alternative for comparison |
| --- | --- | --- |
| Neutral / ready idle | Idle | Idle_Combat for dagger-ready breathing; idle_keep from DarkSorcerer Inplace for a different attitude. Audition first; personality variations are never triggered at random. |
| Forward / backward run | Run_Loop_F_0 / Run_Loop_B_180 | Matching Run_Combat_Loop clips |
| Right / left strafe | Run_Loop_F_R_90 / Run_Loop_F_L_90 | Matching combat variants |
| FR, BR, BL, FL run | Run_Loop_F_R_45, Run_Loop_B_R_45, Run_Loop_B_L_45, Run_Loop_F_L_45 | Matching combat diagonals |
| Starts and stops | Run_Start_ and Run_Stop_ with F_0, B_180, F_L_90, F_R_90 according to heading | Corresponding Run_Combat_Start_ and Run_Combat_Stop_ clips |
| Reversal / moving turn | Continuous directional blend | Run_Combat_Fast_Turn_L/R remain assigned for preview only |
| Stationary camera orbit | Motor retains facing; idle continues | Turn_Combat_90_L/R remain available for isolated preview |

The two forward run choices share 0.667 s duration and 2.82 source units of travel. The neutral forward start has -0.005 source units of vertical displacement against +0.285 for the combat start. This candidate auditions a less guarded posture and reduced vertical excursion without changing movement, gait cadence or turn ownership. Starts/stops still use short 0.22/0.26 s presentation envelopes. Foot contact, compressed gestures, torso separation and the protagonist's personality need footage. Melee attack families in the pack are available for isolated comparison only.

### Compare or change a slot

1. Open StartingWorld or Zone 01 and choose **Tools > Windrunner > Acceptance > Dagger clip preview**, or open it from Character Tuning. Click **Load clip on production model**. Use pack category and search, the project object picker, or the optional project Humanoid index for imported FBX sub-clips. The index runs in batches in Edit Mode; closing its foldout pauses indexing.
2. Pick candidate A, **Pin current clip as A**, then choose B. Drag the A/B weight, or use **Play A → B**. Try the assigned idle, run and fall bases and the UpperBody mask. Pause, scrub, orbit and vary playback speed to compare wrists, hips and feet. The isolated model uses the production Avatar and grip geometry.
3. Expand **Production assignments and draft tuning**, select a named destination and click **Audition slot** to load its effective clip, base, mask and weight. **Assign B to draft** stages a replacement. Base action settings govern playback speed, phase fades, interruption, priority and looping across its variants. Variant mask and influence are separate. Empty mask means full body. Use standard Unity Undo or **Revert drafts**. **Save drafts** commits profile edits explicitly in Edit Mode. Close/reload the window after external edits. Entering Play discards unsaved drafts.
4. To compare complete profiles without changing a scene, duplicate the animation profile and any child modules you intend to change. Assign the duplicate modules to that profile. Choose it in **Author / lane profile** before opening draft assignments. The named slot editor then edits the duplicate modules. Modules still shared by reference are shared edits, so duplicate each module you plan to change. Start the lane. This rebuilds the portable graph for the isolated Play session. Leave the field empty for the installed production profile. The preview graph is independent of gameplay.
5. Press **Insert** in the lane. Compare idle, partial-stick walking, full run, strafe, stops and reversals. Press **Shift+F5** for the Cast ledge, hold and release Cast toward the node, then strafe right off the edge and land. Repeat with the release initiated while moving and airborne. Compare the action clip, context, mask and speed in the overlay with the hips, wrists and feet. **F5** returns to the main deck for jump, Dash, Sling, Second Wind and Arsenal checks. Record 1080p/60 at normal speed, including the frames just before and after each release.

### Verification of this candidate

Unity 6000.6.2f1 passed 100/100 Edit Mode and 48/48 Play Mode cases, zero skipped. The serialized contract audit checked 67 contracts with zero failures. The Windows Development Build succeeded with zero errors and 21 warnings. Its rendered title-screen smoke check ran for 15 seconds with no matched errors. Raw results are under Logs/ProductionMigration in cast-authoring-final-EditMode.xml, cast-context-final-PlayMode.xml, cast-context-final-build.json and cast-context-final-rendered-player.json. The executable is Builds/cast-context-final/Windrunner.exe.

The two selected rendered Play Mode cases also passed (context-drop-PlayMode.xml and context-leg-parity-PlayMode.xml). A single real projectile release remained active while the character started moving and stepped off the ledge. The captured dominant layers were Standing_Throw_1 at 100%, Combo_Attack_Air_Wave_01 at 79%, and Air_throw_01 at 65% mid-transition. The render files are context-stationary-throw-639305284494909342.png, context-moving-throw-639305284496327490.png and context-airborne-throw-639305284497253911.png.

A matched locomotion-only rig provided the reference pose. Across both thighs and calves the maximum local joint difference was 50.88° during the stationary throw and 1.68° after 0.15 s of moving-context blending. Tests also exercised normalized phase continuity, landing, missing overrides, cancellation, priority, root stability, grip geometry and scene reload. The editor suite checked native and FBX library entries and saved and undid a contextual replacement in a disposable alternate profile. The screenshots and joint measurements cover sampled frames. Foot contact, throw readability, timing against the visible projectile, and the final idle/run preference remain open for recorded gameplay review.

## Pack audit

[DaggerClipInventory.csv](DaggerClipInventory.csv) indexes **all 578 standalone clips**, including path, internal clip name, GUID, duration in seconds, sample rate, loop flag, Humanoid muscle count, authored root displacement and rotation between endpoints, endpoint muscle differences, weapon coverage, use decision, production slot and playback settings. Filter the production_slots column for the 39 distinct clips serving 41 slots. Translation is in source RootT units. Rotation is the shortest angle between endpoint quaternions. The muscle seam column is the RMS difference across the 95 normalized muscle channels at the first and last keys. The separate root XYZ ranges measure excursion within the clip, including movement that returns to its start. Left/right arm columns give the largest muscle-channel range on each side. A zero endpoint difference cannot establish smooth velocity across a seam or absence of rotation during a clip.

The selected hold and release differ measurably. Block_Loop has a 0.046 vertical root range and left/right arm ranges of 0.062/0.096 over ten seconds. Combo_Attack_Air_Wave_01 has arm ranges of 0.960/1.504 over 0.917 s, with 0.379 vertical root range. Buff_Air has 1.134/1.321 arm ranges and 0.462 vertical root range. Those larger gestures are lower-weight upper-body overlays. Their source vertical motion does not become a second jump.

The pack contains 575 Generic animation-source FBXs and one Humanoid model FBX, `Model/9CG_Dagger.fbx`. The separately supplied `Animations/Humanoid` clips already carry the retargeted muscles. All 578 have the loop import flag and no AnimationEvents. 574 include tracks referencing both vendor weapon helpers. The four Target clips depict the receiving character. Windrunner mounts its own equipment to its hand grips. Vendor holder paths have no authority over those attachments. No reimport, converted duplicates, vendor controller, demo prefab or sample scene is required.

| Family | Clips | Candidate use |
| --- | ---: | --- |
| Idle | 46 | Combat idle is a breathing loop. Inspect the personality and offset variants separately. |
| Attack | 89 | A single aerial wave slice gives Cast a visible release. Air buff provides a short reserve-release accent. Combo chains, executions and victim choreography have no production combat trigger. |
| Walk / run | 148 / 92 | Eight combat directions per gait, directional start/stop and fast reversal. Shared cycle preserves phase as direction and speed change. |
| Jump | 42 | Vertical takeoff, shared airborne loop, vertical landing and combat double jump. |
| Dodge / roll | 50 / 34 | Preview-only evasions. Their displacement and recovery imply a different move from sustained Air Dash. |
| Hit | 57 | The block hold supplies the upper-body brace. Knockdowns, parries and reactions remain available for later authoring. |
| Turn | 20 | Standing combat quarter turns. The motor supplies actual heading changes. |

## Production mapping

The ground and action children of `Assets/WindrunnerProduction/Animation/Generated/WindrunnerAnimationProfile.asset` remain the character's configuration authority. Exact paths and settings for **every assigned slot** are in the CSV. All assignments use native Humanoid retargeting. The standing and airborne settings below describe presentation time only. Production held phases are interruptible. Cast release (35) yields to the traversal flip (40). Second Wind (5) yields to held Cast, Dash or Sling. An inactive or suppressed animation never blocks the ability.

| Gameplay state | Clip or family | Timing and reason |
| --- | --- | --- |
| Idle, walk and run | Idle_Combat, Walk_Combat_Loop and Run_Combat_Loop | Idle at 0.85×. Eight-way walk/run share the existing gait clock at 1×. Combat samples retain both dagger hands. |
| Starts, stops, pivot and turning | Run_Combat_Start/Stop, Run_Combat_Fast_Turn, Turn_Combat_90 | Four directional starts/stops. Sample complete one-shots over 0.22/0.26 s, pivots over 0.30 s and idle turns over 0.32 s. The source forward stop alone takes 1.50 s and travels about two source units. Review the compressed recovery at full speed. |
| Jump, apex, fall, land | Jump_Combat_Start_0, Jump_Combat_Loop_0, Jump_Combat_End_0 | 0.15 s takeoff and 0.28 s landing. Rise and fall sample the same 0.583 s loop on a continuous clock at 1× across the apex. |
| Double jump / reward flip | Double_Jump_Combat_0 | Existing full-body mapping. 1.35× with a 0.05 s entry. Review the flip against actual airtime. |
| Air Dash | Run_Combat_Fast_Loop | Arms and torso only, weight 0.65. A sustained sprint accent leaves grounded/airborne legs available. Dash stop fades out. |
| Cast charge / release | Block_Loop / Combo_Attack_Air_Wave_01 | Brace at weight 0.60. Release plays once at 2.4× and weight 0.80 (about 0.38 s). The wave is a proxy for projecting air; inspect direction and anticipation in motion. |
| Sling charge / travel / impact | Block_Loop / Custom / CustomFinish | Charge uses weight 0.75. Retain both established full-body traversal clips. Launch cancels the Sling layer and returns to the airborne pose. |
| Second Wind | Buff_Air | Successful activation requests a 0.52 s upper-body accent, priority 5, weight 0.80. A higher-priority action may suppress the gesture. The transfer, VFX and feedback still execute. |

The upper-body override includes torso, head, arms and fingers. It excludes root, legs and IK goals. The solver owns all world displacement and facing. Root-motion extraction remains disabled on the session Animator. Graph creation snapshots action timing and strength. After interruption the bridge consults live ability state once to restore a held loop; expired impact and other one-shots do not automatically replay. Missing or incompatible action slots continue to fall back to locomotion.

**Authored gaps.** There is no dedicated Windrunner Air Cast throw, Sling pull/launch, sustained airborne Dash, Second Wind discharge or equipment-inspection animation. The aerial wave and buff are candidate proxies. Air Dash entry/recovery use fades; Sling launch/recovery use the motor's airborne transition. No random idle, sheath/draw, attack-combo, reaction, or victory/flourish is triggered. Swapping already drawn dagger pairs in a paused menu does not imply drawing from a sheath. Preview those families before assigning them to future mechanics. No IK or procedural foot placement is part of this work.

## Preview, tune and replace

1. Open StartingWorld or Zone 01 and choose **Tools > Windrunner > Character Tuning > Dagger clip preview and gameplay lane**, or **Tools > Windrunner > Acceptance > Dagger clip preview**. Select a category and clip and click **Load clip on production model**. The window samples the imported character model with the scene Avatar in an isolated preview scene. Pause, scrub the time slider, restart, toggle Repeat, adjust speed/orbit and move **Idle → selected pose** in either direction. These controls affect only the preview. Preview shows full clip motion; the gameplay lane shows production masks, grips and transition policy.
2. Click **Play traversal and rewards lane**, then click Game. The lane is runtime-only and uses the production graph, character, feedback and equipped weapons. F5 returns to the deck. The window displays the active action. Use the bridge's **Report Animation Bridge Counters** context command for graph frames, requests, rejections and markers.
3. In Edit Mode select the scene player root, open Character Tuning, **Use selected player**, then choose the **Animation** category. Expand the ground profile, action list and semantic bindings. Edit clips, masks, peak weight, priorities, restart/interrupt rules, repeat policy, speed and fades in drafts. Ground phase durations are independent of gameplay timers. **Use Authored One Shot Durations** switches to clip-length-based timing. Keep the existing action IDs and numeric slots when replacing clips.
4. Use **Validate**, compare changed values and **Revert drafts** or Undo to discard experiments. **Duplicate character profiles** makes a character-local variant including child profiles. Use a disposable scene copy when changing scene bindings. Explicitly **Save changes to assets** in Edit Mode, then start a new Play session to rebuild the graph. The wildcard pack browser and its preview never save profiles. After a saved experiment, Undo the save or reapply the previously captured separate profile set in Edit Mode.

## Manual acceptance

1. In the lane, idle facing the camera, then orbit both sides. Check scale, forward orientation, confident stance, clearance of blades from forearms, hand closure and both grips, including after an Arsenal swap.
2. Walk, run and reverse in each cardinal and diagonal direction. Use a controller for intermediate gait speeds. Rapidly alternate forward/back and left/right. Check starts, stops, pivots, stationary turns, phase continuity, planted-foot slide and return to idle.
3. Jump from rest and a run. Double jump early and late; inspect rise, apex, fall and landing into a run. Compare actual body translation and camera timing with the accepted movement baseline.
4. Hold Dash on ground and in air, release during a reversal and chain into Cast. Cast at all three Air Nodes. Compare charge, early release, perfect release and cancellation; check natural wrist direction and freedom of the legs.
5. Charge Sling on the right-hand enemy, release early and in the perfect window, then follow travel, impact, launch and recovery. Press F6 while charging to lose the target, restore it and repeat. Pause during a charge and close the menu. Watch for frozen hands or repeated impact.
6. Equip Farseer, fill reserve with node returns or F8 test rewards, spend main energy with F7 and activate. Check the gesture while idle, moving and airborne. Try invalid activation at full main. Switch to Training Daggers, return to title and reload. Verify attachment position, pose ownership and return to locomotion.
7. Compare footage at normal speed and frame-step the shortest transitions. Record each as accepted, adjust, or missing content. Automated retarget and graph checks cannot establish blade alignment throughout a motion, perceived personality, natural joint limits, attractive loops or foot contact.

## Validation notes

The isolated Editor preview evaluated each assigned clip at 0%, 25%, 50%, 75% and 99%. The test bound the production Avatar, checked root translation and rotation, bounded both wrists relative to the rig’s Humanoid scale and checked isolation from the scene Animator. The live Cast test used actual mouse input on the production player, interrupted its held pose with a semantic flip, recovered the hold, released and returned to locomotion. Batch culling initially produced a zero wrist delta. The test sets AlwaysAnimate only on its disposable rig to make numerical sampling independent of Game-view repaints. The Arsenal flow uses actual equipment, resources and gamepad activation.

The first integration pass exposed two baseline assumptions. The character-profile isolation test used a hard-coded playback rate; it now verifies that editing a clone preserves the actual source rate. The old missing-Cast lifecycle test also exposed an equal-priority collision with the traversal flip after Cast acquired a real clip. Cast release was moved below the flip, and the test continues to require preservation of the active flip. Separate tests retain the missing/incompatible clip fallback.

Unity 6000.6.2f1 compiled the integrated assemblies. Local implementation checkpoint `9e3b20b0` passed the following checks; the raw reports are in `Logs/ProductionMigration`.

| Gate | Evidence | Result |
| --- | --- | --- |
| Edit Mode, including production Avatar preview, assignment, fallback and profile isolation | dagger-motion-candidate-EditMode.xml | 97/97, zero skipped |
| Play Mode, including ability execution, motion parity, graph recovery and scene lifecycle | dagger-motion-candidate-PlayMode.xml | 47/47, zero skipped |
| Rendered Cast charge/interruption/release and Arsenal/Second Wind | dagger-motion-candidate-cast-PlayMode.xml, dagger-motion-candidate-arsenal-PlayMode.xml | 1/1 each |
| Rendered Sling execution, phase reconstruction and Dash interruption | dagger-motion-candidate-traversal-PlayMode.xml | 3/3 |
| Serialized contract audit | Tools/verify_serialized_contracts.py | 67 source contracts, zero failures |
| Windows Development | dagger-animation-candidate-build.json | Succeeded, 0 errors, 21 warnings |
| Rendered Windows startup | dagger-animation-candidate-rendered-player.json | Initialized and survived 15 seconds, zero matched runtime errors |

The five rendered test executions repeat cases in the ordinary suites. The Windows candidate is `Builds/dagger-animation-candidate/Windrunner.exe`. Its report grew by 6,454,560 bytes relative to the session acceptance build. All final warning headlines occurred in the baseline. The earlier build of this feature reported 38 warnings, matching the baseline; the incremental final build reported 21. The count difference does not establish that compiler or package warnings were fixed. Rendered startup measures process initialization. In-game animation appearance remains subject to the checklist above.

## Recording the run, turn and grip review

1. In Unity choose **Tools > Windrunner > Acceptance > Play traversal and rewards lane**. Select a 1920×1080 Game view and record at 60 fps. F5 returns the character and camera to the origin. The yellow strip marks initial forward. Blue strips mark successive 45° headings. F6 toggles targets for an uncluttered run. This deck exists only during the isolated Play session.
2. Press **Insert** to show the recording overlay. Shift+Insert changes detail, Ctrl+Insert cycles corners and Alt+Insert cycles sizes. These commands are available in the Editor and Development Build. A configured production binding for Insert takes precedence. Direction badges show the configured keys (including alternatives), or stick directions when using a gamepad. The move vector includes analog magnitude. A dot or up arrow marks the press or release frame. A lit button stays held between them.
3. Start with a fixed camera. Run forward from rest, release, then do the same backward and to each side. Hold a diagonal, reverse directly to the opposite diagonal, then repeat at partial and full stick deflection. Next rotate the camera 45°, 90° and 180° while stationary and again while running. Keep a moment before and after each turn in the recording. Compare world yaw for camera-relative intent, actual travel and character facing. The clip and percentage come from the evaluated ground mixer. The action line includes the requested action and the strongest applied action layer, including its fade, mask and clip time. Movement cadence is cycles per second. The multiplier is the playback rate of the dominant directional clip.
4. Restore targets with F6. Repeat a short run with each dagger pair, both directions of strafe, jump, Dash, Cast hold/release and Sling charge/travel/impact. Include a close view of both hands in idle and a rotated Arsenal portrait. Use F7 to spend main energy and F8 to supply rewards for Farseer. See the preceding acceptance steps for interruption and recovery.
5. For an isolated comparison, stop Play, choose **Tools > Windrunner > Acceptance > Dagger clip preview**, select the production rig and the clip named in the recording. Compare the same part of its cycle with the gameplay frames. Record the dagger pair, binding/device and timecode for unnatural foot contact, wrist twist, clipping or abrupt blends.

### Technical basis for this revision

All eight run loops have the expected signed root travel in the clip inventory. They last 0.667 s; the walk loops last 1.05 s. Previously both used the run reference length, advancing a full walk cycle about 1.58 times as quickly as its authored length at the same playback multiplier. The production ground profile now interpolates authored gait frequencies while preserving continuous phase. At full run the existing rate is unchanged. The production profile normalizes against 8.5 m/s with zero yaw offset. The bridge supplies motor velocity in Animator-local space. PlayerMotor.UpdateFacing rotates the visual root toward camera-relative desired travel, and leaves it alone with no directional input. In the rendered diagonal drill the processed input was (0.71, 0.71), planar speed 7.5 m/s, and intent, travel and facing were all 45°. The dominant clip was Run_Combat_Loop_F_0 at 98%, running at 1.75 cycles/s (1.17×). A diagonal on the keyboard can therefore legitimately show forward on the rig. Orbiting the camera alone at rest should leave the rig stationary. Fixed-duration start/stop envelopes remain 0.22/0.26 s; their perceived compression needs footage.

Production uses directional locomotion through reversals and gameplay-controlled facing changes. Previously a single frame above 150°/s while moving could trigger a complete 180° clip; the stationary threshold was 100°/s for a 90° clip. The sampled RootQ heading unwraps to -90 degrees for Turn_Combat_90_L, +90 for its right counterpart, -180 for Run_Combat_Fast_Turn_L and +180 for its right counterpart. Their signs agree with the left/right slots. All four remain assigned for isolated preview. Other ground profiles retain their existing turn policy. Ground profile controls are Enable Turning, Suppress Reversal Pivots, Use Authored Gait Timing and Movement Playback Rate.

Both generated blades extend from hilt toward model +Y. The old sockets inherited original decorative prop rotation. The new socket aligns +Y across the proximal knuckles toward the index finger and constructs the perpendicular face axis separately for each hand. The hand bone owns subsequent motion. DaggerPair left/right offsets and Euler angles remain the final tuning controls and the portrait uses the same mounting operation. A rig without the required mapped fingers uses its existing prop orientation. Forward grip is the working art assumption. Comfort, silhouette, wrist clipping and foot contact throughout these poses still require recorded gameplay review.

### Recording candidate validation on Unity 6000.6.2f1

| Gate | Local evidence in Logs/ProductionMigration | Executed result |
| --- | --- | --- |
| Edit Mode | dagger-recording-EditMode.xml | 98/98, zero skips |
| Play Mode | dagger-recording-PlayMode.xml | 47/47, zero skips |
| Rendered input, locomotion, Cast and scene reload | dagger-recording-render-PlayMode.xml | 1/1 |
| Rendered equipment and portrait | animation-review-portrait-PlayMode.xml | 1/1 during integration |
| Serialized contracts | Tools/verify_serialized_contracts.py | 67 checked, zero failures |
| Windows Development Build | dagger-recording-build.json | Succeeded, 0 errors, 24 warnings |
| Rendered Windows startup | dagger-recording-rendered-player.json | 15 seconds, player initialized, zero matched errors |

The isolated Avatar test mounted both real dagger pairs and sampled each assigned clip at five phases. It checked blade direction toward the index side, hilt proximity to the palm, stable root and retargeted wrists. Gameplay tests exercised real remapped keyboard input, analog stick input, device removal, overlay visibility, scene replacement, action interruption and recovery. The selected rendered tests repeat full-suite cases. The diagonal capture uses U+D because the fixture rebinds forward to U. Render evidence is available as dagger-diagonal-recording-639305240592221512.png, dagger-cast-release-639305240615270396.png and dagger-arsenal-639305236586136246.png. The Windows executable is Builds/dagger-recording/Windrunner.exe. The startup check covers the title screen; gameplay captures are from the rendered Editor. Sustained 1080p/60 capture performance and visual acceptance remain unmeasured.

Compared with the previous 21-warning build, the final report also lists obsolete object searches in AirChimeTrial, PlayerWindFeedback and AutumnPostProcessController, and a canvas progress-bar shader power warning. These files were unchanged. No warning in the final report names the new recorder or modified bridge.
