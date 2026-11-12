# Character animation profiles and replacement workflow

The permanent configuration is the existing WindrunnerAnimationProfile, GroundMotionProfile and PortableCharacterActionProfile chain. There is no second animation framework. Gameplay owns movement and ability timing; animation samples movement and semantic events and never supplies gameplay rewards or root displacement.

## Select or override a character

1. Select the character's **AirflowPortableAnimationBridge** in the scene/Prefab Inspector.
2. Assign its **Profile**. The installed asset is `Assets/WindrunnerProduction/Animation/Generated/WindrunnerAnimationProfile.asset`.
3. For a different character, duplicate that asset and the ground/action profiles it references. Assign the duplicates on the character profile. This creates independent clip/tuning data without copying gameplay scripts. Confirm the Animator has a valid Humanoid Avatar.
4. Leave **Automatically Start Portable In Play Mode** off until the character passes the visual checklist. This preserves the functioning legacy pipeline. Manual takeover suspends the configured legacy humanoid/custom/arm/knife/throw/IK writers and restores their previous states on stop, disable or disposal. A second session cannot take an already-owned Animator.

## Replace clips and tune transitions

- Select the character profile and click **Inspect locomotion clips and transitions**. The Inspector labels pose slots by name, followed by directional walk/run clips, optional diagonal fallback, start/stop/pivot timings, blend speed, movement playback rate and authored one-shot settings. Replace the relevant Object field with a compatible Humanoid clip. Existing portable tuning tools edit this same asset.
- Click **Inspect action clips and phases** for action playback. Keep slot order stable: semantic mappings refer to those slots. Replace the Clip field in place; use Mask for upper-body-only actions, Playback Speed for sample rate, Blend In/Out Seconds for fades, Priority for arbitration, Interruptible for takeover policy, and Allow Same Action Restart for repeated triggers.
- **Loop Until Canceled** is for sustained phases. Give related phases the same Group (for example `Sling`). On the character profile, map the ending/cancel event to Cancel with that group. Play mappings may cancel a preceding group or slot before requesting the new phase. Cancellation never changes gameplay state.
- Higher/equal-priority requests may interrupt an interruptible action. Lower-priority requests and disallowed restarts are rejected. Outgoing masked layers fade toward locomotion while the incoming layer fades in. Fade durations are real animation-update seconds; playback speed changes clip time.

## Installed placeholders and missing content

| Slot / motion | Current source | Status / fallback |
|---|---|---|
| Eight-way idle/walk/run, jump/rise | Existing DarkSorcerer Humanoid in-place profile | Retained; no replacement of existing assignments |
| Forward/left/right start, stop, pivot, fall, landing | Existing compatible Batch19 Combat clips | Filled previously empty portable slots |
| 0 DoubleJumpFlip | Double_Jump_Combat_0 | Mapped to existing Flip event; this does **not** add a gameplay double jump |
| 1 SlingTravel | Custom (legacy slingAir) | Loop phase, ended by impact/launch/cancel mappings |
| 2 SlingImpact | CustomFinish (legacy slingImpact) | One-shot, superseded by higher-priority launch flip |
| 3 CastCharge | None | Reserved loop phase; locomotion continues |
| 4 CastRelease | None | Reserved one-shot; Cast still executes and ends its charge phase |
| 5 AirDash | None | Reserved loop phase; Dash start/stop events already wired |
| 6 SlingCharge | None | Reserved loop phase; Sling still charges normally |
| Backward start / turn-in-place | No exact compatible assignments | Existing motion/idle fallback; unrelated clips were not assigned |

The retained legacy profile also has running/idle landing variants and flourish selection/mirroring. Portable landing currently uses its configured single landing pose; visual parity with the legacy variant selection remains manual acceptance, not a claim of identical poses. Clip metadata is in AnimationClipInventory.json. The gameplay baseline rejects a second airborne jump; the DoubleJumpFlip action presents existing traversal rewards.

## Assign a previously missing action

1. Import the animation as **Humanoid**, with a compatible Avatar and valid duration. Use in-place motion; the player motor remains authoritative.
2. Open the action profile and assign the clip to the named reserved slot. Existing event mappings already target Cast/Dash/Sling phases; no C# edits are needed.
3. Configure looping, phase group, priority, mask, speed and fades. A sustained phase must have an end/cancel mapping. Do not reorder populated slots unless updating their mappings too.
4. Inspect the character profile's validation panel. Missing/non-Humanoid/legacy clips produce clear warnings and are skipped safely; invalid timings or slot indices block takeover. Mandatory missing locomotion configuration keeps legacy ownership.
5. Enter Play Mode and use the existing Production animation menu's manual portable takeover/stop commands. Test idle/movement, reversals, jump/fall/landing, reward flip, normal/perfect/canceled Sling, Cast and Dash; pause and disable/re-enable the bridge. Verify one pose authority, no root movement from animation, no stuck phase, correct fades and exact restoration when stopped.
6. Compare the result against the legacy baseline visually before enabling automatic startup for that character. Save the authored profile changes only after that comparison. Automated graph tests establish lifecycle/routing safety, not animation feel.

Missing clips are content work, not reasons to add placeholder gameplay logic or invent animations. Legacy components remain functional and serialized until replacement visual parity is accepted.
