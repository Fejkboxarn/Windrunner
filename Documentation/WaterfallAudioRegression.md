# RC3 waterfall audio regression

## Cause and evidence

The reference-repair checkpoint `fc550cfc` substituted Water_Waterfall_Small.wav for missing GUID b10f4a34ddd54cb478fe6e57312c97d1 on Waterfall_Splashes_1 and Waterfall_Splashes_2. That GUID has no asset in this repository. The previously silent decorative sources became four audible loops through two nested Waterfall_Small instances in the gameplay environment.

This was an incorrect content repair, not a movement/animation change or a persistent-service regression. A missing reference should not have been treated as evidence that another emitter was intended to play. The original ambience was already present and working.

The resolved Unity audit found:

| Sources | Clip | Volume | Spatial Blend | Distance / rolloff | Routing |
|---|---|---:|---:|---|---|
| Audio/Water/Waterfall (1) | Water_Waterfall_Big | .004 | 1 | min 42.479553, max 125, authored custom curve | SceneMixer / Waterfall |
| Environment (1)/Environment/Waterfall_Small{, (1)}/Waterfall_Splashes_{1,2} (4) | newly substituted Water_Waterfall_Small | 1 | 1 | min 5, max 37.177353, logarithmic | direct, no mixer group |

The four loops bypassed the tuned ambience routing and were much louder at source. They were already 3D: changing Spatial Blend would not fix the root cause. Logarithmic max distance is not a hard mute radius; these extra sources contributed distant tails. Their world positions were around (38–43, 90–119, 72–93), on the waterfall geometry. Inherited scales were approximately 3.1–4.0; no transform/parent changes caused the regression. The actual ambience is at (48.7, 90.354713, 74.95), with its original parent/scale.

One active AudioListener was found on Airflow Camera. Scene, original Audio.prefab and SceneMixer are byte-equivalent after canonical newline normalization to 5P (`7bfdfe5e`). Audit JSON records hashes, source positions/scales and settings. The replaced small clip is referenced by the two splash prefabs, not the gameplay feedback catalog or audio service. Existing global/player feedback implementations were not changed.

## Targeted correction

Clear only those two decorative prefab clip references to a valid None reference. This restores their baseline silence without reintroducing a broken GUID. Keep every AudioSource component, prefab GUID, transform, volume, spatial setting and rolloff intact. Preserve the original single ambience source, its clip/mixer/volume and exact attenuation curve, including its small .037506104 endpoint tail. No arbitrary volume reduction, new global ambience service, runtime muting logic, or changes to creek/river/FEEL/gameplay audio.

## Regression validation

`waterfall-before-PlayMode` reproduced the defect: expected one configured audible waterfall source, observed five (0/1 passed). `waterfall-fixed-PlayMode` passed all 20 tests after correction. The dedicated test checks the loaded scene's five source components but only one enabled/unmuted source with a clip, exact authored spatial/routing/volume/position/curve settings, one listener, and unload/reload destruction of every old waterfall source. It searches by source name and clip name to catch feedback/pool copies too. Final runs: waterfall-final-EditMode **31/31 passed**, waterfall-final-PlayMode **20/20 passed**, both exit 0; serialized-contract and whitespace checks passed. Automated checks verify configuration and lifecycle, not sound-pressure output or perceived loudness.

Audible retest remains required: near/far listening at unchanged system volume, other ambience/ability sounds, and two scene reloads. See area 10 of ManualAcceptanceGuide.md. The existing RC3 executable predates this fix; test the updated Unity project or a newly built player, not the old binary.

## Scope of acceptance tooling

Tools > Windrunner > Acceptance provides read-only source reporting/selection, player selection, and Play-Mode-only portable takeover/restoration and gameplay reload. No command saves scenes or modifies profile assets. Unity Hub's separate unity.exe serve process had blocked the previous name-only test-runner guard; the harness now excludes that known helper and still fails closed for unknown Unity processes. No second Editor was launched against an open project.
