# SMR-M2 — Baseline Update Policy, Artifact Retention Policy, and Evidence-Review Protocol

**Epic:** SMR-M2 (Wave 4.5 — Meta Coordinator)  
**Prereq:** SMR-B3 ✅  
**Status:** ✅ Done

This document defines the three operational workflows that govern how WorldSim headless SMR runs are managed after the infrastructure epics (SMR-B1 through SMR-B4) are in place.

---

## 1. Baseline Update Policy

### What is a baseline?

A **baseline** is a `summary.json` artifact produced by a previous SMR run with known-good simulation state. It is passed to subsequent runs via `WORLDSIM_SCENARIO_BASELINE_PATH` to detect regressions in population, food, mortality, and perf metrics.

### When to update the baseline

Update the stored baseline when **all** of the following are true:

1. The current run passes all assertion invariants (`assertions.json` has zero `failed` entries with `severity=error`).
2. Any metric delta vs. the old baseline is **intentional** — e.g. a balance tweak, a new tech effect, or a deliberate AI rebalance.
3. The delta has been reviewed (see §3 evidence-review protocol) and accepted.
4. The session commit has been pushed to `main` or a long-lived branch.

**Do not update the baseline** when:
- A run fails assertions or has anomaly-gate failures.
- The diff is unexpected or unreviewed.
- You are mid-session iterating on a fix.

### How to update the baseline

```
# 1. Run SMR in assert+compare mode to confirm the new state is clean:
#    (If no canonical baseline exists yet, omit WORLDSIM_SCENARIO_COMPARE on the first baseline creation run.)
WORLDSIM_SCENARIO_ASSERT=true \
WORLDSIM_SCENARIO_COMPARE=true \
WORLDSIM_SCENARIO_BASELINE_PATH=Docs/Baselines/balance-baseline.json \
WORLDSIM_SCENARIO_ARTIFACT_DIR=/tmp/smr-update-check \
dotnet run --project WorldSim.ScenarioRunner

# 2. If exit code == 0 and review passed, promote summary.json to the baseline:
cp /tmp/smr-update-check/summary.json Docs/Baselines/balance-baseline.json

# 3. Commit the updated baseline with a clear message:
git add Docs/Baselines/balance-baseline.json
git commit -m "smr: update balance baseline after <reason>"
```

### Baseline file location

Reserved canonical path once established: `Docs/Baselines/balance-baseline.json`

This file is intended to be committed to the repository once the first clean canonical baseline is accepted. Until then, compare mode may be run against an ad hoc artifact path or skipped during initial baseline creation.

### Baseline versioning

- The file is a `summary.json` / `ScenarioRunEnvelope`. Schema version `smr/v1`.
- Each update is a plain git commit — the git log is the version history.
- When a breaking schema change is made (future `smr/v2`), rename the old file to `balance-baseline-v1.json` and create a fresh `balance-baseline.json` from the next clean run.

### Minimum seed set for a valid baseline

A baseline produced from fewer than 3 seeds is considered **advisory only**. The canonical baseline must be produced with at least `WORLDSIM_SCENARIO_SEEDS=101,202,303` and all three planners (`simple,goap,htn`).

---

## 2. Artifact Retention Policy

SMR artifact bundles are written to a directory specified by `WORLDSIM_SCENARIO_ARTIFACT_DIR`. This section defines what to keep and for how long.

### Local developer runs

- Artifact directories created in `/tmp/` or similar temp locations: **delete freely** after the session.
- If a run exposes a surprising result (anomaly, regression), copy the `manifest.json`, `assertions.json`, `anomalies.json`, and the relevant `runs/*.json` file to `Docs/Evidence/<date>-<topic>/` before deleting the temp dir.
- Do **not** commit full artifact bundles to the repository; they can be large. Only commit extracted evidence snippets when needed.

### CI runs (SMR-B6 and later)

- CI artifact bundles are uploaded as GitHub Actions artifacts with a **14-day retention window**.
- The CI workflow must name the artifact `smr-artifacts-<mode>-<run_id>` for traceability.
- The `manifest.json` from each CI run should be echoed to the step summary for quick dashboard visibility.
- If a CI run produces assertion failures or perf red-zones, the artifact bundle is retained for **30 days** (use `retention-days: 30` in the upload step).

### Evidence archive (long-term)

- For each Wave boundary (e.g. end of Wave 4.5, Wave 5, Wave 6), a **snapshot run** is executed and its `summary.json` + `assertions.json` + `anomalies.json` saved to `Docs/Evidence/WaveX-snapshot/`.
- These wave-boundary snapshots are committed and retained indefinitely — they form the project's longitudinal health record.
- Wave-boundary snapshot runs use: `WORLDSIM_SCENARIO_ASSERT=true`, `WORLDSIM_SCENARIO_PERF=true`, `WORLDSIM_SCENARIO_COMPARE=true` (when a current canonical baseline exists), and at least 1200 ticks.

### What never to commit

- Raw `runs/*.json` directories (too large, high churn).
- `run.log` files (redundant with git log and CI step output).
- Temp `perf.json` files from exploratory local runs.

---

## 3. Evidence-Review Protocol

This protocol defines how a coordinator or developer reviews SMR output before accepting a balance/regression/perf result and before merging to `main`.

### When to run a formal evidence review

Run the full evidence-review checklist when:
- Any PR touches `WorldSim.Runtime`, `WorldSim.AI`, `WorldSim.ScenarioRunner`, or `technologies.json`.
- The balance baseline is about to be updated.
- A Wave boundary snapshot is being captured.
- A new assertion invariant is being added or an existing threshold is being changed.

### Checklist — step by step

#### Step 1 — Run SMR in full mode

```
WORLDSIM_SCENARIO_ASSERT=true \
WORLDSIM_SCENARIO_PERF=true \
WORLDSIM_SCENARIO_COMPARE=true \
WORLDSIM_SCENARIO_BASELINE_PATH=Docs/Baselines/balance-baseline.json \
WORLDSIM_SCENARIO_ARTIFACT_DIR=/tmp/smr-evidence-<date> \
WORLDSIM_SCENARIO_SEEDS=101,202,303 \
WORLDSIM_SCENARIO_PLANNERS=simple,goap,htn \
dotnet run --project WorldSim.ScenarioRunner
```

#### Step 2 — Check exit code

| Exit code | Meaning | Action |
|-----------|---------|--------|
| `0` | All assertions pass, no anomaly gate fail | Proceed to Step 3 |
| `2` | Assertion failure | Fix failing invariants before proceeding |
| `3` | Config/baseline error | Fix config or provide correct baseline path |
| `4` | Anomaly gate fail | Investigate anomalies (Step 3), decide if intentional |

If exit code is non-zero, do **not** update the baseline. Fix the root cause first.

#### Step 3 — Review `assertions.json`

Open `assertions.json` and verify:
- All `passed: true` entries have plausible measured values (no suspicious zeros).
- Any `skipped: true` entries have a documented `skipReason` (e.g. `"CombatEngagements == 0, COMB invariants not applicable"`).
- No `passed: false` entries remain unless they are explicitly accepted as known-flaky (document in `Docs/Evidence/known-flaky.md`).

#### Step 4 — Review `anomalies.json`

For each anomaly:
- **`category: "balance"`** — review the `message` and `measured` vs `threshold`. If the anomaly is caused by an intentional change (e.g. AI rebalance), annotate and accept. If unexpected, investigate before merging.
- **`category: "perf"`** — yellow-zone anomalies are advisory. Red-zone anomalies (`ANOM-PERF-*`) require a comment in the PR explaining why the budget is exceeded (hardware variance, intentional complexity increase, etc.).
- **`category: "compare"`** — regression anomalies (pass-to-fail) are blockers unless the regression is intentional and documented.

#### Step 5 — Review `compare.json` (if compare was enabled)

Check `passToFailRegressions` — if any invariant transitioned from `pass` to `fail` relative to baseline, this is a hard blocker unless:
- The invariant threshold is being intentionally tightened, or
- The change is a known simulation rebalance that the coordinator has signed off on.

Check `thresholdBreaches` — any metric that moved by more than the delta threshold needs a comment explaining the cause.

#### Step 6 — Review `perf.json` (if perf was enabled)

Scan for any `budget.avgTickStatus: "red"` or `budget.p99TickStatus: "red"` entries. For each:
- If on developer hardware that is faster/slower than CI targets, note the skew.
- If the run count with red status is ≤ 1 of N seeds, treat as noise and document.
- If the majority of seeds show red, this is a perf regression — do not merge until investigated.

#### Step 7 — Accept or escalate

| Outcome | Decision |
|---------|----------|
| All assertions pass, no unreviewed anomalies, no unexpected regressions | ✅ Accept — proceed with merge / baseline update |
| Known/accepted anomalies, documented in PR or known-flaky.md | ✅ Accept with documentation |
| Unexpected regressions or red perf on majority of seeds | ❌ Escalate — fix before merge |
| Exit code 3 (config error) | ❌ Fix config, re-run |

### Shorthand review (fast path for small PRs)

For PRs that touch only graphics, HUD, or UI (no runtime/AI changes), the review can be shortened to:
1. Run SMR in assert-only mode (`WORLDSIM_SCENARIO_ASSERT=true`).
2. Verify exit code `0`.
3. No further review needed unless the PR touches `technologies.json` or any world-state logic.

---

## 4. Responsibilities Summary

| Role | Responsibility |
|------|---------------|
| Track B agent | Maintain `ScenarioRunner` tooling; ensure artifact schema stays backward-compatible |
| Track C agent | Maintain AI/planner signals exported to SMR (`AiNoPlanDecisions`, `AiReplanBackoffDecisions`, `AiResearchTechDecisions`) |
| Meta Coordinator | Own the canonical baseline path/policy (`Docs/Baselines/balance-baseline.json` when established); run evidence-review before each Wave boundary; update this doc when policy changes |
| All track agents | Run at minimum `WORLDSIM_SCENARIO_ASSERT=true` before marking any Track epic ✅ if the epic touches runtime/AI logic |

---

## 5. Known Limitations (as of Wave 4.5)

- **No graphical SMR lab yet** — deferred to Wave 10+. Evidence review is text/JSON-based for now.
- **Perf budgets are hardware-dependent** — the thresholds in the perf budget table (avg ≤ 4ms, p99 ≤ 8ms) are calibrated for developer-class hardware. CI runners may show higher times; treat yellow-zone CI perf anomalies as advisory until a CI-calibrated budget is established (SMR-B6 concern).
- **`COMB-*` invariants are skipped when combat is disabled** — this is intentional. Runs without `EnableCombatPrimitives=true` will always skip combat invariant assertions.
- **Baseline must be regenerated after major simulation rewrites** — if `WorldSim.Runtime` undergoes a large structural change, the existing `balance-baseline.json` may no longer be comparable. In that case, delete the old baseline and start fresh after the rewrite stabilizes.
