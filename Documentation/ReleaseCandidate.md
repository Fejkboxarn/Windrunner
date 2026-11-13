# Integrated production-only character candidate

## Latest hardening candidate (2026-11-13)

Current executable: `D:/Fork/Windrunner/Builds/hardening-20261113/Windrunner.exe`. This supersedes the production-clean candidate described historically below. P1 projectile lifetime, restart-only Save and nested validation defects are fixed; P2 capacity/preset/preview reliability and authoritative ability-animation recovery are integrated. See HardeningReport.md for the complete change inventory, limitations and focused manual checklist.

Final Edit Mode hardening-integrated **32/32 passed**. Final Play Mode hardening-capacity-final **25/25 passed**. Capped, uncapped and hotspot rendered captures each **1/1 passed**. Windows build **Succeeded**, 0 errors, 24 existing warning subjects, 30.3081 seconds, 1,212,118,854 bytes. Exact startup results are in Progress.md/TestResults.json. Serialization gate passed; no scenes, prefabs or authored gameplay profiles changed.

This remains a manual-acceptance candidate. No IK/procedural foot placement or broad cleanup was performed. Missing authored content, real hardware/visual/audible acceptance and target-player GPU/performance budgets remain open. Performance evidence distinguishes pacing and fixture overhead; no optimization improvement is claimed.

## Previous candidate record

Branch: production-refactor. Starting checkpoint 9a604f4a; Studio checkpoint fee61215; production-only composition checkpoint dac51246. Final retirement checkpoint is the commit containing this report (see Git log). No push, merge or history rewrite.

Executable: `D:/Fork/Windrunner/Builds/production-clean-20261112/Windrunner.exe`, Unity 6000.6.2f1, Windows Mono. This supersedes the earlier RC3 executable and includes the waterfall correction. It is a validation candidate, not production-readiness sign-off.

## Delivered changes

- Tools > Windrunner > Character Tuning: one searchable categorized window over existing independent module profiles. Isolated drafts, safe live-preview allowlist, explicit asset Save, Undo/Revert, loaded-default comparison, validation, Petra preset, independent profile duplication and compatible future-character binding application. No monolithic runtime configuration or per-frame asset searches.
- Portable animation automatically owns Petra by default. Production-only stop/failure cannot reactivate hidden legacy animation. Existing compatible placeholder clips remain; missing actions keep a valid portable fallback. Diagnostics check actual live ownership.
- Ten obsolete pose/input-repair/overlay components and their ten runtime types removed; seventeen dependent historic installers/custom Editors plus the completed placeholder installer removed. Detached legacy player controller removed after caller/GUID audit. Scene incoming references and obsolete serialized fields cleared. Exact file list, hashes and reasons: SourceRetirements.json and RetiredProductionComponents.json.
- Studio Editor files grouped with original GUIDs. Existing runtime assembly boundaries, numerical tuning, clips, Avatar, gameplay GameObjects, node controllers and third-party assets preserved. Required PlayerMovementVisuals event/bank/lean responsibilities retained.
- Extended regression coverage for profile parity, draft/source isolation, actual save/Undo, no-op asset bytes, invalid configuration, production startup/stop/re-enable and absence of retired runtime types.
- Earlier production service/input/time ownership, ordered movement, ability sessions/resources, Sling presentation collaborator, explicit camera/feedback ownership and waterfall fix remain integrated.

## Executed validation

| Check | Result |
|---|---|
| Unity compilation | Passed through Test Runner and final Windows build |
| legacy-source-retirement Edit Mode | 32/32 passed, exit 0 |
| legacy-source-retirement Play Mode | 22/22 passed, exit 0 |
| production-clean-references Edit Mode, after controller removal | 32/32 passed, exit 0 |
| production-clean-traversal, rendered Play Mode | 1/1 passed, eight real scenarios |
| production-rendered-baseline | 1/1 passed, native allocation calibration |
| Serialized baseline contracts and retired GUID search | Zero failures / zero remaining retired GUID users |
| Exact final candidate headless and DX11 startup | Both survived 15 seconds, initialized, no matched errors; intentional harness termination |
| Windows production-clean-20261112 | Succeeded, 0 errors, 24 warnings; 24.1062 s; 1,212,117,318 bytes |

Full suites cover deterministic motor/target/resource rules, jump/air transitions/reversals, normal/perfect/canceled Sling, perfect Cast/projectile/reward, Dash depletion/reactivation/interruption, water entry/exit/pause, simulated device loss/reconnect, nested time interruptions, feedback listener failures/order, graph ownership/lifecycle, loaded scene/prefab integrity and waterfall source/reload configuration. These tests do not certify audible output, hardware behavior or visual quality.

TestResults.json preserves all actual runs, including corrected failures, source/XML/log hashes, build warnings and startup/profile reports. Local raw evidence is under Logs/ProductionMigration. Detailed performance and final startup results are in PerformanceFindings.md and Progress.md.

## Warnings, retained systems and remaining gates

The final build retains 24 classified warnings: imported mesh settings, three ordered-search API warnings, Unity/vendor shader issues and three optional FEEL Resources with missing integration scripts. Warning subjects match the preceding candidate; shader variant keyword details differ. See BuildWarnings.md and exact build messages in TestResults.json. Zero broken gameplay references does not mean zero missing scripts in optional vendor Resources.

LegacyRetirementInventory.json classifies 264 owned scripts and owned data assets; 1,163 external scripts are retained. Unknown/reflected authoring entry points are not declared obsolete. Live compatibility widget/service adapters and clip-authoring profiles remain with caller evidence and retirement conditions in LegacyRetirement.md. There is no remaining hidden legacy player pose authority in the installed production scene. No known critical implementation blocker remains within the tested single-player scope; general multiplayer bootstrap and automatic rig importing are not provided.

Missing authored content: dedicated Cast charge/release, Dash and Sling-charge clips, exact backward-start/turn-in-place clips. Safe portable fallbacks exist; clips were not fabricated. Placeholder visual acceptance remains required.

Performance: calibrated rendered Editor baseline median 6.1655 ms, p95 6.9400 ms, median native frame allocation 2,664 bytes. Eight rendered traversal/lifecycle scenarios were rerun after source retirement. Editor/fixture overhead is included; these are not shipping FPS, GPU budgets or causal optimization evidence. Target-player allocation call stacks, sustained memory/reload and node/FEEL/Inspector stress remain follow-up measurements.

## Manual acceptance and authoring

Start with [CharacterTuningStudio.md](CharacterTuningStudio.md) for exact Petra/future-character authoring steps, then [ProductionOnlyAcceptance.md](ProductionOnlyAcceptance.md) for eight numbered checks covering automatic animation ownership, gameplay/ability parity, stop/re-enable, live tuning, explicit persistence/Undo, independent presets, feedback/audio and performance. [ManualAcceptanceGuide.md](ManualAcceptanceGuide.md) retains the eleven broad RC3 gameplay checks.

All new manual checks remain **not tested** until recorded by a person. The earlier unspecified 8/11 encouraging RC3 observations do not certify this newly enabled animation owner. Particularly inspect foot placement, landing/action masks, missing-clip fallbacks, waterfall near/far attenuation, real gamepad/rebinding, Play Mode Save/Undo and variant preset GUI operation. Use independent variant assets/scenes for destructive tuning experiments.

Reproduce with fresh labels using Run-UnityTests.ps1, Build-UnityCandidate.ps1, Smoke-UnityCandidate.ps1 (also -Rendered), verify_serialized_contracts.py, audit_retirement.py and Record-MigrationResults.py. Never run a second Editor against the same project.
