# Progression framework implementation

Baseline: aa9e9d58, verified clean on 2026-11-13. Feature branch: feature/progression-framework. Character Tuning hardening is retained, not repeated. Existing production input preferences are the only installed persistence; progression uses a separate versioned save owned by the existing ProjectRuntime service registry.

## Implementation stages

1. Definitions, validation, deterministic quests/relationships/objectives, atomic state and persistence. Test transitions, duplicates, prerequisites, rank/reward transactions, recovery and unknown IDs.
2. Independent race/puzzle/challenge sessions and explicit world adapters, interaction input, safe cinematic control leases. Test real triggers, interruption and lifecycle.
3. Progression Studio, journal/tracking/guidance, isolated fixtures and authoring instructions.
4. Integrated regression, serialized contracts, Windows build/startup and performance evidence. Record manual acceptance separately.

No story content or dagger mechanics will be authored. Existing playground assets and gameplay tuning remain unchanged. New composition is opt-in and explicit. Runtime state never writes authored definitions. Temporary attempts reset on reload; durable chapter rewards and pending cinematics survive. Reward entitlements are committed with progression; future external reward consumers must acknowledge a stable claim ID idempotently.

## Current status

Stage 1 implemented: separate serializable definitions/state, extensible evaluators, prerequisite validation, explicit story acceptance/turn-in, independent character chapters, atomic reward entitlements and pending cinematics, versioned file replacement and recovery. progression-foundation Edit Mode 37/37 and Play Mode 25/25 passed, Unity exit 0. Serialized contract gate and whitespace check passed. Later stages are not complete. Baseline evidence is in HardeningReport.md and TestResults.json. Final content, visual/audio acceptance and target-player GPU budgets remain separate gates.
