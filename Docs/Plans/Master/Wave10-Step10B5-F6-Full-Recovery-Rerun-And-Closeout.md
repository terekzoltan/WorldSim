# Wave 10 Step10B.5-F6 - Full Recovery Rerun And Closeout

Status: planned
Owner: SMR Analyst for rerun/review; Meta Coordinator for closeout decision
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F6 reruns the full recovery evidence only after focused Track B gates have passed. It is not a place for new fixes.

## Prerequisites

Before F6 starts:

- F1 diagnostics are accepted.
- F2 target-knowledge or other confirmed blocker fix is accepted.
- F3 hostile organic pilot/confirm shows useful launch signals or accepted limitations.
- F4 manual downstream diagnostics are accepted.
- F5 stress seed-606 failures are fixed or explicitly routed.
- Full solution build and focused test gates are green.

## Package Order

Run in this order:

- `wave10-organic-hostile-soak-002`
- `wave10-manual-operator-lifecycle-002`
- `wave10-organic-pure-soak-002`
- `wave10-organic-lifecycle-stress-002`

The hostile package runs first because it is the primary blocker from Step10B.2.

## Run Rules

- Use runtime-backed main-run lifecycle evidence.
- Keep `runs[].wave10.runtimeSource = main_world_run` for lifecycle claims.
- Keep `wave10-probes.json` side-probe-only.
- Use conservative drilldown sampling to avoid artifact explosion.
- Do not tune configs mid-run without opening a new fix slice.
- Do not promote artifacts to a canonical baseline unless Meta opens a separate baseline decision.

## Review Questions

The final evidence review must answer:

- Did hostile organic campaigns launch?
- Did launches happen across multiple seed/planner/config combinations?
- Did pure organic remain rare, absent, or healthy?
- Did manual/operator lifecycle remain stable?
- Which lifecycle stages were observed: assembly, march, encounter, siege, resolution?
- Did convoys request/spawn/deliver/fail?
- Did scouts observe targets and did campaigns use intel?
- Did dedicated siege units activate when tech/preconditions allowed them?
- Did stress survival recover?
- Did perf/clustering/no-progress regress?
- Is Track C needed now?
- Is Track A needed now?

## GREEN / YELLOW / RED Decision

GREEN:

- hostile organic launches in multiple combinations,
- manual lifecycle remains meaningful,
- at least partial lifecycle beyond launch appears,
- no hard survival assertion failures,
- remaining downstream gaps are fixed or clearly classified.

YELLOW:

- hostile organic launches but incidence is low,
- pure organic remains rare/zero,
- convoy/scout/siege-unit behavior remains sparse but explained,
- no hard survival failures,
- Meta accepts remaining limitations.

RED:

- hostile organic still has zero launches,
- manual lifecycle regresses,
- stress survival failures persist,
- telemetry cannot explain remaining no-launch/no-lifecycle results.

## Outputs

Produce a checked-in evidence note only if Meta requests persistent evidence.

The evidence note should include:

- artifact paths,
- matrix size,
- exit codes,
- assertion failures,
- anomaly counts,
- launch incidence,
- lifecycle stage incidence,
- convoy/scout/siege-unit interpretation,
- stress survival outcome,
- final GREEN/YELLOW/RED recommendation,
- Track routing.

## Closeout

Meta must update:

- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`,
- `ops/PROJECT_STATE.md`,
- `AGENTS.md` message board if meaningful.

Wave10.5 remains blocked until F6 is accepted GREEN/YELLOW or explicitly deferred.
