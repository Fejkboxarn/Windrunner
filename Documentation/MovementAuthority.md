# Movement and pose authority (continuation audit)

Source scan: `Logs/ProductionMigration/writers-scan.txt`. This inventories owned runtime code and PortableAnimation; vendor effects remain outside the player-root authority.

| Writer | Target / responsibility | Schedule |
|---|---|---|
| PlayerMotor / PlayerMotorSimulation | Root CharacterController displacement; integrated planar/vertical velocity, jump/gravity/rewards; visualRoot world facing | Update, default order |
| AirSlingController | Path correction, final landing snap, planted impact correction; reports observed traversal velocity and requests motor impulses/resets | Update 300 |
| PlayerWaterSwim | Additive drag, fall cancellation and buoyancy/swim correction | Update 1000 |
| PlayerDisplacementAuthority | Single synchronous CharacterController.Move boundary used by the above requests | Exactly at each original request; no queue/coalescing |
| PlayerOrbitCamera | Camera position/rotation, look-ahead, recovery and roll | LateUpdate; Snap/configuration paths |
| PlayerMovementVisuals | presentationRoot local position/rotation and flipPivot local rotation; never the physics root by contract | LateUpdate; legacy procedures gated off in gameplay scene |
| PlayerHumanoidAnimatorDriver / PlayerCustomAnimationDriver | Animator parameters/layers/actions; explicitly disables root motion | Update; retained legacy fallback |
| PlayerRunningArmPoseController | Arm bone local rotations | LateUpdate 1050 |
| PlayerProceduralKnifeFlick / PlayerDualThrowPose | Chest/arm/hand rotations; knife visual local transforms | LateUpdate 1100 |
| PlayerGroundedFootIK / PortableAnimation FootGroundingSolver | Pelvis position, thigh/shin/foot rotations | Animation/IK pipeline; portable takeover suspends legacy writer |
| AirflowPortableAnimationBridge / WindrunnerAnimationSession | Owns Playables graph, suspends/restores configured legacy writers, disables physical root motion | LateUpdate 11000; manual takeover only |
| PortableAnimation IKGeometry / WorldLookAimRuntime / animation jobs | Bone rotations and animation stream; not player physics displacement | Graph evaluation/post-pose stages |

Other transforms are distinct actors/effects: AirSlingNodeWander moves/rotates node roots; AirSlingNodeHealth respawns nodes; AirSlingNodeVisualAnimator turns node visuals; AirNodeIdleHover offsets node visuals. AirPulseProjectile, return streams, knife/tracer/burst visuals move their own spawned transforms. Charge UI modifies graphic vertices and rings. PlayerDustFeedback writes particle emission position/velocity, not player velocity. PortableAnimation GroundingTestMotor is reusable testbed code, not installed player movement.

The gameplay composition gate verifies one motor and shared controller references for motor/Sling/swimming. There is intentionally still more than one movement *requester*: suppressing motor integration during Sling would alter established collision and reward behavior. The new authority executes all requests immediately with source/step diagnostics. Standalone legacy swimming without a motor owns its own driver for its controller. Serialized field names/types and script GUIDs are preserved.

Pose takeover/restoration is tested, but unassigned action mappings remain inactive. No claim is made that all procedural layers can be deleted. No Rigidbody velocity writer or OnAnimatorMove implementation was found in the owned player runtime; the installed graph is in-place.
