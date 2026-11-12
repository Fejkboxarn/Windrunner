# Migration progress

Baseline: `7bfdfe5e` (5P), `production-refactor`, 2026-11-12.

- Repository/source inventory and migration plan created. No 5Q implementation identified.
- Stage 1 complete: executable baseline, inventory, documentation and content-debt gate.
- Current: fix runtime UI material ownership discovered by baseline smoke testing, then motor simulation extraction.
- Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe`.
- Baseline Unity licensing and compilation confirmed by actual batch runs.
- `baseline-EditMode`: 4 passed / 11 failed (wrapper incorrectly rejected normal PASS logs). Corrected wrapper captures errors and requires a PASS message.
- `baseline-fixed-EditMode`: **15 passed / 0 failed**, exit 0.
- `baseline-content-EditMode`: compilation failed on obsolete Unity 6.6 `objectReferenceInstanceIDValue`; changed to entity ID API.
- `baseline-content-fixed-EditMode`: 15 passed / 1 failed, batch-mode empty scene restoration error; corrected cleanup.
- `baseline-content-final-EditMode`: 15 passed / 1 failed, revealing 10 existing broken references: four waterfall AudioSource resources and six bird particle sprite entries. No missing scripts reported. Test now checks exact baseline debt; it does not claim clean content.
- `baseline-PlayMode`: **1 passed / 0 failed**, exit 0. Loaded actual gameplay scene, found one motor and production input host, checked finite player position for 60 frames. This is startup smoke coverage, not gameplay-feel acceptance.
- Raw XML and logs: `Logs/ProductionMigration/` (ignored local artifacts). Baseline inventory includes serialized SHA-256 fingerprints and no duplicate Assets GUIDs.
- `baseline-recorded-EditMode`: **16 passed / 0 failed**, exit 0; integrity test requires the exact 10 baseline reference issues, with no additions.
- Baseline PlayMode changed two shader float values in vendor `Canvas 4.mat` via the resource bar. Restored the known test-induced changes from HEAD; no user changes were present. This is a real asset-ownership defect, so its fix precedes motor work. No scene/prefab migration has been performed.
- Remaining work and manual acceptance matrix: `ProductionMigration.md`.
