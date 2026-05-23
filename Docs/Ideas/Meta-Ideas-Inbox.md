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

Status: merged into `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` Wave 10 SMR evidence guardrail
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
Suggested Meta action: merged as a Wave 10 guardrail; broader checklist/tooling polish remains optional later work.
Suggested revisit trigger: during Wave 10 SMR prep review, verify each new lane/config has purpose, proof type, required counters, expected positive/zero assertions, and explicit non-claims.
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

### 2026-05-20 - SMR Balance Lab As First-Class Headless Workflow

Status: inbox
Area: SMR / tooling / process / runtime
Source context: Wave 9 Step 12B SMR evidence showed that SMR now supports broad health packages plus targeted deterministic feature proof, but deeper balance diagnosis still depends on manual review. Existing docs already contain partial balance infrastructure (`Balance-Loop-Specification.md`, `Session-Balance-QA-Plan.md`, `SMR-M2-Evidence-Review-Protocol.md`, `balance-surface.md`), but there is no active Combined-plan slice that makes "SMR as Balance Lab" a first-class near-future goal.
Idea: Promote SMR from evidence runner into a headless-first Balance Lab. The first useful version should not be a visual dashboard and should not auto-tune. It should consume existing SMR artifacts and produce analyst-grade scorecards plus human-reviewed tuning suggestions.
Why it matters: The highest-value use of SMR is not only pass/fail regression gating, but balance diagnosis: identifying unstable seeds/planners/configs, explaining why a lane is unhealthy, comparing against baseline or holdout runs, and suggesting bounded tuning candidates without masking structural bugs as "balance changes".
Suggested Meta action: promote into a dedicated future addendum or Combined-plan slice after current Wave 9/10 closeout pressure stabilizes. Likely placement: after Wave 10 closeout or as Wave 11 ecology-prep, because the first domain focus should be ecology plus general health.
Suggested revisit trigger: after Wave 9 final closeout acceptance, or before Wave 11 closed-loop ecology planning.
Evidence pointers: `Docs/Plans/Master/Balance-Loop-Specification.md`, `Docs/Plans/Session-Balance-QA-Plan.md`, `Docs/Plans/Master/SMR-M2-Evidence-Review-Protocol.md`, `Docs/Plans/Master/balance-surface.md`, `Docs/Plans/Master/Wave11-Closed-Loop-Ecology-Redesign-Plan.md`, `.artifacts/smr/all-around-smoke-wave9-001/`, `.artifacts/smr/wave9-campaign-supply-final-001/`.

Proposed scope:
- Headless-first Balance Lab; visual lab much later only if artifact/replay leverage is proven.
- First domains: ecology plus general health.
- Inputs: existing SMR artifact bundles (`summary.json`, `assertions.json`, `anomalies.json`, `compare.json`, `perf.json`, `drilldown/index.json`).
- Outputs: `balance-scorecard.json`, optional `balance-suggestions.json`, and a standard analyst report section.
- Suggestions are advisory only. Meta decides whether a finding is bug, tuning candidate, threshold/policy update, baseline refresh, or insufficient evidence.
- No auto-tuning, no automatic config writes, and no baseline refresh coupling.

Proposed phased plan:
- `BL-0`: consolidate existing balance docs into one current source-of-truth note; mark stale parts of `Session-Balance-QA-Plan.md` as superseded where needed.
- `BL-1`: define a Balance Lab scorecard schema over existing SMR artifacts. Include survival, economy, ecology, clustering/backoff, AI no-plan/replan, planner variance, seed variance, and scenario coverage.
- `BL-2`: add a headless analyzer mode or standalone artifact analyzer that reads one or more artifact dirs and produces ranked findings plus "why this matters" summaries.
- `BL-3`: add human-reviewed tuning suggestion rules. Example: if predator zero windows improve but herbivore zero windows regress, classify as ecology tradeoff, not automatic success.
- `BL-4`: add holdout validation policy. Every tuning suggestion must name the trigger matrix, proposed tuning surface, expected metric movement, and holdout matrix.
- `BL-5`: connect to `balance-surface.md`. Only suggest changes for parameters classified `safe` or explicitly approved `guarded`; never suggest changes for `blocked` structural logic.
- `BL-6`: optional later visual lab. Only after headless scorecards prove useful, add replay/drilldown viewer or dashboard consuming existing artifacts.

Initial acceptance criteria:
- The analyzer can consume at least two existing SMR packages and emit a ranked scorecard without rerunning the sim.
- The scorecard distinguishes hard failures, warnings, advisory drift, and unknowns.
- The scorecard explicitly separates regression evidence from balance interpretation.
- Suggestions include confidence, affected domain, candidate parameter surface, expected direction, and required holdout.
- No suggestion mutates code/config automatically.
- Baseline path and compare availability are explicit.
- The ecology/general-health first matrix is documented with seeds/planners/configs.
- Meta can use the output to decide: bug fix, tuning candidate, threshold update, baseline refresh, or more evidence needed.

Non-goals:
- No visual dashboard in the first slice.
- No auto-tuning loop.
- No LLM/Director-driven balance writes.
- No broad gameplay rebalance.
- No treating deterministic feature probes as organic balance proof.
- No canonical baseline update without Meta review.

Risks:
- False authority: the scorecard may look more objective than the underlying metrics justify.
- Overfitting: suggestions optimized to one matrix may regress holdout lanes.
- Bug masking: structural bugs could be mislabeled as tuning candidates.
- Scope creep: a visual lab/dashboard can consume Track A bandwidth before headless value is proven.
- Baseline drift: canonical baseline refresh must remain a Meta-owned decision.

Suggested first concrete slice:
- Create a headless `balance-lab` artifact-analysis plan focused on ecology plus general health. It should read existing artifact dirs, compute a balance scorecard, rank worst runs, classify evidence gaps, and produce human-reviewed tuning suggestions without changing runtime behavior.
