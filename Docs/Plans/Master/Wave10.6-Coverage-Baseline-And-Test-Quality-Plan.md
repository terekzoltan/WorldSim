# Wave 10.6 Coverage Baseline And Test Quality Plan

Status: Meta-reviewed, accepted for Combined insertion
Owner: Meta Coordinator primary, Track B/D implementation owners by slice
Placement: Proposed post-Wave10.5 quality wave before Wave11 `E11-A`

## Purpose

Wave 10.6 creates the first project-level coverage and test-quality baseline for WorldSim.

The goal is not to chase an arbitrary percentage. The goal is to make test protection measurable across the C# simulation stack and the Java refinery service before the next large runtime wave begins.

This wave should answer:

- Which modules have measurable line/branch coverage today?
- Which critical runtime/refinery paths are protected by tests vs only by smoke/evidence artifacts?
- Which coverage gaps are acceptable for now, and which should become future active gates?
- Can CI or a local workflow produce stable coverage artifacts without paid/live dependencies?

## User Decisions Locked For This Plan

- Workflow placement: `Wave10.6 quality` after Wave10.5 and before Wave11.
- Plan shape: PRD + sequencing.
- Initial scope: `.NET + Java`.
- Gate policy: baseline only for now.
- Threshold policy: collect reports and trends first; add soft thresholds later; do not hard-fail CI until a stable baseline exists.
- Artifact policy: create a repo doc plan now. Implementation happens in later reviewed steps.

## Current Baseline Observations

Observed test projects:

- `WorldSim.Runtime.Tests`
- `WorldSim.AI.Tests`
- `WorldSim.RefineryClient.Tests`
- `WorldSim.RefineryAdapter.Tests`
- `WorldSim.ScenarioRunner.Tests`
- `WorldSim.ArchTests`
- `refinery-service-java` JUnit tests

Observed coverage tooling today:

- `coverlet.collector` exists only in `WorldSim.RefineryClient.Tests`.
- The other C# test projects use xUnit but do not currently declare coverlet.
- `refinery-service-java` uses JUnit through Gradle, but JaCoCo is not currently configured.
- No checked-in unified project coverage report exists.
- SMR evidence validates scenario behavior and balance, but it is not line/branch coverage.

## Non-Goals

- Do not hard-fail PR/CI on coverage in Wave10.6.
- Do not tune gameplay to improve coverage numbers.
- Do not rewrite tests wholesale.
- Do not require paid/live LLM or refinery completions.
- Do not treat generated coverage percentages as proof of scenario/balance correctness.
- Do not block Wave11 on reaching a numeric percentage unless the user/Meta explicitly promotes a later soft gate.
- Do not add flaky UI/MonoGame coverage gates for App/Graphics in the first slice.

## Architecture Principles

1. Coverage is evidence, not authority.

Line coverage helps locate blind spots. It does not replace SMR, manual smoke, architecture tests, or step-review.

2. Baseline first, thresholds later.

The first accepted output is a stable measurement pipeline. Only after baseline stability should the project add soft module thresholds.

3. Module-specific interpretation.

Runtime and refinery logic should have higher expected coverage than App/Graphics host code. ScenarioRunner evidence artifacts may matter more than line coverage for long-run behavior.

4. No paid dependency.

Coverage commands must run with fixture/mock/default-safe modes only.

5. Trend over absolute number.

The first metric is the baseline. The second metric is whether coverage regresses as critical code changes.

## Proposed Coverage Targets By Area

These are not hard gates in Wave10.6. They are interpretation guidance for the baseline review.

| Area | Expected first baseline interpretation | Later soft-threshold candidate |
|---|---|---|
| `WorldSim.Runtime` | Critical domain behavior should be visibly tested; low uncovered seams should be reviewed | medium/high, module-specific |
| `WorldSim.AI` | Planner policy and context mapping should show meaningful coverage | medium |
| `WorldSim.RefineryClient` | Parser/applier/contract handling should be high | high |
| `WorldSim.RefineryAdapter` | Translation/runtime-command boundary should be high | high |
| `WorldSim.ScenarioRunner` | Artifact/export/control-flow coverage useful, but SMR evidence remains separate | medium |
| `WorldSim.ArchTests` | Test project only; not a production module target | n/a |
| `WorldSim.App` | Build/smoke/host behavior more important than line coverage initially | no first-wave target |
| `WorldSim.Graphics` | Visual/manual/arch tests more important than raw coverage initially | low/optional later |
| `refinery-service-java` | Validator/planner/mapper/fallback/solver observability should be visible | medium/high by package |

## Artifact Contract

Coverage runs should write local artifacts under:

```text
.artifacts/coverage/<run-name>/
```

Recommended initial artifact shape:

```text
.artifacts/coverage/<run-name>/
  manifest.json
  summary.md
  dotnet/
    coverage.cobertura.xml
    report/
  java/
    jacocoTestReport.xml
    report/
  merged/
    summary.json
    summary.md
```

Raw artifacts remain local unless a later Meta decision creates a checked-in evidence note. Checked-in docs may summarize a baseline, but should not commit large generated HTML/XML reports by default.

## Proposed Tooling Direction

### C#/.NET

Preferred first implementation:

- Add `coverlet.collector` to all relevant C# test projects.
- Run tests with `--collect:"XPlat Code Coverage"`.
- Emit Cobertura XML.
- Use `reportgenerator` only if it is already available or added in a controlled tooling step.

Candidate commands:

```powershell
dotnet test WorldSim.sln --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/test-results
```

Restore caveat:

- The first verification run after package-reference changes may need `dotnet restore` or a one-time test run without `--no-restore`.
- After restore succeeds, repeat the intended `--no-restore` command for the actual evidence capture.

If solution-wide collection is unstable, fall back to per-project runs:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/runtime
dotnet test WorldSim.AI.Tests\WorldSim.AI.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/ai
dotnet test WorldSim.RefineryClient.Tests\WorldSim.RefineryClient.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/refinery-client
dotnet test WorldSim.RefineryAdapter.Tests\WorldSim.RefineryAdapter.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/refinery-adapter
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/scenario-runner
dotnet test WorldSim.ArchTests\WorldSim.ArchTests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/<run-name>/dotnet/arch
```

### Java

Preferred first implementation:

- Add Gradle `jacoco` plugin.
- Add `jacocoTestReport` XML + HTML generation.
- Do not add `jacocoTestCoverageVerification` hard thresholds in Wave10.6.

Candidate command:

```powershell
Push-Location refinery-service-java
.\gradlew.bat test jacocoTestReport
Pop-Location
```

Expected Java artifact source:

```text
refinery-service-java/build/reports/jacoco/test/html/
refinery-service-java/build/reports/jacoco/test/jacocoTestReport.xml
```

Implementation should copy or summarize the XML/report into `.artifacts/coverage/<run-name>/java/` instead of committing generated reports.

## Wave 10.6 Execution Sequencing

### W10.6-M1 - Coverage Strategy Lock

Owner: Meta Coordinator

Prereq:

- Wave10.5 committed and accepted GREEN.

Scope:

- Review this plan.
- Decide whether Wave10.6 blocks Wave11 or runs as an optional quality lane before Wave11.
- Confirm artifact retention policy and no-hard-gate policy.

Acceptance:

- Plan accepted or amended.
- Combined plan updated only after review approval.
- `ops/PROJECT_STATE.md` points to the approved next action.

Verification:

- Plan-review checks ownership, CI risk, generated artifact policy, and no paid/live dependency.

Unlocks:

- W10.6-Q1 C# coverage infrastructure.
- W10.6-Q2 Java coverage infrastructure.

M1 closeout status:

- Accepted by Meta plan-review with one critic subagent. Required edits were folded into this document before Combined insertion.

### W10.6-Q1 - C# Coverage Infrastructure

Owner: Track B primary, Track D consult for refinery projects, Track C notification/consult for `WorldSim.AI.Tests` package/tooling changes

Prereq:

- W10.6-M1 accepted.

Scope:

- Add `coverlet.collector` consistently to relevant C# test projects.
- Prove coverage collection works for all test projects or document project-specific exclusions.
- Keep App/Graphics out of hard coverage interpretation unless already cheaply measurable through existing tests.

In-scope files likely include:

- `WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj`
- `WorldSim.AI.Tests/WorldSim.AI.Tests.csproj`
- `WorldSim.RefineryClient.Tests/WorldSim.RefineryClient.Tests.csproj` (verify existing `coverlet.collector` remains aligned with the shared package policy)
- `WorldSim.RefineryAdapter.Tests/WorldSim.RefineryAdapter.Tests.csproj`
- `WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj`
- `WorldSim.ArchTests/WorldSim.ArchTests.csproj`
- optionally `Directory.Build.props` if a shared package policy is chosen
- optional script/tooling doc under `Docs/Plans/Master/` or `Docs/Evidence/`

Acceptance:

- Coverage collection runs locally for C# test projects.
- No hard threshold is introduced.
- Generated artifacts go under `.artifacts/coverage/` and are not committed.
- Existing tests still pass.

Suggested verification:

```powershell
dotnet test WorldSim.sln --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/w10-6-dotnet-baseline-001/dotnet/test-results
dotnet build WorldSim.sln --no-restore
git diff --check
```

Review focus:

- Test project package drift.
- Generated artifact leakage.
- Slow/flaky solution-wide coverage collection.
- Whether per-project fallback is needed.
- Track C should be notified if `WorldSim.AI.Tests` package/tooling changes produce warnings or test-run differences.

Unlocks:

- W10.6-Q3 unified local coverage runner/report.

### W10.6-Q2 - Java Coverage Infrastructure

Owner: Track D

Prereq:

- W10.6-M1 accepted.

Scope:

- Add Gradle JaCoCo report generation.
- Generate XML and HTML coverage reports for `refinery-service-java` tests.
- Do not add hard JaCoCo verification thresholds.

In-scope files likely include:

- `refinery-service-java/build.gradle.kts`
- optional Java README or coverage note

Acceptance:

- `test jacocoTestReport` runs locally.
- XML report is generated.
- No paid/live LLM path is required.
- No hard threshold is introduced.

Suggested verification:

```powershell
Push-Location refinery-service-java
.\gradlew.bat test jacocoTestReport
Pop-Location
git diff --check
```

Review focus:

- Gradle plugin/config minimality.
- No CI/default hard fail.
- No generated report committed.

Unlocks:

- W10.6-Q3 unified local coverage runner/report.

### W10.6-Q3 - Unified Coverage Runner And Summary

Owner: Meta/Track B, Track D consult

Prereq:

- W10.6-Q1 and W10.6-Q2 accepted.

Scope:

- Add a lightweight, local-first coverage run recipe.
- Prefer documentation first; script only if repetitive commands become error-prone.
- Produce one summary file that lists coverage by module and records command/status.

Possible outputs:

- `Docs/Plans/Master/Wave10.6-Coverage-Runbook.md`
- optional `tools/coverage/` script if repo already accepts tooling scripts
- `.artifacts/coverage/<run-name>/summary.md` generated locally

Acceptance:

- One documented local command sequence can collect C# and Java coverage.
- Output paths are stable.
- Missing optional report tooling has a clear fallback.
- No threshold gate.

Verification:

```powershell
dotnet test WorldSim.sln --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/w10-6-unified-baseline-001/dotnet/test-results
Push-Location refinery-service-java
.\gradlew.bat test jacocoTestReport
Pop-Location
```

Review focus:

- Reproducibility.
- Windows path safety.
- Avoiding generated artifact commits.
- Whether full solution coverage is too slow for regular use.

Unlocks:

- W10.6-Q4 first baseline evidence review.

### W10.6-Q4 - First Coverage Baseline Evidence Review

Owner: Meta Coordinator / SMR Analyst style review

Prereq:

- W10.6-Q3 accepted.

Scope:

- Run the unified baseline once locally.
- Review module-level results.
- Create a short checked-in evidence note, not raw generated reports.
- Classify gaps as accepted, future soft-threshold candidate, or immediate test-debt risk.

Suggested checked-in output:

```text
Docs/Evidence/Coverage/w10-6-baseline-001/README.md
```

Evidence note should include:

- command lines used
- date/run id
- C# modules measured
- Java packages measured
- headline line/branch coverage if available
- known exclusions
- generated artifact path
- decision: baseline accepted or rerun required

Acceptance:

- First baseline is documented.
- No hard threshold is added.
- Top coverage gaps are explicitly routed.
- Raw generated reports remain local-only.

Review focus:

- Whether coverage numbers are trustworthy.
- Whether any critical module has surprisingly absent coverage.
- Whether gaps should block Wave11 or be deferred.

Unlocks:

- W10.6-Q5 soft-threshold design.

### W10.6-Q5 - Soft Threshold And Changed-Code Policy Design

Owner: Meta Coordinator

Prereq:

- W10.6-Q4 accepted baseline.

Scope:

- Design, but do not necessarily implement, future soft gates.
- Prefer changed-code or module-local warnings over repo-wide hard thresholds.
- Decide when a coverage regression should trigger deep-review.

Candidate policies:

- Warning if a module drops by more than a small delta from baseline.
- Warning if changed production files have no direct or scenario test touchpoint.
- Hard gate only after two or more stable baseline runs.
- Exempt App/Graphics from first hard line-coverage gates; require manual/arch smoke instead.

Acceptance:

- Soft-threshold policy doc exists.
- No hard CI fail is enabled.
- Future promotion criteria are explicit.

Unlocks:

- Optional W10.6-Q6 CI integration.

### Optional W10.6-Q6 - Non-Blocking CI Coverage Artifact Upload

Owner: Meta/Track B, CI owner if separate

Prereq:

- W10.6-Q4 accepted.
- Q5 policy reviewed.

Scope:

- Add `workflow_dispatch` coverage run only, or explicitly defer CI integration.
- Upload coverage artifacts.
- Do not block PRs on coverage in this slice.
- Do not add PR, push, or scheduled coverage triggers until Q4 baseline runtime/flakiness and Q5 policy are reviewed.

Acceptance:

- CI can collect and upload coverage artifacts manually through `workflow_dispatch`, if Q6 is implemented.
- No PR hard-fail threshold exists.
- Runtime cost is acceptable.

Review focus:

- CI minutes/runtime.
- Flaky tests under coverage instrumentation.
- Artifact retention.

## Coverage Gap Classification Policy

When Q4 reviews the first baseline, classify each gap as one of:

- `accepted-for-now`: low-risk or hard-to-measure area.
- `future-soft-gate`: important module but no immediate regression.
- `test-debt-risk`: critical behavior with weak test coverage.
- `blocked-before-wave11`: only if the gap directly threatens Wave11 ecology work.

Every `test-debt-risk` or `blocked-before-wave11` item needs an active route in one of:

- Combined plan acceptance text,
- a dedicated follow-up plan,
- `Docs/Review-Findings-Registry.md`, plus active gate routing,
- `ops/PROJECT_STATE.md` next-action/blocker if it changes sequencing.

## Initial Risk Register

| Risk | Impact | Mitigation |
|---|---|---|
| Coverage instrumentation slows tests | Developers stop running it | Keep baseline local/manual first; no PR hard gate |
| Coverage percentage becomes vanity metric | Bad incentives | Review module gaps and changed-code risk, not only total % |
| App/Graphics line coverage is misleading | False confidence or false alarm | Use manual/arch smoke first; defer UI hard thresholds |
| Java/C# report formats are hard to merge | Friction | Store separate reports first; merge summary later |
| Generated artifacts accidentally committed | Repo bloat | Use `.artifacts/coverage/`; review status before commit |
| SMR and coverage are confused | Wrong proof claims | Keep SMR behavior evidence separate from line coverage |
| Hard thresholds added too early | CI churn | Baseline-only in Wave10.6; soft thresholds only after stability |

## Done Definition For Wave 10.6

Wave 10.6 is done when:

- C# coverage can be collected for all relevant test projects or exclusions are documented.
- Java refinery coverage can be collected with JaCoCo.
- A stable local artifact contract exists under `.artifacts/coverage/<run-name>/`.
- A first baseline evidence note is checked in.
- Top gaps are classified and routed.
- No hard coverage gate is enabled.
- Wave11 readiness is not blocked by unknown test-quality risk.

## Recommended Immediate Next Step

Start W10.6-Q1/Q2 in parallel after Combined insertion: C# coverage infrastructure (Track B primary with Track C/D consult) and Java coverage infrastructure (Track D).

Human/user assistance is most useful during W10.6-Q4, when the first coverage baseline gaps need classification. The user should help decide whether any uncovered ecology-adjacent runtime path is important enough to become `blocked-before-wave11`. Human/CI owner approval is also required before W10.6-Q6 adds any CI artifact workflow.
