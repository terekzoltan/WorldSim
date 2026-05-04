# Wave 8.6 W8.6-B1 - Track B Agent Prompt

Role: Track B - Runtime/ScenarioRunner Evidence Tooling

Use this prompt after W8.6-D1 policy lock is GREEN.

## Required Pre-Reads

- `AGENTS.md`
- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
- `Docs/Plans/Master/Refinery-Live-SMR-Plan.md`
- `Docs/Plans/Master/Wave8.6-Paid-Live-Director-SMR-Plan.md`
- W8.6-D1 Track D handoff
- `WorldSim.ScenarioRunner/Refinery/RefineryScenarioRunner.cs`
- `WorldSim.ScenarioRunner.Tests/RefineryLaneArtifactTests.cs`

## Turn-Gate

Report `READY` only if:
- Wave 8.5 `TR2-D` is `✅`,
- W8.6-D1 is GREEN,
- and W8.6-B1 is still open.

If W8.6-D1 is missing or unclear, report `NOT READY` and do not implement.

## Scope

Track B owns ScenarioRunner paid/validator tooling and artifacts.

Allowed scope:
- `WorldSim.ScenarioRunner` refinery lane configuration,
- C# runner env parsing and guardrails,
- artifact fields and scorecard plumbing,
- tests that do not call real OpenRouter,
- minimal read-only adapter/runtime seams only if required to expose already-existing status.

Forbidden scope:
- do not redefine Java marker meanings,
- do not edit Java planner semantics,
- do not put API keys in tests or fixtures,
- do not run real paid API calls in automated tests,
- do not include paid in default `core`, generic `all`, CI, or normal build/test flows.

## Deliverables

1. Enable no-cost rehearsal:
   - support `refinery_live_validator` or an equivalent staged no-cost live-path rehearsal,
   - require Java service but not LLM/API key,
   - use the real runtime/adapter path,
   - produce the same refinery artifact family as `refinery_live_mock`,
   - include markers and failure classification.

2. Enable paid lane only behind guardrails:
   - `refinery_live_paid` no longer plain config-error when explicit paid confirm and preset are present,
   - still config-error without confirm,
   - still excluded from generic `all`,
   - still excluded from CI/default mode.

3. Add paid presets:
   - `paid_micro_total2`,
   - `paid_probe_2x2x2`,
   - optional bounded `custom` only if estimate <= 8 and all safety conditions hold.

4. Add preflight cost estimate:
   - estimate formula includes run count, checkpoint count, C# retry count, Java director retry count,
   - estimate is printed before run,
   - estimate is persisted to manifest/summary,
   - hard cap blocks unsafe shapes.

5. Add mandatory rehearsal gate:
   - paid refuses to run without a GREEN rehearsal artifact or an approved staged package path,
   - missing/RED rehearsal gives deterministic config_error,
   - paid never silently skips this check.

6. Add artifact/scorecard fields:
   - paid preset,
   - estimated completion cap,
   - observed completion count when markers are available,
   - paid confirm present as boolean only, not the raw confirm value,
   - rehearsal artifact reference,
   - scorecard blocks for balance stability, director creativity, failure hardening, formal/refinery quality.

## Acceptance Criteria

- `core` lane unchanged.
- `WORLDSIM_SCENARIO_MODE=all` does not include paid.
- Paid lane fails config_error without explicit confirm.
- Paid lane fails config_error if estimated completions exceed 8.
- Paid lane fails config_error without GREEN rehearsal proof.
- Automated tests make no real OpenRouter calls.
- Existing `RefineryLaneArtifactTests` remain green or are updated with equivalent coverage.
- Full ScenarioRunner test suite is green.

## Verification

Minimum commands:

```powershell
dotnet test "WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj" --filter "RefineryLaneArtifactTests"
dotnet test "WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj"
dotnet build "WorldSim.sln"
```

Add focused tests for:
- paid missing confirm,
- paid missing rehearsal,
- paid estimate cap fail,
- paid preset estimate success without making API calls,
- `all` excludes paid,
- validator rehearsal artifact shape.

## Handoff Message Required

Return a concise handoff to Meta and SMR Analyst containing:
- verdict GREEN/YELLOW/RED,
- changed files,
- exact paid env surface,
- preset estimate math,
- no-cost rehearsal command recipe,
- paid micro command recipe with placeholder API key instructions,
- tests run and result,
- remaining risks.
