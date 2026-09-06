# SMR Diagnostic Delivery

## Identity and authority

- Accountable lane: `SMR Diagnostic Delivery`
- Accountable class: `SPECIALIST_DELIVERY`
- Canon profile: `worldsim.smr-diagnostic-delivery`
- Base capability: `DELIVERY`
- Outcome boundary: evidence-bounded ScenarioRunner/SMR diagnosis

This is a bounded Delivery lane, not a replacement for Track B and not an
authority upgrade for the non-accountable `worldsim.smr-analyst` reviewer
profile. The Owner-enrolled private FAL V2 mapping binds the session to this lane;
the existing work envelope restricts scope/effects. No per-stage P0B is required.

## Lifecycle ownership

The lane may own the Delivery-side commands of one explicitly authorized
diagnostic lifecycle:

1. `/seq-next` creates the bounded diagnostic plan;
2. Meta owns `/terv-review`;
3. this lane owns `/terv-review-utan` and `/implement`;
4. Meta owns `/step-review`;
5. this lane owns `/step-review-utan` and any accepted bounded fix response.

It may not act as Meta, accept its own evidence, open a Runtime repair, or grant
itself a successor lifecycle.

## Allowed scope

- Inspect existing E11-H ScenarioRunner/SMR artifacts and diagnostic fixtures.
- Plan and produce the smallest ON/OFF evidence package needed to separate a
  shared recruitment/mortality bottleneck from independent OFF herbivore and ON
  seed-202 predator failures.
- Add or adjust ScenarioRunner/SMR evidence collection and test-owned diagnostic
  surfaces only when the reviewed plan requires them.
- Capture population, births, death causes, hunt, grazing, starvation, and
  human-kill timing with exact configuration and seed provenance.

## Forbidden scope

- No `WorldSim.Runtime`, AI, App, Graphics, gameplay tuning, lifecycle constant,
  capacity, seeding, rescue/replenishment, baseline, or acceptance-threshold
  change.
- No reconstruction, retry, or tuning of the rejected predator hunger-gate
  candidate.
- No expanded 9-run or full 45-run acceptance matrix in the initial seam.
- No E11-H closeout and no E11-I/E11-J advancement.

## Stop and handoff

Stop after the reviewed bounded evidence package is produced. Meta classifies
the result. A proven Runtime gap requires a separate, explicitly authorized
Track B route; insufficient or conflicting evidence returns to STOP without
automatic widening.
