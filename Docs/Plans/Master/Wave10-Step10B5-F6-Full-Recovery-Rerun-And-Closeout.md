# Wave 10 Step10B.5-F6 - Full Recovery Rerun And Closeout

Status: planned
Owner: SMR Analyst for rerun/review; Meta Coordinator for closeout decision
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F6 reruns the full recovery evidence only after focused Track B gates have passed. It is not a place for new fixes.

F6 should not repeat the 5-hour Step10B.2 pattern by default. It uses staged package escalation and early-stop rules so the decision core runs first.

## Prerequisites

Before F6 starts:

- F1 diagnostics are accepted.
- F2 target-knowledge or other confirmed blocker fix is accepted.
- F3 hostile organic pilot/confirm shows useful launch signals or accepted limitations.
- F4 manual downstream diagnostics are accepted.
- F5 stress seed-606 failures are fixed or explicitly routed.
- Full solution build and focused test gates are green.

## Package Order

Run in this order, with early-stop checks between packages:

- `wave10-organic-hostile-soak-002`
- `wave10-manual-operator-lifecycle-002`
- `wave10-organic-pure-soak-002` only if hostile/manual results are healthy enough that pure rarity context matters.
- `wave10-organic-lifecycle-stress-002` only after targeted stress sentinel lanes pass or are explicitly accepted/routed.

The hostile package runs first because it is the primary blocker from Step10B.2.

Manual lifecycle runs second because it is the control group: it proves whether runtime-owned campaign behavior still works after launch.

Pure organic and broad stress are escalation packages, not mandatory default packages.

## Early-Stop Policy

Stop before later packages if:

- hostile organic remains zero-launch,
- hostile organic no-launch is not explained by diagnostics,
- manual lifecycle launch/control behavior regresses,
- targeted stress sentinel still fails hard survival assertions,
- artifact shape/provenance breaks.

If stopped early, the F6 report should still be complete: explain which package stopped the run, why later packages were skipped, and what the next owner is.

## Recommended Matrix Sizes

Hostile organic:

- Start with 3 seeds x 3 planners x medium/standard.
- Add large config only if medium/standard are healthy or topology coverage is needed.
- Expand to 10 seeds only after the 3-seed matrix is useful.

Manual lifecycle:

- Keep enough coverage to preserve the control group.
- Prefer 3 seeds x 3 planners x 3 configs before expanding.

Pure organic:

- Start with 3 seeds x 3 planners x 2 configs.
- Full 90-run pure package is optional and only useful if hostile organic is at least partially healthy.

Stress:

- Start with sentinel lanes: small-lowpop and small-highpop, seed 606, simple/goap/htn.
- Expand only if sentinel passes and broad topology/survival coverage is still needed.

Perf:

- Keep lifecycle proof packages `perf=false` unless perf is the claim.
- Run separate `perf-long` or `perf-stress` packages after lifecycle behavior is healthy.

## Run Rules

- Use runtime-backed main-run lifecycle evidence.
- Keep `runs[].wave10.runtimeSource = main_world_run` for lifecycle claims.
- Keep `wave10-probes.json` side-probe-only.
- Use conservative drilldown sampling to avoid artifact explosion.
- Do not tune configs mid-run without opening a new fix slice.
- Do not promote artifacts to a canonical baseline unless Meta opens a separate baseline decision.
- Do not run pure/stress just to confirm an already decisive hostile/manual RED.

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
- Were any packages skipped by early-stop policy, and was that skip valid?
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
- skipped package rationale, if early-stop triggered,
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
