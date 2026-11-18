# Terrain Prototype Transfer

Open **Tools > Windrunner > Terrain Prototype Transfer** in Unity 6000.6.2f1. This single Editor-only window adapts the three original TD-Production tools; it has no player-build dependency or automatic execution.

## Usage

1. Save your scene. For Zone 01, open **Tools > Windrunner > Zone 01 > Open world**. To trial the tool, save a separate scene copy and use **Tools > Windrunner > Terrain Studio > Attach editable working copy** on its Terrain. A copied scene alone still shares TerrainData until a working copy is attached.
2. Turn off **Automatic Updates** in Terrain Studio for the target. Leave them off while inspecting the result. The transfer tool never changes that setting for you.
3. Have both Terrain objects loaded. Drag the donor Terrain into **Source Terrain** and the editable recipient into **Target Terrain**. Hierarchy selection order does not matter. Inspect the TerrainData paths in the preview: they must be different. A TerrainCollider, if present, must reference the target's same data. Another loaded Terrain may not share the target data.
4. Select **Trees**, **Grass / details**, and/or **TerrainLayers**. Click **Preview transfer**. Review the source/target asset paths and each category's existing and added counts. Preview applies the proposed append to a temporary native TerrainData clone and rejects any painted-content changes before enabling Apply.
5. Click **Apply previewed append (Undo supported)**. Existing indices stay fixed; source entries absent from the target are appended in source order. Matching entries, including duplicates within the source, are skipped. Existing target duplicates are retained to protect their indices. Tree prefabs plus bend factor and nav-mesh LOD determine equality. Unity 6's detail-prototype equality and copy constructor preserve the full settings. TerrainLayers retain references to the same assets and deduplicate by asset identity.
6. Inspect the native Terrain Inspector's tree, detail and layer palettes. The original painted trees, grass, textures, heights and holes should look unchanged. The new prototypes are available for subsequent intentional painting. New TerrainLayer channels have zero weight, and new detail channels have no instances. Transfer does not paint any content.
7. Use **Edit > Undo** and **Edit > Redo** to inspect recovery, then **File > Save Project** when satisfied. Apply marks only the target data dirty; it does not globally save assets. After Undo/Redo or changing either terrain, preview again. Apply also rejects a stale preview if either TerrainData changes or is rebound.

## Protection and boundaries

Transfer is append-only; no destructive replacement mode is exposed, even on apparently empty terrain. No existing prototype slot is removed, reordered or overwritten. Missing references, invalid detail settings, shared source/target data, mismatched colliders, read-only/package targets and invalid Studio bindings block the operation. Play Mode is excluded.

A target with zero TerrainLayers must first have its initial layer deliberately established in Unity. Unity initializes first-layer coverage, so doing that implicitly would violate the transfer's no-paint guarantee. Zero detail resolution, as on the current Zone 01 source, is retained: the tool adds prototype definitions without allocating a grass grid. Set up that grid separately if you later decide to paint grass.

TerrainData receives only the requested prototype-array append. No height/detail/alphamap setters or scene rebindings are used. Exact preservation checks compare heights, holes, tree instances, every existing detail-density layer, and every original alphamap channel; additional channels must be zero. Unity can allocate another control texture when adding layers beyond a four-channel boundary. Existing channel weights remain unchanged. An unexpected failure during Apply reverts its isolated Undo group.

Terrain Studio paint history, protected overrides, surface profiles, road profiles and wheel meshes are not transfer targets. Re-enabling Studio automatic rebuilding is a separate authoring decision: its existing rules still govern future generated paint. Source assets are never edited. TerrainLayer and prefab references remain shared, so future edits to those underlying assets affect every terrain using them, as with the original tools.

Preview and Apply deliberately clone TerrainData and inspect full paint arrays. Large tiles require transient memory and may pause the Editor; this is an explicit authoring operation, not a per-frame or background job. There is no memory/performance optimization claim.

## Original source and adaptation

Read from the local `D:/Fork/TD-Production` checkout, remote `https://github.com/Fejkboxarn/TD-Production.git`, commit `35bb713bc5530eff70e70230257e804c0f614f2b`. All three original files were clean against that commit:

- `Assets/Editor/TerrainTreePrototypeCopier.cs`: tree prefab, bend-factor and nav-mesh LOD copying.
- `Assets/Editor/TerrainDetailPrototypeCopier.cs`: independent detail prototype copying. The adaptation uses Unity's copy constructor to include all supported fields.
- `Assets/Editor/TerrainTextureLayerCopier.cs`: shared TerrainLayer asset references.

The originals relied on selection order, replaced whole arrays and saved all assets. Windrunner consolidates them into explicit source/target fields, previewed append, semantic deduplication, data validation and a single Undo operation. The original project was not modified, and no vendor assets or runtime code were imported.

## Validation and manual follow-up

Automated evidence is recorded in Documentation/Progress.md. Tests use disposable assets, including a Terrain Studio editable working copy derived from `Assets/WindrunnerProduction/World/Terrain/CentralBasin.asset`. The accepted Zone 01 scene and original TerrainData remain untouched. This validates the actual installed Zone 01 source through the supported editable-copy workflow; it does not apply changes to the user's world.

Manual follow-up: open the window, assign two terrains, verify the category toggles and readable preview, append on a disposable working copy, inspect the new palette entries, then Undo/Redo and save/reopen. Visually confirm existing painted content is unchanged and paint one new tree/detail on the disposable copy to check its authored rendering. No manual visual acceptance is claimed by the automated suite.
