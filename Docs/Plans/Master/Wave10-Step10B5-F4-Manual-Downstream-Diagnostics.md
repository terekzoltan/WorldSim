# Wave 10 Step10B.5-F4 - Manual Downstream Diagnostics

Status: accepted / closed - F5 stress seed-606 unblocked
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F4 explains why manual/operator campaigns progressed partially but did not naturally activate convoys, scouts, or dedicated siege units.

## Purpose

Manual lifecycle evidence showed the runtime can create campaigns and advance some of them to encounter/resolution. However, three downstream systems stayed silent: supply convoys, scout intel, and siege units.

F4 adds reason counters and fixes only clear runtime reachability bugs.

## Implementation Outcome

Implemented a compact nested `manualDownstreamDiagnostics` block under Wave10 telemetry. It is additive and keeps existing flat Wave10 counters intact.

Shape:

- `manualDownstreamDiagnostics.convoy`: `evaluated`, `eligible`, `requested`, `blockedReason`, `spawned`, `delivered`, `failed`
- `manualDownstreamDiagnostics.scout`: `observationPasses`, `liveScoutActors`, `skippedByRelation`, `skippedByRadius`, `nearestHostileDistance`, `freshIntel`
- `manualDownstreamDiagnostics.siegeUnit`: `encounterCampaigns`, `techLocked`, `resolverDisabled`, `noTarget`, `alreadyPresent`, `spawned`, `actionTicks`

No behavior tuning was made. The only runtime changes are additive diagnostics/counter recording.

Local F4 pilot:

- Artifact: `.artifacts/smr/wave10-step10b5-f4-manual-downstream-diagnostics-001/`
- Scope: 1 seed (`101`) x 1 planner (`simple`) x 1 manual lifecycle config, `Headless`, assert mode, drilldown enabled.
- Result: `exitCode=0`, `assertionFailures=0`, `anomalyCount=0`, no `wave10-probes.json`.
- Main-run provenance: `runtimeSource=main_world_run`, `proofType=manual_operator`, `timelineSemantics=tick_sampled`.

Pilot interpretation:

- Convoy: `evaluated=451`, `eligible=0`, `requested=0`, `blockedReason=none`, `spawned=0`, `delivered=0`, `failed=0`; classification `explained_expected` for this pilot because no low-supply convoy eligibility/request occurred.
- Scout: `liveScoutActors=0`, `observationPasses=0`, `freshIntel=0`, `nearestHostileDistance=-1`; classification `routed_to_future` / `needs_meta_decision` if positive scout lifecycle evidence is required, because this pilot had no live scout actors to exercise runtime observation.
- Siege unit: `encounterCampaigns=243`, `techLocked=8`, `noTarget=3`, `spawned=0`, `actionTicks=0`; classification `explained_expected` for no-tech manual lifecycle because `siege_craft` was not unlocked. This does not prove dedicated siege-unit failure.

Recommended routing:

- Accept F4 as diagnostics/evidence-surface GREEN if Meta only needs absence explanations.
- Do not open F5/F6/full package automatically.
- If Meta wants positive downstream behavior proof, open a separate approved evidence slice for scout-role availability and/or tech-enabled siege proof; do not globally unlock `siege_craft` in the default lifecycle config.

## Meta Closeout - 2026-06-20

Decision:
- F4 accepted as diagnostics GREEN.
- F5 is explicitly unblocked next, under normal stress seed-606 scope only.
- The old F3 standard `movementSpeedMultiplier=0` collapse remains invalid as F5 evidence.
- F4 residuals do not open Track C/A: positive scout-role lifecycle or tech-enabled siege-unit lifecycle would require separate Meta-approved evidence slices if needed.
- F6 and full hostile/pure/stress/perf broad packages remain blocked until later Meta/SMR decision.

Closeout interpretation:
- Convoy absence is explained in the pilot as no low-supply eligibility/request.
- Scout absence is explained in the pilot as no live scout actors / no observation pass, not as a strategist or UI issue.
- Siege-unit absence is explained in the no-tech pilot as `siege_craft` tech lock despite encounter pressure, not as a dedicated siege-unit failure.

## Convoy Diagnostics

Answer:

- Did any active campaign become low supply?
- What was min/avg supply readiness?
- What was max sustained out-of-supply ticks?
- Did the strategist request a convoy?
- Were requests blocked by cap?
- Were requests blocked by throttle?
- Were requests blocked by home defense?
- Were requests blocked by route budget?
- Were convoys spawned but not delivered/failed?

Additive telemetry may include:

- convoy eligible campaign count,
- convoy decision count,
- convoy no-request reason,
- convoy request/spawn/delivery/failure separation.

## Scout Diagnostics

Answer:

- Were scout actors present by faction?
- Were any persons scout-capable?
- Did observation attempts run?
- Were observations skipped by relation?
- Were observations skipped by radius?
- What was nearest hostile target distance vs scout radius?
- Did fresh scout intel expire too quickly?

Do not turn this into Track C unless the missing piece is clearly planner/role behavior rather than runtime observation policy.

## Siege-Unit Diagnostics

Answer:

- Was `siege_craft` unlocked for the attacker?
- Did the campaign enter encounter/siege relevance?
- Was there a valid target structure?
- Was a siege unit already present?
- Was resolver disabled?
- Was the campaign invalid/incomplete?

Important interpretation:

- If `siege_craft` is not unlocked, zero dedicated siege units may be expected.
- Do not globally unlock `siege_craft` in all lifecycle configs just to get a positive counter.
- If proof is needed, add a separate tech-enabled evidence config with explicit non-claim boundaries.

## Allowed Fixes

Allowed:

- add reason counters,
- fix unreachable runtime maintenance path,
- fix incorrect no-spawn/no-request conditions,
- add focused tests for reason counters,
- add a dedicated tech-enabled package only with Meta approval.

Not allowed:

- broad campaign balancing,
- global tech unlocks,
- App/Graphics changes,
- Track C strategy changes without explicit routing,
- overclaiming convoy request as delivery proof.

## Tests

Add focused coverage for:

- low-supply campaign can become convoy-eligible,
- blocked convoy increments the correct reason,
- spawned convoy is distinct from delivered/failed convoy,
- scout absence vs out-of-radius is distinguishable,
- siege-unit tech-locked no-spawn is distinguishable from invalid campaign/no target.

## Verification

Run focused runtime/ScenarioRunner tests and build.

Then run a small manual lifecycle confirm:

- 1-3 seeds,
- 1-3 planners,
- medium manual config,
- 3000-6000 ticks.

## Acceptance

F4 is accepted when:

- no-convoy/no-scout/no-siege-unit outcomes are explained by counters,
- clear runtime reachability bugs are fixed or explicitly routed,
- manual launch success does not regress,
- final evidence wording can distinguish request, spawn, delivery, and feature-gated absence.

## Handoff To F6

The F4 handoff must include:

- whether convoy behavior is fixed, expected sparse, or routed,
- whether scout behavior is fixed, expected sparse, or routed,
- whether siege-unit behavior is tech-gated, fixed, expected sparse, or routed,
- tests run,
- pilot artifact paths if any.
