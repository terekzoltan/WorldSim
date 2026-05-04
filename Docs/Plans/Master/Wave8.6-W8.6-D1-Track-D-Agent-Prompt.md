# Wave 8.6 W8.6-D1 - Track D Agent Prompt

Role: Track D - Refinery Boundary / Java Director Semantics

Use this prompt for the first Wave 8.6 implementation slice.

## Required Pre-Reads

- `AGENTS.md`
- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
- `Docs/Plans/Master/Refinery-Live-SMR-Plan.md`
- `Docs/Plans/Master/Wave8.6-Paid-Live-Director-SMR-Plan.md`
- `Docs/Plans/Master/Tools-Refinery-Agent-Guide.md`
- `refinery-service-java/README.md`

## Turn-Gate

Report `READY` only if:
- Wave 8.5 `TR2-D` is `✅`,
- Wave 8.6 exists in the Combined plan,
- and W8.6-D1 is still open.

If any prerequisite is missing, report `NOT READY` and do not implement.

## Scope

Track D owns policy and semantics only.

Allowed scope:
- Java paid/validator semantics documentation,
- Java env policy documentation,
- marker interpretation and telemetry meaning,
- formal/refinery scorecard taxonomy,
- optional Java code only if a real marker/telemetry gap blocks the policy lock,
- Java tests for any Java code changes.

Forbidden scope:
- do not edit `WorldSim.ScenarioRunner` paid lane implementation,
- do not enable C# paid behavior,
- do not add API keys or secrets,
- do not make paid live default,
- do not change bridge patch semantics unless a new plan review approves it.

## Deliverables

1. Lock the Java paid-live profile policy:
   - default model remains `openai/gpt-5.4-mini` unless you find a blocking reason,
   - `PLANNER_LLM_ENABLED=false` remains default,
   - paid requires `PLANNER_LLM_ENABLED=true` and a local `PLANNER_LLM_API_KEY`,
   - `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED=true` is recommended for Wave 8.6 rehearsal/paid pilots,
   - `PLANNER_DIRECTOR_MAX_RETRIES=0` for `paid_micro_total2`,
   - `PLANNER_DIRECTOR_MAX_RETRIES=1` for `paid_probe_2x2x2`.

2. Lock marker and telemetry interpretation:
   - `llmCompletionCount` is observed completion count for one `/v1/patch`,
   - `llmRetryRounds` is validator feedback/correction rounds,
   - `directorStage:*` is pipeline truth,
   - `directorSolver*` is solver-sidecar truth,
   - no artifact may treat `directorStage:refinery-validated` as solver-backed validation.

3. Define formal/refinery quality scorecard terms:
   - valid vs fallback,
   - warning/invariant IDs,
   - budget marker presence,
   - unsupported solver-sidecar claims,
   - stable diagnostic requirements.

4. Produce Track B handoff:
   - exact Java markers Track B should parse or summarize,
   - whether telemetry endpoint is required for Wave 8.6 artifacts,
   - which warning IDs should be scorecard-visible,
   - no-secret/capture assumptions.

## Acceptance Criteria

- Paid-live policy is explicit and safe.
- No paid behavior becomes default.
- Track B has enough semantics to implement guardrails without redefining Java meanings.
- Any Java code changes have focused tests and `./gradlew test` passes.
- If no code changes are needed, state that explicitly and provide docs-only verification.

## Verification

Minimum:
- `./gradlew test` if Java code changed.
- Documentation diff review for policy consistency.
- Confirm no API keys or secret examples are committed.

## Handoff Message Required

Return a concise handoff to Meta and Track B containing:
- verdict GREEN/YELLOW/RED,
- changed files,
- exact marker/telemetry semantics,
- paid preset Java settings,
- remaining risks,
- whether W8.6-B1 may start.
