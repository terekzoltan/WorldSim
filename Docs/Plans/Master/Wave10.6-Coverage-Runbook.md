# Wave10.6 Coverage Runbook

Status: W10.6-Q3 governance locked
Owner: Meta Coordinator with Track B technical prep and Track D Java consult
Last updated: 2026-06-27

## Purpose

This runbook defines the first stable local coverage command sequence for WorldSim after `W10.6-Q1` and `W10.6-Q2`.

The goal is reproducible local evidence, not a hard threshold.

## Locked Governance

- Coverage remains baseline-only in Wave10.6.
- No hard percentage gate is introduced.
- Raw generated XML/HTML artifacts remain local-only under `.artifacts/coverage/<run-name>/` or ignored native build output paths.
- `WorldSim.ArchTests` coverage is test/tooling-only and must not be interpreted as production-module protection.
- App/Graphics host code remains outside the first hard coverage interpretation lane.
- Java JaCoCo outputs are referenced from native Gradle build paths in v1; they are summarized in the local summary artifact and checked-in evidence note rather than copied or committed as generated reports.

## Run Naming

Use a descriptive local run id:

```text
w10-6-<lane>-<nnn>
```

Examples:

- `w10-6-unified-baseline-001`
- `w10-6-unified-rerun-002`

## Artifact Contract

Local run root:

```text
.artifacts/coverage/<run-name>/
```

Expected v1 shape:

```text
.artifacts/coverage/<run-name>/
  summary.md
  dotnet/
    runtime/<guid>/coverage.cobertura.xml
    ai/<guid>/coverage.cobertura.xml
    refinery-client/<guid>/coverage.cobertura.xml
    refinery-adapter/<guid>/coverage.cobertura.xml
    scenario-runner/<guid>/coverage.cobertura.xml
    arch/<guid>/coverage.cobertura.xml
  java/
    referenced-paths-only-in-summary.md
```

V1 note:

- The .NET outputs are collector-produced lane artifacts under `.artifacts/coverage/<run-name>/dotnet/...`.
- The Java outputs are native Gradle JaCoCo files under ignored `refinery-service-java/build/reports/jacoco/test/` paths. In Wave10.6-Q3/Q4 they are referenced from `summary.md`, `java/referenced-paths-only-in-summary.md`, and the checked-in evidence note instead of being copied into a committed artifact tree.

## Recommended Default Command Sequence

### 1. .NET baseline

Default stable path: per-project fallback.

Reason:

- `W10.6-Q1` proved that solution-wide collection can time out.
- The per-project fallback is the accepted stable evidence path.

Run from repo root:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/runtime
dotnet test WorldSim.AI.Tests\WorldSim.AI.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/ai
dotnet test WorldSim.RefineryClient.Tests\WorldSim.RefineryClient.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/refinery-client
dotnet test WorldSim.RefineryAdapter.Tests\WorldSim.RefineryAdapter.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/refinery-adapter
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/scenario-runner
dotnet test WorldSim.ArchTests\WorldSim.ArchTests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/arch
```

Optional local experiment only, not the default evidence command:

```powershell
dotnet test WorldSim.sln --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/test-results
```

If the solution-wide attempt is slow or times out, do not treat that as a Wave10.6 blocker. Use the per-project fallback as the evidence path.

### 2. Java baseline

Run from `refinery-service-java`:

```powershell
.\gradlew.bat test jacocoTestReport
```

Native JaCoCo output paths:

```text
refinery-service-java/build/reports/jacoco/test/jacocoTestReport.xml
refinery-service-java/build/reports/jacoco/test/html/
```

### 3. Local summary artifact

After the commands finish, create or update:

```text
.artifacts/coverage/<run-name>/summary.md
```

Minimum summary contents:

- run id and date
- exact command lines used
- PASS/FAIL status for each lane
- dotnet XML paths
- Java XML/HTML reference paths
- fallback notes, if any
- known caveats about trustworthiness or interpretation

## Interpretation Rules

- Treat the first Wave10.6 baseline as evidence, not as a promotion gate.
- A non-zero lane is useful, but the first Q4 review must still ask whether the number is trustworthy.
- The `.NET` collector output in this repo is lane-first, not yet a merged module-local report. Avoid overclaiming per-test-project Cobertura root percentages as perfect module truth.
- `WorldSim.ArchTests` should stay out of production-module threshold thinking.
- `WorldSim.ScenarioRunner` can still matter even if its first collector lane is weak or surprising; compare it against its evidence/export role, not only line percentages.
- App/Graphics absence from the first baseline is acceptable for now by plan.

## Fallback Notes

- If a project needs a first-run restore after tooling changes, do a one-time restore/test and then repeat the intended `--no-restore` evidence command.
- If Java is already up-to-date, `jacocoTestReport` may finish as `UP-TO-DATE`; this is still acceptable if the XML/HTML outputs are present and referenced.
- If a lane fails, do not invent a merged summary. Record the failed lane, keep the raw local artifact path, and route the gap in the Q4 evidence review.

## First Accepted Baseline Example

The first accepted unified baseline run used:

- Run id: `w10-6-unified-baseline-001`
- Local summary: `.artifacts/coverage/w10-6-unified-baseline-001/summary.md`
- Checked-in evidence note: `Docs/Evidence/Coverage/w10-6-baseline-001/README.md`

## Optional Q6 Manual CI Artifact Lane

Workflow:

```text
.github/workflows/coverage-baseline.yml
```

Trigger:

```text
workflow_dispatch only
```

Behavior:

- Runs on `windows-latest`.
- Runs the accepted per-project `.NET` coverage lanes.
- Runs Java `test jacocoTestReport` for `refinery-service-java`.
- Writes `.artifacts/coverage/<run-name>/summary.md`.
- Uploads `.artifacts/coverage/` as a GitHub Actions artifact.
- Copies Java JaCoCo XML/HTML into the uploaded artifact tree for that manual run.

Policy:

- No PR, push, or scheduled trigger.
- No coverage percentage threshold.
- Test failures can fail the manually-triggered workflow, but coverage percentages do not.
- Uploaded reports are CI artifacts only; raw reports remain non-committed.

Suggested manual input:

```text
run_name = w10-6-ci-manual-001
retention_days = 14
```

First accepted manual CI run:

- Run name: `w10-6-ci-manual-001`
- GitHub run id: `28326071232`
- Status: `Success`
- Duration: `9m 7s`
- Artifact count: `1`
- Summary evidence: `.NET Cobertura XML count = 6`, Java JaCoCo XML present, Java JaCoCo HTML present
- Checked-in evidence note: `Docs/Evidence/Coverage/w10-6-ci-manual-001/README.md`

## Non-Goals For This Runbook

- No CI trigger design.
- No PR/push/scheduled coverage enforcement.
- No generated HTML/XML commit flow.
- No merged cross-language percentage score.
- No claim that line coverage replaces SMR or manual runtime evidence.
