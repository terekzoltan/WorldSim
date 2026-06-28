# Wave10.6 Coverage Soft Policy

Status: W10.6-Q5 accepted policy design
Owner: Meta Coordinator
Last updated: 2026-06-28

## Purpose

This document defines the first WorldSim coverage soft-policy after the Wave10.6 baseline.

The policy is advisory. It is meant to guide reviews and future tooling work without creating a hard coverage gate.

## Inputs

- `Docs/Plans/Master/Wave10.6-Coverage-Baseline-And-Test-Quality-Plan.md`
- `Docs/Plans/Master/Wave10.6-Coverage-Runbook.md`
- `Docs/Evidence/Coverage/w10-6-baseline-001/SMR-REVIEW.md`
- `Docs/Evidence/Coverage/w10-6-baseline-001/README.md`
- `Docs/Review-Findings-Registry.md`

## Locked Decisions

1. Wave10.6 does not introduce a hard coverage threshold.
2. No PR, push, scheduled, or CI hard-fail coverage gate is enabled by this policy.
3. Raw generated coverage reports remain local-only unless a later approved artifact workflow says otherwise.
4. The current `.NET` baseline is lane-first evidence, not merged module truth.
5. `WorldSim.ScenarioRunner` is not a production line-coverage target for early policy.
6. `WorldSim.App` and `WorldSim.Graphics` stay outside the first hard line-coverage interpretation lane.

## Lane Classes

### Production Module Advisory Lane

These modules may become future soft-warning candidates after report trust improves:

- `WorldSim.Runtime`
- `WorldSim.AI`
- `WorldSim.RefineryClient`
- `WorldSim.RefineryAdapter`
- `refinery-service-java`

Policy:

- Use coverage as a review signal, not as pass/fail authority.
- Prefer module-local or changed-code interpretation over repo-wide totals.
- Do not compare `.NET` lane root percentages as if they were clean module totals until a merged or module-filtered report exists.

### Evidence Tooling Lane

These projects are reviewed for evidence trust rather than production line percentage:

- `WorldSim.ScenarioRunner`
- `WorldSim.ArchTests`

Policy:

- `WorldSim.ScenarioRunner` quality is governed by artifact-shape tests, schema/backward-compatibility tests, provenance checks, and SMR evidence review.
- The first `WorldSim.ScenarioRunner` collector output of `0%` is an instrumentation/reporting caveat, not a Wave11 blocker and not a reason to force normal percentage targets.
- Fix ScenarioRunner collector behavior only if a future reviewed plan creates a dedicated tools-coverage lane or needs coverage over runner internals.
- `WorldSim.ArchTests` coverage is test/tooling-only and must not be included in production-module threshold thinking.

### Manual And Visual Lane

These areas are initially governed by manual and architecture evidence, not line coverage:

- `WorldSim.App`
- `WorldSim.Graphics`

Policy:

- Require focused manual smoke, UI readability evidence, architecture boundary tests, or snapshot-consume tests when these areas change.
- Do not introduce early hard line-coverage targets for host/rendering/UI surfaces.

## Soft Warning Rules

The following rules are review prompts only. They do not fail CI or block commits by themselves.

### Changed-Code Evidence Prompt

Trigger a review prompt when a change touches production code and none of the following changes or evidence are present:

- a directly related unit/regression test,
- a ScenarioRunner/SMR artifact-shape or evidence-lane test when the change affects evidence exports,
- an architecture/boundary test for ownership or layering changes,
- a manual smoke note for App/Graphics changes where automated line coverage is not the right proof.

This is the preferred first soft policy because it focuses on review relevance instead of headline percentages.

### Trusted Module Delta Prompt

Do not activate module delta warnings until a lane has at least two stable comparable baseline runs with the same report method.

After that, a future policy may warn on candidate defaults such as:

- line coverage drop greater than `3` percentage points for a trusted module-local report,
- branch coverage drop greater than `5` percentage points for a trusted module-local report,
- repeated downward trend across two consecutive baseline runs even if a single drop is below the candidate default.

These candidate values are not active gates in Wave10.6.

### Deep-Review Escalation Prompt

Trigger or recommend deep-review when:

- changed runtime, AI, refinery, or evidence-export code has no credible test/evidence touchpoint,
- an existing SMR artifact schema/provenance contract changes without compatibility tests,
- a future trusted coverage report shows a repeated unexplained module drop,
- a coverage report is used to claim behavior proof that actually requires SMR or manual evidence.

## Future Promotion Criteria

Before any soft warning becomes a regular workflow gate, all required criteria must be met:

1. At least two comparable local baseline runs exist for the target lane.
2. The report source is documented in `Docs/Plans/Master/Wave10.6-Coverage-Runbook.md` or its successor.
3. `.NET` module interpretation is either merged/module-filtered, or explicitly lane-specific with no module-truth overclaim.
4. ScenarioRunner is either explicitly excluded from production coverage policy or has a separate reviewed tools-coverage lane.
5. App/Graphics remain governed by manual/architecture evidence unless a later UI/test strategy says otherwise.
6. Meta review confirms the warning has low false-positive risk and does not incentivize vanity coverage.
7. Human/CI owner approval exists before any CI artifact workflow or automation is added.

## Q6 Status

The user/CI owner explicitly approved optional `W10.6-Q6` after this policy was accepted.

Implemented workflow:

```text
.github/workflows/coverage-baseline.yml
```

Q6 remains non-blocking:

- manual trigger only,
- artifact upload only,
- no PR, push, or scheduled trigger,
- no coverage threshold fail.

## Non-Claims

- This policy does not prove runtime behavior.
- This policy does not replace SMR evidence review.
- This policy does not make ScenarioRunner line coverage a production quality target.
- This policy does not reopen refinery model/solver semantics.
- This policy does not block Wave11.
