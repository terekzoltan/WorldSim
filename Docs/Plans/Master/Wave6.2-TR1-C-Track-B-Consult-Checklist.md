# Wave 6.2 TR1-C - Track B Consult Checklist (Runtime-Fact Boundary)

Purpose:
- Provide a concrete Track B consult package for Wave 6.2 / TR1-C Step 3.
- Keep the C# bridge boundary stable while Java internals move to `tools.refinery` layered artifacts.

Scope:
- This is a consult artifact, not a runtime/adapter implementation task.
- Production behavior is unchanged by this document.

## 1) Readiness and sequencing

- Step status: TR1-B is complete, TR1-C is complete on Track D side.
- Consult target: confirm runtime-fact ownership and boundary stability.
- Critical constraint: do not introduce new C# formal semantics during TR1-C consult.

References:
- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
- `Docs/Plans/Master/Tools-Refinery-Migration-Plan.md`
- `Docs/Plans/Master/Tools-Refinery-Agent-Guide.md`

## 2) Canonical consult baseline (Track B position)

Track B keeps C# runtime as deterministic producer/consumer of bridge facts and commands.

Track B does:
- export stable runtime facts from C# snapshot,
- consume validated bridge output via adapter/runtime commands,
- avoid duplicating Java formal-model invariants in C#.

Track B does not:
- re-implement Refinery/model-layer semantics in C#,
- treat prompt-only fields as formal runtime facts.

## 3) Runtime-fact boundary table (proposed)

Use this table as the TR1-C consult decision baseline.

| Area | Field | Status now | Proposed TR1-C consult status | Notes |
|---|---|---|---|---|
| request root | `tick` | present | stable runtime fact | Java `DirectorRuntimeFacts.tick` consumes this directly |
| constraints | `maxBudget` | present (root) | stable runtime fact | keep root canonical |
| constraints | `outputMode` | present (root) | stable bridge control | not runtime fact, but stable request control |
| snapshot.world | `colonyCount` | present | stable runtime fact | current safest colony-count source |
| snapshot.director | `beatCooldownRemainingTicks` | present | stable runtime fact | consumed by Java mapper/prompt paths |
| snapshot.director | `remainingInfluenceBudget` | present | stable runtime fact | consumed by mapper/LLM fallback paths |
| snapshot.director | `activeBeats[]` | present | stable runtime fact | keep `beatId/severity/remainingTicks` |
| snapshot.director | `activeDirectives[]` | present | stable runtime fact | keep `colonyId/directive` |
| snapshot.director | `colonyPopulation` | present | transitional only (not canonical) | must not substitute colony count as formal fact |
| snapshot.world | `storyBeatCooldownTicks` | absent in C# | legacy fallback only | Java fallback path should remain transitional |
| constraints | `constraints.director.*` | Java fallback exists | transitional only | C# emits root constraints; keep nested fallback non-canonical |
| snapshot.director | `effectiveOutputMode/source/stage` | present | observability only | runtime/HUD debug surface, not formal runtime facts |
| snapshot.director | `lastCheckpointBudgetUsed/lastBudgetCheckpointTick` | present | observability only | useful diagnostics, not required formal facts |
| snapshot.director | `foodReservesPct/moraleAvg/economyOutput` | present | prompt-context only for now | do not promote to formal facts in TR1-C |
| snapshot.director | `activeDomainModifiers/activeGoalBiases` | present | transitional debug/context | do not freeze as runtime-fact contract yet |

## 4) Boundary risks to call out in consult

1. `colonyPopulation` vs `colonyCount` semantic drift
- Risk: fallback uses a population value where colony-count semantics are required.
- Track B ask: formal runtime fact must use true colony count.

2. Dual-source cooldown drift
- Risk: Java still supports legacy `world.storyBeatCooldownTicks` path.
- Track B ask: maintain backward compatibility if needed, but mark this route transitional.

3. Typed-contract mismatch
- Risk: `WorldSim.Contracts.V2.DirectorSnapshotData` is narrower than actual runtime JSON envelope.
- Track B ask: do not claim typed DTO as canonical boundary until explicitly aligned.

4. Prompt-context vs formal-runtime facts
- Risk: fields consumed in prompt text are mistaken as formal runtime facts.
- Track B ask: distinguish clearly between model-runtime facts and optional narrative context.

## 5) Decisions to lock during TR1-C consult

- D1: Canonical colony-count source during TR1/TR2 transition.
  - Recommended: keep `snapshot.world.colonyCount` canonical until explicit promotion is implemented and tested.

- D2: Canonical budget source priority.
  - Recommended: `constraints.maxBudget` first, then `snapshot.director.remainingInfluenceBudget`, then configured default.

- D3: Canonical cooldown source.
  - Recommended: `snapshot.director.beatCooldownRemainingTicks`; keep `world.storyBeatCooldownTicks` as transitional fallback only.

- D4: Constraint shape policy.
  - Recommended: root `constraints.*` is canonical; nested `constraints.director.*` remains compatibility fallback only.

- D5: Formal runtime-fact minimum set for TR2-A handoff prep.
  - Recommended minimum: `tick`, `colonyCount`, `beatCooldownRemainingTicks`, `remainingInfluenceBudget`, `activeBeats`, `activeDirectives`.

## 6) Handoff output expected from consult

After consult close, produce a short note containing:
- agreed canonical runtime-fact list,
- agreed transitional-only fallback list,
- explicit ownership split (C# exports facts, Java normalizes/maps facts),
- TR2-A mapper input assumptions.

## 7) Non-goals for this step

- No runtime/adapter command-surface rewrite.
- No forced switch to typed C# snapshot DTO in this step.
- No migration of Java prompt-builder to strict formal runtime-fact feed in this step.
- No new gameplay semantics in C# to mirror Java model internals.
