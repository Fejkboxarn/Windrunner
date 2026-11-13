# Windrunner progression framework candidate — 2026-11-13

Branch: feature/progression-framework. Verified baseline: aa9e9d58. Earlier hardening checkpoints af3ed592 and 4590c687 are ancestors and remain intact. Local checkpoints: 3ffce279 (rules/persistence), 769a1b00 (world/control integration), f45cdb8b (Studio/journal/fixture), followed by the checkpoint containing this report. Nothing pushed or merged.

## Delivered implementation

- Separate authored definitions, mutable records and versioned save state; stable IDs and localization-ready text keys.
- Deterministic quest availability/acceptance/readiness/turn-in/completion; parallel objectives and validated prerequisite groups; extensible evaluators and occurrence/unique-object deduplication.
- Independent relationship ranks/chapters with one active chapter per character, atomic rank/reward/cutscene unlock transactions and next-chapter cutscene gating. Optional challenges cannot advance ranks.
- Independent races/traversal courses with ordered/alternative checkpoints, scaled time, fail/cancel/restart, personal best and exactly-once first-clear entitlements.
- Independent puzzles with order/destination/time policies, resets, permanent solution and partial persistence. Gates/routes/moving authored objects consume state through explicit presentation adapters.
- Contextual interaction with actor/range/channel/prerequisite checks; centralized rebindable actions. Actual Air Cast projectile hits and lethal Sling impacts have explicit adapters. Collection/discovery presentation reconstructs from saved facts.
- Cinematic session/Timeline orchestration, pending/playing/watched/skipped state, replay without grants, skip/interruption recovery, explicit missing-content fallback and scoped input/time/camera ownership. Storage failure restores gameplay control rather than stranding it. Portable player animation remains its authority.
- Existing ProjectRuntime registry owns the service and one semantic feedback observer. Scene reload binds the surviving input service and does not duplicate rewards/notifications.
- Progression Studio: searchable categorized definitions, standard serialized editing/Undo, safe creation, remapped catalog duplication, structural/content validation and pre-Play scene-wiring guards.
- Functional keyboard-first journal, one tracked quest, compact objective HUD, optional general-area guidance, binding-derived interaction prompts and completion/unlock notifications. Other quests continue progressing independently.
- An isolated authored engineering fixture with two independent characters, existing PlayerMotor/input, a race, puzzle, turn-ins, marker Timeline and save/reload composition. It is excluded from shipping scene settings and is not final story content.

The main playground remains unchanged. Integration is opt-in through documented scene composition; the main executable does not silently install demonstration quests or a new journal on the accepted player. The standalone fixture is the packaged framework exercise.

## Executed validation

| Check | Actual result |
|---|---|
| Unity compilation / Edit Mode | 46/46, progression-candidate, exit 0 |
| Latest full Play Mode | 31/31, progression-notification-final, exit 0 |
| Rendered real-motor fixture | 1/1, progression-rendered-fixture, exit 0 |
| Command/storage profiling | 1/1, progression-profile, exit 0 |
| Serialized contract gate | Zero failures; no existing scene/prefab/profile/meta changes |
| Fixture references/wiring | Executed Edit Mode audit, no broken references or invalid configured bindings |
| Main Windows build | Succeeded, 0 errors, 21 reported warnings, 24.2806404 s, 1,212,201,809 bytes |
| Fixture Windows build | Succeeded, 0 errors, 5 reported warnings, 6.0804185 s, 113,727,421 bytes |
| Final rendered startup | Main and fixture each survived 15 s, initialized, no matched errors |
| Final headless startup | Main survived 15 s, initialized, no matched errors |

Main executable: `Builds/progression-final-20261113/Windrunner.exe`.
Fixture executable: `Builds/progression-final-fixture-20261113/Windrunner.exe`.
Both contain the same final progression assembly SHA-256: `8DC68FB1FBE0B32B68FED3A9D4EFC9F8383BB5E5D9CA4C5992DE7B63AD0B0961`.

Startup harnesses intentionally stop only their launched processes. They are not graceful-shutdown, gameplay-input, pixel, audio or cinematic-quality acceptance. Full failed iterations and corrections remain in ProgressionValidation.md/TestResults.json; no failed run is relabeled as passing.

The main build's warning subjects match the previous candidate except that three pre-existing CS0618 diagnostics were not repeated in the incremental final report (AirChimeTrial, AutumnPostProcessController, PlayerWindFeedback). Those sources were not fixed or changed. The remaining subjects are inherited importer/mesh and package/shader warnings. The fixture's five warnings are two Unity debug shaders and three FEEL optional Cinemachine/PostProcessing resource references. No new progression-owned warning was observed. Existing BuildWarnings.md retains the detailed prior classification.

## Performance and limits

One event matching 10 active quests measured 0.1927 ms median / 0.2570 ms p95; 100 quests measured 1.8436 / 4.9651 ms. Small durable file replacements measured 3.0752 / 4.8330 ms. These are separate command measurements, not combined frame costs or allocation/GPU claims. See ProgressionPerformance.md for exact workload and reproduction.

Synchronous durable commits can hitch under bursts; occurrence receipts and successful attempt IDs grow over long saves. Validate real content/save sizes and consider measured batching/asynchronous durability before large-scale shipping, preserving the transaction boundary. Dense Studio catalogs/journals and target-player CPU/GPU budgets remain unprofiled. Existing production capped/uncapped evidence is retained, not misrepresented as a new progression budget.

## Remaining content and acceptance

- All new manual acceptance steps in ProgressionAuthoring.md remain **not manually accepted**. Automated execution does not approve gameplay feel, Studio window usability, HUD readability, controller defaults, camera framing or audio.
- Final characters, story quests/dialogue, cinematic assets/NPC animation, localization text and progression FEEL/audio recipes are authored-content work. The two new semantic keys have no fabricated sounds or effects.
- Direct Timeline player bindings are rejected. Nested Control/Signal tracks and arbitrary UnityEvents require review; do not use them to create another pose authority or disable unrelated gameplay components.
- The implemented ownership scope is the project's existing single local player, with independent character relationships. Multiplayer/cloud sync and world streaming implementation are outside scope; stable IDs and registry-owned state permit future scene composition without quest instance IDs.
- External reward kinds beyond built-in unlock entitlements need idempotent consumers keyed by claim ID. Future equipment references are stored, but dagger mechanics/equipment implementation are intentionally absent.
- Unknown/retired content records survive; deliberate ID changes or incompatible rank/objective redesign need explicit content migration. Unsupported/corrupt saves fail closed, with a backup retained rather than silently reset.

The requested framework implementation is integrated and automated checks pass within this scope. This is an engineering release candidate, **not production-readiness or final narrative sign-off**. Continue with content authoring and the focused manual checklist rather than repeating Character Tuning hardening or a general architecture migration.

Architecture/save contracts: ProgressionFramework.md. Numbered authoring/manual instructions: ProgressionAuthoring.md. Evidence: ProgressionValidation.md, ProgressionPerformance.md, TestResults.json and ignored raw artifacts under Logs/ProductionMigration.
