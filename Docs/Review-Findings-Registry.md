# Review Findings Registry

Purpose:
- Capture reusable findings from plan reviews, step reviews, deep reviews, and post-review fix loops.
- Keep entries short and actionable so future Track agents can avoid repeated mistakes.
- Record only findings worth reusing; do not duplicate every routine review note.

Entry format:

```text
## YYYY-MM-DD - <Target> - <Severity> - <Short title>

- Track: <Track / role>
- Source: <review type, commit, PR, or doc reference>
- Finding: <what was wrong or risky>
- Impact: <why it matters>
- Resolution / guidance: <what to do next time>
- Status: open | fixed | guidance
```

Severity guide:
- `blocking`: must be fixed before commit or step closeout.
- `major`: likely bug, regression, ownership violation, or missing verification.
- `minor`: non-blocking improvement or maintainability issue.
- `guidance`: reusable process/coordination note.

Entries:

## 2026-04-30 - Wave 8 ScenarioRunner Tests - Minor - Bound nested runner process helpers

- Track: Track B / SMR Analyst test harness
- Source: Wave 8 deep review after Step 7B closeout
- Finding: ScenarioRunner xUnit helpers launched nested `dotnet run` processes with unbounded waits and no process-tree cleanup.
- Impact: A hung or externally timed-out test could leave orphan `dotnet.exe`/MSBuild processes and poison later verification with file locks.
- Resolution / guidance: Use a shared test-only process helper with a generous default timeout, env override (`WORLDSIM_SCENARIO_TEST_TIMEOUT_MINUTES`), concurrent stdout/stderr reads, and `Kill(entireProcessTree: true)` cleanup. Do not apply this timeout to direct/manual SMR CLI runs.
- Status: fixed

## 2026-04-30 - SMR Clustering Deep Evidence - Minor - Drilldown topN may miss clustering-worst runs

- Track: SMR Analyst / ScenarioRunner observability
- Source: Step review for `clustering-deep-wave8-prewave9-001`
- Finding: The drilldown `topN` selector can choose runs with `score=0` and omit the highest clustering/backoff anomaly runs, because the generic drilldown score is not clustering-focused.
- Impact: A clustering investigation can correctly rank worst runs from `summary.json`/`anomalies.json`, but drilldown artifacts may not include detailed timelines for the actual clustering-worst runs.
- Resolution / guidance: For clustering-focused evidence, explicitly verify whether `drilldown/index.json` covers the worst anomaly runs. If not, rank from `summary.json`/`anomalies.json` and either record that limitation or add a future clustering-aware drilldown selector.
- Status: guidance

## 2026-04-30 - TR2-B Minimal Solver Slice - Minor - Report unsupported nested output features explicitly

- Track: Track D / Tools.Refinery migration
- Source: Step review for Wave 8.5 `TR2-B`
- Finding: Minimal solver slices may intentionally model only a subset of the existing internal assertion DTO. If nested fields are omitted from the formal problem, they can be mistaken as solver-validated unless diagnostics or tests make the omission explicit.
- Impact: Later bridge-mapping work could accidentally assume fields such as effects, biases, campaign, or causal chains were formally validated when the minimal slice only proved story/directive shell consistency.
- Resolution / guidance: For every intentionally unsupported assertion field, either emit an explicit unsupported-feature diagnostic or add a test that proves the field is out of scope for the current slice. Do not silently treat omitted fields as solved/validated facts.
- Status: guidance

## 2026-05-03 - TR2-D Cross-Track Review - Guidance - Classify no-churn diffs before closeout

- Track: Meta Coordinator / Track D / Track B
- Source: Step review for Wave 8.5 `TR2-D`
- Finding: A Track D no-churn gate can legitimately detect parallel Track B-owned C#/ScenarioRunner diffs during a coordinated cross-track step.
- Impact: Treating all non-empty no-churn gates as Track D bugs can incorrectly block allowed parallel work, while ignoring them can hide ownership violations.
- Resolution / guidance: Stop Track D closeout, classify the foreign diffs by owner/scope, then resume only the owning track's verification. Mark the step YELLOW until the dependent Track B consume/evidence pass and Track D final verification are both complete.
- Status: guidance

## 2026-05-03 - TR2-D Evidence Fixtures - Major - Keep marker-rich evidence payloads semantically consistent

- Track: Track B / ScenarioRunner evidence
- Source: Step review for Wave 8.5 `TR2-D` Track B B2
- Finding: Repo-local marker-rich fixture/mock responses can claim `directorSolverValidatedCoverage:story_core` while omitting a core field such as story beat `severity` from the response patch payload.
- Impact: The artifact proves parser persistence but overclaims solver-backed semantic coverage, weakening truth-in-labeling evidence for TR2-D.
- Resolution / guidance: Marker-rich evidence responses must include every core field implied by their claimed coverage. For `story_core`, include `beatId`, `text`, `durationTicks`, and `severity`; for `directive_core`, include `colonyId`, `directive`, and `durationTicks`.
- Status: fixed

## 2026-05-04 - W8.6-D1 Paid-Live Policy Lock - Major - Prove documented env vars map to runtime properties

- Track: Track D / Java refinery config
- Source: Step review for Wave 8.6 `W8.6-D1`
- Finding: A policy handoff documented `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED` as the switch for `directorSolver*` markers before `application.yml` explicitly mapped that env var to `planner.director.solverObservabilityEnabled`.
- Impact: Track B could follow the handoff and still fail to enable solver observability, producing paid/rehearsal artifacts without the expected `directorSolver*` evidence.
- Resolution / guidance: Any documented operator env var used as a cross-track contract must be backed by code/config or the exact supported property mechanism must be documented and tested. Prefer explicit `application.yml` mappings for Java Spring properties.
- Status: fixed

## 2026-05-04 - W8.6-D1 Paid-Live Policy Lock - Major - Separate current enforcement from planned guardrails

- Track: Meta Coordinator / Track D / Track B
- Source: Step review for Wave 8.6 `W8.6-D1`
- Finding: Refinery live SMR docs described paid-live guardrails as code-enforced even though `refinery_live_validator` and `refinery_live_paid` guardrail implementation belonged to the later Track B `W8.6-B1` step.
- Impact: Future agents could assume paid confirmation, rehearsal proof, completion caps, or concurrency locks already existed and start paid or review work on a false safety premise.
- Resolution / guidance: Planning docs must distinguish currently enforced behavior from planned downstream enforcement, especially around paid/live/secret/cost guardrails.
- Status: fixed
