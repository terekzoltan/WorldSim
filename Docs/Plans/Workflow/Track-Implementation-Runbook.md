# Track Implementation Runbook

Purpose:
- define a reusable implementation workflow for a track or ownership lane,
- keep track work small, testable, and coordination-friendly,
- and reduce drift between planning, implementation, and closeout.

Status:
- reusable template
- suitable for feature tracks, subsystem tracks, or domain lanes

## Core responsibilities

A track implementation role owns:

1. reading the active frontier before coding
2. implementing only the approved slice for its lane
3. preserving architecture and ownership boundaries
4. running the required verification gates
5. producing a clear closeout and handoff

## Non-responsibilities

A track should not:

1. silently absorb another track's work because it is nearby
2. declare downstream readiness without the needed verification
3. treat a prep note as if it were a lock note
4. broaden the plan because a larger refactor feels cleaner
5. bypass the canonical evidence or test gates for convenience

## Required pre-read set

Before implementation, read:

1. `[STATUS_DOC]`
2. `[MASTER_PLAN]`
3. the track-specific implementation plan or epic doc
4. `[ARCH_RULES_DOC]` if the slice touches boundaries
5. the most recent cross-track handoff or lock note if the slice depends on another lane

## Default implementation workflow

### 1. Check readiness before coding

Answer these questions first:

1. Is the prerequisite actually done?
2. Is the ownership clear?
3. Is the scope already approved?
4. Is there a lock note if the work depends on a consult decision?

If the answer is no, stop and report `NOT READY` or `BLOCKED`.

### 2. Restate the slice in minimal terms

Before editing, write the scope in one tight sentence:

- what is in scope
- what is explicitly out of scope
- which tests or gates must pass

This reduces accidental scope creep.

### 3. Preserve boundaries while implementing

While coding:

1. prefer the smallest correct change
2. preserve the intended layering
3. do not add compatibility or translation layers without a real need
4. keep additive changes additive where the plan expects that
5. avoid hidden cross-track coupling

### 4. Run the required gates

At minimum, run the gates promised by the plan or required by `[QUALITY_GATES]`.

Examples:

- targeted unit tests
- subsystem tests
- solution build
- arch tests
- smoke or replay run
- evidence run if the slice changes runtime, AI, benchmark, or scenario behavior

### 5. Produce closeout with evidence

The closeout should say:

1. what changed
2. what was verified
3. what downstream lane is now unblocked
4. what remains deliberately out of scope

## Track status vocabulary

Use a small, durable vocabulary:

- `NOT READY`
- `READY`
- `IN PROGRESS`
- `BLOCKED`
- `DONE`

If a repo already uses emoji or another encoding, map to that, but keep the meaning stable.

## Handoff states

When another lane depends on your output, use one of these states explicitly:

1. `READY FOR IMPLEMENTATION`
2. `READY FOR CONSUME`
3. `NEEDS LOCK`
4. `BLOCKED`
5. `FYI ONLY`

## Default closeout structure

Use this structure unless the project has a better house style:

1. `Scope`
2. `Implementation`
3. `Verification`
4. `Downstream Handoff`
5. `Remaining Non-Goals`

## Copyable ready/not-ready templates

### `READY`

```text
Status: READY

Approved scope:
- ...

Required gates:
- ...

Guardrails:
- ...
```

### `NOT READY`

```text
Status: NOT READY

Missing prerequisite:
- ...

Why this blocks implementation:
- ...

Next action:
- ...
```

### Closeout / handoff

```text
Status: DONE

Scope shipped:
- ...

Verification:
- ...

Downstream handoff:
- State: READY FOR CONSUME
- Owner: ...
- Consuming lane should read: ...

Still out of scope:
- ...
```

## Cross-track discipline rules

If your track touches a shared surface:

1. say which shared types or files changed
2. say which other lane consumes them
3. say whether a same-PR consume was required
4. do not assume downstream consume happened unless you verified it

## Minimum re-entry context

After context compression or a new session, re-read:

1. `[STATUS_DOC]`
2. `[MASTER_PLAN]`
3. your track plan
4. the last handoff note for your frontier
5. the last closeout note in your own lane if the worktree is already dirty

## Quality bar reminders

Good track work is:

- small
- explicit
- verified
- easy to hand off

Weak track work is:

- wide and under-tested
- dependent on chat memory
- unclear about what became ready
- ambiguous about ownership or remaining scope
