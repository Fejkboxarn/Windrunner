# Suntemple URP compatibility repair

## Open and use

Use **Tools > Windrunner > Suntemple > Open Repaired DemoMap**, or open `Assets/WindrunnerCompatibility/Suntemple/DreamscapeSunTemple/Scenes/DemoMap.unity`. The identically named scene under `Assets/Polyart` is the preserved original, not the repaired demonstration. No production startup scene/build list is replaced.

For manual world authoring, use the prefabs and materials under `Assets/WindrunnerCompatibility/Suntemple/DreamscapeSunTemple`. In Environment Authoring Studio's existing Sources list, add that folder as a source named “Suntemple URP”; enable it and refresh. Shared effects are under the sibling `SharedResources` folder. This does not alter placement preferences or automatically place anything in Zone 01.

The isolated demo camera retains the vendor controls: WASD, mouse look, Space/Left Ctrl vertical movement, Shift/Alt speed modifiers, Q/E and Z/X sun adjustment, T lighting reset, V/B FOV and N FOV reset. It now reads the installed Input System. These are demonstration controls, not production player changes. Mouse/input ergonomics remain manual acceptance.

## Provenance and preservation

Baseline: `ddc888ee`, `feature/zone01-world-blockout`. Original import: `Assets/Polyart/PolyartStudio/DreamscapeSunTemple`, with dependencies under `Assets/Polyart/PolyartStudio/SharedResources`. The import was untracked; its embedded `SunTempleURP Click me! Unity 6.0.unitypackage` was ignored. Nothing was downloaded. The local embedded Unity 6.0 URP package supplied compatible vendor shaders/materials. Its original archive remains locally preserved and is not committed as a duplicate archive.

Before repair, 3,710 files (the import and existing modified package/player settings) were copied with SHA-256 hashes to `Logs/SuntempleRepair/baseline`. Those settings changes predated this task: Post Processing 3.5.4, Terrain Tools 5.3.3 and PPv2 scripting defines. The original package README explicitly requires Splines, Post Processing and Shader Graph. The repaired scene uses a URP Volume; retained original source scripts still require the imported PPv2 dependency. No production renderer asset, quality configuration or input backend was changed.

The compatibility copy uses distinct deterministic GUIDs. Original meshes/textures remain shared dependencies. `SuntempleVariantMapping.json` records every original/variant path, GUID and local source choice. Original scenes, materials, shaders and prefabs are retained. Large scenes, native assets and textures use scoped Git LFS rules.

## Actual defects and repair

- Built-in/custom shader variants were mixed with URP content. The original rendered overview had extensive magenta terrain/cliffs/water/vegetation. Local vendor URP shader graphs and shaders replace the corresponding materials in project-owned variants; foliage, decals, layered surfaces, water and impostors retain their specialized shaders. Standard/legacy particle fallbacks are converted separately with original textures, UV transforms, colors, smoothness, emission and alpha settings.
- The flowing-fountain shader GUID `06136d6c615617344a5e05eb73d79d94` exists in neither the import nor its embedded URP archive. `FountainFlowURP.shader` is an explicitly approximate replacement using the existing normal, foam, flow textures and serialized water settings. It provides animated flow, lit transparent water and depth-edge fading. It does not claim exact unavailable-source parity or restore that shader's unknown refraction implementation.
- The copied ocean shader needs its relative Gerstner include; the local original include and instancing helper are retained alongside the copied shaders. No global pipeline replacement is used.
- 3,727 missing authoring components share GUID `4bd5e5ddd242bd64581fb5ed9c0e0507`, with Polybrush-style mesh/vertex data. Only the repaired scene removes these orphans. All 34,004 mesh bindings and 34,004 additional vertex-stream bindings were checked unchanged during removal. Original authoring records remain recoverable in the original scene. Do not assume Polybrush editing is restored.
- The local vendor URP scene is **not** substituted wholesale: it differs in embedded meshes. Instead the original scene is copied and remapped, preserving geometry/transforms. TerrainData is separately copied from the original; only prototype/layer references are remapped.
- PPv2 scene components are replaced by the local vendor URP volume profile. Camera color/depth requests are explicit for water. Legacy demo input readers are adapted in project-owned copies; normal production input and camera ownership are untouched.
- Remaining particle fallbacks include a flattened project-owned material variant and a replacement for Unity's Built-in default particle material. Original asset parents are retained.

## Limitations

The original OceanTool's underwater screen effect uses `OnRenderImage` and is not supported by URP. Above-water ocean rendering is repaired; the underwater effect is not certified or presented as working. No project-wide renderer feature was introduced to emulate it. The project-owned OceanTool copy does not attach that Built-in callback under URP. It also fixes the confirmed null SceneView exception; serialized wave parameters and public controls are retained.

The vendor shader graphs produce warnings about `pow` input domains and matrix macro redefinitions; the local Unity 6.0 ocean shader also produces deprecation warnings under Unity 6.6. These must be distinguished from compilation errors. The original Built-in package remains intentionally unrepaired, so opening the original DemoMap can still show its original problems. The repaired copy is the supported entry.

Automated camera captures are not mouse-driven Editor acceptance, and no desktop control was used. Interactive flythrough, fine transparency/refraction appearance, underwater requirements and artistic matching still require manual review. No claim of performance optimization is made for the 34,019-renderer demo.

## Validation evidence

Evidence directory: `Logs/SuntempleRepair`. Initial audit: 47,348 objects / 34,019 renderers / 3,727 missing scripts. Repaired audit: 47,349 objects / 34,019 renderers / zero missing scripts, material slots or mesh references. 311 dependency materials and 86 renderer-used materials audited; no shader errors in the rendered audit. Vendor warnings remain as above.


Additional reference findings: the missing alternate-grass diffuse uses the installed same-pack `T_Suntemple_Grass_01_C` as an explicit fallback; the missing olive coverage-mask reference is cleared to the shader default (the same fallback the unresolved reference previously rendered with). Original mask/alternate-grass appearance cannot be reconstructed from absent content. Heightmaps, layer order, alphamaps, details and tree instances are not changed. Stale saved texture properties not declared by the active shader are retained for traceability, not treated as active missing inputs.

`SuntempleDependencyFindings.json` distinguishes demo dependencies from unused extras. Original `DynamicMesh_0..19` and `PF_OceanPlane` placeholder prefabs contain absent meshes/materials. Their 21 newly generated compatibility copies were excluded after verifying they are neither demo dependencies nor referenced by other compatibility prefabs; the originals remain preserved. Shared legacy effects and example tools have additional unavailable dependencies; do not treat the entire SharedResources folder as an approved library. Decal/Ocean/Path generator prefab mesh slots are populated by their authoring component, unlike missing static imported meshes; the loaded demo has valid mesh bindings. Two unused newly generated materials (`M_Insect`, `M_Runes`) with unavailable shaders were excluded from the compatibility output after confirming no references to them; their original files remain intact. No replacement package was downloaded.

The first full Edit Mode run completed 207 tests: 199 passed, 8 failed. One new test failed on the two unused effect shaders; another failed when restoring an empty initial scene setup, leaving the demo open and contaminating later terrain checks. That exposed the vendor OceanTool null SceneView bug and a terrain collider from the leaked demo scene. Test isolation and the scoped ocean adapter were corrected before the rerun. Intermediate compile errors in the new diagnostic API use and copied controller extension method were corrected; these failed attempts are retained in Logs.

The second full run passed 207/208; the remaining new test exposed direct material/collider work in vendor OnValidate. The scoped copy now coalesces that work onto the next Editor callback, tolerates destroyed/unconfigured objects, and preserves existing serialized parameters. Focused Suntemple tests then passed 5/5. Final full-suite/build evidence follows below.

Build preflight additionally found the vendor flythrough dereferencing an unassigned rim light. DemoMap has an authored sun and no assigned rim; the project-owned reader now treats both lighting controls as optional and retains the camera binding. No new light or lighting balance was invented. LOD renderer references and required sun/camera bindings are explicitly checked before capture/build.

## Standalone runtime correction and final evidence

The first explicit GPU-rendered standalone run exposed 836 invalid mesh-bound errors, one per decal projector. The vendor projector initializes bounds in Editor-only OnValidate, then regenerates geometry in runtime Start; its fallback divides by the uninitialized height. The project-owned DecalProjector retains authored meshes on Start, initializes projection bounds for runtime generation, uses the equivalent finite local fallback height, and destroys only meshes it owns. Original source remains intact. A regression checks authored mesh identity and finite generated vertices. This is a runtime defect, not a shader-clock or shader-reference issue.

The final Windows Development build is `Builds/SuntempleURP_20261119_161413/Suntemple.exe`: succeeded, zero errors, 64 warnings, 33.4697688 seconds (cached build). Compilation and actual D3D11 rendering used Unity 6000.6.2f1 and the existing production pipeline on the RTX 5070 Ti. The opt-in development probe completed six 1600x900 camera captures and exited successfully with **zero logged errors**. Captures cover the overview/architecture, flowing fountain, vegetation, lake/transparent fountain basin, fire/ocean and ground. All six were visually inspected. Evidence: `Documentation/Evidence/Suntemple/runtime-*.png`; detailed log: `Logs/SuntempleRepair/runtime-r4`.

Earlier batch back-buffer captures were black and rejected as evidence. Explicit offscreen camera rendering corrected the capture method; the subsequent run revealed the decal defect and was not accepted as a reliability pass. Only the final corrected run is the accepted standalone result. No desktop input/control was used. No full Play Mode suite was run for this isolated package repair; standalone lifecycle/render validation is recorded separately.

Remaining warnings include vendor shader pow-domain/macro warnings, Unity 6.0 water shader deprecations under 6.6, inherited obsolete API/compiler warnings, and engine mesh/build suggestions. Build warning counts depend on cached compilation and are not a performance or warning-reduction claim. Runtime also reports an initially undersized ocean plane before its first update and an authored mirrored-window BoxCollider with negative effective scale; the ocean subsequently renders and Unity uses an absolute box size. Geometry and light balancing were not changed to suppress those warnings.

## Manual acceptance still required

1. Open the repaired scene through the Tools menu above, then enter Play. Confirm the flythrough controls, optional lighting controls and FOV reset work comfortably. No mouse-driven acceptance was performed.
2. Inspect foliage cutouts, architecture shadows, fire/emission, layered ground, flowing fountain and ocean while moving the camera. Automated views sample these surfaces but cannot certify every viewpoint in this large scene.
3. Review the explicitly approximate fountain shader, alternate grass fallback and default olive mask against your artistic expectations. Their missing originals were not available locally.
4. Use compatibility prefabs for new environment authoring. Do not infer that unused original SharedResources examples or the original Built-in DemoMap were repaired. Underwater post-processing remains unsupported in URP.

Final validation: Full Edit Mode **209/209 passed**, zero failures/skips (355.5258233 seconds); focused Suntemple **6/6 passed**; serialized contracts **67 passed**, zero failures. Windows Development build **0 errors / 64 warnings**. Six inspected D3D11 standalone captures completed with **zero logged errors**. Final preservation audit: **3,751 hashes passed**, including all 3,710 baseline import/config files and all 41 protected production world files. No full Play Mode suite or performance certification claimed.

Unity normalized keywords in 11 original Suntemple and four TriForge materials; these exact incidental changes and the test fixture empty-array serialization were inspected and restored after the Editor exited. Original local archives remain preserved. Machine-readable evidence: `SuntempleValidation.json`. No push or merge.

Git/LFS audit: 810 unique staged LFS objects (3,810,844,254 bytes) verified locally by size and SHA-256, zero failures. No missing metadata, unexpected staged paths, archives, caches or builds in the checkpoint. Diff whitespace findings are Unity-generated empty metadata fields; imported vendor files were not bulk-formatted.
