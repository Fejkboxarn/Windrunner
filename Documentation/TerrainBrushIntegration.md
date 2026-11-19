# Terrain Brush Library — world-branch integration

Destination: `feature/zone01-world-blockout`, initially clean at `b7e7f1a8`. Source: `codex/terrain-brush-library` / `f063f4bb`. The source was a self-contained child of the destination baseline. Controlled `git cherry-pick -x` produced `41ceee44`, without conflicts; its tree exactly matched the source. No other branch was merged or modified. Other existing worktrees were left alone.

Transferred the Editor-only workspace and Terrain Studio link, ten R16 sculpt stamps, nine paint masks, eight presets, disposable demonstration, tests, documentation, binary-asset Git attributes and original .meta files. No production scene or TerrainData was transferred or regenerated. Usage: **Tools > Windrunner > Terrain Brush Library**; see [authoring guide](TerrainBrushLibrary.md).

## Ownership-validation investigation

The existing implementation first checks live Terrain/Collider bindings, editable Studio working-copy ownership, transforms, paint history and automatic-update state. It then discovers other scenes/prefabs and asks Unity for their recursive dependencies. The same checks run again before Apply. This intentionally includes indirect ScriptableObject, nested-prefab and importer dependencies across installed content, rather than assuming vendor folders cannot reference working data.

An experimental breadth-first traversal of direct dependencies was tested against Unity's complete recursive result. It failed equivalence: direct traversal missed implicit importer/icon dependencies (including Input System, MagicaCloth, FEEL and URP icons) and added an unrelated post-processing profile. The experiment was discarded in full. No cache, exclusion list, ownership shortcut or weakened acceptance test was shipped. The original implementation remains byte-equivalent to the source checkpoint.

The first experimental full run passed 198/199; a focused diagnostic rerun failed 0/1 and identified the exact differences. These failures are not counted as successful validation. A subsequent full run passed 199/200 and exposed a test-fixture issue: importing the prop after sculpting could refresh TerrainData wrappers, so managed-reference identity was not an asset-ownership assertion. The fixture now creates its prefab before editing and checks the TerrainData asset path plus actual heights/weights. No production implementation change was required. Final results and measured native query timings are recorded in `TerrainBrushValidation.json` under `world_branch_integration`.

The remaining delay is an acknowledged authoring limitation. A safe future cache would need proven invalidation for imports, moves/deletions, new dependencies, loaded scenes, prefab stages, Undo/Redo and unsaved state. This integration does not introduce that additional architecture merely to conceal the delay.

## Interoperability and preservation

Destination tests retain existing Terrain Studio procedural painting, road, Manual Artistic Overrides, Prototype Transfer and Environment Authoring Studio coverage. Added explicit checks cover:

- Creating an indirect prefab → paint-state → working-TerrainData reference after Preview; Apply must reject it. Removing that disposable reference permits the original preview to apply.
- Paint Apply, Undo/Redo, asset save and forced reimport with matching weights.
- Environment Authoring Studio prefab placement on sculpted terrain, then placement Undo, without modifying heights, paint or TerrainData ownership.
- Separate native ownership-query timings, with all safety checks preserved in production code.

Existing stamp tests cover preview purity, R16 library completeness, protected weights, unchanged layer indices, sculpt/paint separation, stale previews, independent presets and saved-demo reopening. No production terrain is used as a test target.

Manual mouse-driven usability was not exercised; command-line validation only, consistent with the user's desktop-control restriction. No Play Mode or Windows build is required by this Editor-only integration and neither is claimed. No push or merge into main.

## Measured remaining cost

On this destination checkout, three samples covered 970 scene/prefab roots and 4,980 recursive dependencies. Local terrain/history validation took 0.110 / 0.152 / 0.184 ms; asset discovery took 37.309 / 15.924 / 15.751 ms; Unity's native recursive dependency query took **5,368.070 / 5,276.156 / 5,013.145 ms**. These are Stopwatch elapsed call durations, not frame-time or allocation measurements. The native query dominates; brush height/mask calculations are not implicated by this breakdown. No performance improvement is claimed. The conservative ownership scan still blocks each explicit Preview/Apply for several seconds in this project.

## Destination verification

Final Unity 6000.6.2f1 compilation and full rendered Edit Mode run passed **200/200**, zero failures/skips (`Logs/ProductionMigration/terrain-brush-world-verified-EditMode.xml`). Serialized source contracts: **67/67**. All41 protected world files retained their original SHA-256 values. Git reports no differences from `b7e7f1a8` in production World, TriForge or Raygeas assets, and no differences from source `f063f4bb` in the brush implementation/library/demo/.meta subtree. Known incidental vendor particle keyword and progression-fixture empty-array serialization changes were restored after testing. No disposable test folders remain.

Repeat native-query samples in the final passing run were 5,192.054 / 5,158.793 / 5,001.487 ms, consistent with the earlier measurement. This limitation remains explicit; the integration is validated, but no speedup or manual usability acceptance is claimed.
