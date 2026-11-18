# Environment Authoring Studio

Open **Tools > Windrunner > Environment Authoring Studio**. This Editor-only workspace supports manual authoring; it never generates a zone or scatters vegetation. Branch: `codex/environment-authoring-studio`, based on world-building `5b743597`. The abandoned experiment remains absent.

## Library and sources

The verified initial roots are:

- `Assets/TriForge Assets/Fantasy Forest Environment` — TriForge, primary woodland/meadow palette.
- `Assets/Raygeas/Grasslands - Stylized Nature` — Raygeas, grassland and shoreline/water assets. No separately named Coastal folder was found.
- `Assets/Raygeas/HIGHLANDS - Stylized Environment` — Raygeas, highlands/alpine assets; preserved locally and currently untracked on the world-building baseline.

The source registry is editable. In **Sources**, enter a verified publisher/package label and project folder, approve/enable it, optionally enter default biomes, include/exclude path fragments and allowed categories, then **Apply registry and rescan**. Filters use semicolons; categories are exact names. Missing folders are safe. A newly installed pack at a registered root is discovered automatically. A different/new root requires explicit approval; publisher attribution is never guessed from an asset filename. No packages are downloaded or reimported by the Studio.

Imports, moves and deletions enqueue GUID updates while the window is open. Opening the window or pressing Refresh scans enabled roots. Processing is limited to 40 entries / approximately 6 ms per Editor update (an individual Unity call can exceed the budget). No full-project scan runs during repaint. Visible grid rows alone request thumbnails; Unity supplies the preview cache. Source meshes/textures can still make first-preview loading expensive.

Search name/category/source/biome/notes; filter category, source text, biome, type, favorites, approval or collection. The type filter distinguishes prefab files from imported models. **Recent first** reflects explicit placement, not thumbnail browsing. Uncheck Approved to see retained missing/unavailable entries. GUID, path, package, structural compatibility and diagnostic details appear on selection. Categories inferred from verified directory names or TerrainLayer type are suggestions only. Ambiguous files remain Unclassified. Use Category override, Source override, Biome tags, Notes, Preferred usage and Approval to correct them. Blank overrides inherit the source suggestion. Rescans never overwrite saved metadata.

Favorites, classifications, collections, source registrations and presets save to **ProjectSettings/WindrunnerEnvironmentAuthoring.asset** when you edit them. This is authoring metadata, not an imported asset modification. GUID identity survives moves; missing collection/favorite references remain placeholders and reconnect when their GUID returns. Keep original `.meta` files. Removing a source registration does not erase saved classifications or collections.

## Preview and manual placement

1. Select a prefab/model. The larger preview uses Unity's existing Editor preview, including drag-orbit and wheel zoom where that asset editor supports them. Textures, materials and TerrainLayers use their native preview or thumbnail fallback. Broken assets show diagnostics rather than being instantiated into the world.
2. Set scale multiplier, Y rotation, offset, normal alignment or upright placement. Random Y and scale variation are **off by default**. Grid snapping is optional. Ground snap requires a collider; with snap disabled placement uses a horizontal plane through the chosen parent's height (or world zero).
3. Choose a parent explicitly, use the current hierarchy selection, or **Find existing category group**. The latter resolves existing `WorldRoot/Environment/{Trees,Rocks,Cliffs,Bushes,Grass,Water,GroundDetails}` or `WorldRoot/Structures`. Missing groups are reported; no roots or groups are created, and existing objects are never reparented.
4. Click **Place in Scene view**, hover a collider and click once per prefab. The wire bounds guide shows approximate footprint; it is not a second pose/physics authority or a full ghost renderer. R rotates 15 degrees; Shift-R reverses. Alt navigation remains available. Scale controls remain in the Studio. Escape or Stop placement cancels.
5. Undo/Redo uses Unity's normal stack. Prefab links and original mesh/material references are retained. Save your scene explicitly when satisfied. Placement refuses Play Mode, preview scenes, Prefab Mode and a parent from another scene. Inspect overlaps, nonuniform parent scale and grounding after placement; collider count and bounds are diagnostic hints, not automatic collision resolution.

## Collections and placement presets

Woodland, Meadow, Shoreline and Highlands palettes start **empty** so the tool does not make world-design choices. Add selected GUIDs, remove entries, duplicate collections, set tags/notes/preferred variations and save placement settings. **Use placement preset** copies settings only. **Browse** filters the library. Neither action paints, populates or changes a scene. Missing GUID entries and empty collections are valid.

## Terrain workspace

Select an explicit **Destination Terrain**. Inspect it read-only: data path/editability, loaded-scene sharing, prototype counts, layers, Studio binding, automatic updates, native/procedural dirty rectangles and protected captures are displayed. The existing SetupValidation API supplies road/painting diagnostics when bound.

To prepare vegetation/layers:

1. Select a candidate prefab/model, detail texture or TerrainLayer in Library.
2. Choose Trees, Details or Layers in Terrain workspace.
3. **Preview append of selected asset** constructs a disposable source in a preview scene and invokes the existing TerrainPrototypeTransfer preview. This does not save the source or alter the destination.
4. Read the source/target data paths and prototype counts. **Apply previewed append** is explicit and Undo-enabled. Existing prototype indices, heights, alphamaps, placements and Studio state are checked by the existing transfer transaction. Changing target data or Undo/Redo invalidates the preview. An empty target's first TerrainLayer must be initialized explicitly in Unity; the tool refuses to initialize paint silently.
5. Select destination for native Terrain painting. The Studio does not paint instances. Prototype sizing uses conservative defaults (1×1 detail size, instancing for mesh details); inspect Unity's native prototype settings before painting. Structural compatibility does not guarantee suitability of every vendor tree/grass shader.

For transfers from an existing source Terrain, open **Terrain Prototype Transfer** directly. For editable copies, spline roads, procedural texturing, dirty-region rebuilds and Manual Artistic Overrides, open **Terrain Studio**. Existing APIs, safeguards and ownership remain authoritative. Recommended sequence: edit → save scene/data → capture relevant Manual Artistic Overrides → preview procedural changes → deliberately apply → verify. Automatic updates must be disabled for prototype transfer. Shared-data checks cover loaded Terrains; unloaded scene ownership still needs review. No destructive replace/reorder/repaint operation is exposed by this Studio.

## Validation and performance

**Scene diagnostics > Inspect active scene** is explicit and read-only. It reports missing prefab/script/material/shader/mesh links, missing LOD renderers, shared TerrainData, missing layers, suspicious scale, likely floating/buried rocks and high renderer/tree counts. Copy exports the full report; the UI shows at most 500 entries. Rock grounding uses a bounds/name heuristic and cannot prove an artistic placement wrong. Counts/LOD warnings are not measured bottlenecks. Diagnostic scans are synchronous and user-triggered, not background scene mutations; large scenes can take time.

No automatic repair, source-material conversion, object movement or deletion is offered. Unsupported shader checks are structural/runtime-support checks, not proof of visual correctness on every target GPU. Missing optional texture slots are informational. Existing forest vendor exceptions remain documented in the forest import report. Material variants can be made using normal Unity duplication when deliberately needed; this tool does not invent or convert them automatically.

Preview editor instances are disposed on selection change, window close, play transitions and assembly reload. Disposable terrain sources are also released on target change, Undo, apply, close or failure. Catalog callbacks are unsubscribed when closed. Editor code stays in the existing Editor-only terrain assembly; there is no runtime singleton, generator or gameplay dependency.

## Acceptance boundaries

Automated evidence and exact results are recorded separately in `EnvironmentAuthoringEvidence.json`. Tests use disposable scenes/assets, not production painting. The existing terrain transfer/Studio regressions validate paint preservation, captured overrides, roads and index safety. Asset library settings use GUIDs; JSON disk roundtrip tests verify retained metadata/presets independent of catalog lifetime. Rendered preview tests exercise Unity's thumbnail renderer and repeated preview Editor cleanup.

Manual follow-up: choose representative real tree/rock prefabs, orbit/zoom, place one into your own working scene with chosen parent/scale, Undo/Redo, and verify the native Terrain painting result for your chosen grass shader and size. These are user acceptance checks, not completed world design. No production terrain, scene hierarchy, quest, NPC, Air Node or road changes are part of this task.


## Executed validation — 2026-11-18

Unity 6000.6.2f1 compilation and full rendered Edit Mode suite passed **155/155**, zero failures/skips (`environment-studio-checkpoint`). Nine new authoring tests cover callbacks/import/move/remove/reconnect, manual GUID metadata, missing packs, installed sources, disk-persisted presets/favorites, placement with prefab links and Undo/Redo, layer paint preservation/shared-data refusal, tree/detail dedup/stale-preview cleanup, native rendered previews and idle/filter behavior. Existing Zone 01, terrain authoring, forest and prototype-transfer tests also passed. Serialized-contract audit: zero failures. **3,558 original files** including production world, Terrain Studio data, ProjectSettings and source packages matched their baseline SHA-256 hashes after restoring incidental Unity particle-keyword normalization.

The final synthetic 5,000-entry filter benchmark took 371.702 ms for 20 queries (~18.59 ms/query); idle processing consumed zero queued entries. This does not establish import latency, frame time, allocations, GPU cost or absence of long-session native retention. The real open window indexed 1,374 entries and displayed native thumbnails. Actual window screenshots were shown inline during the session; no screenshot file was exported. Desktop interaction stopped at the user's request and must not resume without asking permission. Extended mouse-driven usability, preview orbit/zoom and an actual interactive Editor-restart favorites check remain manual acceptance, not claimed passes. JSON disk reload and catalog recreation were automated.

Initial fixture errors and the batch-only asynchronous preview failure are retained in the logs. The final test uses Unity's native synchronous RenderStaticPreview and releases its texture/editor. No new Windows build or Play Mode run was needed/executed for this Editor-only addition; existing builds remain untouched. Exact evidence and limitations: `EnvironmentAuthoringEvidence.json`.
