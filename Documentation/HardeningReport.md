# Reliability hardening — 2026-11-13

Scope: production-refactor, starting eeeb4ba4. IK/procedural foot placement, broad architecture rewrites and legacy asset deletion are excluded. Existing gameplay tuning and scene/prefab assets remain unchanged.

## Implemented boundaries

1. P1 projectile lifetime: immutable launch speed/lifetime/hit radius; hit/miss notification and delayed reward behavior unchanged. Preview changes/disposal cannot strand outstanding projectiles.
2. P1 active graph safety: restart-only controls are read-only in Play; session-level Save rejects a transaction containing restart-only changes. Author those settings in Edit Mode. No unpredictable live graph rebuild.
3. P1 coherent validation/persistence: nested draft references are temporarily remapped for whole-graph validation. Action additions/removals are checked against proposed children. Source conflict/preflight checks precede writes; grouped Undo and rollback restore original memory/file bytes after a persistence exception. This does not guarantee power-loss/crash atomicity.
4. P2 resource capacity: preserve absolute energy, clamp downward, notify on capacity changes and reconcile depletion thresholds. Increasing/restoring capacity does not refill energy. Unchanged settings preserve ordinary gameplay rules.
5. P2 rewards: retain explicit encounter-owned policies; Studio scope view lists their effective amounts and exclusion from preview/presets. No silent recipient-policy or encounter reward migration.
6. P2 presets: null supported profile slots can be filled; proposed composition validation, ambiguity/type checks, grouped Undo and rejection rollback. Duplication validates and refuses existing outputs; failure cleans only newly created files.
7. P2 coverage: effective sensitivity shows base × user preference. Hand-trail controls are clearly separate Edit Mode scene/prefab overrides with ordinary Undo/Save Scene. Dead Cast ring controls hidden in Studio, serialized compatibility preserved. Global feedback/input preferences and encounter timing/health remain separately labeled authorities.
8. P2 preview safety: Undo/Redo triggers validation; external mutations are polled every 0.1 seconds in the Editor while preview is active. Invalid preview restores bindings. Arbitrary external writes are not synchronously intercepted before their first runtime frame.
9. Animation recovery: new/rebound production graphs reconstruct sustained actions from current Sling phase, Cast charge and Dash state. No semantic publication, resource spend or gameplay event replay. Existing placeholders/missing-clip fallbacks remain. Recovery re-enters the applicable clip with its configured blend; it does not restore a disposed graph's exact historical playback timestamp or replay transient completed actions.

## Validation evidence

- P1 checkpoint af3ed592: 32/32 Edit Mode, 23/23 Play Mode, exit 0. Includes hit+expiry of simultaneous projectiles after draft destruction, reward exactly once, cleanup, active graph preserved after rejected Save, added/removed nested slots, actual Save/Undo and injected persistence-failure rollback.
- P2 checkpoint 4590c687: 32/32 Edit Mode; 23/23 final Play Mode. Initial 21/23 failure preserved: Undo fixture grouping restored the wrong historical snapshot; failed test left its scene and contaminated subsequent collision checks. Correct complete-object snapshot/group passed full rerun.
- Animation: hardening-animation-refs 24/24 Play Mode, exit 0. Live graph rebuilt from authoritative charge/delay/travel/impact/idle state; layer contribution, unchanged resources/state and no feedback replay asserted. Runtime-only compatible clip validates reserved charge slot without altering assets.
- Initial animation compile failed due missing test assembly references; fixed with explicit test-only animation references. Initial performance harness compile failed due Unity namespace shadowing; fixed with global qualification. Logs retained; neither failed invocation produced passing XML.
- Integrated hardening-final-compile and hardening-integrated: 32/32 Edit Mode each, exit 0. Reordering/reference replacement and real nested-profile duplication also execute.
- hardening-integrated and hardening-release Play Mode: 25/25 each, exit 0. Real motor velocity changes/restores under preview, capacity notifications, invalid Undo and automatic external-change shutdown pass. Cast/Dash recovery uses current authoritative state with compatible runtime-only test clips; no source assets changed.
- Capped, uncapped and targeted hotspot rendered runs: 1/1 each passed. Detailed measurements and unavailable GPU timing are recorded in PerformanceFindings.md. Final hardening-capacity-final Play Mode passed 25/25, including depletion recovery exactly once after capacity edits; Windows hardening-20261113 succeeded with 0 errors and 24 existing warning subjects. Both exact Windows headless and DX11 startup checks passed for 15 s with initialization and no matched errors; intentional harness termination is not graceful-shutdown acceptance. Raw results are in Progress.md/TestResults.json.

## Performance policy

Run RenderedSceneProfileTests with and without Run-UnityTests.ps1 -Uncapped. Each records cap/VSync, restores previous settings and calibrates native frame allocation. GPU duration is -1 when unavailable. Targeted rendered hotspot batches cover node-count targeting, water triangle counts, projectile create/expire and resource capacity transitions. CPU batch time excludes setup; frame GC/memory includes fixture/Editor overhead. Reflection cost is labeled in projectile/resource batches. No optimization or shipping FPS improvement is claimed.

## Remaining acceptance and limits

No broad cleanup or asset-dependent animation work was performed. Missing Cast/Dash/Sling-charge and directional clips remain authored-content tasks. Encounter settings are intentionally not duplicated into character presets. General multiplayer composition, full production-route GPU budgets, hardware reconnect and broad collision/water combinations remain outside this targeted hardening pass.

The phase-recovery test sets authoritative model phases while paused to isolate graph reconstruction; it is not an end-to-end controller-input traversal capture. Existing real-input ability tests separately exercise normal/perfect/canceled traversal and rewards. New Studio tests exercise session APIs; window layout, scene override widgets and full designer interactions still need manual acceptance.

## Focused manual checklist — all initially not tested

1. Open Batch1_Playground, select Airflow Player, open Character Tuning and load. In Play, change Cast pulse speed, fire and close/reload Studio during flight. Existing pulses must retain launch behavior, resolve once and disappear; new pulses use current settings.
2. In Play inspect Animation/feedback cached modules: controls must be read-only. Safe-module Save remains explicit. Stop Play and author animation in Edit Mode; Save/restart must use a coherent graph without enabling legacy writers.
3. In an independent variant, add/remove/reorder action slots and update semantic bindings, Validate and Save. Invalid indices must reject before any asset changes. Undo saved edits, reload drafts and verify restoration. Do not experiment on shared Petra assets.
4. During preview reduce Air Power maximum below current, then Revert/close: current clamps downward and never refills merely from restoring capacity; UI updates and Dash depletion/recovery remain consistent.
5. Expand effective-values/scope view. Compare normal-node reward tuning against the explicitly excluded Sling death-reward amount; encounter overrides should remain unchanged. Verify user sensitivity multipliers and Edit Mode hand-trail scene overrides; Undo and Save Scene deliberately.
6. In a copied character composition, clear a module reference and apply a compatible preset. Expect restoration and grouped Undo. An incompatible preset must leave all original bindings unchanged. Duplicate to a fresh path and verify independent nested modules.
7. In Play change a valid draft through Undo/Redo or another Editor tool to an invalid range. Expect preview shutdown and original binding restoration; no implicit asset save.
8. Disable/re-enable the animation bridge during Sling charge/travel/impact and active Cast/Dash. Expect current phase or documented missing-clip fallback, no repeated rewards/costs/feedback, and normal cancellation/recovery. Evaluate visual blends separately from automated ownership checks.
9. Compare ground/air movement, abilities, camera and waterfall mix to the accepted baseline. Capture capped and uncapped performance separately; do not interpret Editor averages as target-player budgets.

Preset validation temporarily assigns and restores candidate scene bindings; a rejected application may conservatively leave Unity's scene dirty flag set even though references are restored. No automatic scene save occurs. Inspect/Undo scene overrides before explicitly saving. Asset duplication creates new assets rather than deleting originals; removing an unwanted valid duplicate remains a deliberate authoring action.
