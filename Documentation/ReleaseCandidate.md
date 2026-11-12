# Integrated architecture release candidate

Branch: production-refactor. Implementation checkpoints: 05ba46c1 (portable action phases and placeholder integration), 9354f251 (remaining player presentation camera bindings and scenario profiling). Earlier checkpoints remain in Git and Progress.md. No push, merge or history rewrite.

Final executable: `D:/Fork/Windrunner/Builds/migration-rc3-final-20261112/Windrunner.exe`.

## Change inventory

- Application/service lifetime, input/rebinding/device interruption, nested pause/hit-stop arbitration and scene/UI reload composition.
- Motor numerical rules, synchronous displacement authority, jump/flow/steering; preserved motor/Sling/swim ordering.
- Cast/Dash sessions, resource spend/reward policy, Sling charge/path/phase logic and synchronous presentation collaborator.
- Explicit player input/feedback/camera ownership; source-filtered action events and isolated reward recipients.
- Existing portable animation framework extended with deterministic action priority, restart/interruption, speed, masks, phase looping/cancellation and per-character profiles. Compatible existing clips integrated; safe reserved slots for missing content. One portable graph owner per Animator, tested suspension/restoration of configured legacy writers.
- Named locomotion/action Inspector workflow, clip inventory and replacement guide.
- Ten decorative reference repairs from earlier stages; exact-hash allowance for those repairs and three animation profile changes. Existing GUIDs and gameplay scene/prefab references preserved.
- Expanded deterministic, real-component, lifecycle, reference and performance regression tooling. Native allocation calibration replaces the failed per-thread allocation inference.

## Executed validation

| Check | Actual result |
|---|---|
| integrated-actions-final Edit Mode | 31/31 passed, Unity exit 0 |
| presentation-owner-final Play Mode | 19/19 passed, Unity exit 0 |
| traversal-scenarios and repeat, rendered | 1/1 each passed; eight scenarios per run |
| Serialized contracts against 7bfdfe5e | Passed; allowlisted profile/decorative changes only |
| Diff whitespace | Passed |
| Windows migration-rc3-actions-20261112 | Succeeded, zero errors, 24 warnings, 32.39 s |
| That build's headless and DX11 startup | Both survived 15 seconds, initialized, no matched runtime errors |
| Final Windows migration-rc3-final-20261112 (tooltip corrected) | Succeeded, zero errors, 24 warnings, 25.77 s; 1,222,130,750 bytes |

Full suites cover motor/target rules, action timing/priority/fallbacks, ownership/lifecycle, water entry/exit/pause, device disconnection/reconnection, interrupted Cast/Sling, perfect traversal/release, Dash depletion/reward/reactivation, nested interruptions, feedback ordering/listener failure and loaded scene/prefab integrity. Successful hardware interaction and visual quality are not inferred. The only source change after these suites is an Inspector tooltip correction; the final build below recompiles it.

TestResults.json retains failures as well as successful runs, test/source/log hashes, build warnings and player reports. Raw logs and binaries remain local ignored artifacts. Prior graph fan-out, ownership-guard, transition-slot and private-field failures were corrected, not omitted.

Final candidate headless and rendered DX11 startup both survived 15 seconds with initialization confirmed and no matched runtime errors. Harness termination is deliberate; graceful shutdown and interactive gameplay are not tested by this smoke check.

## Performance

PerformanceFindings.md contains both idle rendered captures and repeated movement/reversal/jump, action graph, UI reload, Sling, Dash, Cast and swimming measurements. Native Editor CPU/GC/memory counters include test costs and cannot attribute allocations to gameplay. The standalone DX11 startup/idle sample used 10.766 aggregate CPU seconds over 15 wall seconds, peak working set 958.96 MiB and ending private memory 2456.24 MiB; startup work is included. These are process measures, not frame rate, GPU timing, a leak test or a target budget. No speculative optimization was justified by this sample.

## Completion and remaining work

Core single-player architectural implementation is integrated; no known unresolved critical engineering defect was identified by the executed audit/tests. **Production readiness and visual/gameplay acceptance are not signed off.**

- Noncritical engineering debt: retained standalone legacy discovery/installers, three ordered-search API warnings, imported mesh compatibility warnings, vendor shader upgrade warnings, optional FEEL template Resources. All 24 warning entries remain documented; none is claimed fixed by a cache change.
- Missing content: dedicated Cast charge/release, Dash and Sling-charge clips; exact backward-start and turn-in-place clips. Safe fallbacks exist. DoubleJumpFlip presents the existing flip reward; no new double-jump mechanic was introduced.
- Acceptance-dependent migration: automatic portable activation and retirement of legacy humanoid/procedural/overlay components. Existing compatible clips require character-specific visual assessment, especially landing variants and full-body action masks.
- Performance follow-up: target-hardware player frame/GPU budgets, allocation call stacks, node-count/FEEL saturation, cloth, and large-selection Inspector stress. Existing samples establish reproducibility, not shipping optimization completion.

## Manual Unity/player acceptance checklist

Practical numbered steps, expected results, failures and setup: [ManualAcceptanceGuide.md](ManualAcceptanceGuide.md). User reports 8/11 areas tested without significant gameplay differences; the specific areas are not identified, so individual items remain unassigned. Waterfall audio failed manual acceptance; see [WaterfallAudioRegression.md](WaterfallAudioRegression.md) for the targeted correction and required audible retest. The old RC3 binary does not contain this subsequent fix. Architecture is frozen except targeted regression fixes and validation tooling.

- Compare acceleration/friction/reversals, air momentum, jump/coyote/buffer timing and gravity with 5P.
- Manual/reward Dash chains; normal/perfect/blocked/canceled Sling and Cast; collisions/occlusion/target destruction and reward order.
- Water entry/exit and ability overlap on authored water meshes.
- Camera bank/FOV/sensitivity, pause during hit-stop/impact hold, rebind persistence and real gamepad reconnect.
- Portable takeover, jump/fall/landing, flip and Sling travel/impact/launch; action interruption/masks; stop/disable/re-enable restoration. Approve visual parity before enabling automatic takeover or removing legacy components.
- Shader/art appearance, particle dagger mesh, waterfall/audio mix, FEEL visibility/audibility, UI overlap and scene reload.
- Rendered Windows traversal and performance on target hardware; sustained reload/memory checks.

Reproduce with fresh labels using Run-UnityTests.ps1, Build-UnityCandidate.ps1, Smoke-UnityCandidate.ps1 (optional -Rendered), verify_serialized_contracts.py and Record-MigrationResults.py. Never start a second Editor against the project.
