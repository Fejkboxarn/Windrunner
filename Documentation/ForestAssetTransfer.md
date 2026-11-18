# Fantasy Forest Environment transfer

Destination: `feature/zone01-world-blockout`, starting from `c5707d33`. Source: `codex/dagger-pack-animation` at `bbc9af04`, forest import commit `11bb927b`. No branch merge, animation import, scattering or Zone 01 authoring was performed.

## Available assets

Original location retained: `Assets/TriForge Assets/Fantasy Forest Environment/`.

- 170 prefabs: trees with seasonal/LOD/billboard variants, bushes, ground foliage/grass, mushrooms, rocks, props, waterfalls, particle effects and the optional wind controller.
- 96 FBX models, 48 materials, 73 PNG/TGA textures (including the delivered normal maps, terrain and wind textures), 23 TerrainLayers, three generated URP shaders and two supporting scripts.
- The original readme and Amplify shader-function source are preserved. No separate license text was found in the transferred package; the readme records the package name, vendor support contact and Unity 6 URP instructions. No new license entitlement is asserted.
- 859 source files including folder/asset metadata; original paths and GUIDs retained. No duplicate package GUIDs were found.

The 78 omitted files are demo scenes, their TerrainData/lightmaps/reflection probes, demo post-processing assets and associated metadata. The package's scene-contained demo water plane is omitted with those scenes. No scene, post-processing profile or rendering setting is installed automatically.

The preserved `Logs/ProductionMigration/blockout-preserved-forest-archive/` contains the 39,927,091-byte **Built-in Renderer Fantasy Forest Content.unitypackage**, plus metadata and its prior SHA-256 manifest. It contains 412 pathname entries; its archive-only content is alternate Built-in materials, scenes and post-processing/baked data, not missing production URP vegetation. It was inventoried without extracting over the URP package. It remains preserved and ignored, and is not committed. The tracked URP files, rather than this alternate archive, are the authoritative transfer source.

## Using the package

Use the original **Prefabs/Trees**, **Prefabs/Ground Foliage**, **Meshes**, **Materials**, **Textures** and **Terrain Layers** folders. Drag prefabs into a disposable scene to inspect them before authoring the real zone. Summer Birch 1, Bush 1, Rock 1 and autumn Grass 01 were rendered in the compatibility test. No materials require conversion for the tested Unity 6000.6.2f1 URP configuration.

To transfer terrain definitions, populate an explicit donor Terrain's prototype lists with the desired prefabs/layers, then use **Tools > Windrunner > Terrain Prototype Transfer**. It transfers existing definitions; it does not automatically derive prototypes from asset folders. Use an editable working target and disable Terrain Studio Automatic Updates first. A regression verifies one actual tree, one mesh/instanced grass prototype and all 23 TerrainLayers append to a disposable Zone 01 Studio working copy without changing heights, paint, tree placements, detail maps or Studio state. Undo and repeat/deduplication are checked.

The supplied wind controller writes `FFE_*` shader globals when instantiated; it is available but not added to Zone 01. The texture-pan component is retained for authored effect prefabs. No global wind, camera, volume or lighting setup is changed by importing these assets.

## Original vendor defects and unavailable content

These exceptions are **not repaired or declared production-ready** by the transfer. The regression asserts their exact known scope and rejects additional missing component references. Prefer the other variants until collision/authoring review is complete.

| Original content | Finding | Consequence |
|---|---|---|
| Trees/P_FFE_Tree_1_Autumn.Prefab | Collider references missing mesh GUID `63e07f0a284968f4196e3440c4d65ff1`. | Do not rely on its complete authored collision. Rendering mesh/materials resolve. |
| Trees/P_FFE_Tree_1_Winter.prefab | Collider references missing mesh GUID `ff24dca63113bd548ab81afb635a5c38`. | Same restriction; other capsule colliders do not prove the missing mesh is redundant. |
| P_FFE_Rock1_Winter, Rock2_Winter, Rock3, Rock3_Winter, Rock4, Rock4_Winter | Each contains at least one empty MeshCollider slot. | Review collision before using these as traversal obstacles; rendered meshes resolve. |
| P_FFE_Bush_2.Prefab / bush_2 | Animator Avatar reference does not resolve. | Static mesh renders independently; no animated-bush functionality is claimed. Prefer Bush 1 for the verified path. |
| Bedroll, Birch 1/2 and Tree 01/02 Winter model importers | Three old external material-remap GUIDs are absent (`518b4b554b18a944fb1a95a36af6a4cc`, `c3bc1d19299eafc4fa9c1b880053a78b`, `baaa3ffba61ed4e43bd9785299dccbf5`). | The delivered prefab material assignments resolve. Prefer those prefabs to raw model drag-and-drop; review importer material assignments before using raw FBX assets directly. |
| Shaders/4WayChaos.asset | References unavailable Amplify Shader Editor script GUID `78b2425a2284af743826c689403a4924`. | Optional authoring helper requires the vendor's authoring tool. Compiled/generated URP shader sources are present and work independently; no Amplify installation is implied. |

Missing GUID assets were absent from both the source branch and preserved archive. No replacement GUID, collider shape or Avatar was fabricated. These limitations mean **not every original dependency was recoverable**, even though the delivered rendering assets and tested terrain prototypes are usable.

Particle prefabs have an intentionally empty second material slot while particle trails are disabled; this is checked separately from a missing active render material. Four particle materials cause URP to discard obsolete `_FADING_ON` keywords during import; original on-disk material source is retained, and no destructive material conversion or project-wide rendering change is committed.

## Narrow transfer-tool compatibility correction

Appending all 23 layers caused native control-texture allocation callbacks to flag Terrain Studio's `nativeDirty` region, even though paint values remained identical. The transfer now scopes its verified prototype-only write through TerrainPainter's existing `Writing` notification guard, restores that flag in `finally`, and keeps exact paint checks and Undo rollback. No serialized Studio settings are changed. This correction is limited to the existing Editor tool; it does not alter terrain painting rules or gameplay.

## Storage and validation

Existing LFS rules cover source art. Package-scoped case-aware rules now also cover mixed-case FBX/TGA names and the large PNG textures (the wind mask is approximately 44.6 MB). Metadata and Unity text remain regular Git content. The source branch's LFS object integrity check passed before transfer. Final destination LFS verification and test totals are recorded in Progress.md/TestResults.json.

Automated checks include prefab component references, missing scripts, renderer materials, meshes, shader support/compiler errors, texture references, all TerrainLayers, representative rendered pixel checks, real-package terrain transfer and the existing Zone 01 open/validate/close test. Render evidence is local under `Logs/ProductionMigration/forest-*.png`. Passing with documented vendor exceptions is not equivalent to a claim that the entire vendor package has no defects.

Manual follow-up: inspect selected asset silhouettes, normals, alpha edges and LOD transitions under Zone 01 lighting; test the intended collision of any chosen obstacle; check optional wind and particle appearance in a disposable scene. Do not place the flagged collision/Avatar variants without reviewing their limitations. Nothing has been automatically placed or painted in Zone 01.
