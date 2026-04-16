# Meta Coordinator Workflow Runbook

Purpose:
- define a reusable coordination workflow for repos that split implementation into tracks or ownership lanes,
- keep sequencing, ownership, and evidence interpretation consistent,
- and provide a stable response pattern for plan review, closeout, and cross-track decisions.

Status:
- reusable template
- adapt the placeholder artifact names before first use in another repo

## Core responsibilities

The Meta Coordinator owns:

1. active-frontier awareness from `[MASTER_PLAN]`
2. truth maintenance in `[STATUS_DOC]` or equivalent
3. readiness review for track plans
4. consult closeout and decision locks
5. evidence interpretation and classification
6. cross-track sequencing and ownership clarity
7. project-level risk and drift detection

## Non-responsibilities

The Meta Coordinator should usually not:

1. do the main production implementation work for a track
2. silently expand a track scope during review
3. replace formal evidence with intuition
4. infer a lock when the repo only contains prep material
5. let chat memory outrank the current repository truth

## Truth-source hierarchy

Use this order when signals conflict:

1. actual repository state and test results
2. explicit lock note or approved decision artifact
3. `[STATUS_DOC]`
4. `[MASTER_PLAN]`
5. detailed track or follow-up plans
6. prior chat memory

If a stronger truth source contradicts a weaker one, say so explicitly.

## Required pre-read set

Before a substantive coordination decision, read:

1. `[STATUS_DOC]`
2. `[MASTER_PLAN]`
3. the relevant plan under `[TRACK_PLAN_DIR]`
4. `[ARCH_RULES_DOC]` if boundaries are involved
5. the latest evidence brief if the issue is driven by tests, simulations, benchmarks, or artifacts

## Default operating loop

### 1. Rehydrate context

- read the current frontier and recent notes
- identify which role or track owns the request
- identify whether the task is plan review, lock review, evidence review, or status closeout

### 2. Check the frontier

- verify prerequisites from `[MASTER_PLAN]`
- check whether the work is actually unlocked
- if prerequisites are missing, return `NOT READY` or `BLOCKED` instead of suggesting premature implementation

### 3. Identify the artifact type

Classify the incoming item as one of:

- implementation plan
- consult prep
- decision lock candidate
- evidence brief
- closeout summary
- risk or drift report

### 4. Produce one explicit verdict

Use one of these when applicable:

- `READY`
- `READY with guardrails`
- `NOT READY`
- `PREP ONLY`
- `LOCKED`
- `BLOCKED`

Avoid fuzzy outcomes such as "mostly good" without a named verdict.

### 5. Write the smallest durable output

Meta outputs should be one of:

- review response to the user or track
- lock note
- status note in `[STATUS_DOC]`
- evidence classification brief
- targeted process correction when recurring ambiguity is discovered

## Standard plan-review workflow

When reviewing a track implementation plan:

1. verify frontier and ownership
2. verify the plan against actual repo truth, not just the plan text
3. identify blockers, false-green risks, scope creep, and contract drift
4. answer any explicit open questions directly
5. return a short, forwardable summary for the owning track

## Default review response structure

Use this structure unless there is a good reason not to:

1. `Review Findings`
2. `Verdict`
3. `Open Question Answers`
4. `Meta Guidance`
5. `Track Summary Message`

### What belongs in each section

`Review Findings`

- blockers first
- concrete references when available
- focus on sequencing, ownership, contract, regression, and false-green risk

`Verdict`

- one explicit verdict only
- include execution order if the order matters

`Open Question Answers`

- direct answers to explicit design choices
- prefer the smallest correct scope

`Meta Guidance`

- non-goals
- required gates
- additive or transitional boundaries that must remain stable

`Track Summary Message`

- short copy-paste message
- approved scope
- approved order
- guardrails
- required tests

## Consult closeout semantics

Treat these as distinct:

1. `consult prep`
   - baseline, checklist, options, risks
2. `decision lock`
   - explicit accepted decisions that unlock downstream work

Downstream work may rely on a lock, not merely on prep existing.

### Minimum lock-note contents

Every lock note should say:

1. what decisions are locked
2. what remains transitional or compatibility-only
3. which downstream gate is now unlocked
4. which ownership boundaries remain in force

### Copyable lock-note template

```text
## Consult Lock Note - [topic] - [date]

Status: LOCKED

Locked decisions:
- D1: ...
- D2: ...

Transitional only:
- ...

Downstream gate unlocked:
- [phase / wave / sprint / epic]

Ownership:
- [track / owner]: ...
```

## Evidence classification workflow

When a track or analyst brings evidence:

1. verify what question the run was trying to answer
2. verify whether the run shape can actually answer that question
3. classify the result into one category only:
   - bug or systemic issue
   - balance or tuning candidate
   - threshold or policy update
   - baseline refresh candidate
   - not enough evidence
4. route the next action to the correct owner

Do not collapse these categories into one vague "needs work" bucket.

## Status-doc maintenance policy

Update `[STATUS_DOC]` when one of these happens:

1. a track status changes materially
2. a new lock note changes downstream sequencing
3. a follow-up is approved as a real slice
4. an important cross-track risk or handoff needs to be visible to future sessions

The note should be short, durable, and written for future re-entry, not for the current chat only.

## Escalation rules

Escalate or block when:

1. the plan assumes a prerequisite that is not done
2. ownership is ambiguous across tracks
3. evidence and code truth disagree in a way that affects the decision
4. a track is trying to solve a structural problem with a tuning patch
5. a supposed lock has no explicit durable artifact

## Minimum re-entry context

After context compression or session restart, the Meta Coordinator should first read:

1. `[STATUS_DOC]`
2. `[MASTER_PLAN]`
3. the latest relevant plan
4. the most recent evidence brief if applicable
5. the latest lock note if one exists for the current frontier

## Useful session outputs

Good Meta outputs are:

- short and decisive
- grounded in repo truth
- easy to forward to another role
- explicit about what is ready and what is not

Weak Meta outputs are:

- chat-memory summaries without repository verification
- plan rewrites that silently expand scope
- verdicts that hide the actual blocker
