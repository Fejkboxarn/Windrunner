# Terrain Brush Library

Open **Tools > Windrunner > Terrain Brush Library**, also linked from Terrain Studio.
This is a manual workspace in the existing Terrain Studio Editor assembly. It adds no runtime terrain authority, automatic level generation, road generation or vegetation scattering.

## Safe workflow

1. Open your own terrain scene. In Terrain Studio, attach an editable working copy if needed. Keep **Automatic Updates OFF**.
2. Select that Terrain explicitly in the brush window. Shared working data, mismatched colliders, non-unit transforms and source-only terrains are rejected.
3. Choose Sculpt or Paint, a thumbnail, and settings. Search/category filters and per-user favorites help navigation.
4. Enter a world X/Z centre, or enable **Pick centre in Scene** and click the terrain. Picking only moves the preview; it never edits terrain.
5. Click **Preview**. Cyan surface lines show proposed sculpt heights; orange dots show paint changes. Inspect the target asset and affected-cell count.
6. Click **Apply preview**. Paint requires confirmation because it blends unprotected existing weights. Unity Undo/Redo restores the data and paint-history state together. Any target/history/brush changes invalidate the preview.
7. Save the scene and working assets normally. Nothing is implicitly saved by a stamp.

Sculpt changes heights only. It marks existing dirty regions for a later explicit Terrain Studio rebuild; it does not rebuild paint or roads. Paint changes weights only and never adds/reorders TerrainLayers. Append missing layers explicitly using **Terrain Prototype Transfer** before previewing again.

Existing protected overrides reduce/stop paint influence. New paint is pending manual paint: use **Protect pending paint rectangle** (protects the entire pending rectangle, including untouched cells), or review/protect/release it in Terrain Studio's Manual Artistic Overrides before another paint stamp. Releasing protection permits later procedural replacement; it does not undo the painted pixels.

## Controls and presets

Size is diameter in world metres. Strength scales influence. Rotation turns the heightfield/mask. Falloff softens the circular footprint edge. Invert reverses the field inside that footprint.

Raise/Lower add/subtract the selected height in metres, weighted by mask and strength. Absolute blends toward a world-space elevation: it is a masked flatten operation, not a replacement heightmap. Terrain's representable elevation range clamps results.

Load one of eight preset assets, then edit the working settings without modifying that asset. **Save settings as new preset** creates a new asset; it refuses to overwrite existing assets. A paint preset still requires its TerrainLayer in the target's palette. No missing layer is silently substituted.

## Library provenance and intended use

All 19 fields are original 257 × 257, linear, readable R16 mathematical data embedded in Editor-only assets. No image lighting is interpreted as elevation. No Blizzard art or brush data was copied. Erosion-inspired channels are analytical shapes, not a physical erosion simulation.

| Sculpt stamp | Use |
|---|---|
| Rolling Hills | Overlapping broad low rises; modest height and strength |
| Rounded Mountain | Smooth mountain mass with asymmetric shoulders |
| Sharp Peak | Central pointed summit; increase diameter with height |
| Long Ridge | Narrow winding ridge; rotate to establish direction |
| Rocky Plateau | Flat-topped mass with softened escarpment |
| Escarpment | Raised half-footprint; overlap deliberately for a cliff line |
| Broad Valley | Lower to carve a wide depression |
| Natural Gully | Lower to carve a narrower winding channel |
| Eroded Slope | Directional elevation with analytic drainage-like grooves |
| Terrain Breakup | Low-amplitude correlated variation; use restrained heights |

Paint masks: Grass Patches, Dirt Variation, Rocky Ground, Exposed Cliff, Forest Floor, Mud Patches, Sandy Shoreline, Worn Path and Organic Variation. They control influence only; actual colour and material come from existing TriForge/Raygeas layers. Cliff and shoreline masks do not automatically classify slope or water level; position and rotate them manually.

Eight starting presets: Gentle Woodland Hills, Rocky Highlands, Eroded Mountain Ridge, Broad Valley, Forest Soil Breakup, Exposed Rock Transition, Natural Grass Variation and Sandy Shoreline.

## Disposable demonstration and validation

The separate `Assets/WindrunnerTerrain/Editor/Brushes/Demonstration/Disposable brush demonstration.unity` illustrates a hill, peak, ridge, depression and existing Raygeas material transitions. It owns its source/working TerrainData and profile; it references no production Zone 01 terrain. It is an authoring demonstration, not a gameplay scene or biome generator.

`TerrainStampDemonstration.Create` is an explicit, one-shot batch fixture builder that refuses to overwrite an existing demonstration. It is not run on window opening or project import. Captures are written to `Logs/TerrainBrushLibrary/game-camera.png` and `scene-camera.png` from actual Unity cameras, not image generation.

Command-line validation does not verify mouse ergonomics. Manually check thumbnail selection, Scene picking, preview readability and Undo/Redo on a disposable copy before extensive authoring. All scene interactions remain under the designer's control.

## Validation evidence (2026-11-19)

Unity 6000.6.2f1 generated the library and compiled the workspace. Initial terrain-only run: 39/39. Expanded full rendered Edit Mode run: 197/197, no failures or skips (`Logs/ProductionMigration/terrain-brush-final-EditMode.xml`). This includes existing terrain painting, roads, prototype transfer and environment-authoring regressions, plus eight new brush/demo tests. Final terrain-only compilation/regression after missing-asset UI guards: 42/42. Exact final results are recorded in `TerrainBrushValidation.json`.

Coverage includes R16 precision/library completeness, preview purity, height/paint separation, protected weights, palette preservation, height and paint Undo/Redo, saved/reimported height values, independent preset drafts, stale terrain-position rejection, automatic-update rejection, another prefab sharing TerrainData, and reopening the saved demonstration with independent data and no missing scripts. Different diameters/strengths and rotated stamps are exercised by the tests/demo. The existing serialized source-contract audit also passed (67 contracts).

Rendered inspection: both cameras show distinct broad hill, pointed peak, long ridge and carved valley forms, with smooth elevation rather than high-frequency noise. Raygeas grass/mud/stone blends render without missing/pink materials. This intentionally sparse technical fixture is not a finished environment or artistic acceptance of Zone 01. The broad overview does not establish close-range texture-art quality.

Safety validation is intentionally conservative. A bulk dependency query reduced the sculpt preview/apply/Undo test from 26.48 s to 11.07 s, and the protected-paint test from 46.47 s to 16.45 s, including fixture setup/teardown. These are individual test durations, not a dedicated benchmark or a per-click latency guarantee. Preview/Apply can still pause for several seconds while checking the whole project; checks are not performed while hovering or continuously cached across ownership changes.

The first demonstration invocation failed before creating assets because Unity disallows adding a scene alongside an unsaved untitled scene. Corrected fixture setup then completed with exit 0. No production scene was replaced. Mouse-driven usability remains a manual check; no desktop control was used. Play Mode and Windows builds were not rerun for this Editor-only feature.

Final metadata audit: 44 new .meta files, no duplicate new GUIDs, and all 25 GUIDs referenced by the new YAML assets resolved. R16 binary fields also loaded successfully in Unity; source textures and prototype indices were not replaced. All41 protected production world files matched their pre-task SHA-256 hashes.
