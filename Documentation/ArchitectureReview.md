# Production architecture review

Source of truth: current source, enabled Batch1_Playground scene, owned prefab integrity tests, and `Tools/audit_architecture.py`. Static inventory covers 205 owned runtime files (AirflowPrototype, WindrunnerProduction, PortableAnimation). Vendor demos are not claimed production-audited. Matches identify review sites, not bugs by themselves.

| Boundary | Current authority / evidence | Remaining limitation |
|---|---|---|
| Application services | ProjectRoot owns registry/state lifetime; input/feedback hosts adapt Unity. Scene reload keeps the canonical root and hands scene UI to the persistent input service. | One application input host by design; explicit reader/service binding supports isolation but is not a multiplayer feature. |
| Player input | Reader binds early host once or accepts an explicit service; camera sensitivity/pause follows its reader. | Standalone legacy scenes keep fallback input. Successful interactive rebind persistence/gamepad hardware still needs acceptance. |
| Movement | MotorSimulation owns numerical state; one synchronous PlayerDisplacementAuthority executes motor/Sling/swim requests in original order. | Do not coalesce steps; broad water/Sling collision combinations need gameplay acceptance. |
| Abilities/resources | Cast/Dash sessions, Sling phase/charge/path models, explicit budget/payment policy; death reward uses configured player. | Sling facade still adapts physics and node lifecycle. This is intentional Unity integration, not a second simulation authority. |
| Sling presentation | Regular AirSlingPresentation owns camera/FOV, banking, flip, impact audio/VFX and hit-stop commands. | Calls remain synchronous to preserve damage/reward/event order. Node selection/availability still belongs to traversal/targeting. |
| Animation | Session owns graph and restores legacy writers; semantic events filtered by player; explicit feedback-service binding. | Empty action profile blocks authored action takeover and legacy retirement. No placeholder action mappings activated. |
| Feedback | Explicit emitter service binding, failure-isolated ordered dispatch, bounded audio/VFX/FEEL implementations. | No representative saturation/visual duplicate-playback acceptance; legacy presentation remains intentionally active where migration flags require it. |
| Time | Nested leases arbitrate pause/hit-stop; only UnityTimeAuthority writes engine clock in owned production code. | Future vendor time recipes must use this boundary. |
| Content | GUIDs, serialized contracts and scene bytes preserved except exact documented decorative repairs. | Three optional FEEL Resources have missing scripts in packaged content. Gameplay-scene integrity is not whole-package integrity. |
| Performance | Calibrated native frame allocations and two real DX11 rendered baselines. Reviewed sample procedural material teardown and cached Inspectors. | No shipping-player GPU/traversal/cloth/large-node profile or allocation call-stack attribution. No new FPS claim. |

The scan finds one CharacterController.Move boundary. Multiple pose writes target distinct root, presentation, camera and bone layers; the live graph test verifies legacy suspension/restoration. Legacy humanoid/arms/knife/throw/IK code is functional fallback and cannot be removed while action content is absent. Runtime scene discovery remains in legacy/UI/environment adapters; core configured camera paths are owner-scoped, but this does not establish multi-player compatibility for every prototype adapter.

Highest remaining risks are authored action integration, optional vendor Resources included with missing scripts, shader/importer upgrade warnings, broad collision/input-hardware acceptance, and unmeasured production stress cases. BuildWarnings.md classifies all original 21 build entries. Disabling compatibility paths or deleting vendor/prototype assets solely to reduce counts would not establish parity.

Architectural completion is not signed off. Required next work: authored action profile and acceptance of takeover; compatible vendor integration or reviewed packaging exclusion; targeted importer/shader validation; representative traversal/pool/player profiling; remaining compatibility-adapter composition review. None is implied complete by compilation or the scoped source inventory.
