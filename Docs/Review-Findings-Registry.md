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
