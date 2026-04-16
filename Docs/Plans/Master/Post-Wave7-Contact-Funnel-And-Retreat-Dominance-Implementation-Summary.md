# Post-Wave7 Contact Funnel And Retreat Dominance Implementation Summary

Status: implemented and evidence-backed
Owner: SMR Analyst
Audience: Meta Coordinator
Last updated: 2026-04-15

## 1. Purpose

This document is the implementation and evidence summary for the two post-Wave7 SMR plans below:

- `Docs/Plans/Master/Post-Wave7-Contact-Funnel-SMR-Observability-Plan.md`
- `Docs/Plans/Master/Post-Wave7-Retreat-Dominance-SMR-Followup-Plan.md`

The goal is to give the Meta Coordinator one compact but high-context handoff document that explains:

- what was actually implemented,
- where the code changed,
- what was verified,
- what the new telemetry means,
- what the follow-up runs proved,
- and what the next engineering step should be.

## 2. Executive Summary

The work completed in four stages.

Stage 1 implemented a new runtime-owned SMR `contact` telemetry slice.

- The slice is additive.
- It does not change gameplay behavior.
- It exports actor-contact funnel signals, group-battle funnel signals, and nullable first-occurrence ticks.
- The telemetry is owned by `WorldSim.Runtime`, then consumed by `WorldSim.ScenarioRunner` for run-level and drilldown/timeline artifacts.

Stage 2 executed the parked retreat-dominance follow-up using the new telemetry.

- The `standard` micro lane and `medium` control lane were both run with `htn / seed 101 / 2400 ticks`.
- The new funnel evidence showed that the residual `standard` retreat-heaviness was not primarily an early-routing problem.
- The strongest signal was much later and much weaker group-battle pairing and battle-damage realization on the larger `standard` topology.

Stage 3 landed a retained narrow runtime fix for large-topology contact realization.

- The retained fix is person-level and large-topology-only.
- It improves hostile chase/contact realization in `Fight`, `RaidBorder`, and recent-hostile pursuit paths.
- A rejected broad world-side topology expansion was intentionally not kept because it regressed the bad lane.

Stage 4 ran the post-fix evidence passes.

- Post-fix `standard` reruns showed better direct actor-contact and stronger combat follow-through.
- `medium` control reruns stayed stable.
- A small full-planner confirm matrix (`simple/goap/htn`) showed the remaining `standard` vs `medium` gap is cross-planner rather than HTN-only.
- A later baseline-reconcile rerun proved the current workspace matches the retained-fix evidence baseline.

Bottom line:

- The contact-funnel slice is now available and useful.
- The residual `standard` problem was narrowed from a vague “retreat-heavy” observation to a concrete large-topology combat-realization problem.
- The retained large-topology fix is part of the canonical baseline now.
- The remaining `standard` vs `medium` quality gap is still real, but it is no longer best described as HTN-only or routing-first.

## 3. Source Plans And Outputs

Primary design plans:

- `Docs/Plans/Master/Post-Wave7-Contact-Funnel-SMR-Observability-Plan.md`
- `Docs/Plans/Master/Post-Wave7-Retreat-Dominance-SMR-Followup-Plan.md`

Related prior context:

- `Docs/Plans/Master/Post-Wave7-AI-Debug-SMR-Observability-Plan.md`
- `Docs/Plans/Master/Post-Wave7-Behavior-Diagnosis-Decision-Note.md`
- `Docs/Plans/Master/Post-Wave7-Contact-FollowThrough-Fix-Plan.md`
- `Docs/Plans/Master/SMR-Minimum-Ops-Checklist.md`

Cross-session notes updated:

- `AGENTS.md`

Primary evidence artifact bundles created during this work:

- `.artifacts/smr/contact-funnel-artifact-smoke/`
- `.artifacts/smr/contact-funnel-drilldown-smoke/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-002/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-medium-control-001/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-005/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-medium-control-003/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-full-planner-confirm-001/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-reconcile-001/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-medium-reconcile-001/`

## 4. Contact Funnel Slice

## 4.1 Design Goal

The contact-funnel slice was created because the pre-existing SMR stack could already answer:

- high-level combat counters,
- AI goal and cause dominance,
- broad backoff and routing pressure,

but it still could not answer where combat conversion was breaking inside the runtime pipeline.

The new slice was intended to answer questions such as:

- did the actor actually sense a hostile,
- did the actor start pursuit,
- did adjacent direct contact happen,
- did direct faction combat damage happen,
- did group combat pairing happen,
- did paired battle ticks produce damage or deaths,
- and did routing begin before meaningful battle damage.

## 4.2 Review-Corrected Semantics

The implementation follows the review-adjusted semantics rather than the earlier draft.

The most important semantic corrections were:

- split the funnel into actor-contact and group-battle layers,
- do not model persistent battle sessions,
- keep the group-battle metrics pairing/tick-oriented,
- make raid-path instrumentation first-class,
- measure `routingBeforeDamage` in `World.TryStartRouting(...)`,
- keep all `first*Tick` fields nullable.

This matters because `World.Update()` clears active battle/group state every tick and rebuilds it again in the same tick. A session-style battle model would have been misleading in the current architecture.

## 4.3 Files Changed For The Contact Funnel Slice

| File | Change | Why |
|---|---|---|
| `WorldSim.Runtime/Diagnostics/ScenarioContactTelemetry.cs` | New runtime-owned records | Canonical additive telemetry shape |
| `WorldSim.Runtime/Simulation/World.cs` | New counters, first-tick fields, per-tick dedupe sets, snapshot builder, battle/routing instrumentation | Runtime is the source of truth |
| `WorldSim.Runtime/Simulation/Person.cs` | Fight/raid/direct-combat/routing instrumentation | Actor-contact and direct duel coverage |
| `WorldSim.Runtime/Simulation/Combat/RuntimeCombatState.cs` | Added battle tick flags | Battle-tick damage/death classification |
| `WorldSim.ScenarioRunner/Program.cs` | Run-level and timeline `contact` export | Artifact consumption |
| `WorldSim.Runtime.Tests/ContactFunnelTelemetryTests.cs` | New runtime semantic tests | Protect semantics |
| `WorldSim.ScenarioRunner.Tests/ContactFunnelArtifactTests.cs` | New artifact schema tests | Protect runner export shape |
| `WorldSim.ScenarioRunner.Tests/ArtifactBundleTests.cs` | Extended existing artifact assertions | Ensure additive schema coverage |
| `WorldSim.ScenarioRunner.Tests/DrilldownTests.cs` | Extended timeline assertions | Ensure drilldown coverage |
| `AGENTS.md` | Added session note | Cross-session handoff |

## 4.4 Runtime Data Model That Was Added

The runtime-owned telemetry shape lives in:

- `WorldSim.Runtime/Diagnostics/ScenarioContactTelemetry.cs`

It introduces two records:

- `ScenarioContactTelemetrySnapshot`
- `ScenarioContactTimelineSnapshot`

Run-level `ScenarioContactTelemetrySnapshot` fields:

- `HostileSensed`
- `PursueStarts`
- `AdjacentContacts`
- `FactionCombatDamageEvents`
- `FactionCombatDeaths`
- `RoutingStarts`
- `BattlePairings`
- `BattleTicksWithDamage`
- `BattleTicksWithDeaths`
- `RoutingBeforeDamage`
- `FirstHostileSenseTick`
- `FirstPursueTick`
- `FirstAdjacentContactTick`
- `FirstFactionCombatDamageTick`
- `FirstFactionCombatDeathTick`
- `FirstBattlePairingTick`
- `FirstBattleDamageTick`
- `FirstBattleDeathTick`
- `FirstRoutingTick`
- `FirstRoutingBeforeDamageTick`

Timeline `ScenarioContactTimelineSnapshot` fields:

- `HostileSensed`
- `PursueStarts`
- `AdjacentContacts`
- `FactionCombatDamageEvents`
- `FactionCombatDeaths`
- `RoutingStarts`
- `BattlePairings`
- `BattleTicksWithDamage`
- `BattleTicksWithDeaths`
- `RoutingBeforeDamage`

All run-level first-occurrence fields use nullable `int?` semantics.

## 4.5 Runtime Implementation Details

The core runtime implementation lives in `WorldSim.Runtime/Simulation/World.cs`.

Main additions:

- `BuildScenarioContactTelemetrySnapshot()` builds the canonical runtime-owned snapshot.
- private cumulative counters were added for all funnel metrics.
- private nullable first-tick fields were added for all first-occurrence metrics.
- three per-tick actor dedupe sets were added:
  - hostile sensed dedupe,
  - pursue dedupe,
  - adjacent-contact dedupe.
- those dedupe sets are cleared at the start of every `World.Update()` tick.

The event-report methods added to `World` are:

- `ReportContactHostileSensed(Person actor)`
- `ReportContactPursueStart(Person actor)`
- `ReportContactAdjacentContact(Person actor)`
- `ReportContactFactionCombatDamage()`
- `ReportContactFactionCombatDeath()`
- `ReportContactRoutingStart()`
- `ReportContactBattlePairing()`
- `ReportContactBattleTickWithDamage()`
- `ReportContactBattleTickWithDeath()`
- `ReportContactRoutingBeforeDamage()`

These methods are intentionally tiny and single-purpose so the semantics remain easy to audit.

## 4.6 Person-Side Instrumentation Details

The actor-side instrumentation lives in `WorldSim.Runtime/Simulation/Person.cs`.

### `TryAttackOrPursueHostilePerson(...)`

This became the main entry point for actor-contact instrumentation.

What it now records:

- `hostileSensed` when a hostile candidate is found,
- `pursueStarts` when the hostile is not adjacent and the actor enters a chase branch,
- `adjacentContacts` when the hostile is already adjacent and direct engagement begins.

This path covers both:

- `ExecuteFightAction()`
- `ExecuteRaidBorderAction()`

because both already flow through `TryAttackOrPursueHostilePerson(...)`.

### `ApplyCombatDamage(World world, float amount, string source)`

This now records direct duel metrics only when `source == "FactionCombat"`.

What it now records:

- `FactionCombatDamageEvents`
- `FactionCombatDeaths`

This deliberately does not conflate direct duel damage with `GroupCombat` damage.

### `BeginRouting(...)`

`BeginRouting(...)` now returns `bool` instead of `void`.

Return meaning:

- `true` = this call caused a new routing transition
- `false` = the actor was already routing

This change was needed so `World.TryStartRouting(...)` can count only new routing transitions instead of repeated re-assertions.

## 4.7 Group-Battle Instrumentation Details

The group-battle instrumentation is split across:

- `WorldSim.Runtime/Simulation/World.cs`
- `WorldSim.Runtime/Simulation/Combat/RuntimeCombatState.cs`

### Pairing creation

`ResolveGroupCombatPhase()` now records `battlePairings` every time a new `RuntimeBattleState` is created.

### Per-battle-tick damage and death

`RuntimeBattleState` now contains:

- `HadDamageThisTick`
- `HadDeathThisTick`

`ResolveBattleTick(...)` uses these flags to record:

- `battleTicksWithDamage`
- `battleTicksWithDeaths`

This is intentionally pairing-tick scoped, not session scoped.

### Routing-before-damage

`TryStartRouting(...)` was changed from:

- `TryStartRouting(RuntimeCombatGroup group)`

to:

- `TryStartRouting(RuntimeBattleState battle, RuntimeCombatGroup group)`

This preserves access to the current battle tick context so `routingBeforeDamage` can be measured where the plan required it.

The implementation now:

- starts routing only when `BeginRouting(...)` reports a new transition,
- increments `routingStarts` on each new transition,
- increments `routingBeforeDamage` only when that new routing transition happens before any damage was recorded for the current battle tick.

## 4.8 Important Semantic Caveat

One subtle but important point for future readers:

- actor-contact funnel metrics only instrument fight/raid action execution paths,
- but group-battle pairings can still arise through runtime combat-group eligibility without those actor-contact metrics firing first.

This is why a run can legitimately show:

- `hostileSensed = 0`
- `pursueStarts = 0`
- `adjacentContacts = 0`

while still showing non-zero:

- `battlePairings`
- `battleTicksWithDamage`
- `battleTicksWithDeaths`

This exact pattern appears later in the `standard-default / htn / 101` follow-up evidence.

That is not a bug in the telemetry implementation.

It is a direct result of the funnel intentionally separating:

- actor-driven direct contact paths,
- from group-battle pairing paths.

## 4.9 ScenarioRunner Integration

The runner integration was implemented in `WorldSim.ScenarioRunner/Program.cs`.

Changes:

- `BuildScenarioRunResult(...)` now consumes `world.BuildScenarioContactTelemetrySnapshot()` and stores it in `ScenarioRunResult.Contact`.
- `BuildTimelineSample(...)` now consumes `world.BuildScenarioContactTelemetrySnapshot().ToTimelineSnapshot()` and stores it in `ScenarioTimelineSample.Contact`.
- `ScenarioRunResult` and `ScenarioTimelineSample` were extended additively with `Contact` fields.

This preserved the same architectural rule used by the AI telemetry slice:

- runtime owns the data,
- runner serializes it,
- runner does not reconstruct combat semantics from scratch.

## 4.10 Contact Slice Verification

### Runtime semantic tests

New file:

- `WorldSim.Runtime.Tests/ContactFunnelTelemetryTests.cs`

Coverage implemented there:

- empty world returns empty snapshot,
- fight path reports hostile sense and pursue start,
- fight path reports adjacent contact and direct faction damage,
- raid path reports hostile sense, pursue, and adjacent contact,
- battle pairing, battle-damage tick, and battle-death tick are counted,
- `TryStartRouting(...)` counts `routingBeforeDamage` in battle context,
- actor-contact events are actor-per-tick deduped.

Executed command:

```text
dotnet test "WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj" --filter "FullyQualifiedName~ContactFunnelTelemetryTests"
```

Observed result:

- green

### Runner artifact tests

New file:

- `WorldSim.ScenarioRunner.Tests/ContactFunnelArtifactTests.cs`

Coverage implemented there:

- run-level `contact` block exists,
- timeline/drilldown `contact` block exists,
- repeated same-seed runs keep deterministic `contact` JSON.

Extended existing tests:

- `WorldSim.ScenarioRunner.Tests/ArtifactBundleTests.cs`
- `WorldSim.ScenarioRunner.Tests/DrilldownTests.cs`

Executed verification:

```text
dotnet build "WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj"
dotnet build "WorldSim.sln"
```

Observed result:

- both green

Important note:

- full process-spawn `dotnet test` execution for the runner-side contact tests timed out in this environment,
- so runner verification was completed by build plus manual artifact smoke instead of a full green `dotnet test` run.

### Manual runner artifact smoke

Artifact smoke bundle:

- `.artifacts/smr/contact-funnel-artifact-smoke/`

What it proved:

- run-level `summary.json` includes the new `contact` block,
- all expected fields serialize,
- nullable first-tick fields serialize as `null` when no event is observed.

Drilldown smoke bundle:

- `.artifacts/smr/contact-funnel-drilldown-smoke/`

What it proved:

- `drilldown/index.json` is written normally,
- `timeline.json` includes the compact cumulative `contact` block.

## 5. Retreat Dominance Follow-Up

## 5.1 Goal Of The Follow-Up

The retreat-dominance follow-up was intended to answer one narrow question after the broad threat fix and contact follow-through fix had already landed:

- why does the larger `standard` topology still look more retreat-heavy than the smaller `medium` control lane?

The plan explicitly said this should happen only after the contact funnel telemetry existed.

That dependency is now satisfied.

## 5.2 Actual Run Package Executed

The plan suggested:

- `planner-compare-wave7-retreat-dominance-standard-001`
- `planner-compare-wave7-retreat-dominance-medium-control-001`

Actual executed follow-up bundles:

- `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-002/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-medium-control-001/`

The `standard` suffix became `002` because `001` had already been used earlier for a pre-contact-funnel sanity-check run.

Exact matrix for both bundles:

- seed: `101`
- planner: `htn`
- ticks: `2400`
- drilldown: `true`
- sample interval: `25`

Standard bundle configs:

- `standard-default`
- `standard-fastmove`

Medium control bundle configs:

- `medium-default`
- `medium-fastmove`

## 5.3 Bundle Health

Both bundles completed cleanly.

Standard bundle health:

- `exitCode = 0`
- `anomalyCount = 0`

Medium control bundle health:

- `exitCode = 0`
- `anomalyCount = 0`

## 5.4 Key Metrics From The Follow-Up

| Lane | hostileSensed | pursueStarts | adjacentContacts | factionCombatDamageEvents | factionCombatDeaths | routingStarts | battlePairings | battleTicksWithDamage | battleTicksWithDeaths | routingBeforeDamage | firstBattlePairingTick | firstAdjacentContactTick | retreat_refuge | noProgressBackoffFlee | combatEngagements | combatDeaths |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `standard-default` | 0 | 0 | 0 | 0 | 0 | 17 | 73 | 73 | 4 | 0 | 339 | null | 53 | 183 | 118 | 4 |
| `standard-fastmove` | 20 | 16 | 4 | 4 | 1 | 29 | 65 | 65 | 1 | 0 | 778 | 1274 | 44 | 156 | 118 | 2 |
| `medium-default` | 14 | 9 | 5 | 5 | 0 | 32 | 404 | 404 | 21 | 0 | 55 | 204 | 22 | 78 | 659 | 20 |
| `medium-fastmove` | 62 | 42 | 20 | 20 | 1 | 70 | 421 | 421 | 21 | 0 | 60 | 233 | 17 | 93 | 741 | 23 |

## 5.5 Main Reading Of The Results

### Early routing is not the main residual issue

The clearest negative result is:

- `routingBeforeDamage = 0` in all four lanes

This means the residual `standard` retreat-heaviness is not best explained by “battles start but routing triggers before damage.”

That specific hypothesis is now materially weakened.

### The strongest gap is pairing volume and timing

The biggest gap is not actor-contact alone.

It is the much later and much weaker group-battle pairing profile on `standard`.

Comparison:

- `battlePairings`
  - `standard`: `73`, `65`
  - `medium`: `404`, `421`
- `firstBattlePairingTick`
  - `standard`: `339`, `778`
  - `medium`: `55`, `60`

This is the strongest evidence in the whole package.

The larger-topology `standard` lane is not just a little slower.

It is dramatically later and dramatically sparser in battle-pairing realization.

### Fastmove helps direct actor-contact, but does not close the real gap

`standard-fastmove` improves direct actor-contact versus `standard-default`:

- `hostileSensed`: `0 -> 20`
- `pursueStarts`: `0 -> 16`
- `adjacentContacts`: `0 -> 4`
- `factionCombatDamageEvents`: `0 -> 4`

But the main gap remains:

- `battlePairings` is still only `65`
- first battle pairing is even later at `778`
- retreat-heavy AI visibility still remains high

So fast movement helps some direct-contact realization, but it does not solve the topology-scale pairing-density problem.

### Medium lane acts as a healthy control

The `medium` control bundle confirms that the runtime can realize contact and battle flow strongly under a smaller topology.

Key signals:

- much earlier first pairing,
- far higher pairing volume,
- far higher battle-damage volume,
- much lower `retreat_refuge` dominance,
- much lower `noProgressBackoffFlee` pressure.

This strongly supports a topology-sensitive runtime explanation rather than a pure planner explanation.

### Why `standard-default` can show zero actor-contact but non-zero group battles

This is an important interpretation note.

`standard-default` showed:

- `hostileSensed = 0`
- `pursueStarts = 0`
- `adjacentContacts = 0`

while still showing:

- `battlePairings = 73`
- `battleTicksWithDamage = 73`

This is not contradictory.

It comes from the deliberate telemetry design split:

- actor-contact metrics track fight/raid action execution,
- group-battle metrics track combat-group pairing and battle resolution.

In other words, the runtime can still form combat groups and paired battles through its combat-group eligibility and spatial logic even if the direct fight/raid actor-contact path does not dominate the same lane.

That distinction turned out to be useful rather than confusing, because it exposed that the real standard-lane bottleneck is deeper in the pairing-density layer.

## 5.6 Follow-Up Verdict

The follow-up plan asked for a verdict of the form:

- is the problem sense,
- pursue,
- contact,
- pairing,
- routing-before-damage,
- or visibility-heavy retreat despite healthy combat flow?

Pre-fix verdict from the first contact-funnel follow-up:

- the residual `standard` retreat-heaviness was not primarily `routing-before-damage`,
- it was best explained by weaker and later combat realization on the larger topology,
- and the highest-value next engineering direction was a narrow runtime fix around large-topology contact realization.

## 5.7 Retained Runtime Fix

After the follow-up evidence, a targeted runtime fix was implemented and retained as baseline.

Files changed:

- `WorldSim.Runtime/Simulation/World.cs`
- `WorldSim.Runtime/Simulation/Person.cs`
- `WorldSim.Runtime.Tests/ContactFollowThroughTests.cs`

What changed in code:

- `World` now exposes `IsLargeCombatTopology` as an internal runtime fact using the explicit map-area gate:
  - `Width * Height >= 18000`
- `Person.ExecuteFightAction()` now uses a topology-aware hostile chase radius.
- `Person.ExecuteRaidBorderAction()` now uses a topology-aware hostile-contact radius.
- `Person.TryGetRecentHostilePursuitTarget(...)` now uses a topology-aware recent-hostile pursuit radius.

Concrete retained behavior:

- on large topologies only, fight/raid chase radius gets a small `+2` extension,
- on large topologies only, recent-hostile pursuit radius also gets a small `+2` extension,
- medium/smaller topology remains on the old path.

Important boundary:

- this is not telemetry-driven gameplay,
- and it does not key behavior off `ReportContact*` or exported `contact` metrics.

It only changes the underlying combat/contact conditions in the `Person` runtime paths.

### 5.7.1 Rejected intermediate attempt

An intermediate world-side topology expansion was tried and rejected.

That attempt widened:

- combat eligibility radius,
- combat clustering radius,
- and pairing window.

It was not retained because it regressed the `standard-default` bad lane during validation.

This is important for future work:

- the canonical baseline is the retained person-level large-topology fix,
- not any broad world-side topology rewrite.

### 5.7.2 Runtime guard tests for the topology gate

The retained topology gate is protected by focused runtime tests.

Relevant test coverage now includes:

- `LargeTopology_FightAction_PursuesHostileAtExtendedDistance()`
- `MediumTopology_FightAction_DoesNotUseExtendedChaseRadius()`
- `LargeTopology_PursuesRecentHostileAcrossExtendedMemoryRadius()`

These are intentionally runtime-centered and prove that:

- the large-topology path does change,
- the medium path does not silently inherit the change.

## 5.8 Post-Fix Evidence

### 5.8.1 Standard lane post-fix rerun

Post-fix standard bundle:

- `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-005/`

Compared against the pre-fix contact-funnel follow-up baseline:

- `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-002/`

Important `standard-default` deltas (`002 -> 005`):

- `combatEngagements`: `118 -> 142`
- `battlePairings`: `73 -> 76`
- `battleTicksWithDamage`: `73 -> 76`
- `battleTicksWithDeaths`: `4 -> 3`
- `hostileSensed`: `0 -> 27`
- `pursueStarts`: `0 -> 14`
- `adjacentContacts`: `0 -> 13`
- `factionCombatDamageEvents`: `0 -> 13`
- `factionCombatDeaths`: `0 -> 1`
- `retreat_refuge`: `53 -> 43`
- `noProgressBackoffFlee`: `183 -> 171`

Important `standard-fastmove` deltas (`002 -> 005`):

- `combatEngagements`: `118 -> 171`
- `battlePairings`: `65 -> 106`
- `battleTicksWithDamage`: `65 -> 106`
- `battleTicksWithDeaths`: `1 -> 4`
- `firstBattlePairingTick`: `778 -> 262`
- `noProgressBackoffFlee`: `156 -> 142`

Interpretation:

- the retained fix improved direct actor-contact realization and combat follow-through in the `standard` lane,
- without any sign that `routingBeforeDamage` became the new bottleneck.

### 5.8.2 Medium control post-fix rerun

Post-fix medium control bundle:

- `.artifacts/smr/planner-compare-wave7-retreat-dominance-medium-control-003/`

Observed result:

- `medium-default` and `medium-fastmove` stayed materially aligned with the earlier control evidence,
- which is exactly what the explicit topology gate was intended to preserve.

## 5.9 Full-Planner Confirm Matrix

After the retained large-topology contact fix landed, the previously optional full-planner confirm matrix was executed.

Artifact bundle:

- `.artifacts/smr/planner-compare-wave7-retreat-dominance-full-planner-confirm-001/`

Matrix:

- planners: `simple`, `goap`, `htn`
- seed: `101`
- configs:
  - `medium-default`
  - `medium-fastmove`
  - `standard-default`
  - `standard-fastmove`
- ticks: `2400`
- drilldown: `true`

Bundle health:

- `exitCode = 0`
- `anomalyCount = 0`

Representative contact/combat values from the matrix:

| Config | Planner | hostileSensed | pursueStarts | adjacentContacts | battlePairings | combatEngagements | retreat_refuge |
|---|---|---:|---:|---:|---:|---:|---:|
| `medium-default` | `simple` | 33 | 15 | 18 | 389 | 663 | 25 |
| `medium-default` | `goap` | 33 | 15 | 18 | 389 | 663 | 25 |
| `medium-default` | `htn` | 14 | 9 | 5 | 404 | 659 | 22 |
| `medium-fastmove` | `simple` | 6 | 4 | 2 | 431 | 732 | 16 |
| `medium-fastmove` | `goap` | 6 | 4 | 2 | 431 | 732 | 16 |
| `medium-fastmove` | `htn` | 62 | 42 | 20 | 421 | 741 | 17 |
| `standard-default` | `simple` | 9 | 5 | 4 | 44 | 78 | 46 |
| `standard-default` | `goap` | 9 | 5 | 4 | 44 | 78 | 46 |
| `standard-default` | `htn` | 27 | 14 | 13 | 76 | 142 | 43 |
| `standard-fastmove` | `simple` | 6 | 2 | 4 | 125 | 199 | 45 |
| `standard-fastmove` | `goap` | 6 | 2 | 4 | 125 | 199 | 45 |
| `standard-fastmove` | `htn` | 4 | 4 | 0 | 106 | 171 | 50 |

What the matrix confirms:

- the retained fix is not producing an HTN-only story,
- all planners still show `routingBeforeDamage = 0`,
- the remaining `standard` vs `medium` gap is cross-planner,
- `simple` and `goap` are effectively identical on this seed/config family,
- `htn` still has a distinct texture, but it does not overturn the topology diagnosis.

Why the drilldown index is not the main evidence here:

- the confirm-matrix `drilldown/index.json` selected only medium lanes because all runs scored `0` under the current ranking heuristic,
- so for this package the primary evidence is `summary.json`, `manifest.json`, and `anomalies.json`, not the drilldown ordering.

## 5.10 Baseline Reconcile Proof

The current workspace was later reconciled back to the retained-fix evidence baseline before planning the next slice.

Canonical baseline selected:

- retained-fix code state in `Person.cs` / `World.cs`,
- refreshed summary/handoff truth,
- post-fix artifacts:
  - `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-005/`
  - `.artifacts/smr/planner-compare-wave7-retreat-dominance-medium-control-003/`
  - `.artifacts/smr/planner-compare-wave7-retreat-dominance-full-planner-confirm-001/`

Reconcile proof bundles written from the current workspace:

- `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-reconcile-001/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-medium-reconcile-001/`

Observed result:

- the current workspace reproduces the canonical HTN post-fix standard and medium control artifacts exactly at the headline metric level,
- so the retained-fix state can be treated as canonical baseline for the next follow-up slice.

## 5.11 Recommended Next Steps

Most likely next engineering step if more work is needed:

- no immediate broad rewrite is justified,
- the next slice should be a narrow, runtime-centered, evidence-gated large-topology combat-realization follow-up.

If another evidence step is needed before more code, the most useful one is:

- a smaller targeted residual package around the remaining `standard` large-topology gap,
- not another broad planner matrix.

What does not currently look justified:

- reopening broad threat arbitration,
- reopening routing-first hypotheses,
- planner-specific tuning while the gap remains cross-planner,
- or reintroducing the rejected broad world-side topology expansion.

## 5.12 First Large-Topology Follow-Up Attempt Did Not Clear The Gate

After the reconcile and the new large-topology fix plan were in place, an initial implementation pass was attempted under the new slice.

Important outcome:

- the targeted HTN verification package did not show any material improvement over the retained baseline,
- so the experimental gameplay changes were not promoted,
- and the workspace was returned to the canonical retained-baseline state.

Relevant targeted attempt bundles:

- `.artifacts/smr/planner-compare-wave7-large-topology-combat-realization-001/`
- `.artifacts/smr/planner-compare-wave7-large-topology-combat-realization-002/`
- `.artifacts/smr/planner-compare-wave7-large-topology-combat-realization-003/`
- `.artifacts/smr/planner-compare-wave7-large-topology-combat-realization-004/`

What this means:

- the first recursive runtime-only follow-up ideas tried inside the approved seam did not move the canonical `htn / 101` `standard-default` or `standard-fastmove` lanes,
- `medium-default` remained stable,
- the gate behaved correctly,
- and no full-planner confirm rerun was justified after these attempts.

This is a useful result, not a failure of process.

It narrows the next recursive iteration further:

- the next attempt must still stay runtime-local,
- but it should not simply retry the same class of local hostile-sharing / memory / extra-step chase tweaks,
- because those did not change the canonical targeted package at headline metric level.

## 5.13 Second Large-Topology Follow-Up Attempt Also Did Not Clear The Gate

After the Meta-guided replan, a second narrow follow-up was attempted on the World combat-group formation / pairing seam.

Character of the attempt:

- World-owned combat-core anchor experiment,
- explicit geometry alignment in the gated path,
- no threshold rewrite,
- no clustering rewrite,
- no planner-specific tuning.

Relevant targeted attempt bundle:

- `.artifacts/smr/planner-compare-wave7-large-topology-combat-realization-anchor-001/`

Outcome:

- the targeted HTN package again did not move the canonical `standard-default` or `standard-fastmove` lanes at headline metric level,
- `medium-default` remained stable,
- so this world-seam anchor experiment was also not promoted,
- and the runtime code was returned to the canonical retained baseline.

This further narrows the next recursive search space:

- the next world-local pass should not simply retry the same effective-anchor-only idea,
- and should instead look for another narrow combat-group formation / pairing-realization seam under the same guardrails.

## 5.14 Third Large-Topology Follow-Up Attempt Also Did Not Clear The Gate

After the second no-promo, a third narrow follow-up was attempted under the Meta-recommended `BuildCombatGroups()` seam.

Character of the attempt:

- World-owned combat-core cluster-bridge / cohesion merge experiment,
- topology-gated,
- no pairing-threshold rewrite,
- no anchor rewrite,
- no planner-specific tuning,
- targeted HTN package only.

Relevant targeted attempt bundle:

- `.artifacts/smr/planner-compare-wave7-large-topology-combat-realization-bridge-001/`

Outcome:

- the targeted HTN package again matched the retained baseline at headline metric level for:
  - `standard-default`
  - `standard-fastmove`
  - `medium-default`
- therefore the cluster-bridge experiment was also not promoted,
- no full-planner confirm rerun was justified,
- and the runtime code was returned to the canonical retained baseline.

This further narrows the next recursive search space:

- the next world-local pass should not simply retry the same combat-core cluster-merge idea,
- and the next hypothesis should target a different formation/pairing-realization seam under the same guardrails.

## 5.15 Fourth Large-Topology Follow-Up Attempt Also Did Not Clear The Gate

After the third no-promo, a fourth narrow follow-up was attempted on the World pairing seam using a combat-core frontier signal.

Character of the attempt:

- World-owned frontier-distance pairing eligibility/selection experiment,
- explicit anti-outlier support guard,
- explicit frozen geometry guard,
- no anchor rewrite,
- no cluster rewrite,
- no pairing-threshold rewrite,
- no Person fallback tweak.

Relevant targeted attempt bundle:

- `.artifacts/smr/planner-compare-wave7-large-topology-combat-realization-frontier-001/`

Outcome:

- the targeted HTN package again matched the retained baseline at headline metric level for:
  - `standard-default`
  - `standard-fastmove`
  - `medium-default`
- therefore the frontier pairing experiment was also not promoted,
- no full-planner confirm rerun was justified,
- and the runtime code was returned to the canonical retained baseline.

This further narrows the next recursive search space:

- the next world-local pass should not simply retry the same frontier-pairing seam,
- and at this point the next hypothesis needs to be materially different from:
  - Person chase/memory tweaks,
  - anchor-only representation,
  - combat-core cluster-bridge merge,
  - frontier-distance pairing unlock.

## 6. Verification Commands Actually Run

Commands executed for implementation verification:

```text
dotnet test "WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj" --filter "FullyQualifiedName~ContactFunnelTelemetryTests"
dotnet build "WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj"
dotnet build "WorldSim.sln"
dotnet test "WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj" --filter "FullyQualifiedName~ContactFollowThroughTests|FullyQualifiedName~ContactFunnelTelemetryTests"
```

Commands executed for manual runner smoke and evidence:

```text
WORLDSIM_SCENARIO_TICKS=8 WORLDSIM_SCENARIO_SEEDS=711 WORLDSIM_SCENARIO_PLANNERS=simple WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\contact-funnel-artifact-smoke" dotnet run --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_TICKS=8 WORLDSIM_SCENARIO_SEEDS=712 WORLDSIM_SCENARIO_PLANNERS=simple WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=1 WORLDSIM_SCENARIO_SAMPLE_EVERY=2 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\contact-funnel-drilldown-smoke" dotnet run --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=htn WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=2 WORLDSIM_SCENARIO_SAMPLE_EVERY=25 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\planner-compare-wave7-retreat-dominance-standard-002" WORLDSIM_SCENARIO_CONFIGS_JSON='<standard configs>' dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=htn WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=2 WORLDSIM_SCENARIO_SAMPLE_EVERY=25 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\planner-compare-wave7-retreat-dominance-medium-control-001" WORLDSIM_SCENARIO_CONFIGS_JSON='<medium configs>' dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=htn WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=2 WORLDSIM_SCENARIO_SAMPLE_EVERY=25 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\planner-compare-wave7-retreat-dominance-standard-005" WORLDSIM_SCENARIO_CONFIGS_JSON='<standard configs>' dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=htn WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=2 WORLDSIM_SCENARIO_SAMPLE_EVERY=25 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\planner-compare-wave7-retreat-dominance-medium-control-003" WORLDSIM_SCENARIO_CONFIGS_JSON='<medium configs>' dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=simple,goap,htn WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=6 WORLDSIM_SCENARIO_SAMPLE_EVERY=25 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\planner-compare-wave7-retreat-dominance-full-planner-confirm-001" WORLDSIM_SCENARIO_CONFIGS_JSON='<medium+standard configs>' dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=htn WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=2 WORLDSIM_SCENARIO_SAMPLE_EVERY=25 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\planner-compare-wave7-retreat-dominance-standard-reconcile-001" WORLDSIM_SCENARIO_CONFIGS_JSON='<standard configs>' dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=htn WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=2 WORLDSIM_SCENARIO_SAMPLE_EVERY=25 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\planner-compare-wave7-retreat-dominance-medium-reconcile-001" WORLDSIM_SCENARIO_CONFIGS_JSON='<medium configs>' dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=htn WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=3 WORLDSIM_SCENARIO_SAMPLE_EVERY=25 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\planner-compare-wave7-large-topology-combat-realization-anchor-001" WORLDSIM_SCENARIO_CONFIGS_JSON='<standard+medium targeted configs>' dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=htn WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=3 WORLDSIM_SCENARIO_SAMPLE_EVERY=25 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\planner-compare-wave7-large-topology-combat-realization-bridge-001" WORLDSIM_SCENARIO_CONFIGS_JSON='<standard+medium targeted configs>' dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"

WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=htn WORLDSIM_SCENARIO_DRILLDOWN=true WORLDSIM_SCENARIO_DRILLDOWN_TOP=3 WORLDSIM_SCENARIO_SAMPLE_EVERY=25 WORLDSIM_SCENARIO_ARTIFACT_DIR="...\.artifacts\smr\planner-compare-wave7-large-topology-combat-realization-frontier-001" WORLDSIM_SCENARIO_CONFIGS_JSON='<standard+medium targeted configs>' dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"
```

## 7. Meta Coordinator Handoff Snapshot

Safe assumptions for future sessions:

- the contact-funnel telemetry slice exists and is wired end-to-end,
- the slice is runtime-owned and additive,
- the runner serializes both run-level and drilldown/timeline `contact` blocks,
- the key residual `standard` problem is not best explained by early routing,
- the retained large-topology person-level contact fix is part of the canonical baseline,
- the `medium` control lane remained stable after that retained fix,
- the full-planner confirm matrix says the remaining `standard` vs `medium` gap is cross-planner rather than HTN-only,
- the current workspace has been reconciled back to that retained baseline,
- and the first four recursive follow-up passes after that baseline were all no-promo outcomes.

Things that should not be assumed:

- do not assume actor-contact zeros imply no combat in the lane,
- do not assume the contact slice covers every hostility precursor outside fight/raid action execution,
- do not assume the full runner-side process-spawn test harness was green in this environment; build plus manual smoke was used there,
- do not resurrect the rejected broad world-side topology expansion without fresh evidence,
- do not simply retry the already-rejected local hostile-share / memory / extra-step, effective-anchor-only, combat-core cluster-bridge, or frontier-pairing follow-up families without a new hypothesis.

Best next step if the goal is new code:

- a narrow, runtime-centered, evidence-gated large-topology combat-realization follow-up.

Best next step if the goal is one more evidence pass before code:

- a small targeted residual package rather than another broad planner matrix.
