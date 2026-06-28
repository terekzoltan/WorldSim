# Wave10.6 Q6 Manual CI Coverage Artifact Run

Status: GREEN accepted CI-owner proof
Owner: CI owner / Meta Coordinator
Date: 2026-06-28
Run name: `w10-6-ci-manual-001`
GitHub run id: `28326071232`
Workflow: `Coverage Baseline Artifact`
Workflow file: `.github/workflows/coverage-baseline.yml`

## Decision

The optional `W10.6-Q6` manual CI artifact lane is accepted as operationally proven.

This does not introduce a coverage threshold or a PR/push/scheduled gate. It proves that the manually triggered CI artifact lane can run and publish coverage reports.

## Evidence Source

The CI owner provided GitHub Actions screenshots showing:

- Workflow run: `Coverage Baseline Artifact #1`
- Trigger: `workflow_dispatch`
- Branch/ref: `master` / `refs/heads/master`
- Commit shown in run UI: `fdd26db`
- Status: `Success`
- Total duration: `9m 7s`
- Artifact count: `1`
- Job: `Manual coverage artifact` succeeded

The generated summary screenshot showed:

- Run name: `w10-6-ci-manual-001`
- GitHub run id: `28326071232`
- `.NET Cobertura XML count`: `6`
- Java JaCoCo XML present: `True`
- Java JaCoCo HTML index present: `True`

## Acceptance Mapping

| Acceptance | Evidence | Verdict |
|---|---|---|
| Manual trigger only | Summary states `workflow_dispatch`; workflow file has no PR/push/scheduled trigger | PASS |
| Artifact produced | GitHub run UI shows `Artifacts: 1` | PASS |
| .NET report coverage artifacts present | Summary reports `.NET Cobertura XML count: 6` | PASS |
| Java JaCoCo XML present | Summary reports `Java JaCoCo XML present: True` | PASS |
| Java JaCoCo HTML present | Summary reports `Java JaCoCo HTML index present: True` | PASS |
| No coverage threshold semantics | Summary states no percentage threshold or hard fail | PASS |

## Non-Claims

- This run does not prove a coverage percentage target.
- This run does not make `WorldSim.ScenarioRunner` a production line-coverage target.
- This run does not replace SMR evidence or manual App/Graphics smoke evidence.
- This run does not commit raw generated XML/HTML reports to the repository.

## Outcome

`W10.6-Q6` is closed GREEN. Wave11 is not blocked by W10.6 coverage infrastructure or policy.
