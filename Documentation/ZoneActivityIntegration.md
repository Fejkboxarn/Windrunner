# Zone 01 relay and pursuit integration

## Scope and checkpoints

Normal checkout: `D:/Fork/Windrunner`, `feature/zone01-world-blockout`. Baseline `5db46de2` already contains Highlands assets and Environment Authoring Studio. Source `f54d6894` was selectively cherry-picked with provenance as `6fd8d01b`, before production scene changes. No other feature branches were merged. The source and performance worktrees remain unchanged. No desktop control was used.

## Four chapters

| Quest | Completion | Turn-in rank | Cinematic |
|---|---|---|---|
| Basin hunt | Existing three creatures | Ally (unchanged) | Existing basin milestone |
| Ridge retrieval | Existing timed pickup/delivery | Trusted (unchanged) | Existing retrieval milestone |
| Wind Relay | Three sequential Air Cast nodes | Placeholder Rank 3 | Clearly marked three-second placeholder |
| Ground and Air Pursuit | Ground capture, then airborne Cast capture | Placeholder Rank 4 | Clearly marked three-second placeholder |

The user explicitly authorized the two appended placeholder ranks and chapters. Existing thresholds, rewards, unlocks and quest definitions are retained. No new permanent reward or invented lore was added. A challenge completion only makes its quest ready for NPC turn-in; chapter rank advances through the existing transaction. The next quest remains locked until the prior milestone resolves. Existing saves completing one or two original chapters retain their earned state.

The existing Waykeeper RelationshipContact offers all four quests without a new NPC or quest authority. Journal tracking and objective facts use the existing progression catalog and save writer. The cinematic-UI Wayfinder is absent on this branch; `ZoneQuestActivity.ObjectiveAnchor` supplies the current relay, moving pursuit target or NPC turn-in anchor for later integration. Relay hosts also expose all currently available anchors.

## Find and play

1. Open the normal project and `Assets/WindrunnerProduction/World/Scenes/World_Zone01.unity`. For saved progression, enter via the existing Main Menu and select a disposable playthrough.
2. Interact with Waykeeper and finish the existing hunt and retrieval quests. Return for each turn-in and resolve/skip its milestone.
3. Accept Wind Relay. Go west to the cyan nodes: first near **(-85, 21.5, 25)**, then east through the two subsequent nodes, spaced 20 metres apart. Charge and release Air Cast while targeting each available node. Nodes have 35-second windows, an eight-second warning and a 120-second overall attempt. No perfect release is required. Return to Waykeeper.
4. Accept Ground and Air Pursuit. The green ground target starts southwest near **(-85, 18.9, -45)**. Approach within its 1.8-metre capture radius, or use Interact inside that radius. A qualifying missed interception can reverse its route. After capture, go southeast to the orange airborne target near **(65, 24, -45)**. Capture it with an aimed Air Cast, then return to Waykeeper.
5. Each encounter has a labeled retry pedestal. Interact nearby to restart only that open objective's attempt. Completed objectives cannot be reset. The component also provides a Play Mode context command for development. These controls never erase quest, chapter or save progress.

Retries and scene reloads restart incomplete attempts; timer/route position and partially activated relay nodes are deliberately not saved. Completed ground capture survives a reload into the airborne phase. Pausing through the production input owner pauses activity simulation. Failure does not reset quest acceptance. Attempts time out after 150 seconds for pursuit; the retry pedestal remains available.

## Move and tune

- Hierarchy: `WorldRoot/Traversal/Quest03_WindRelay` and `WorldRoot/Traversal/Quest04_Pursuit/{GroundPursuit,AirPursuit}`. Existing objects were not reparented.
- Move a whole course using its root. Keep pursuit roots at unit scale. Relay nodes are individual prefab instances and can be moved independently.
- Profiles: `Assets/WindrunnerProduction/World/Activities/{WindRelay,GroundPursuit,AirPursuit}.asset`. Use the existing Wind Relay/Pursuit Studio tools for drafts, route editing and explicit application. Ground and air settings are independent; Hybrid remains available in the reusable authoring tools.
- Ground route uses terrain ray projection at runtime. Editor-authored positions are not automatically moved when terrain changes. After manual relocation, validate every segment for obstacles and reachable terrain.
- The orange target's collider is a trigger: a solid collider blocks production Air Cast's obstruction ray. Do not make it solid without an equivalent targeting-compatible setup. Its real AirNode and existing Cast callback own capture; no fake projectile adapter is used.
- Placeholder target materials and milestone timelines are local to the new Activities folder. Original environment materials, vendor assets and animation content remain untouched.

## Preservation and implementation

`ZoneQuestActivity` composes the existing objective evaluator with encounter lifecycle and presentation. PursuitHost adds an optional objective gate so the air stage cannot start prematurely or the ground stage restart after completion. Existing default behavior remains unchanged. Scene validation recognizes the typed activity adapters and production collider-free Air Nodes.

The scene retains all 631 original serialized record IDs, with 81 new records. Only four original records have permitted extensions: Traversal child list, cinematic child list, PlayableDirector bindings and Journal area markers. Installation compared all 494 original transforms and 280 original components outside the explicit Journal/director changes. Twenty-four protected existing world files, including CentralBasin TerrainData, compare byte-for-byte with the baseline. No terrain heights, layers, painting, road rules, overrides, vegetation placements, original quest objects or environment packs were changed.

## Validation and limitations

Exact executed results are recorded in `ZoneActivityIntegrationEvidence.json` and Progress.md. Rendered evidence comes from command-line Play Mode camera captures in `Logs/ZoneActivities`, with disposable saves and virtual input. It is not human gameplay acceptance. Ground near-miss/capture and new relay/air Cast interactions use the real production player; the original hunt setup uses its existing authoritative damage/impact callbacks. Test placement teleports the player between encounter setup points, so automated completion does not establish walking-route readability or chase fairness.

Manual checks still required: NPC/Journal readability; navigating from the NPC to each course; relay timing comfort; ground interception fairness; airborne aiming and camera readability during normal movement; whole-route obstacle clearance after future manual world edits; placeholder cinematic presentation. No terrain or artistic acceptance is claimed.
