# Production architecture review

Source of truth: current source, enabled Batch1_Playground scene, owned prefab integrity tests, and `Tools/audit_architecture.py`. Static inventory covers 205 owned runtime files (AirflowPrototype, WindrunnerProduction, PortableAnimation). Vendor demos are not claimed production-audited. Matches identify review sites, not bugs by themselves.

| Boundary | Current authority / evidence | Remaining limitation |
|---|---|---|
| Application services | ProjectRoot owns registry/state lifetime; input/feedback hosts adapt Unity. Scene reload keeps the canonical root and hands scene UI to the persistent input service. | One application input host by design; explicit reader/service binding supports isolation but is not a multiplayer feature. |
| Player input | Reader binds early host once or accepts an explicit service; camera sensitivity/pause follows its reader. | Standalone legacy scenes keep fallback input. Successful interactive rebind persistence/gamepad hardware still needs acceptance. |
| Movement | MotorSimulation owns numerical state; one synchronous PlayerDisplacementAuthority executes motor/Sling/swim requests in original order. | Do not coalesce steps; broad water/Sling collision combinations need gameplay acceptance. |
| Abilities/resources | Cast/Dash sessions, Sling phase/charge/path models, explicit budget/payment policy; death reward uses configured player. | Sling facade still adapts physics and node lifecycle. This is intentional Unity integration, not a second simulation authority. |
| Sling presentation | Regular AirSlingPresentation owns camera/FOV, banking, flip, impact audio/VFX and hit-stop commands. | Calls remain synchronous to preserve damage/reward/event order. Node selection/availability still belongs to traversal/targeting. |
| Animation | Session owns graph and restores legacy writers; semantic events filtered by player; explicit feedback-service binding. | Configurable phase/priority/mask/speed/cancellation graph and compatible placeholder clips installed. Auto-start remains off pending visual acceptance; missing Cast/Dash/charge clips fall back safely. |
| Feedback | Explicit emitter service binding, failure-isolated ordered dispatch, bounded audio/VFX/FEEL implementations. | No representative saturation/visual duplicate-playback acceptance; legacy presentation remains intentionally active where migration flags require it. |
| Time | Nested leases arbitrate pause/hit-stop; only UnityTimeAuthority writes engine clock in owned production code. | Future vendor time recipes must use this boundary. |
| Content | GUIDs, serialized contracts and scene bytes preserved except exact documented decorative repairs. | Three optional FEEL Resources have missing scripts in packaged content. Gameplay-scene integrity is not whole-package integrity. |
| Performance | Calibrated native frame allocations and two real DX11 rendered baselines. Reviewed sample procedural material teardown and cached Inspectors. | No shipping-player GPU/traversal/cloth/large-node profile or allocation call-stack attribution. No new FPS claim. |

The scan finds one CharacterController.Move boundary. Multiple pose writes target distinct root, presentation, camera and bone layers; the live graph test verifies legacy suspension/restoration. Legacy humanoid/arms/knife/throw/IK code is functional fallback and cannot be removed until visual replacement parity is accepted. Runtime scene discovery remains in legacy/UI/environment adapters; core configured camera paths are owner-scoped, but this does not establish multi-player compatibility for every prototype adapter.

## Final compatibility review

CompatibilityDependencies.json records actual scene/prefab references and source callers for 14 reviewed adapters. Each is still serialized in Batch1_Playground and referenced by existing tooling/tests. Removal would require a deliberate scene migration, not just deleting source.

| Retained adapter group | Ownership and disposition |
|---|---|
| Cast/Sling charge UI, reticle, Cast presentation | Different renderers and migration generations; camera fallback now follows the configured ability, explicit overrides preserved. Old overlay retention is compatibility debt; duplicate appearance requires visual acceptance before deleting it. |
| Humanoid driver, foot IK and procedural arm/knife/throw writers | Legacy fallback is suspended/restored by the existing portable bridge. Portable session rejects a second portable owner. Character profile and action slots are configurable; visual retirement is pending acceptance, not a missing animation framework. |
| InputStartupRepair | Explicitly returns for UsesProductionInput; cannot re-enable actions intentionally paused by the service. Retained for standalone legacy installers, not an active second input authority. |
| AirPowerProgressBarUI | Typed resource reads; retained vendor material lease owns/disposes the instance. Reflection is confined to optional vendor widget compatibility. |
| AirNodeIdleHover | Node presentation selection feedback; its scene targeter fallback is single-player compatibility, not node selection authority. Configured references win. |
| Trial, autumn postprocess, wind feedback | Serialize a player reference; only missing-reference legacy paths use first-player search. Three obsolete API warnings remain to preserve ordering. They do not add another motor or camera transform authority. |
| Root, input and feedback Active hosts | Application lifetime compatibility bridges, with explicit non-owning reader/emitter/animation binding. Single local-player product scope; general multiplayer is not claimed. |

The complete owned-runtime scan covers 206 sources: one CharacterController.Move boundary, four engine-clock writes inside the time authority, eight application-host accesses, 20 scene-discovery matches, 90 pose-write matches and 29 material sites. Matches include conditional compatibility paths. This inventory is not a proof of obsolete code or an exhaustive vendor-source audit. No additional deletion has demonstrated replacement parity.

## Optional vendor Resources

- MM_6D_Shake.asset is referenced by FEEL's MMF_PlayerDemo scene. Runtime load sites are in OnAddFeedback under MM_CINEMACHINE / MM_CINEMACHINE3. Neither package is installed.
- MMDefaultPostProcessingProfile.asset is referenced only by MMDefaultPostProcessingVolume.prefab; the prefab has no serialized referencers in Assets. Its resource load is in editor-only automatic setup guarded by UNITY_EDITOR && MM_POSTPROCESSING. The old postprocessing package is absent; production uses URP.
- These three resources are packaged because they reside under Resources, so warnings remain real. They are unsupported optional templates, not required gameplay assets or critical architecture blockers. No vendor asset was deleted, recreated or silently repaired with an unrelated script. Installing those integrations later requires matching dependencies/templates.

## Completion boundary

The single-player ownership, gameplay extraction, interruption/resource composition, portable action configuration, presentation separation and regression infrastructure are implemented. No known unresolved critical engineering defect was found in the executed scope. This is an integrated architecture candidate, **not production-readiness or visual-parity sign-off**.

Noncritical engineering debt: legacy single-player discovery paths and installers, optional vendor packaging/API/importer/shader warnings, legacy presentation retirement after acceptance, and deeper player/GPU/allocation attribution. Missing authored Cast/Dash/Sling-charge clips are safe reserved slots; compatible placeholders cover available transitions. Manual acceptance must decide animation activation, legacy retirement, game feel, rendered shader/audio quality, controller hardware and target-platform performance. No critical work is considered completed merely by compiling; see exact tests and candidate evidence in ReleaseCandidate.md.
