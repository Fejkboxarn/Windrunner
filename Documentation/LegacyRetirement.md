# Legacy retirement audit

Initial inventory: 283 owned scripts, 1,163 external scripts retained, 2,934 text assets searched. LegacyRetirementInventory.json records every owned script's classification, source mentions, serialized GUID references and retirement condition. CharacterCompositionAudit.json records the loaded scene's enabled/disabled components and profile dependencies before production-only migration. Nothing was deleted during this audit. Source mentions include tooling/comments and cannot alone prove runtime use; dynamic reflection and animation-event methods remain additional removal risks.

| Group | Classification | Evidence / retirement condition |
|---|---|---|
| PlayerMotor/Simulation/Displacement, Cast/Dash/Sling sessions and facades, targeters/resources | Required | Current sole gameplay authorities. Prototype namespace does not make the functioning Unity adapter obsolete. Preserve ordered physics and existing references. |
| PlayerHumanoidAnimatorDriver and PlayerGroundedFootIK | Replaceable | Enabled in the starting scene. Replace pose ownership with portable graph and validate locomotion/action/lifecycle before removing serialized components or source. |
| Custom animation, arms, knife, dual throw drivers | Replaceable | Disabled but serialized; bridge, movement-visual facade and historic installers reference their types. Production-only tests must show they are inactive; source deletion requires removing those caller contracts, not merely disabling components. |
| PlayerMovementVisuals | Required / mixed legacy presentation | Emits jump/land/flip/footstep events used by audio/feedback and owns presentation-root bank/lean. Legacy procedural pose switch is already disabled. Cannot remove entire component without replacing event/presentation responsibilities. |
| PlayerInputStartupRepair | Replaceable | Enabled but returns immediately with production input. Retain until legacy standalone authoring support is deliberately retired and callers removed. No active second input authority. |
| AirCastFeedback, AirTargetIndicator, AirChargeVisual | Replaceable | Disabled quarantine components in LegacyPresentation. Historic installers and migration tools reference them; removal from scene is safe only after reference audit, while deleting scripts also requires tool cleanup. |
| Input/feedback Active hosts and widget adapters | Required compatibility | Service lifetime, optional vendor widgets and standalone composition still use them. Explicit binding APIs exist. Remove only after every retained installer/runtime caller has replacement wiring. |
| Batch installers/migration utilities | Uncertain / replaceable | Not runtime gameplay but still rebuild historical setups and reference legacy types. Archive or retire as a reviewed dependency group; do not partially delete scripts and break compilation. |
| Third-party assets/resources and optional FEEL templates | Required or uncertain, retained | No deletion authorization is inferred from missing optional packages. They remain documented packaging debt. |
| Unreferenced owned source/assets | Uncertain | A zero static-reference count is insufficient to delete reflection, Editor menu or animation-event targets. No speculative obsolete verdict. |

Removed so far: **none**. Configuration Studio is Editor-only and reuses existing runtime profiles. Subsequent stage records must distinguish disabled production-path writers from physically retired source/assets.
