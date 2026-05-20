# Wave 9 Final SMR Closeout

Status: GREEN. Wave 9 closeout accepted by Meta after SMR Analyst evidence review. Wave 10 / P6-E may start.

This is final Wave 9 closeout evidence, not a baseline-compare claim. Baseline path: none.

## Artifact Packages

Package A: `.artifacts/smr/all-around-smoke-wave9-001/` (local raw artifact, ignored; do not commit unless explicitly requested).

- Purpose: organic peaceful all-around regression pressure.
- Matrix: 27 Headless runs (`101,202,303` seeds x `simple,goap,htn` planners x `small-default`, `medium-default`, `standard-default`).
- Result: `exitCode=0`, `assertionFailures=0`, `anomalyCount=12`.
- Health signals: minimum living colonies `4`, minimum people `24`, minimum food `894`, starvation-with-food `0`, AI no-plan `0`.
- Scope caveat: combat and diplomacy were disabled; this package does not claim full combat/siege side-effect coverage.

Package B: `.artifacts/smr/wave9-campaign-supply-final-001/` (local raw artifact, ignored; do not commit unless explicitly requested).

- Purpose: deterministic Wave9 campaign/supply feature proof.
- Matrix: 36 Headless runs (`101,202,303` seeds x `simple,goap,htn` planners x four Wave9 scenarios).
- Result: `exitCode=0`, `assertionFailures=0`, `anomalyCount=0`.
- Run-level Wave9 metadata: `evidenceKind=deterministic_probe`, `timelineSemantics=not_tick_sampled`.
- Drilldown Wave9 timeline samples stayed default/not-sampled; they are not temporal Wave9 proof.

## Closeout Questions

- Did armies consume supply? Yes, targeted package counters were positive.
- Did low supply affect morale, stamina, retreat, route, or campaign behavior? Yes, supply depletion produced positive out-of-supply, attrition, and routing counters.
- Did carrier/resupply behavior execute? Yes, carrier assignments, deliveries, member inventory consumption, ration pool consumption, and resupply delivered counters were positive.
- Did foraging extend campaigns without becoming infinite food? Bounded foraging proof was positive: attempts, successes, food gained, and cap-reached evidence were present.
- Did campaigns assemble, march, and encounter targets? Yes, campaign launch, assembly completion, march start/progress, and encounter counters were positive.
- Did campaign state remain deterministic and non-stuck? Targeted package completed all 36 runs with no anomalies and positive lifecycle counters across all seeds/planners.
- Did new mechanics introduce starvation-with-food, clustering/backoff, no-progress, AI no-plan, or economy regressions? No Wave9 blocker was found. Package A did report non-blocking clustering/backoff warnings, routed below.

## Residual Risks And Routing

- Organic campaign/supply tick-sampled behavior remains unknown; current Wave9 proof combines organic peaceful regression pressure with deterministic feature probes.
- Package A is peaceful and does not fully cover combat/siege side effects.
- Package A reported 12 `ANOM-CLUSTER-HIGH-BACKOFF` warnings concentrated in medium/standard lanes. This is not a Wave9 blocker because survival/economy/AI health stayed green and targeted Wave9 proof passed. Route: Wave 10 SMR packages must continue ranking clustering/backoff signals and treat worsening movement/occupancy regressions as in-scope review evidence.
- Raw artifacts remain local-only and ignored.

## Decision

Wave 9 closeout is accepted. Wave 10 / P6-E may start. Final Wave 10 acceptance will require its own SMR prep and closeout gates.
