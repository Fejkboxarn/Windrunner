# Zone 01 world-building preparation

Open **Tools > Windrunner > Zone 01 > Open world**. Scene: `Assets/WindrunnerProduction/World/Scenes/World_Zone01.unity`. Work on `feature/zone01-world-blockout`.

## Hierarchy and ownership

The existing root and gameplay paths are intentionally retained. Zone01Setup's selection shortcuts depend on WorldRoot/PlayerSpawn, Characters/Waykeeper, QuestRegions/North Basin and Collectibles/Ridge cache. Existing scene references, player child paths, prefab internals and lifecycle owners remain intact. No original object was renamed or reparented. Instead, existing categories gained identity-transform authoring groups:

```text
WorldRoot
  WorldServices                  existing session, project root, camera, event system/UI
  Terrain
    Central basin · 2 km          existing Terrain + matching TerrainCollider
    Roads                        empty reserved group for manually authored road objects
    TerrainAuthoring             empty reserved group; do not move Studio-owned children here
  Environment
    Directional Light            retained original location and settings
    Rocks / Cliffs / Trees / Bushes / Grass / GroundDetails
    Water / Decorations / Audio_VFX
  Structures
    Buildings / Bridges / Ruins
  Landmarks                      existing north pillars and east spire; add landmarks here
  Traversal
    AirNodes
      StartingArea               nodes 01–02
      HillsideRoute              nodes 03–05
      OpenTraversal              nodes 06–08
    SlingTargets                 empty organizational placeholder, not new enemies
    WindZones / TraversalHelpers
  PlayerSpawn                    unchanged
  Characters                     production player and Waykeeper, unchanged
  Enemies                        original three basin creatures/Sling targets, unchanged
  QuestRegions                   North Basin qualifying-kill region, unchanged
  Collectibles                   Ridge cache/timed retrieval, unchanged
  Interactables
  CutsceneStaging                 existing director/cinematic camera/signals
  DevelopmentOnly                existing reserved Editor/development category
```

Drag manually placed forest prefabs into the matching Environment group, and structures into Structures. Keep the original root and all organizational parents at identity scale/rotation. Terrain-painted trees/details stay in TerrainData; these groups are only for independently placed GameObjects. No scenery was generated or scattered.

The installed scene has one CentralBasin TerrainData binding and **no StudioTile or StudioRoad components**. No existing road spline or Studio working-copy binding was moved or created. The empty Roads/TerrainAuthoring groups reserve discoverable authoring locations without pretending that existing road content is installed. When explicitly attaching an editable Studio working copy later, let Terrain Studio retain its owned children and terrain/collider bindings; do not relocate them merely to fill these folders. Terrain Prototype Transfer remains under Tools > Windrunner, using the existing editable-copy workflow.

## Production nodes and route positions

There was no standalone production Air Node prefab in the repository. The existing production `Air Node 01` in StartingWorld is the same configured scene template used by the established acceptance fixture. Its complete object was saved as `Assets/WindrunnerProduction/World/Prefabs/ZoneAirNode.prefab`, after rejecting external scene-component dependencies. Eight linked prefab instances reuse its visuals, targeting component, feedback, cooldown and reward configuration. The StartingWorld scene itself was not changed. Each instance is independently selectable and movable.

| Node | Group | World position (metres) |
|---|---|---|
| 01 | StartingArea | (-12, 21.504, 15) |
| 02 | StartingArea | (12, 21.504, 28) |
| 03 | HillsideRoute | (-160, 21.563, -25) |
| 04 | HillsideRoute | (-180, 21.885, -25) |
| 05 | HillsideRoute | (-200, 22.368, -25) |
| 06 | OpenTraversal | (-18, 21.504, 42) |
| 07 | OpenTraversal | (0, 21.504, 53) |
| 08 | OpenTraversal | (18, 21.504, 42) |

All nodes sit 3.5 m above sampled terrain. The western route climbs the actual shallow hillside; it is a separate short route reached by walking west, not an aerial connection from the starting pair. Consecutive nodes within each route are below 35 m separation, inside the current 42 m Air Node targeting limit. Grounded approach rays from 10 m south are unobstructed. The starting nodes avoid Waykeeper at (0,7), the spawn and the eastern retrieval. Open traversal leaves the north pillars and enemy region intact.

**Air Cast nodes and Air Sling targets are different production types.** AirCaster targets AirNode; AirSlingController targets AirSlingNode. The new nodes are the former. They intentionally do not acquire an enemy health component or Sling behavior. For Sling testing, continue north to the existing three basin creatures at approximately z=86–94. They retain their quest-bound identities, health, movement and rewards. No extra qualifying enemies, quest completions or hybrid node behavior were introduced. Do not expect right-click Sling to select these eight Cast nodes.

## Practical in-Unity checks

1. Open Zone 01 and expand WorldRoot/Traversal/AirNodes. Select StartingArea and press **F** in Scene view. Enable **Gizmos**; the production AirNode wire markers remain available. During Play, the existing AirTargeter gizmos show candidate lines/scores when its configured diagnostics are enabled. No runtime debug UI or new visualization component was added.
2. Enter Play directly. The production player should spawn at the unchanged clearing position. Look left toward node 01 and right/north toward node 02. Target indicators should follow camera aim. Hold/release the normal Cast input for ordinary/perfect releases. Confirm familiar feedback, cooldown and resource rewards.
3. Walk west to HillsideRoute and try 03 → 04 → 05. Use ordinary movement between attempts. Inspect vertical clearance and camera comfort over several attempts; do not change targeting or physics tuning to compensate for a placement preference.
4. Return to OpenTraversal and try 06 → 07 → 08 with Cast and Dash. Continue north to the existing Sling creatures for combined traversal. Sling kills inside North Basin remain legitimate quest kills; these are not disposable test-only enemies.
5. Use the normal production Farseer equipment and overflow/transfer controls to check Second Wind while collecting rewards. No free resource reset, reward multiplier or special ability mode was added.
6. Speak to Waykeeper, test the original hunt and timed retrieval, then save/reload using a disposable playthrough if doing acceptance work. Original arrival, catalog, region and quest bindings are preserved.
7. Exit Play. Confirm no terrain data, roads, prototype arrays or paint assets changed. Drag one forest prefab into the appropriate empty group for your own authoring, save deliberately, and keep vendor collision limitations from ForestAssetTransfer.md in mind.

## Evidence and limits

Preparation verified all 464 existing transforms (path, parent, world position/rotation/scale) and 242 component configurations before saving. A separate serialized-record comparison preserves every existing record ID and component body; only four existing Transform child lists gain new groups. Two incidental Unity serializer normalizations were restored to original source text. TerrainData bytes and all existing terrain/prototype/texture assets remain unchanged.

Validation totals and evidence labels are in Progress.md/TestResults.json. Automated target acquisition uses the real production player on this scene, moving it to repeatable approach positions and controlling the test camera; it is not an assertion that a human has completed every route. Manual visual placement, movement-chain comfort, camera clearance under arbitrary approaches and full Cast/Sling play-feel acceptance remain open. Existing resource, quest, session and ability regressions cover system behavior without changing gameplay code.
