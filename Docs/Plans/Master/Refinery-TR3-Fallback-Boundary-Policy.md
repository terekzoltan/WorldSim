# Refinery TR3 Fallback Boundary Policy

Status: TR3-B implementation artifact
Owner: Track D
Scope: Wave 10.5 TR3-B

## Purpose

TR3-B makes the current deterministic director fallback boundary explicit while the project migrates from imperative Java validation toward real `tools.refinery`-backed formal modeling.

The fallback remains a near-term operational safety net. It is not solver-backed validation, not a second hidden planner, and not a place for growing new director intelligence.

## Current Boundary

- `DirectorRefineryPlanner` owns the validation loop, retry loop, fallback invocation decision, warning composition, and fallback telemetry.
- `DirectorDeterministicFallbackPlanner` owns only deterministic fallback patch construction after validation/retry exhaustion.
- `ComposedPatchPlanner` owns response explain marker composition and keeps existing marker truth unchanged.
- The C# `WorldSim.ScenarioRunner` refinery lane owns paid preset, cap, rehearsal artifact, trigger scheduling, and artifact persistence policy.

## Fallback Output Policy

The deterministic fallback may emit only the existing conservative shapes:

- one story beat when no story cooldown is active,
- one colony directive when at least one colony exists,
- one campaign op only through the existing `campaignEnabled` gate.

The fallback must not emit:

- story effects,
- directive biases,
- causal chains,
- new fallback marker vocabulary,
- paid/live instructions,
- or solver coverage claims.

Campaign fallback behavior is intentionally unchanged in TR3-B: it remains allowed when `campaignEnabled=true` and absent when the gate is off.

## Marker And Telemetry Policy

TR3-B does not add new fallback explain marker vocabulary. Existing response truth remains:

- `directorStage:fallback-deterministic` identifies fallback pipeline stage.
- `directorFallback` warning text identifies validation exhaustion fallback.
- `/v1/director/telemetry` fallback counters remain aggregate operational evidence.

`directorStage:refinery-validated` still does not imply solver-backed validation. Solver-sidecar truth remains isolated to `directorSolver*` markers when explicitly enabled.

## Paid-Live Guardrail Boundary

Java-side Track D responsibilities:

- safe default config (`PLANNER_LLM_ENABLED=false`),
- marker and telemetry truth,
- stable retry/completion semantics,
- documentation of Java-side paid profile expectations.

Track B / ScenarioRunner responsibilities:

- `refinery_live_paid` lane execution,
- paid confirmation,
- rehearsal artifact validation,
- trigger policy execution,
- completion cap enforcement,
- artifact persistence and scorecard output.

TR3-B must not duplicate ScenarioRunner paid-run guardrails in Java and must not run paid evidence.

## Future Policy Notes

`season_boundary` remains future policy maturation and requires a separate Track B scheduling step. TR3-B may reference it as direction only; it does not implement scheduling changes.

Shared fallback/vocabulary marker expansion belongs to TR3-C or later explicit Meta-approved vocabulary work.
