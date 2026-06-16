# Wave 10 Step10B.5-F4 - Manual Downstream Diagnostics

Status: planned
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F4 explains why manual/operator campaigns progressed partially but did not naturally activate convoys, scouts, or dedicated siege units.

## Purpose

Manual lifecycle evidence showed the runtime can create campaigns and advance some of them to encounter/resolution. However, three downstream systems stayed silent: supply convoys, scout intel, and siege units.

F4 adds reason counters and fixes only clear runtime reachability bugs.

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
