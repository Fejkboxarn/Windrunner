# Dagger Pack animation candidate

Local feature branch `codex/dagger-pack-animation` starts at the integrated acceptance checkpoint `3feecb35`. The production model is the existing Humanoid Character instance of `Assets/AirflowPrototype/VampGirl_V2/Vamgirl_Windrunner.fbx`. The desired male assassin is the animation direction. This pass used the installed character and Avatar.

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
