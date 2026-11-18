# Terrain Studio

Open the saved Zone 01 scene with **Tools > Windrunner > Zone 01 > Open world**, then **Tools > Windrunner > Terrain Studio**. The target is the Terrain under WorldRoot/Terrain. Unity 6000.6.2f1, the project's existing URP renderer and Unity Splines 2.9.1 are sufficient. The tool is opt-in. Opening the window or entering Play never paints the accepted scene.

## Designer quick start

1. **Attach a working copy.** Open a disposable copy of the scene for a trial. Select its Terrain, open **Tools > Windrunner > Terrain Studio**, check **Target terrain**, select **Basin surfaces**, and click **Attach editable working copy**. Save the scene. In the Inspector, Terrain and TerrainCollider should now reference the same new working asset.
2. **Set the surface rules.** In **Terrain Materials** assign grass, soil and rock. Duplicate a shared profile before changing just one zone. In **Height and Slope Rules** adjust the slope curves or elevation. Pick a small rectangle under **Automatic Terrain Painting**, then click **Preview selected paint**.
3. **Sculpt a mountain.** Once existing artwork is protected, enable **Automatic Updates**. Use the native Terrain Inspector’s **Raise or Lower Terrain** brush on the working tile. Release the brush and allow the pending update to finish. Compare the ridge and its base. Use **Walkability Visualization** to inspect grades against the player controller.
4. **Lay out roads.** In **Road and Path Authoring**, pick a centre, select a preset, and click **Create curved road at centre**. Select the road in the Hierarchy and use Unity’s Splines tool to drag, insert or extend knots. After moving or deleting a road, check its former location as well as its new junction. Keep connected tiles and splines in the same loaded scene.
5. **Tune carriage tracks.** Assign a carriage preset to the selected road. Under **Carriage Road Wear** edit wheel spacing, width, wear and breakup. Assign the wheel material and rebuild the road. Check both tracks in Scene view and cross them in Play Mode. Wheel geometry should follow the ground without a collision step.
6. **Protect hand painting.** Use the native Terrain texture brush, then **Manual Artistic Overrides > Protect pending native edits**. If no callback was recorded, click **Find unprotected edits**, review the detected rectangle, then protect it. You can also pick a smaller rectangle and use **Capture selected paint**. Use full opacity to hold the exact mixture. **Clear** releases one capture. **Release pending edits** authorizes replacing unprotected strokes.
7. **Review a rebuild.** Start with **Rebuild selected region**. Read the tile, number of cells, affected regions and protected captures in the confirmation. A road move can include its old footprint outside the selection. Cancel if the scope is unexpected. Full rebuild replaces generated coverage on the whole working tile.
8. **Recover.** Use **Edit > Undo** immediately after the unwanted edit or bake. Inspect both the terrain and the road before resuming automatic updates. **Revert generated paint and wear** restores captured source paint plus protected artwork, clears rendered wear and disables automatic updates. To recover a saved design, restore the matching scene, working data, paint history, mesh and metadata together.
9. **Validate before commit.** Run **Tools > Windrunner > Validate Terrain Setup** and inspect the Console for each terrain in the active scene. Repair the named object or asset, address unresolved paint, and rebuild pending regions. Save, close and reopen the scene. Confirm that the intended working assets and their metadata are in the diff.

**Manual acceptance on a disposable scene**

- Sculpt with a small then a broad brush on a visible ridge. Check transition quality, responsiveness after release, and untouched custom paint.
- Move a carriage junction across a slope and tile seam. Inspect it close up, at distance and in motion. Check shoulders, wheel spacing, flicker, floating strips and remnants at the former location.
- Walk, run and jump across both wheel lines and the seam in Play Mode. Compare the feel with the same approach before the bake.
- Capture a recognizable hand-painted mark, move the road, save and reopen. Check artistic appearance, then clear, rebuild and undo. Confirm the intended mark returns.

## Start an editable tile

1. Select **Basin surfaces** and **Attach editable working copy**. Studio duplicates the TerrainData and binds the terrain and its existing collider to the same copy. Scene wiring, heights, holes, trees and existing layer weights start from the source. The original Zone 01 asset remains available. Attach every tile you want to paint individually.
2. Save the scene. Commit its working TerrainData, binary paint-history asset, baked ribbon mesh and their metadata together. Unity's Undo reverses the scene bindings from setup. Created asset files remain in the project so references and redo stay recoverable. Duplicate scenes sharing an existing working-data asset need independent working data before separate editing.
3. In **Automatic Terrain Painting**, enter a world X/Z centre and metre dimensions or click **Pick centre in Scene view** and click the terrain. **Preview selected paint** shows grass, soil and rock coverage without writing assets. **Rebuild selected region** paints that rectangle and repairs changed road footprints. **Rebuild entire terrain** establishes a uniform rule pass over the whole tile. A selected rectangle does not implicitly replace unpainted surrounding terrain when a global rules update is pending.
4. In **Automatic Updates**, enable automatic painting when the protected areas are ready. This authorizes full-tile updates for changed rules. Sculpting queues the changed height rectangle plus the curvature sampling margin. Spline changes compare previous and current footprints. Disjoint road locations are processed separately. Updates defer for about half a second after edits and until the active drag ends. Saving a managed scene flushes eligible pending work and saves its paint history, terrain and mesh. Outstanding native-paint conflicts keep their manual resolution step.

## Materials and authoring controls

- **Terrain Materials** selects the surface profile and grass, soil and rock TerrainLayers. The supplied presets use existing Raygeas Grasslands terrain diffuse and normal textures. The tool owns copies of the TerrainLayers. In the Project window, select a Studio layer to edit texture tiling in metres, normal strength, remapping or roughness. Duplicate a profile or layer before making changes intended for just one part of the project. Keep existing layer identities when replacing art so captured paint continues to resolve.
- **Height and Slope Rules** exposes slope curves in degrees, a world-elevation meadow multiplier, two deterministic noise scales, seed, exposure direction, and signed curvature. The optional feathered soil region uses world X/Z metres. Flat summits can keep meadow coverage. Exposed steep faces and convex ridges increase rock. The Studio's broad and detail variation affects coverage, not collision.
- **Road and Path Authoring** creates a Unity SplineContainer at the chosen position. With the road selected, activate Unity's **Splines** tool in Scene view. Drag, insert or extend knots and change tangent mode to reshape it. Rotate or move the road's transform, duplicate it in the Hierarchy or delete it normally. Select a road in the window and use **Assign preset** to switch its shared treatment. Dirt, footpath, carriage and gravel presets have editable surface, core width, shoulder width, shoulder curve, vegetation coverage and priority. Vegetation coverage retains underlying paint; it does not plant grass instances.
- **Carriage Road Wear** edits spacing, width, intensity, irregularity and longitudinal repeat in metres. Terrain splats supply broad shoulder and road coverage. A separate narrow, feathered ribbon follows each wheel line, with patterned opacity, edge breakup and tapered ends for open splines. The wheel mesh uses terrain-sampled heights and a small depth bias. It has no collider. Same-priority broad roads blend at junctions and higher-priority shoulders overlay lower-priority coverage. Rebuild Selected Road visits the managed tiles in that scene.
- **Walkability Visualization** reads the bound production CharacterController. The Zone 01 controller has a 50° slope limit and 0.32 m step offset. Green samples are below the marginal band, amber samples fall within 5° of the limit and red samples exceed it. Judge steps, obstacles, run-up, momentum, gaps and actual aerial reach in Play Mode. This diagnostic never changes player settings.
- **Diagnostics and Validation** shows pending bounds, terrain and paint dimensions, palette/pass estimate, loaded texture memory, steep road samples, missing materials and tile-layout warnings. Inspect touching neighbors for correct links, compatible boundary heights and repeating texture phase. The presets use 4 m and 8 m repeats, which divide Zone 01's 2,048 m footprint. For unusual tile widths, select tiling that divides adjacent origin spacing or author matching layer offsets. Rules and spline sampling use common world coordinates even when neighboring paint-map resolutions differ. Keep connected tiles and their roads in the same loaded scene.

## Manual paint, Undo and recovery

Use **Manual Artistic Overrides** to capture existing paint *before* a generated pass where its exact appearance matters. Paint with the native Terrain Inspector on the working tile, switch back and capture either the selected rectangle or the pending native edits. Full opacity locks the captured material mixture and suppresses wheel detail there. Lower opacity blends it over generated paint. Captures store layer identities and independent weights. Overlaps apply in capture order.

Native texture callbacks pause automatic replacement. Before each write Studio also compares the affected cells to its last recorded output; this catches paint edits made without a callback. If Studio detects drift, **Protect pending native edits** records it. **Release pending edits** explicitly authorizes regeneration of that area. A native edit is not a permanent override until captured. Removing a captured TerrainLayer, changing paint resolution, or sharing working data across two loaded terrains requires resolving the binding before painting.

**Clear** beside a capture gives that area back to the rules. **Revert generated paint and wear** restores the source weight snapshot with surviving protected overrides, empties the ribbon mesh and switches automatic updates off. Terrain sculpting is retained. Use standard Unity Undo/Redo for region capture, paint generation, road transforms and spline editing. A deferred repaint and the artist's original sculpt/transform can occupy separate Undo steps. If a rule change is pending after Undo, inspect the tile before requesting a new rebuild. For a saved rollback, restore the scene, history, mesh and working TerrainData from the same Git checkpoint.

## Manual acceptance in Unity

Use a separately saved copy of Zone 01 and attach its own fresh working copy. Start with automatic updates off.

1. Pick an 80 × 80 m region. Raise and smooth a hill with Terrain Inspector **Paint Terrain**. Preview, then rebuild the region. Inspect grass on its top, exposed rock on steep sides and soil transitions. Check outside the selection for unrelated paint changes.
2. Enable automatic updates. Sculpt repeatedly in one spot. After releasing the brush, verify updated coverage and inspect **Diagnostics**. Undo and redo the sculpt and its paint pass. Both terrain shape and paint should recover.
3. Create a **Dirt road** across the hill. Insert a knot and bend it. Assign **Carriage road**. Inspect the two worn lines, softer shoulders and intervening surface near the camera, from uphill and at a distance.
4. Enter Play and run across and along both wheel lines. Check for bouncing, snagging, flickering or hovering. Inspect **Baked wheel wear** in Edit Mode; it should have a MeshFilter and renderer only.
5. Move the road well away from its original footprint. Extend, duplicate and delete a branch. The original ground should return, intersections should blend and discarded tracks should disappear. Raise ground under the remaining road and verify that its lines follow the new surface.
6. Use native terrain paint to detail a clearing. Capture it at full protection. Sculpt nearby, change a curve and rebuild. The clearing must survive. Try a partially blended capture, clear it and undo/redo. A pending native-paint warning must prevent silent replacement.
7. Turn on the walkability overlay. Compare sample colors below and above the bound controller limit and look for implausible road segments. Test uncertain slopes with the actual character.
8. Save, close and reopen. Confirm the hill, knot/tangent positions, protected material mixture and baked wheel geometry. Move the saved road once more and check for old paint. Revert generated paint on the test copy and confirm the hill remains.

## Rendering scope

Zone 01's 512² control map samples about every 4 m. The independent wheel geometry samples about every 0.8 m along a spline, so narrow tracks do not depend on a new full-world texture. Baked strips are batched per tile and material, with two triangles across each half-width segment. Only the working TerrainData and ordinary MeshRenderer output are needed for play. The shipped surface palette appends three materials to the original blockout layer, keeping the default terrain within one four-layer group.

The supplied terrain textures have normals but lack calibrated material-height masks. The default uses stock URP Terrain Lit and world-space weight transitions. Per-pixel height blending needs compatible mask maps; triplanar cliff mapping would add texture samples and a separate shader maintenance cost. Neither is enabled. Steep cliff stretching, visible repetition on long sightlines, junction art and final carriage-track detail still need art-direction review. The stone preset uses a tileable stony ground texture; replace it with a cliff-specific material when available. This milestone does not add road grading or terrain streaming.

## Validation and measurements

Validated against the Zone 01 baseline 98ee51d7 on Unity 6000.6.2f1. The final full Edit Mode suite passed 71/71, the production Play Mode suite passed 35/35 and the additional rendered terrain comparison passed 1/1. The checks used native TerrainData and TerrainCollider, including a non-persistent working copy of the actual basin. The baseline source and scene GUIDs are preserved. The original basin's Git blob is 319e6cebd84b34a1c81991a5a34faabb795b3bdf on both branches.

The tests exercised rule determinism, normalized weights, local rebuilding while a global rule pass was pending, two adjacent paint resolutions, a cross-border road, collision ray hits and normals across the wheel lines, native edits without a callback, capture, sculpt, Undo/Redo and reopening a saved scene. Deferred-update tests moved and deleted a spline and sculpted while automatic updates were active. Temporary test assets use isolated, uniquely named folders.

On the 512² basin with one carriage road, full paint and bake took **580.3 ms**. Seven 20 × 20-cell rebuilds had a median of **8.0 ms** and maximum of **8.8 ms**. Four TerrainLayers referenced **32.0 MiB** of loaded unique textures on this machine. The test road produced 570 vertices and one wheel material. Editing a road that crosses a tile can rebuild that tile's ribbon mesh; off-tile segments do not expand its culling bounds. These are small-road-network measurements.

| RTX 5070 Ti Editor sample | Broad paint only | Paint and wheel wear | Pixels changed by wear |
| --- | ---: | ---: | ---: |
| Near view, about 20 m from spline origin | 1.66 ms | 1.73 ms | 7,557 |
| Far view, about 91 m from spline origin | 1.62 ms | 1.70 ms | 544 |

Each measurement is the median of 15 camera renders into a 960 × 540 target with synchronous pixel readback after warm-up. The original blockout measured 2.39 ms in the near view. The harness measures a render plus synchronization on one machine; it does not establish isolated GPU cost, gameplay FPS or a shipping budget. Inspect the local PNG files named in the terrain-integrated-view XML/log. Early testing caught a far fade that removed all wheel pixels in the farther view; the corrected shader was verified at both positions.

**Reproduction.** With the Editor closed, run the normal EditMode and PlayMode commands in Tools/Run-UnityTests.ps1 with fresh labels. Run a separate EditMode pass with -Rendered -Filter RenderedBasinRoadNearAndFar. Run python Tools/verify_serialized_contracts.py and package the regular Windows candidate with Tools/Build-UnityCandidate.ps1 -MainMenu. For the disposable direct-play Windows preview, run the guarded matching Editor with -batchmode -quit -executeMethod Windrunner.TerrainAuthoring.Tests.PaintedZoneCandidate.Build -windrunnerBuildLabel followed by a fresh output label. The packaging check copies the production scene, applies the shipped profile and a carriage road, and requires the build report to contain the working TerrainData and mesh and to omit the editor paint-history asset. Its direct-scene session uses in-memory progression.

**Windows results.** The final three-scene production candidate built in 17.09 s with 0 errors and 21 warnings. The separately painted direct-play basin built in 7.38 s with 0 errors and 8 warnings and passed the packed-data check. Both Windows executables initialized and survived 15 seconds of rendered startup with zero matched runtime errors. Build warnings cover existing mesh import settings, stripped Core debug shaders, optional FEEL resources and vendor shader diagnostics; the last regular build contains no Terrain Studio compiler warning. See TestResults.json for messages and per-test evidence. Production startup and direct-scene startup do not replace the manual acceptance sequence above.

Local builds are under Builds/terrain-studio-integrated and Builds/terrain-studio-painted-final. The regular build retains the accepted Zone 01 blockout. The painted preview demonstrates the opt-in working-copy workflow without modifying that scene. Art direction, cliff treatment, junction quality, wheel appearance in motion, brush ergonomics and physical play feel require designer acceptance.

## Editing workload and reliability verification, 17 November 2026

On Unity 6000.6.2f1 the benchmark cloned CentralBasin into a disposable scene, keeping its 2,048 × 2,048 m size, 1,025² height samples and 512² paint cells. It arranged 16 carriage roads in two crossing groups of eight at 85 m spacing. Total sampled length was 19,545.4 m across 24,451 points. After full paint it alternated five 2 × 2 height edits with five road translations of 8 m in X and 14 m in Z. Four more 2 × 2 edits completed through the automatic callback. Both runs used the same headless Editor on this machine.

| Operation | Before | After |
| --- | ---: | ---: |
| Sample all 16 splines, cold | 78.9 ms | 87.9 ms |
| Repeat planning with identical inputs | — | 0.76 ms |
| Full repaint and ribbon bake, with paths prepared | 845.0 ms | 882.2 ms |
| Local sculpt, median of five | 182.9 ms | 149.0 ms |
| Translate one road, median of five | 254.8 ms | 231.1 ms |
| Automatic edit to completion, median of four including debounce | 734.2 ms | 696.0 ms |

The Editor reuses a road’s sampled geometry while its spline, profile, identity and world transform match. A local edit near a road can still rebuild the whole tile’s wheel mesh and record complete Undo snapshots. In this dense workload Unity’s total allocated memory grew by approximately 28 MiB per manual edit with Undo enabled. This is an Editor-wide change in memory use, including native resources. The per-thread GC API failed an 8 KiB positive control. The separate whole-Editor frame probe reported `EDIT-FRAME idle=452 controlPeak=4195243 sculptFramePeak=24513334 bytes (whole Editor frames)`. Neither counter isolates bytes allocated by Terrain Studio. These short runs establish editing costs on one machine; long-session memory retention remains unmeasured.

Automated verification passed 75/75 Edit Mode and 35/35 Play Mode, with the network workload and rendered comparison each passing separately. The disposable graded-road fixture compared 85 production motor displacement calls, collision flags, ray contact positions and normals before painting, after carriage baking and after changing wear and spacing. The protected-junction fixture exercised automatic updates, save/reopen, release and Undo/Redo. Rendered coverage was visible at both camera positions. The regular and disposable painted Windows builds had 21 and 8 warnings respectively, zero errors, and each initialized for a 15-second rendered startup check. The painted package included the working TerrainData and wheel mesh and excluded authoring history. See TestResults.json for individual runs and raw timing samples.

To repeat the editing workload, close Unity and run `./Tools/Run-UnityTests.ps1 -Platform EditMode -Filter IntersectingNetworkWorkload -Label <fresh-label>`. Use a new label for each run. The designer checklist above remains open.


## Transfer existing terrain palettes

Use **Tools > Windrunner > Terrain Prototype Transfer** to append tree, grass/detail and TerrainLayer definitions from another Terrain. See [the transfer guide](TerrainPrototypeTransfer.md) for explicit source/target selection, working-copy safety, preview and Undo. Existing painted indices are retained. Disable Automatic Updates before transfer; the tool does not change Studio rules or protected overrides.
