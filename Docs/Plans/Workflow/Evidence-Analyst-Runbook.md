# Evidence Analyst Runbook

Purpose:
- define a reusable workflow for roles that run headless evidence, benchmark, scenario, replay, or regression lanes,
- keep evidence reports decision-useful,
- and separate evidence production from product-direction authority.

This maps naturally to roles such as:

- `SMR Analyst`
- benchmark analyst
- replay analyst
- simulation evidence owner
- regression-run operator

Status:
- reusable template
- works best when the repo already has stable artifact output and a baseline policy

## Core responsibilities

The Evidence Analyst owns:

1. choosing the right run profile for the question being asked
2. running the evidence tool or harness correctly
3. reading the produced artifacts in a disciplined order
4. ranking the most suspicious runs, not just quoting averages
5. writing a report that distinguishes healthy signals, suspicious signals, and unknowns

## Non-responsibilities

The Evidence Analyst should not:

1. declare architectural direction alone
2. merge or baseline-refresh on its own authority unless the repo explicitly gives that power
3. over-interpret a run shape that cannot answer the actual question
4. use one noisy run as proof of a broad conclusion
5. hide uncertainty when the artifact does not support a strong claim

## Required pre-read set

Before running evidence, read:

1. the current issue, suspicion, or trigger request
2. `[STATUS_DOC]`
3. the relevant follow-up or implementation plan if the run is validating a specific fix
4. the current evidence policy or review protocol
5. the baseline path and compare policy if compare mode exists

## Default evidence workflow

### 1. Start from the question, not the command

State the exact question first.

Examples:

- did the new fix remove the zero-contact lane?
- did perf regress in the large topology profile?
- is the planner-specific change actually planner-specific?
- did the live integration fail because of transport, validation, or apply?

### 2. Choose the smallest run that can answer the question

Typical profile classes:

| Situation | Preferred profile style |
|---|---|
| general health check | smoke or all-around profile |
| planner or strategy comparison | compare profile on same seed and config matrix |
| hotspot diagnosis | deep or drilldown profile |
| performance suspicion | perf-focused long profile |
| live integration smoke | explicit live lane with tighter guardrails |
| post-fix confirmation | triggering profile plus one holdout profile |

If the chosen run cannot answer the question, change the run before you execute it.

### 3. Declare run intent before launch

Every evidence run should declare:

1. profile name
2. seeds
3. planners or variants
4. configs
5. mode
6. artifact dir
7. baseline path or `none`
8. what this run can answer
9. what this run cannot answer

### 4. Read artifacts in a fixed order

Recommended order:

1. `manifest`
2. `summary`
3. `anomalies`
4. `assertions`
5. `compare`
6. `perf`
7. drilldown or timeline artifacts
8. worst-run specific per-run files

This avoids getting lost in raw detail too early.

### 5. Rank the worst runs

Do not stop at run-wide averages.

Always identify:

1. top suspicious runs
2. why they are suspicious
3. whether they cluster around one seed, one planner, one config, or one lane

### 6. Write a disciplined report

Use this structure:

1. `Run Config`
2. `Healthy Signals`
3. `Suspicious Signals`
4. `Worst Runs Ranked`
5. `Unknowns`
6. `Suggested Next Run`

## Truth rules for evidence work

1. artifacts outrank intuition
2. the chosen run shape limits what can be concluded
3. deterministic and non-deterministic lanes should not be mixed casually in one conclusion
4. if compare mode is absent, say that explicitly
5. if a baseline is advisory only, say that explicitly

## Known traps

Common evidence mistakes:

1. using a profile that is too broad for diagnosis
2. interpreting a live or paid lane as if it were exact-baseline deterministic evidence
3. calling a result planner-specific when the bad lane reproduces across planners
4. missing that a perturbation did not actually apply in the runtime
5. reading only success or failure counts without the worst-run context

## Report template

```text
## Run Config
- Profile: ...
- Seeds: ...
- Variants / planners: ...
- Configs: ...
- Mode: ...
- Artifact dir: ...
- Baseline: ...

## Healthy Signals
- ...

## Suspicious Signals
- ...

## Worst Runs Ranked
1. ...
2. ...
3. ...

## Unknowns
- ...

## Suggested Next Run
- ...
```

## Handoff to Meta or tracks

When handing evidence upward, include:

1. the exact question the run answered
2. the strongest supported finding
3. the strongest unsupported but still-open question
4. whether the issue looks like:
   - bug or systemic issue
   - balance or tuning candidate
   - threshold or policy update
   - baseline refresh candidate
   - not enough evidence

The final classification may remain with the Meta Coordinator, but the analyst should narrow it responsibly.

## Minimum re-entry context

After context compression or a new session, re-read:

1. the last approved evidence plan for the issue
2. `[STATUS_DOC]`
3. the latest reviewed evidence note
4. the baseline or compare policy doc if compare mode matters
5. the exact artifact bundle path of the last key run

## Good evidence behavior

Good evidence work is:

- explicit about scope
- artifact-first
- good at ranking worst runs
- honest about unknowns

Weak evidence work is:

- run-heavy but question-light
- average-only
- careless with deterministic versus live evidence
- confident beyond what the artifacts justify
