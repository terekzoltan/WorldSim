# Meta Ideas Inbox

Status: informal intake, not an approved plan
Owner: Meta Coordinator
Review cadence: wave boundary or explicit user request

## Purpose

This file is a lightweight inbox for useful ideas, TODOs, and process improvements that appear during implementation, review, SMR analysis, or manual testing but are not part of the active scope.

Use it to prevent good ideas from disappearing while also preventing them from silently becoming requirements.

This file is not a plan, not a gate, and not final project direction. The Meta Coordinator periodically triages it and either promotes, integrates, defers, rejects, or deletes entries.

## When To Add An Entry

Add an entry only when all of these are true:

- The idea is useful but outside the current approved scope.
- The idea has enough context that a future Meta Coordinator can understand it quickly.
- The idea should not block the current step.
- The idea is not already captured in an active plan, review finding, or evidence doc.

Do not add entries for routine bugs that should be fixed now, vague wishes without action, secrets, raw paid evidence, or large speculative rewrites.

## Entry Rules

- Keep each entry short: one screen or less.
- Always include source context and why it matters.
- Label the likely area: `SMR`, `process`, `tooling`, `runtime`, `graphics`, `AI`, `refinery`, `docs`, or `other`.
- Use `Status: inbox` for new items.
- Include a suggested Meta action: promote, merge into an existing plan, keep for later, reject, or needs investigation.
- If an idea depends on evidence, link the artifact or doc path, not raw pasted logs.
- Do not modify active sequence status from this file.
- Do not treat this file as acceptance criteria unless Meta promotes an entry into an approved plan.

## Meta Triage Rules

At a wave boundary, after a major review, or when the user asks, Meta should triage this file:

1. Read all `Status: inbox` entries.
2. For each entry, choose one outcome:
   - `promoted`: moved into Combined plan, a wave plan, checklist, or active acceptance criteria.
   - `merged`: folded into an existing plan/doc without becoming its own task.
   - `deferred`: intentionally kept for a later wave or post-wave phase.
   - `rejected`: not worth doing; remove or mark with one-sentence reason.
   - `needs investigation`: requires a focused planning/review step before deciding.
3. If promoted or merged, add the destination path and remove the inbox entry once the destination is committed or otherwise durable.
4. If deferred, add a concrete revisit trigger such as `after Wave 10 closeout`.
5. Delete stale entries that have no owner, no evidence, or no clear value after two wave boundaries.
6. If every entry is resolved and no new intake is expected, Meta may delete this entire file to avoid permanent clutter.

## Entry Template

```text
### YYYY-MM-DD - Short Title

Status: inbox
Area: SMR | process | tooling | runtime | graphics | AI | refinery | docs | other
Source context: <where this came from>
Idea: <what to consider>
Why it matters: <benefit / risk reduced>
Suggested Meta action: <promote / merge / defer / reject / investigate>
Suggested revisit trigger: <wave boundary / after specific closeout / explicit user request>
Evidence pointers: <docs/artifacts/commits, if any>
```

## Inbox Entries

### 2026-05-20 - SMR Matrix Lane Manifest

Status: inbox
Area: SMR
Source context: Wave 9 Step 12A SMR prep validation used a compact `3 seeds x 3 planners x 4 scenarios = 36 runs` matrix.
Idea: Add a standard lane manifest section to future SMR package docs explaining why each matrix axis exists: seeds, planners, configs, duration, world size, and expected proof counters.
Why it matters: Makes SMR packages easier to review and prevents accidental over- or under-sampling.
Suggested Meta action: merge into `Docs/Plans/Master/SMR-Minimum-Ops-Checklist.md` or future Wave 9/10 SMR evidence docs if useful.
Suggested revisit trigger: before Step 12B final Wave 9 closeout evidence.
Evidence pointers: `Docs/Evidence/SMR/wave9-campaign-supply/README.md`, `.artifacts/smr/wave9-campaign-supply-focused-001/`.

### 2026-05-20 - Automatic SMR Coverage Summary

Status: inbox
Area: SMR / tooling
Source context: Step 12A validation required manual checking that each deterministic lane produced its required positive counters.
Idea: Add a small generated coverage summary to SMR artifacts that maps each scenario/lane to required counters and pass/fail values.
Why it matters: Reduces manual reviewer work and lowers false-green risk when matrix size grows.
Suggested Meta action: investigate as a ScenarioRunner artifact enhancement after the current Wave 9 closeout path is stable.
Suggested revisit trigger: after Wave 9 final evidence or before Wave 10 SMR prep.
Evidence pointers: `.artifacts/smr/wave9-campaign-supply-focused-001/summary.json`.

### 2026-05-20 - Scenario-Balanced Drilldown Selection

Status: inbox
Area: SMR / tooling
Source context: Step 12A drilldown selected the top 3 runs, which all came from `army_supply_depletion` because scores were tied at zero.
Idea: Add an optional scenario-balanced drilldown mode, for example one selected timeline per configured scenario type, while keeping top-N worst-run drilldown for anomaly work.
Why it matters: Makes review packages more representative when the purpose is surface validation rather than worst-run debugging.
Suggested Meta action: investigate for future SMR tooling; do not block current Step 12A because timeline semantics were still validated.
Suggested revisit trigger: before Wave 10 SMR prep if drilldown review remains manual.
Evidence pointers: `.artifacts/smr/wave9-campaign-supply-focused-001/drilldown/index.json`.

### 2026-05-20 - Standard SMR Verdict Block

Status: inbox
Area: SMR / process
Source context: Step 12A needed explicit separation between prep validation, Step 12B unblock, and final Wave acceptance.
Idea: Standardize an SMR report block with: `prep sufficient`, `Step N unblock recommendation`, `not final acceptance`, `artifact path`, `run count`, `anomaly triage`, and `residual risks`.
Why it matters: Prevents evidence reports from overclaiming acceptance and makes Meta closeout faster.
Suggested Meta action: merge into `Docs/Plans/Master/SMR-Minimum-Ops-Checklist.md` if it fits existing reporting rules.
Suggested revisit trigger: before Step 12B final Wave 9 closeout evidence.
Evidence pointers: current Wave 9 Step 12A validation report.

### 2026-05-20 - Two-Tier SMR Package Pattern

Status: inbox
Area: SMR / process
Source context: Step 12A used a fast deterministic prep matrix; Step 12B will still need broader final evidence.
Idea: Use a two-tier pattern for larger waves: a compact deterministic prep package such as `3x3xN`, then a broader or longer final closeout package focused on organic behavior and endurance.
Why it matters: Keeps prep validation cheap while preserving stronger final acceptance standards.
Suggested Meta action: defer until after Wave 9 final evidence, then decide whether to make it a general Wave 10+ SMR pattern.
Suggested revisit trigger: after Step 12B final Wave 9 closeout review.
Evidence pointers: `Docs/Plans/Master/Wave9-10-SMR-Closeout-Plan.md`, `.artifacts/smr/wave9-campaign-supply-focused-001/`.
