# Wave10.6 Coverage Baseline 001 - SMR Review Packet

Status: Recommendation packet for Meta classification
Owner: SMR Analyst
Date: 2026-06-27
Run id: `w10-6-unified-baseline-001`

## Scope

This is the Step 4a evidence-review packet for `W10.6-Q4`.

It reviews the first local unified coverage baseline and recommends how Meta should classify the major gaps.

It does not introduce a threshold and does not by itself unblock Wave11. Meta owns the final Step 4b classification.

## Inputs Inspected

- `Docs/Plans/Master/Wave10.6-Coverage-Baseline-And-Test-Quality-Plan.md`
- `Docs/Plans/Master/Wave10.6-Coverage-Runbook.md`
- `.artifacts/coverage/w10-6-unified-baseline-001/summary.md`
- `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/runtime/6675d71f-aba7-460a-84cf-3a012fdcc42a/coverage.cobertura.xml`
- `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/ai/05068286-1ef6-4215-b1ef-ac4de8c8aee0/coverage.cobertura.xml`
- `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/refinery-client/0361681c-c98c-4ccf-aab0-c3167457f75c/coverage.cobertura.xml`
- `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/refinery-adapter/b775e48d-6a5a-46b8-96d9-41e380d65458/coverage.cobertura.xml`
- `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/scenario-runner/58597ab1-090b-43c4-a59c-e6dad19a5f69/coverage.cobertura.xml`
- `.artifacts/coverage/w10-6-unified-baseline-001/dotnet/arch/f9b3b61a-d6c3-4719-9eaf-8d09c3c3cb92/coverage.cobertura.xml`
- `refinery-service-java/build/reports/jacoco/test/jacocoTestReport.xml`
- `refinery-service-java/build/reports/jacoco/test/html/`

## Headline Observations

- The accepted `.NET` baseline path is the per-project fallback from `W10.6-Q1`, not the solution-wide collector path.
- The baseline is useful, but the current `.NET` Cobertura roots are lane-first collector outputs, not yet a merged/module-filtered production truth.
- `WorldSim.Runtime`, `WorldSim.AI`, `WorldSim.RefineryClient`, and Java JaCoCo all show meaningful first-baseline coverage.
- `WorldSim.ArchTests` remains test/tooling-only and should not influence production threshold thinking.
- `WorldSim.ScenarioRunner.Tests` passed in the baseline run, but the collector lane reported `0%` line and branch coverage in the emitted Cobertura XML.

## Major Gap Findings

### 1. Lane-first `.NET` roots limit module-level interpretation

Observed risk:

- The current `.NET` baseline is a per-test-project collector capture.
- Low lane-wide percentages can include broad loaded surface rather than a clean module-local truth.

Recommended classification:

- `future-soft-gate`

Reason:

- This is important for future policy design, but not a direct Wave11 blocker in the first baseline.

Recommended route:

- `W10.6-Q5` should decide whether merged or module-filtered reporting is required before any soft warning policy is promoted.

### 2. `WorldSim.ScenarioRunner` collector lane is zero despite passing tests

Observed risk:

- The baseline run passed `WorldSim.ScenarioRunner.Tests`, but the emitted Cobertura lane reported `0.00%` line and branch coverage.

Recommended classification:

- `test-debt-risk`

Reason:

- This is a real instrumentation or measurement trust problem.
- It does not yet prove a Wave11 ecology/runtime blind spot, but it is strong enough to require an active route.

Recommended route:

- Add an active registry finding.
- Keep the issue visible in `W10.6-Q5` so the later policy design does not treat ScenarioRunner as healthy just because tests are green.

### 3. Runtime/AI/refinery baseline is sufficient for Wave11 non-blocking recommendation

Observed signal:

- `WorldSim.Runtime` and `WorldSim.AI` have meaningful first-baseline coverage.
- `refinery-service-java` now has visible JaCoCo coverage.
- No major uncovered ecology-adjacent runtime blind spot is evident from this first baseline alone.

Recommended classification:

- `accepted-for-now`

Reason:

- The first baseline is strong enough to inform Q5 without forcing a Wave11 stop.

## Recommendation To Meta

Recommended Meta Step 4b decision:

- Accept the first baseline evidence note.
- Keep raw generated reports local-only.
- Route `ScenarioRunner` zero collector output as `test-debt-risk`.
- Route missing merged/module-filtered `.NET` interpretation as `future-soft-gate`.
- Do not classify any current item as `blocked-before-wave11`.

## Non-Claims

- This review does not create a numeric threshold.
- This review does not claim merged module truth from the current `.NET` lane roots.
- This review does not claim SMR/runtime behavior proof is replaced by line coverage.
- This review does not authorize CI gates, PR gates, or raw generated report commits.
