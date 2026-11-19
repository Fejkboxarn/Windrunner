# Wind Relay & Dynamic Pursuit — authoring and acceptance guide

Worktree: `D:/Fork/Windrunner/Logs/QuestRelayPursuitWorktree`, branch `codex/quest-relay-pursuit`, baseline `5db46de2`. No animation, cinematic UI or performance branch was merged. All production assets/world content remain at the baseline. This guide describes the new activity code; exact test results are recorded separately in RelayPursuit.md.

## Open the demonstrations

1. Open this worktree with Unity **6000.6.2f1**, with no second Editor using it. Choose **Tools > Windrunner > Quest Authoring > Open Relay & Pursuit demonstration**. The scene is `Assets/WindrunnerActivities/Demonstration/RelayPursuitDemo.unity`.
2. Enter Play Mode. This copied production-player scene uses an unsaved memory session, not an existing playthrough. Its four labelled lanes are isolated above the original template scenery; it is not Zone 01 and does not edit any terrain.
3. Approach a **START** pedestal, aim at it and press the existing **Interact** binding. The installed interaction controller handles input. The guide's GUI buttons are optional debugging conveniences when the cursor is unlocked, not the required gameplay workflow.
4. Relay lane: Cast at the available cyan node. Subsequent nodes become available in order; traverse toward the next node using the normal movement/abilities. Each window is 25 seconds, yellow in the final five. Let one expire, return to START and retry. Completion advances the optional demonstration quest, not relationship rank.
5. Ground lane: start the moving target. Touch it to capture or use Interact nearby. To provoke a near miss, approach and pass about 2–3 m beside it, outside its 1.5 m capture radius but inside its 4 m near-miss radius. It must detect approach followed by retreat, anticipate/decelerate, then reverse. Standing beside it is not a near miss.
6. Air lane: start the target and use actual Air Cast to intercept it. Close proximity alone does not capture it. Try a missed interception and observe reversal, then a real hit and quest completion.
7. Hybrid lane: observe the ground segment, authored ascent, aerial segment and ground return. It uses the same pursuit session and route representation. Repeat pause/resume, failure/retry and capture.
8. Reload the demonstration scene for a fresh unsaved quest session. The start stations retry failed/canceled attempts; completed replayable pursuits can start again. First-clear rewards remain controlled by the existing progression ledger. Manual fun/fairness, difficulty and authored presentation acceptance are still required.

## Create a Wind Relay

1. Open **Tools > Windrunner > Quest Authoring > Relay & Pursuit Studio**. Click **New Wind Relay**, saving into a project-owned folder. Enter a stable challenge ID and optional region ID.
2. Choose Sequential, Unordered or Graph. Add relays with unique stable IDs. Sequential uses list order; Unordered permits every node initially; Graph derives roots and incoming prerequisites from explicit destination links.
3. For branching A→B→D / A→C→D, make D use **Any** incoming branch to accept either route. Make B/C non-required if either branch should suffice; keep D required. For both branches, make B/C required and D's incoming rule **All**. Cyclic graphs are rejected rather than introducing implicit repeat semantics.
4. Choose activation flags per node: Cast, Sling, Proximity, Interact and/or External. Proximity/Interact use the configured radius; Cast/Sling retain their production targeting/projectile/traversal rules. A relay can additionally require a grounded or airborne player.
5. Configure availability delay, active duration (zero disables expiry), warning threshold, overall timer and whether that timer starts on entry or first activation. Configure full/partial reset, retry limit (-1 unlimited) and optional per-node reset override. Partial progress survives retry in the same session, not a save/load.
6. Optional extra main AirPower uses the existing `AirPower.Restore` boundary after successful activation. It does not manufacture eligible overflow rewards or charge Second Wind. Existing AirNode Cast rewards are unchanged and separate; avoid accidental double incentives when authoring a bonus. The default extra reward is zero.
7. **Validate**, then **Save draft**. Draft edits/preview do not mutate the profile until Save. Assign the explicit player binding and optional existing relay prefab, then **Create bound scene objects**. Creation uses the saved profile. Bind actual AirNode/AirSlingNode components for enabled ability channels; no fake projectile or Sling simulation is created.
8. Position the nodes manually. Select the host to see connections and radii. Bind the player's motor if using grounded/airborne conditions and AirPower if using an extra bonus. Save the scene explicitly. Use a start interaction or UnityEvent calling `BeginActivity`; `Cancel` ends an attempt. The demonstration includes reusable `ActivityStartInteraction` pedestals.
9. Assign optional renderer, AudioSource/clips and visual-state UnityEvents per RelayInteraction. State colors use a MaterialPropertyBlock, never edit shared materials. Audio clips are optional; no audio or VFX asset was invented. Countdown is exposed as `SecondsRemaining`; hook it into the desired presentation without making UI authoritative.

## Create a pursuit

1. Create a Pursuit profile in the Studio. Select a target prefab or use a placeholder target while blocking out. Set Ground, Air or Hybrid, endpoint behavior (Stop/Loop/PingPong), direction and starting route distance.
2. Add waypoints in local metres. Select a bound PursuitHost to drag Scene position and tangent handles. Outgoing curved segments use the start point's outgoing tangent and the next point's incoming tangent. The runtime bakes arc lengths once; moving through the cached polyline is distance-based. Zero-length spans are skipped. Do not use zero-length points as required checkpoint events.
3. Set base/min/max speed, acceleration, deceleration and independent turn speed. A per-point speed override replaces base speed for its outgoing segment. Optional distance-response curves multiply that speed before min/max clamping. Pauses apply when arriving at waypoints. For Hybrid, airborne flags describe outgoing segments. A waypoint touching a ground segment is grounded at the shared boundary. Use a separate interior airborne waypoint to author the flight apex, then a ground landing boundary and a following ground segment. Shared endpoints keep transitions continuous.
4. Configure capture methods, radius and per-segment capture eligibility. Cast and Sling require explicitly assigned real production target components and listen to the bound player's authoritative callbacks. Interact uses the installed input/interaction service. The pursuit root must have unit scale, and the target must be a separate transform with no dynamic Rigidbody or Animator root-motion position authority.
5. Ground queries bake a terrain-projected route at attempt start, with distances measured after projection. They use an explicit mask, height and offset. The adapter excludes its target and player colliders; missing ground or a saturated query stops the attempt with an error rather than silently moving off valid terrain. Validate the entire route in Play Mode, especially cliffs and bridges; restart the attempt to rebuild after terrain changes. Sampling is approximate, so inspect tightly varying ground. Air-only routes retain authored elevation.
6. Configure near-miss radius, minimum closing speed, retreat threshold, minimum approach duration, probability, delay, cooldown and maximum count. Detection uses relative swept motion plus attempt tracking/hysteresis. Capture wins over near miss. Large discontinuities above the configured teleport distance do not trigger a capture or miss; keep that threshold above legitimate frame displacement and below expected teleports.
7. Choose None, Reverse, Burst or ReverseAndBurst. Reaction anticipation decelerates before reversing; turn orientation is separately rate-limited. Burst speed/acceleration/duration remain bounded by maximum speed. A reversal does not invent a new route or free-roaming AI.
8. Add optional waypoint event IDs, pauses, capture eligibility and reversal eligibility. Events can drive presentation; checkpoint objective credit uses the same durable progression fact service. Do not give a target another movement script that competes with the host.
9. Save the draft, create the bound scene objects and assign actual target/caster/sling/actor references. Save the scene explicitly. `BeginActivity` and `Cancel` are UnityEvent-friendly entry points.

## Preview and Undo

The Studio preview is a separate PreviewRenderUtility scene. It copies static rendering meshes/material references without instantiating target gameplay scripts; skinned-only targets use a sphere fallback. It offers play/pause, distance scrub, simulated speed, reverse and reset. Preview shows authored local coordinates, not production terrain raycasts or quest state. Scene handles edit the profile with Undo; press Save profile explicitly. Closing the window disposes preview resources. Preview is not a substitute for Play Mode ground/capture tests. No mouse-driven desktop validation was performed.

## Link objectives and persistence

- Register the challenge in the selected ProgressionCatalog using the Studio button, then save that catalog explicitly through existing authoring tools. Choose replayability, prerequisites and first-clear rewards in the existing progression authoring workflow.
- Whole activity: existing fact evaluator, `TraversalCompleted` and the challenge ID (or match the resultKind you deliberately configured). No parallel quest authority is installed.
- Individual relay/checkpoint: `Interaction`, definition ID `challengeId/nodeId` or `challengeId/eventId`. Use unique-target counting if repeat visits should not increment a count. Objectives follow existing acceptance/credit rules; challenge facts are not a new blanket retroactive-credit policy.
- Optional activity completion never grants relationship ranks by itself. For a story chapter, explicitly bind its objective and keep the existing story turn-in/chapter rules. `requiredActiveQuest` can gate a host; cancellation/completion stops its attempt.
- Critical completion and per-node facts use existing synchronous transactions, checksums/backups and slot ownership. A write failure leaves capture/activation retryable. Node fact and whole-challenge completion are distinct commands; a successfully committed node fact remains durable even if the subsequent completion write fails.
- Completed non-replayable activities remain unavailable after reload. Mid-attempt route position, timers, reactions, retry counts and partial relay progress are intentionally **restart-on-load**, with no new save schema. Replayable activities use fresh attempts; first-clear reward entitlement is not duplicated.
- Moving objective anchors are exposed through `PursuitHost.ObjectiveAnchor` and `WindRelayHost.CopyAvailableAnchors`. The latter returns all available branches. This baseline does not contain the latest cinematic Wayfinder, so no unrelated UI branch or center-screen crosshair was imported.

## Manual acceptance still needed

Check normal/perfect Cast, Sling charge/traverse/impact/launch, fast diagonal interception, reversal readability, very slow/fast route variants, pause/cinematic overlap, target disable/destruction, scene reload, and failed-attempt retry. Review target clips/VFX/audio and ground-route aesthetics with actual authored assets. Automated correctness does not establish that the chase is fun or fair. Scene handles, Undo, draft save/discard and preview cleanup have automated coverage where stated; interactive mouse usability remains manual by user instruction.

## Repeat command-line validation

Close Unity first and run from this isolated worktree. Use fresh labels so evidence is not overwritten:

```powershell
./Tools/Run-UnityTests.ps1 -Platform EditMode -Label activity-review-01 -Rendered
./Tools/Run-UnityTests.ps1 -Platform PlayMode -Label activity-review-01 -Rendered
python ./Tools/verify_serialized_contracts.py
```

The suite wrapper excludes the separate `Profiling` category by default. For focused checks, use `-Filter RelayPursuitRuleTests`, `-Filter ActivityAbilityIntegrationTests`, or `-Filter ActivityDemoTests`. `ActivityDemoTests` writes camera-rendered evidence to `Logs/RelayPursuit`; it uses disposable memory progression and a virtual input device, never a real save slot. These camera images do not capture IMGUI overlays. The existing save regression suite supplies file-store coverage; activity-specific failure/idempotency tests use an injected store.

For a Windows Development candidate, guard with `Tools/Assert-UnityEditorClosed.ps1`, then use the matching Editor's batch-mode execute method `Windrunner.Activities.Editor.ActivityDemoBuilder.BuildWindowsCandidate` with `-windrunnerActivityBuildLabel <fresh-label>`. It explicitly includes the demonstration, Main Menu, Zone 01 and StartingWorld in the build without changing EditorBuildSettings. Run `Tools/Smoke-UnityCandidate.ps1 -Label <fresh-label> -Rendered -Seconds 15` for a bounded graphics-enabled startup check. That check is not pixel, frame-budget or graceful-shutdown validation. Keep manual authoring, chase readability and gameplay-feel acceptance separate from these results.
