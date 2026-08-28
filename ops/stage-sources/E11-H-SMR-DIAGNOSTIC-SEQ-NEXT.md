# E11-H SMR Diagnostic Delivery Planning Context

Status: `SEQ_NEXT / DIAGNOSTIC_SEAM_AUTHORIZED`
Logical identity: `worldsim-e11h-step5c4-smr-diagnostic-seq-next-v1`
Wave / Epic: `Wave 11 / E11-H`
Accountable lane: `SMR Diagnostic Delivery / SPECIALIST_DELIVERY / worldsim.smr-diagnostic-delivery`
Prior disposition: `RETAIN_DIAGNOSTIC_ONLY / STOP`

## Why this seam is open

The rejected Track B predator hunger-gate candidate remains diagnostic evidence
only. Its controlled checks passed `4/4`, but focused continuity regressed to
`1/10` (`ON 1/5`, `OFF 0/5`) from the pre-candidate `ON 4/5`, `OFF 1/5` result.
No source or implementation authority from that candidate is retained.

Meta now authorizes one evidence-only specialist lifecycle because the failed
result exposes two unresolved classes of failure without proving one Runtime
repair: OFF herbivore extinction and ON seed-202 predator extinction. This seam
must identify whether they share a recruitment/mortality bottleneck or are
independent failures before any Track B repair may be considered.

## Required `/seq-next` plan

Produce the smallest reviewable plan for an ON/OFF evidence package that:

1. names the competing hypotheses and the observation that would distinguish
   them;
2. reuses the smallest representative failed and control cases instead of the
   expanded 9-run or full 45-run acceptance matrix;
3. captures population, births, death causes, hunt, grazing, starvation, and
   human-kill timing with exact mode/configuration/seed provenance;
4. states any ScenarioRunner/SMR evidence-tooling change separately from the
   evidence run itself;
5. defines a deterministic stop condition and a Meta classification handoff.

## Scope boundary

Allowed: existing evidence inspection, ScenarioRunner/SMR diagnostic planning,
test-owned diagnostic instrumentation when review approves it, and the bounded
evidence package.

Forbidden: Runtime, AI, App, Graphics, gameplay tuning, baselines, thresholds,
seeding, capacity, rescue/replenishment, predator-human policy, candidate source
reconstruction, expanded matrices, E11-H closeout, E11-I, or E11-J.

## Required terminal

The plan ends at Meta evidence classification. If evidence proves one bounded
Runtime-owned gap, Meta may later create a separate Track B `/seq-next` route.
Otherwise the project returns to `STOP` or opens only a separately authorized,
narrower evidence seam. This artifact grants no lifecycle send by itself.
