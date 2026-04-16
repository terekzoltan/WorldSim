# Post-Wave7 Contact Funnel SMR Observability Plan

Status: review-adjusted proposal
Owner: SMR Analyst
Last updated: 2026-04-12

## 1. Purpose

The current SMR stack can already explain:

- high-level combat outcomes,
- AI goal/command/cause dominance,
- and broad backoff/routing pressure.

What it still cannot explain precisely is where combat conversion breaks inside the contact pipeline.

The goal of this slice is to add a runtime-owned, SMR-exported `contact` telemetry block that can answer:

- was a hostile actor sensed,
- did pursuit begin,
- did direct adjacent contact happen,
- did direct faction-combat damage happen,
- did group-combat pairing happen,
- did group-combat ticks actually produce damage or deaths,
- and did routing begin before meaningful battle damage.

This is an observability slice, not a gameplay feature slice.

## 2. Why This Is Not A Combined-Plan Epic

This plan is intentionally outside `Combined-Execution-Sequencing-Plan.md`.

Reasoning:

- it does not introduce a new gameplay wave,
- it extends the SMR/productization layer,
- and it is the natural follow-up to the already approved AI-debug-to-SMR observability slice.

Source-of-truth policy:

- this document is the implementation plan,
- implementation may later add an `AGENTS.md` note,
- but this is not a new wave marker.

## 3. Review-Driven Corrections From The Prior Draft

The prior contact-funnel draft was directionally correct, but semantically too optimistic about battle lifecycle identity.

This revised plan incorporates the key review corrections:

1. Split the funnel into two layers:
   - actor-contact funnel
   - group-battle funnel
2. Do not model persistent battle sessions in v1.
3. Do not use session-style `battleStarts`, `battlesWithDamage`, `battlesWithDeaths` names in v1.
4. Treat raid-path instrumentation as first-class.
5. Measure `routingBeforeDamage` in `World.TryStartRouting()`, not in generic `Person.BeginRouting()`.
6. Make first-occurrence ticks nullable (`int?` semantics), not sentinel-driven.

## 4. Runtime Constraint That Shapes The Design

The current runtime does **not** persist stable battle-session identity across ticks.

Important implementation detail:

- `World.Update()` clears active combat groups and battles each tick,
- then `ResolveGroupCombatPhase()` rebuilds them again in the same tick.

Implication:

- a v1 telemetry slice must not pretend these are durable battle sessions,
- so metrics must be phrased in terms of **pairings and battle ticks**, not stable battle lifecycles.

## 5. Approved v1 Scope

## 5.1 Mandatory runtime-owned `contact` block

### Actor-contact funnel

- `hostileSensed`
- `pursueStarts`
- `adjacentContacts`
- `factionCombatDamageEvents`
- `routingStarts`

### Group-battle funnel

- `battlePairings`
- `battleTicksWithDamage`
- `battleTicksWithDeaths`
- `routingBeforeDamage`

### First-occurrence ticks

- `firstHostileSenseTick`
- `firstPursueTick`
- `firstAdjacentContactTick`
- `firstFactionCombatDamageTick`
- `firstFactionCombatDeathTick`
- `firstBattlePairingTick`
- `firstBattleDamageTick`
- `firstBattleDeathTick`
- `firstRoutingTick`
- `firstRoutingBeforeDamageTick`

All first-occurrence fields are nullable (`int?`).

## 5.2 Cheap high-value extra included in v1

- `factionCombatDeaths`

Reason:

- this closes the semantic gap between direct duel contact and group-battle contact,
- so a run can clearly show “direct combat kills happened, but group-battle deaths did not” without ambiguity.

## 5.3 Mandatory instrumentation coverage

Both of these paths must be instrumented:

- `ExecuteFightAction()`
- `ExecuteRaidBorderAction()`

If needed, `FindRaidTarget()` can also contribute supporting instrumentation context, but the primary observable events should still be recorded at action execution time.

## 5.4 Explicit non-goals

This slice does not implement:

- persistent battle-session IDs,
- full event stream export,
- per-actor path history,
- UI/Graphics visualizations,
- pathfinding diagnostics,
- or gameplay behavior changes.

## 6. Metric Semantics

This section is the core contract. The names below are normative.

## 6.1 Actor-contact funnel

### `hostileSensed`

Definition:

- count of actor-tick events where a hostile actor is concretely found as a valid contact candidate in runtime action logic.

Expected primary instrumentation:

- `Person.ExecuteFightAction()` when `FindNearestHostilePerson(...) != null`
- `Person.ExecuteRaidBorderAction()` when nearby hostile-actor engage logic succeeds

Deduping rule:

- actor-per-tick dedupe

### `pursueStarts`

Definition:

- count of actor-tick events where a hostile actor is sensed and the actor enters a pursue/chase movement branch rather than immediately striking or falling back.

Expected primary instrumentation:

- `ExecuteFightAction()` when hostile is sensed and distance is greater than adjacent-contact range
- raid-origin pursue path when raid converts toward hostile actor pursuit

Deduping rule:

- actor-per-tick dedupe

### `adjacentContacts`

Definition:

- count of actor-tick events where hostile actor contact reaches adjacent melee/direct-combat range.

Expected primary instrumentation:

- `ExecuteFightAction()` hostile branch when adjacency/contact threshold is reached
- raid-to-fight conversion branch if it reaches direct hostile adjacency

Deduping rule:

- actor-per-tick dedupe

### `factionCombatDamageEvents`

Definition:

- count of direct faction-combat damage applications.

Expected primary instrumentation:

- `Person.ApplyCombatDamage(...)` when `source == "FactionCombat"`

### `factionCombatDeaths`

Definition:

- count of combat deaths caused by direct faction-combat path, not by group-combat path.

Expected primary instrumentation:

- runtime-owned death accounting tied to the combat damage source

Important:

- this metric is separate from group-battle death metrics on purpose.

### `routingStarts`

Definition:

- count of new routing transitions.

Expected primary instrumentation:

- `Person.BeginRouting(...)`, but only on state transition from not-routing to routing

Reason this is acceptable here:

- generic routing start does not need fragile battle context,
- unlike `routingBeforeDamage`, which does.

## 6.2 Group-battle funnel

### `battlePairings`

Definition:

- count of per-tick battle pairings created by `ResolveGroupCombatPhase()`.

Important semantic note:

- this is **not** a stable battle session count,
- it is a pairing/tick-level metric aligned to the current runtime architecture.

### `battleTicksWithDamage`

Definition:

- count of paired battle ticks where at least one group-combat damage event occurred.

Expected instrumentation:

- `World.ResolveBattleTick()` using tick-local damage tracking

### `battleTicksWithDeaths`

Definition:

- count of paired battle ticks where at least one combat death occurred during group-combat resolution.

Expected instrumentation:

- `World.ResolveBattleTick()` and/or world-owned post-damage accounting associated with the current battle pairing tick

### `routingBeforeDamage`

Definition:

- count of routing transitions started from group combat before any damage has been recorded for that current paired battle tick.

Expected instrumentation point:

- `World.TryStartRouting()`

Reason:

- this is the last stable place where battle/group context is still intact before assignment clearing and generic routing state take over.

## 6.3 First-occurrence ticks

All first-occurrence tick fields are nullable.

Semantics:

- `null` means “not observed in run”
- non-null means the first world tick where that funnel event type was observed

## 7. Runtime-Owned Architecture

## 7.1 Recommendation

Like the AI telemetry slice, this should be runtime-owned.

That means:

- runtime defines the canonical contact snapshot,
- `ScenarioRunner` only consumes it,
- semantic drift stays low,
- tests stay closer to simulation truth.

## 7.2 Proposed runtime records

New file:

- `WorldSim.Runtime/Diagnostics/ScenarioContactTelemetry.cs`

Suggested records:

- `ScenarioContactTelemetrySnapshot`
- `ScenarioContactTimelineSnapshot`

Keep the shape intentionally compact and additive.

## 8. File-Level Work Plan

## 8.1 `WorldSim.Runtime`

### New

- `WorldSim.Runtime/Diagnostics/ScenarioContactTelemetry.cs`

### Update

- `WorldSim.Runtime/Simulation/World.cs`
- `WorldSim.Runtime/Simulation/Person.cs`
- `WorldSim.Runtime/Simulation/Combat/RuntimeCombatState.cs`

### `World.cs`

Add cumulative contact counters and first-occurrence tick fields.

Suggested world-owned counters:

- `TotalHostileSensed`
- `TotalPursueStarts`
- `TotalAdjacentContacts`
- `TotalFactionCombatDamageEvents`
- `TotalFactionCombatDeaths`
- `TotalRoutingStarts`
- `TotalBattlePairings`
- `TotalBattleTicksWithDamage`
- `TotalBattleTicksWithDeaths`
- `TotalRoutingBeforeDamage`

Suggested world-owned first tick fields:

- `FirstHostileSenseTick`
- `FirstPursueTick`
- `FirstAdjacentContactTick`
- `FirstFactionCombatDamageTick`
- `FirstFactionCombatDeathTick`
- `FirstRoutingTick`
- `FirstBattlePairingTick`
- `FirstBattleDamageTick`
- `FirstBattleDeathTick`
- `FirstRoutingBeforeDamageTick`

Also add actor-per-tick dedupe sets, reset once per `World.Update()` tick:

- sensed dedupe
- pursue dedupe
- adjacent-contact dedupe

### `RuntimeCombatState.cs`

Add tick-local battle flags on runtime battle pairing objects:

- `HadDamageThisTick`
- `HadDeathThisTick`

These flags are explicitly pairing-tick scoped, not persistent session flags.

### `Person.cs`

Instrumentation points:

- `ExecuteFightAction()`
- `ExecuteRaidBorderAction()`
- `ApplyCombatDamage(...)`
- `BeginRouting(...)` for generic routing-start transition count only

## 8.2 `WorldSim.ScenarioRunner`

### Update

- `WorldSim.ScenarioRunner/Program.cs`

Work:

- consume runtime contact snapshot
- add additive nested `contact` block to `ScenarioRunResult`
- add additive compact `contact` block to `ScenarioTimelineSample`
- preserve backward compatibility

## 8.3 `WorldSim.ScenarioRunner.Tests`

### New

- `WorldSim.ScenarioRunner.Tests/ContactFunnelArtifactTests.cs`

### Update

- `ArtifactBundleTests.cs`
- `DrilldownTests.cs`

## 8.4 `WorldSim.Runtime.Tests`

### New

- `WorldSim.Runtime.Tests/ContactFunnelTelemetryTests.cs`

This should be the main semantic contract test file for the slice.

## 9. Exact Instrumentation Plan

## 9.1 `ExecuteFightAction()`

Instrument:

- hostile sensed
- pursue start
- adjacent contact

Do not assume this is the only contact origin.

## 9.2 `ExecuteRaidBorderAction()`

Instrument:

- hostile sensed for raid-origin hostile actor detection
- pursue start when raid converts toward hostile actor chase
- adjacent contact when raid-origin engage becomes direct actor contact

Reason:

- raid-origin contact is explicitly in scope and must not be invisible.

## 9.3 `ApplyCombatDamage(...)`

Instrument:

- `FactionCombat` damage events
- direct-duel death attribution as needed for `factionCombatDeaths`

## 9.4 `ResolveGroupCombatPhase()`

Instrument:

- `battlePairings`
- `firstBattlePairingTick`

## 9.5 `ResolveBattleTick()`

Instrument:

- `battleTicksWithDamage`
- `battleTicksWithDeaths`
- `firstBattleDamageTick`
- `firstBattleDeathTick`

These are per-pairing-tick counters, aligned with current runtime architecture.

## 9.6 `TryStartRouting()`

Instrument:

- `routingBeforeDamage`

Reason:

- this is the stable point where battle/group context still exists,
- and where the semantics are least misleading.

## 10. ScenarioRunner Artifact Shape

## 10.1 Run-level shape

Each run gets an additive nested `contact` block.

Suggested shape:

```json
"contact": {
  "hostileSensed": 142,
  "pursueStarts": 97,
  "adjacentContacts": 44,
  "factionCombatDamageEvents": 18,
  "factionCombatDeaths": 2,
  "routingStarts": 9,
  "battlePairings": 7,
  "battleTicksWithDamage": 5,
  "battleTicksWithDeaths": 2,
  "routingBeforeDamage": 3,
  "firstHostileSenseTick": 142,
  "firstPursueTick": 166,
  "firstAdjacentContactTick": 198,
  "firstFactionCombatDamageTick": 198,
  "firstFactionCombatDeathTick": 641,
  "firstRoutingTick": 812,
  "firstBattlePairingTick": 224,
  "firstBattleDamageTick": 224,
  "firstBattleDeathTick": 641,
  "firstRoutingBeforeDamageTick": 233
}
```

## 10.2 Timeline shape

Each timeline sample gets a compact cumulative `contact` block.

Suggested shape:

```json
"contact": {
  "hostileSensed": 61,
  "pursueStarts": 38,
  "adjacentContacts": 12,
  "factionCombatDamageEvents": 4,
  "factionCombatDeaths": 0,
  "routingStarts": 3,
  "battlePairings": 2,
  "battleTicksWithDamage": 1,
  "battleTicksWithDeaths": 0,
  "routingBeforeDamage": 1
}
```

No first-occurrence fields in timeline samples.

## 11. Test Plan

## 11.1 Runtime semantic tests

Mandatory coverage:

1. fight path reports hostile sensed and pursue start
2. fight path reports adjacent contact
3. raid path reports hostile sensed / pursue / adjacent contact
4. faction-combat damage events are counted
5. faction-combat deaths are counted separately
6. battle pairings are counted from group combat phase
7. battle ticks with damage are counted
8. battle ticks with deaths are counted
9. routing-before-damage is counted from `TryStartRouting()`
10. actor-contact events are actor-per-tick deduped
11. first-occurrence ticks stay null until observed

## 11.2 ScenarioRunner artifact tests

Mandatory coverage:

1. `summary.json` contains run-level `contact`
2. `runs/*.json` contains run-level `contact`
3. `timeline.json` contains compact `contact`
4. repeated same-seed runs produce deterministic `contact` payloads
5. nullable first-occurrence fields serialize correctly

## 12. Evidence Run Plan After Implementation

## 12.1 Schema smoke

Run:

- `planner-compare-contact-funnel-smoke-001`

Goal:

- verify schema and readability

## 12.2 Golden bad lane diagnostic

Primary run:

- `standard-default / htn / 101`

Optional compare:

- `standard-fastmove / htn / 101`

Goal:

- identify exactly where the residual funnel weakens:
  - sense
  - pursue
  - adjacent contact
  - battle pairing
  - battle damage tick
  - battle death tick
  - routing before damage

## 12.3 Broader regression package

After the micro lane is useful:

- `medium-*`
- `standard-*`
- `simple/goap/htn`
- seed `101`

## 13. Acceptance Criteria

The slice is accepted when all of the following hold:

1. Runtime exports a contact snapshot owned by runtime, not reconstructed in `ScenarioRunner`.
2. `ScenarioRunner` exports additive nested `contact` blocks at run level and timeline level.
3. Actor-contact and group-battle metrics are semantically distinct.
4. Raid-origin hostile contact is observable in the same funnel.
5. `routingBeforeDamage` is measured from world combat logic, not generic routing state cleanup.
6. First-occurrence fields are nullable and deterministic.
7. A residual bad lane can be diagnosed more precisely than with the current AI telemetry alone.

## 14. Deferred Extensions

Explicitly deferred until after v1:

- persistent battle-session identity,
- per-actor target history,
- path trace export,
- structure-damage funnel metrics,
- per-command/per-goal contact funnel slicing.

If persistent battle sessions are needed later, that should be treated as a larger runtime capability change, not hidden inside this lightweight telemetry slice.
