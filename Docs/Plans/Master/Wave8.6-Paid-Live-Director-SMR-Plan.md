# Wave 8.6 - Guardrailed Paid LLM Director SMR Pilot

Status: planning draft, ready for Meta/Track review

Owner split:
- Meta Coordinator owns sequencing, acceptance, and paid-run go/no-go decisions.
- Track D owns Java director/refinery semantics, LLM policy, validator/fallback meaning, formal/refinery quality markers, and telemetry meaning.
- Track B owns `WorldSim.ScenarioRunner` paid/validator lane tooling, cost/cap guardrails, artifact shape, and deterministic no-paid tests.
- SMR Analyst owns evidence package execution/review and the final scorecard recommendation.

## 1. Why This Wave Exists

Wave 8.5 proved the safe foundation:
- the real runtime/adapter path can be exercised headlessly through `ScenarioRunner`,
- the `refinery_fixture` and `refinery_live_mock` lanes can produce refinery artifacts,
- Java can expose solver-sidecar truth through normalized `directorSolver*` markers,
- and `core` remains the default deterministic SMR lane.

The missing operational capability is intentional paid-live LLM evidence. The project needs to learn whether the Director can act as a bounded world-level intervention layer during SMR runs, instead of relying only on manual app/F6 observation.

Wave 8.6 pulls a small part of the later paid-live guardrail work forward before Wave 9. It does not replace TR3. It creates a tightly capped, local-only, advisory paid pilot that lets us evaluate the LLM Director under real model calls without making paid behavior part of default CI, generic SMR, or Wave 9 closeout.

## 2. Non-Goals

Wave 8.6 does not:
- make `refinery_live_paid` part of CI,
- make paid runs part of `WORLDSIM_SCENARIO_MODE=all`,
- create a canonical deterministic baseline from paid LLM output,
- auto-tune balance parameters or write runtime config from LLM output,
- expand solver validation beyond the TR2-D core story/directive claims,
- generalize refinery families beyond Director,
- or start Wave 9 supply/campaign implementation.

## 3. Key Definitions

Use these terms consistently in plans, prompts, artifacts, and reviews:

- Run: one `ScenarioRunner` world execution for one seed/planner/config combination.
- Checkpoint: one accepted director trigger inside a run; this is the headless equivalent of one bounded F6-like request.
- Completion: one OpenRouter LLM response request made by the Java service.
- Java correction retry: an additional LLM completion inside one checkpoint after validator feedback rejects or repairs an invalid candidate.
- C# request retry: an additional runtime/adapter HTTP request attempt after request failure. Wave 8.6 paid presets keep this at `0`.
- Concurrency: number of paid runs executing at the same time. Wave 8.6 hard-locks paid concurrency to `1`.

Cost ceiling estimate:

```text
run_count * checkpoints_per_run * (REFINERY_RETRY_COUNT + 1) * (PLANNER_DIRECTOR_MAX_RETRIES + 1)
```

For Wave 8.6 paid presets, `REFINERY_RETRY_COUNT=0` and concurrency is `1`.

## 4. Preset Model

Wave 8.6 should introduce named paid presets rather than asking operators to combine raw env vars by hand.

### 4.1 Required Presets

`paid_micro_total2`

Purpose:
- first real paid LLM smoke with the smallest useful matrix.

Shape:
- 2 seeds,
- 1 planner,
- 1 config,
- 1 checkpoint per run,
- max 1 completion per checkpoint,
- total estimated completions: 2,
- concurrency: 1,
- default capture: `hash`.

Expected Java settings:
- `PLANNER_MODE=pipeline`,
- `PLANNER_LLM_ENABLED=true`,
- `PLANNER_DIRECTOR_MAX_RETRIES=0`,
- `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED=true`,
- default model remains `openai/gpt-5.4-mini` unless Track D changes it in an explicit policy note.

`paid_probe_2x2x2`

Purpose:
- optional second paid probe after the micro run is GREEN; this checks repeated checkpoints and one Java correction retry budget.

Shape:
- 2 seeds,
- 1 planner,
- 1 config,
- 2 checkpoints per run,
- max 2 completions per checkpoint,
- total estimated completions: 8,
- concurrency: 1,
- default capture: `hash`.

Expected Java settings:
- `PLANNER_MODE=pipeline`,
- `PLANNER_LLM_ENABLED=true`,
- `PLANNER_DIRECTOR_MAX_RETRIES=1`,
- `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED=true`.

### 4.2 Custom Presets

Custom paid shapes may be useful later, but Wave 8.6 should only allow custom paid runs if all of these are true:
- explicit paid confirm is present,
- estimated completions are at or below the Wave 8.6 hard cap,
- concurrency is `1`,
- capture is not `full`,
- and the mandatory no-cost rehearsal gate is satisfied.

Wave 8.6 hard cap:

```text
estimated completions <= 8
```

No override above 8 belongs in Wave 8.6. If a larger matrix is desired, it should be a later planning item.

## 5. Mandatory No-Cost Rehearsal Gate

Paid runs must be blocked until a no-cost live-path rehearsal is GREEN.

Recommended rehearsal sequence:
- `refinery_live_validator` or equivalent no-cost live Java pipeline rehearsal,
- Java service running locally,
- `PLANNER_LLM_ENABLED=false`,
- `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED=true`,
- same runtime/adapter path as paid,
- same checkpoint scheduler/wait semantics as paid,
- no API key required.

The rehearsal proves:
- request path reaches Java,
- checkpoint settles,
- apply status is recorded,
- `directorStage:*` is parseable,
- budget/warning markers are captured when present,
- `directorSolver*` markers are parsed if emitted,
- artifacts are complete enough for the scorecard,
- and no request/apply failure blocks the paid run.

Implementation options:
- simplest: require `WORLDSIM_SCENARIO_REFINERY_REHEARSAL_ARTIFACT=<path>` pointing to a GREEN rehearsal artifact before `refinery_live_paid` starts,
- better: add a staged package mode that runs rehearsal first and paid second in one operator command,
- either way: paid must not start if rehearsal is missing or RED.

## 6. Paid Opt-In And Secret Policy

Paid local runs require all of these:
- `WORLDSIM_SCENARIO_LANE=refinery_live_paid`,
- explicit preset selection,
- explicit paid confirmation string,
- `PLANNER_LLM_ENABLED=true` on the Java side,
- `PLANNER_LLM_API_KEY` set only in the local environment,
- explicit request timeout,
- explicit settle timeout,
- cost estimate printed before the first request,
- cost estimate recorded in artifacts,
- and no CI/generic mode involvement.

Recommended confirm string:

```text
WORLDSIM_SCENARIO_REFINERY_PAID_CONFIRM=I_UNDERSTAND_OPENROUTER_COSTS
```

Secret rules:
- never write `PLANNER_LLM_API_KEY` to artifacts,
- never log request headers,
- default paid capture is `hash`,
- `full` capture is not permitted in Wave 8.6 paid presets,
- redacted prompt/response summaries may be considered later, but not required for this wave.

## 7. Scorecard

Wave 8.6 evidence uses a staged full scorecard. It is not a single pass/fail line.

### 7.1 Balance Stability

Question:
- Did the Director make the world less stable, or create regressions compared to no-director/core control?

Signals:
- exit code,
- assertion failures,
- anomaly counts,
- starvation-with-food,
- ecology collapse markers,
- clustering/backoff/no-progress,
- combat contact/routing regressions when combat is enabled,
- economy/survival headline deltas,
- campaign-irrelevant side effects before Wave 9.

Wave 8.6 acceptance:
- paid output is advisory,
- no RED survival/economy/ecology regression may be ignored,
- small YELLOW drift is allowed only with explicit review note.

### 7.2 Director Creativity

Question:
- Did the LLM Director produce useful, non-monotonic, context-aware story/directive choices within constraints?

Signals:
- story beat text hash/frequency,
- directive frequency,
- repeated directive/story monotony,
- relationship between snapshot facts and proposed intervention,
- whether the intervention is plausibly useful without bypassing formal gates.

Wave 8.6 acceptance:
- minimal creativity is enough for `paid_micro_total2`,
- no expectation of rich narrative variety in the first two-call pilot,
- repeated generic output is a YELLOW follow-up, not an automatic blocker.

### 7.3 Failure Hardening

Question:
- Can we diagnose request/apply/fallback/retry failures without the app?

Signals:
- request failure kind,
- apply status,
- fallback count,
- Java `llmStage`,
- `llmCompletionCount`,
- `llmRetryRounds`,
- timeout behavior,
- warning count,
- terminal checkpoint outcome.

Wave 8.6 acceptance:
- no silent lost checkpoint,
- request/apply failure must show a clear reason,
- paid pilot may be YELLOW if the LLM request fails due to external service/API issue, but the artifact must classify it deterministically.

### 7.4 Formal/Refinery Quality

Question:
- Are the validator/refinery constraints good enough to keep the LLM useful but bounded?

Signals:
- `directorStage:*`,
- `directorSolver*`,
- invariant warning IDs,
- fallback vs validated status,
- unsupported claims,
- normalized op ordering,
- budget markers,
- story/directive core coverage.

Wave 8.6 acceptance:
- `directorStage:*` remains pipeline truth,
- `directorSolver*` remains solver-sidecar truth,
- no artifact may treat `directorStage:refinery-validated` as solver-backed validation,
- formal warnings must be visible and categorized.

## 8. Evidence Packages

Recommended local raw artifact paths:
- `.artifacts/smr/wave8.6-validator-rehearsal-001/`
- `.artifacts/smr/wave8.6-paid-micro-total2-001/`
- `.artifacts/smr/wave8.6-paid-probe-2x2x2-001/` optional

Recommended checked-in evidence summary path:
- `Docs/Evidence/SMR/wave8.6-paid-live-director-pilot/README.md`

The checked-in evidence summary should include:
- exact command/environment summary with secrets omitted,
- model name,
- preset name,
- estimated completion cap,
- observed completion count,
- artifact paths,
- scorecard table,
- go/no-go recommendation,
- and whether Wave 9 may start.

## 9. Implementation Sequence

### Step W8.6-D1 - Track D policy lock

Deliverables:
- paid/validator semantics lock,
- Java env policy lock,
- model default confirmation,
- completion/retry marker interpretation,
- formal/refinery scorecard taxonomy,
- no-secret capture policy,
- Track B handoff for required markers and expected Java behavior.

Acceptance:
- plan/docs updated,
- no Java behavior change unless needed to expose already-existing markers safely,
- Java tests updated only if Track D changes policy code,
- paid default remains off.

### Step W8.6-B1 - Track B runner guardrails

Deliverables:
- `refinery_live_validator` enabled as no-cost rehearsal or equivalent staged rehearsal path,
- `refinery_live_paid` enabled only behind explicit confirm and preset,
- paid preflight cost estimate,
- paid hard cap enforcement,
- paid capture guardrails,
- paid artifact fields,
- no-paid regression tests proving `core`, `all`, and CI-safe paths do not run paid,
- focused paid-preflight tests that do not make real API calls.

Acceptance:
- all paid behavior is explicit opt-in,
- max estimate and cap are visible,
- paid custom shapes are bounded,
- rehearsal missing/RED blocks paid,
- no real OpenRouter call is made by automated tests.

### Step W8.6-SMR1 - SMR Analyst rehearsal + paid micro evidence

Deliverables:
- run no-cost rehearsal,
- if GREEN, run `paid_micro_total2` locally with user-provided API key,
- write checked-in evidence summary,
- classify the scorecard,
- recommend GREEN/YELLOW/RED for Wave 9 kickoff.

Acceptance:
- paid micro artifact exists or the failure artifact explains why paid was not reached,
- observed completions do not exceed estimate,
- scorecard has all four blocks,
- no secret leaked.

### Optional Step W8.6-SMR2 - paid probe

Only run if `paid_micro_total2` is GREEN and the user explicitly approves the larger cost envelope.

Deliverables:
- run `paid_probe_2x2x2`,
- update evidence summary,
- compare micro vs probe observations.

Acceptance:
- optional; does not block Wave 9 unless Meta explicitly makes it blocking after micro evidence.

## 10. Closeout Rules

Wave 8.6 can close GREEN if:
- D1 policy is locked,
- B1 guardrails are implemented and tests pass,
- no-cost rehearsal is GREEN,
- paid micro either runs GREEN or fails with a classified external/non-code reason that Meta accepts as YELLOW,
- paid cost cap is enforced,
- no secret appears in artifacts,
- `core` remains default and generic `all` excludes paid,
- and Meta accepts the evidence summary.

Wave 8.6 closes YELLOW if:
- paid micro cannot complete due to external API/key/service issue but guardrails and rehearsal are GREEN,
- and the failure is fully diagnosable.

Wave 8.6 closes RED if:
- paid can run without explicit confirmation,
- estimated completions can exceed the hard cap,
- paid enters default/CI/generic `all`,
- secrets leak,
- request/apply failures are silent,
- or scorecard evidence is missing.

## 11. Relationship To Wave 9 And TR3

Wave 8.6 is serialized before Wave 9 by current Meta decision because paid-live Director evidence can influence how we evaluate future SMR balance loops.

Wave 8.6 does not unblock TR3 by itself. TR3 still requires Wave 10 closeout plus Wave 8.5. Wave 8.6 provides earlier paid-live evidence and guardrail experience that TR3-B should consume later.

## 12. Open Follow-Ups After Wave 8.6

- Whether `season_boundary` should become the preferred paid trigger policy.
- Whether paid evidence should get a manual `workflow_dispatch` lane with GitHub secrets.
- Whether an offline SMR artifact analyzer should rank Director interventions.
- Whether balance tuning should consume Director evidence automatically or remain human-reviewed.
- Whether future paid probes can safely expand above 8 estimated completions.
