# Wave10.6 Coverage Baseline 001

Status: GREEN accepted baseline evidence
Owner: Meta Coordinator
Date: 2026-06-27
Run id: `w10-6-unified-baseline-001`

## Decision

The first unified Wave10.6 coverage baseline is accepted.

This acceptance does not introduce a numeric gate. It records the first stable local coverage/evidence surface before Wave11 runtime work.

Wave11 is not blocked by this baseline.

## Inputs Used

- Step 4a SMR packet: `Docs/Evidence/Coverage/w10-6-baseline-001/SMR-REVIEW.md`
- Runbook: `Docs/Plans/Master/Wave10.6-Coverage-Runbook.md`
- Coverage plan: `Docs/Plans/Master/Wave10.6-Coverage-Baseline-And-Test-Quality-Plan.md`
- Combined sequencing: `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
- Local summary artifact: `.artifacts/coverage/w10-6-unified-baseline-001/summary.md`
- Java JaCoCo XML: `refinery-service-java/build/reports/jacoco/test/jacocoTestReport.xml`
- Java JaCoCo HTML: `refinery-service-java/build/reports/jacoco/test/html/`

## Commands Used

From repo root:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/w10-6-unified-baseline-001/dotnet/runtime
dotnet test WorldSim.AI.Tests\WorldSim.AI.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/w10-6-unified-baseline-001/dotnet/ai
dotnet test WorldSim.RefineryClient.Tests\WorldSim.RefineryClient.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/w10-6-unified-baseline-001/dotnet/refinery-client
dotnet test WorldSim.RefineryAdapter.Tests\WorldSim.RefineryAdapter.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/w10-6-unified-baseline-001/dotnet/refinery-adapter
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/w10-6-unified-baseline-001/dotnet/scenario-runner
dotnet test WorldSim.ArchTests\WorldSim.ArchTests.csproj --no-restore --collect:"XPlat Code Coverage" --results-directory .artifacts/coverage/w10-6-unified-baseline-001/dotnet/arch
```

From `refinery-service-java`:

```powershell
.\gradlew.bat test jacocoTestReport
```

## Execution Result

### .NET lanes

The following PASS notes are based on the accepted local command output from the baseline run plus the presence of emitted Cobertura XML files.

| Lane | Result | Notes |
|---|---|---|
| `runtime` | PASS | `512` tests passed; Cobertura XML emitted |
| `ai` | PASS | `119` tests passed; Cobertura XML emitted |
| `refinery-client` | PASS | `24` tests passed; Cobertura XML emitted |
| `refinery-adapter` | PASS | `48` tests passed; Cobertura XML emitted |
| `scenario-runner` | PASS | `99` tests passed; Cobertura XML emitted |
| `arch` | PASS | `16` tests passed; Cobertura XML emitted |

### Java lane

The following PASS note is based on the accepted local Gradle command output from the baseline run plus the presence of JaCoCo XML/HTML paths.

| Lane | Result | Notes |
|---|---|---|
| `refinery-service-java` | PASS | `test jacocoTestReport` finished `BUILD SUCCESSFUL`; JaCoCo XML + HTML paths present under ignored `build/` |

## Headline Coverage Figures

These are the first locally captured baseline numbers. They are useful review inputs, not hard gates.

### .NET Cobertura lane figures

| Lane | Line coverage | Branch coverage | Local XML |
|---|---|---|---|
| `runtime` | `80.71%` (`12548/15546`) | `74.22%` (`4896/6596`) | `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/runtime/6675d71f-aba7-460a-84cf-3a012fdcc42a/coverage.cobertura.xml` |
| `ai` | `80.02%` (`1262/1577`) | `66.27%` (`798/1204`) | `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/ai/05068286-1ef6-4215-b1ef-ac4de8c8aee0/coverage.cobertura.xml` |
| `refinery-client` | `77.17%` (`328/425`) | `67.02%` (`126/188`) | `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/refinery-client/0361681c-c98c-4ccf-aab0-c3167457f75c/coverage.cobertura.xml` |
| `refinery-adapter` | `28.74%` (`4815/16752`) | `23.46%` (`1670/7118`) | `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/refinery-adapter/b775e48d-6a5a-46b8-96d9-41e380d65458/coverage.cobertura.xml` |
| `scenario-runner` | `0.00%` (`0/20428`) | `0.00%` (`0/8442`) | `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/scenario-runner/58597ab1-090b-43c4-a59c-e6dad19a5f69/coverage.cobertura.xml` |
| `arch` | `1.14%` (`212/18442`) | `0.12%` (`9/7499`) | `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/arch/f9b3b61a-d6c3-4719-9eaf-8d09c3c3cb92/coverage.cobertura.xml` |

### Java JaCoCo figures

| Lane | Line coverage | Branch coverage | Local XML |
|---|---|---|---|
| `refinery-service-java` | `86.26%` (`2040/2365`) | `67.32%` (`855/1270`) | `refinery-service-java/build/reports/jacoco/test/jacocoTestReport.xml` |

## Trustworthiness Caveats

### .NET lane truth is useful but not yet merged module truth

The Wave10.6 `.NET` baseline is currently a per-test-project collector capture.

That means:

- the lane XML roots are useful evidence,
- but they are not yet a de-duplicated merged report by production module,
- and surprisingly low lane-wide percentages can include broad loaded surface rather than a clean single-assembly truth.

This especially affects interpretation of:

- `refinery-adapter` lane-wide percentages,
- `scenario-runner` zero collector output,
- `arch` lane percentages, which are not a production-module target anyway.

### Java trust is better scoped in this baseline

The Java JaCoCo output is a native package-level report generated directly by Gradle. It is still baseline evidence only, but its report-level totals are less ambiguous than the current `.NET` lane roots.

## Gap Classification

| Area / gap | Classification | Reasoning | Route |
|---|---|---|---|
| `WorldSim.Runtime` lane has meaningful baseline coverage | `accepted-for-now` | Ecology-adjacent runtime behavior is visibly covered in the first baseline and does not show an obvious blind spot severe enough to block Wave11. | Revisit in `W10.6-Q5` only if threshold design needs a module-local warning rule. |
| `WorldSim.AI` lane has meaningful baseline coverage | `accepted-for-now` | Planner/policy layer is not absent in the first baseline and does not create a direct Wave11 blocker. | Candidate later soft warning in `W10.6-Q5`. |
| `WorldSim.RefineryClient` lane is healthy enough for first baseline | `accepted-for-now` | Client/parser contract handling is meaningfully covered without paid/live dependency. | Candidate later soft warning in `W10.6-Q5`. |
| `refinery-service-java` JaCoCo baseline exists with good headline totals | `accepted-for-now` | Java validator/planner/mapper surface is now measurable with native report generation and no threshold gate. | Future package-level soft policy design in `W10.6-Q5`. |
| `.NET` baseline lacks a merged/module-filtered summary | `future-soft-gate` | The first baseline is lane-first, which limits how confidently Q4 can interpret module-local percentages. | `W10.6-Q5` should decide whether merged or filtered reporting is needed before any soft gate promotion. |
| `WorldSim.RefineryAdapter` lane percentage is low, but lane scope is broad | `future-soft-gate` | The current lane result is important, but not trustworthy enough by itself to call a direct module regression or Wave11 blocker. | Revisit in `W10.6-Q5` together with merged/module-filtered reporting design. |
| `WorldSim.ScenarioRunner` collector lane reports `0%` despite `99` passing tests | `test-debt-risk` | This is a real risk signal: either instrumentation is not capturing the executable/control-flow surface, or the current lane is not measuring the intended runtime path. | Routed to `Docs/Review-Findings-Registry.md` and `W10.6-Q5` as an active follow-up. |
| `WorldSim.ArchTests` lane is very low | `accepted-for-now` | This is explicitly a test/tooling-only project and not a production target for threshold thinking. | Keep out of first production-module gate design. |
| `WorldSim.App` / `WorldSim.Graphics` absent from the first baseline | `accepted-for-now` | This is allowed by the Wave10.6 plan; manual/architecture smoke matters more initially than raw line coverage here. | Remains a later optional lane, not a Wave11 blocker. |

## Wave11 Unblock Decision

After reviewing the Step 4a SMR packet and the first baseline artifacts, no Q4 item is classified as `blocked-before-wave11`.

Reason:

- the ecology-adjacent runtime and AI lanes are not absent,
- Java refinery baseline coverage is visible,
- the main sharp risks are coverage interpretation and ScenarioRunner instrumentation trust, not a direct unknown runtime blind spot in the Wave11 ecology path.

Wave11 `E11-A` is therefore unblocked by W10.6-Q4.

## Residual Routing

- Keep raw coverage XML/HTML local-only under `.artifacts/coverage/...` and ignored `build/` paths.
- Route the `ScenarioRunner` zero collector lane as an active `test-debt-risk` finding.
- Route the missing merged/module-filtered `.NET` summary as a `future-soft-gate` design item.
- Do not convert this evidence note into a numeric hard gate.

## Next Action

Proceed to `W10.6-Q5` soft-threshold and changed-code policy design.
