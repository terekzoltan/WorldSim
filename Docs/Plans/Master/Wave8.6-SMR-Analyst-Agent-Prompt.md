# Wave 8.6 SMR Analyst Prompt

Role: SMR Analyst / Meta Evidence Reviewer

Use this prompt after W8.6-B1 is GREEN.

## Required Pre-Reads

- `Docs/Plans/Master/Wave8.6-Paid-Live-Director-SMR-Plan.md`
- `Docs/Plans/Master/Refinery-Live-SMR-Plan.md`
- W8.6-D1 Track D handoff
- W8.6-B1 Track B handoff
- `Docs/Plans/Master/SMR-M2-Evidence-Review-Protocol.md`
- `Docs/Plans/Master/SMR-Minimum-Ops-Checklist.md`

## Objective

Run and review the first staged Wave 8.6 evidence package:
- mandatory no-cost validator rehearsal,
- paid `paid_micro_total2` pilot if rehearsal is GREEN,
- optional `paid_probe_2x2x2` only if the user explicitly approves after micro results.

## Scope

Allowed:
- run local ScenarioRunner evidence commands,
- inspect local `.artifacts/smr/...` outputs,
- write checked-in evidence summary under `Docs/Evidence/SMR/wave8.6-paid-live-director-pilot/README.md`,
- classify scorecard GREEN/YELLOW/RED,
- recommend whether Wave 9 may start.

Forbidden:
- do not commit raw `.artifacts` bundles,
- do not include API keys or raw secrets in docs,
- do not change runtime/Java code,
- do not make paid evidence a deterministic baseline.

## Evidence Package Names

Recommended raw local paths:
- `.artifacts/smr/wave8.6-validator-rehearsal-001/`
- `.artifacts/smr/wave8.6-paid-micro-total2-001/`
- `.artifacts/smr/wave8.6-paid-probe-2x2x2-001/` optional

Recommended checked-in summary:
- `Docs/Evidence/SMR/wave8.6-paid-live-director-pilot/README.md`

## Mandatory Scorecard

Evaluate four blocks:

1. Balance stability
   - survival/economy/ecology/combat headline drift,
   - anomalies,
   - no-progress/backoff/clustering,
   - starvation-with-food or zero-species pressure if present.

2. Director creativity
   - story/directive specificity,
   - monotony,
   - snapshot relevance,
   - whether the intervention looks meaningful without bypassing constraints.

3. Failure hardening
   - request/apply/fallback/retry classification,
   - timeout and error kind clarity,
   - `llmStage`, `llmCompletionCount`, `llmRetryRounds`,
   - terminal checkpoint consistency.

4. Formal/refinery quality
   - `directorStage:*`,
   - `directorSolver*`,
   - invariant/warning IDs,
   - budget marker behavior,
   - unsupported feature visibility.

## Pass/Fail Rules

GREEN:
- rehearsal GREEN,
- paid micro ran or was intentionally skipped by an accepted external issue,
- no secret leaked,
- estimated and observed completions are within cap,
- no default/CI paid behavior observed,
- no RED scorecard item.

YELLOW:
- rehearsal GREEN,
- paid micro blocked by external API/key/service issue but artifact is diagnostic,
- or paid micro runs with non-blocking quality concerns that require follow-up.

RED:
- paid starts without guardrails,
- cap is bypassed,
- secret leaks,
- request/apply failures are silent,
- artifacts cannot support the scorecard,
- or paid evidence causes unclassified serious simulation regression.

## Output Required

Return and document:
- artifact paths,
- exact command summary with secrets omitted,
- model name,
- preset name,
- estimated completion cap,
- observed completion count,
- scorecard table,
- findings and risks,
- recommendation: Wave 9 may start / Wave 9 should wait,
- whether optional `paid_probe_2x2x2` is recommended.
