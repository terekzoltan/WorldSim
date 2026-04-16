# Post-Wave7 Retreat Dominance SMR Follow-up Plan

Status: parked follow-up, to be used after contact-funnel telemetry lands
Owner: SMR Analyst
Last updated: 2026-04-12

## 1. Purpose

The current broad threat fix and contact follow-through fix closed the major blockers:

- the Wave 7 wire is green,
- the broad defensive root cause was addressed,
- the old zero-contact bad lane no longer stays zero-contact,
- and the major follow-through blocker was also improved.

The remaining qualitative observation is narrower:

- the `standard` lane still tends to look more retreat-heavy than the `medium` lane.

This plan defines the later SMR follow-up that should be run **after** contact-funnel telemetry is implemented, not before.

## 2. Why This Is Deliberately Narrow

This is not another broad diagnostic package.

It exists to answer one focused question:

- why does larger-topology pressure still produce more retreat-heavy behavior even after the main threat and follow-through fixes landed?

Because the broad blockers are already gone, this follow-up should stay small, lane-focused, and explicitly qualitative.

## 3. Dependency On Contact-Funnel Telemetry

This plan assumes the contact-funnel observability slice has already landed.

Primary dependency:

- `Docs/Plans/Master/Post-Wave7-Contact-Funnel-SMR-Observability-Plan.md`

Without that slice, the retreat-heavy residual can still be observed, but not explained cleanly.

## 4. Primary Questions

The follow-up should decide which of these residual patterns is dominant in larger topology:

1. hostile actor sense is fine, but pursue is weak,
2. pursue happens, but adjacent contact under-realizes,
3. adjacent contact happens, but battle pairing is weaker,
4. battle pairing happens, but routing starts too early,
5. battle ticks produce damage, but retreat pressure still dominates behavior visibility.

## 5. Recommended Run Package

## 5.1 Primary micro lane

Run name suggestion:

- `planner-compare-wave7-retreat-dominance-standard-001`

Matrix:

- seed: `101`
- planners: `htn`
- configs:
  - `standard-default`
  - `standard-fastmove`
- ticks: `2400`
- drilldown: `true`

Reason:

- the residual qualitative concern is strongest in the `standard` lane,
- and `HTN` was the most informative residual planner in prior contact-follow-through validation.

## 5.2 Comparative control lane

Run name suggestion:

- `planner-compare-wave7-retreat-dominance-medium-control-001`

Matrix:

- seed: `101`
- planners: `htn`
- configs:
  - `medium-default`
  - `medium-fastmove`
- ticks: `2400`
- drilldown: `true`

Reason:

- `medium` acts as the closest healthy-ish control for the same family of scenarios.

## 5.3 Optional full regression follow-up

Only if the micro lane shows a useful signal:

- `simple/goap/htn`
- `medium-*`
- `standard-*`
- seed `101`

## 6. Metrics To Read

## 6.1 Existing run-level combat metrics

- `combatEngagements`
- `battleTicks`
- `ticksWithActiveBattle`
- `combatDeaths`
- `peakActiveBattles`
- `noProgressBackoffFlee`
- `noProgressBackoffCombat`

## 6.2 Existing AI telemetry

- `goalCounts`
- `commandCounts`
- `debugCauseCounts`
- `targetKindCounts`
- `latestDecision`

## 6.3 New contact-funnel telemetry

Actor-contact funnel:

- `hostileSensed`
- `pursueStarts`
- `adjacentContacts`
- `factionCombatDamageEvents`
- `factionCombatDeaths`
- `routingStarts`

Group-battle funnel:

- `battlePairings`
- `battleTicksWithDamage`
- `battleTicksWithDeaths`
- `routingBeforeDamage`

First-occurrence timing:

- all nullable `first*Tick` fields from the contact block

## 7. Primary Interpretation Rules

## 7.1 Sense/Pursue weakness

If `standard` shows:

- good `hostileSensed`
- but weak `pursueStarts`

then the residual issue is more likely pursuit willingness / transition into chase.

## 7.2 Contact realization weakness

If `pursueStarts` is healthy but `adjacentContacts` lags badly, then the residual issue is likely spatial or pursuit follow-through.

## 7.3 Pairing weakness

If `adjacentContacts` looks reasonable but `battlePairings` is low, then the residual issue is likely group-battle conversion rather than actor contact itself.

## 7.4 Early routing weakness

If `battlePairings` and `battleTicksWithDamage` are present but `routingBeforeDamage` is high, then routing pressure is still eating the battle too early.

## 7.5 Visibility-heavy retreat without hard failure

If combat flow is actually healthy but:

- `debugCauseCounts` still heavily show `retreat_refuge`,
- `targetKind=retreat` stays high,
- and combat still completes,

then the remaining issue may be presentation/qualitative balance rather than a hard combat conversion blocker.

## 8. Expected Output Of The Follow-up

The follow-up should produce a short verdict of this form:

1. `standard` retreat-heaviness is caused primarily by sense/pursue/contact/pairing/routing-before-damage
2. `medium` acts as the control and confirms whether this is topology-sensitive
3. next engineering step is either:
   - targeted runtime fix,
   - or no code change, only qualitative tuning,
   - or no action if the residual is judged acceptable.

## 9. Non-Goals

This follow-up does not aim to:

- reopen broad threat/arbitration fixes,
- reopen contact follow-through as a broad rewrite,
- or introduce new campaign mechanics.

Its sole purpose is to explain the remaining retreat-heavy qualitative difference after the major blockers have already been removed.
