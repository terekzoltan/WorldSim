# Cross-Track Handoff Protocol

Purpose:
- define a reusable handoff protocol for repos where multiple tracks or role lanes touch adjacent surfaces,
- reduce coordination loss between implementation, consume, evidence, and lock decisions,
- and keep handoffs durable enough for later sessions to re-enter cleanly.

Status:
- reusable template
- designed to work with a central `[STATUS_DOC]` or note board

## Why this protocol exists

In multi-track repos, the common failures are:

1. prep being mistaken for a lock
2. a producer assuming consume already happened
3. a consumer assuming a boundary is stable when it was only transitional
4. a blocker being discussed in chat but not recorded durably

This protocol makes those states explicit.

## Core handoff artifact types

Use these artifact types consistently.

### 1. Prep note

Use when:

- a track has gathered baseline context
- options are being prepared
- dependencies and risks are listed
- but no final decision is locked yet

State label:

- `PREP ONLY`

### 2. Lock note

Use when:

- a consult or ambiguity has been resolved
- downstream work may now rely on the decision

State label:

- `LOCKED`

### 3. Consume handoff

Use when:

- one lane has produced a stable enough output for another lane to consume

State labels:

- `READY FOR IMPLEMENTATION`
- `READY FOR CONSUME`

### 4. Blocker note

Use when:

- downstream work must stop until a missing input, lock, or owner decision is provided

State label:

- `BLOCKED`

### 5. FYI note

Use when:

- visibility matters
- but no immediate downstream action is required

State label:

- `FYI ONLY`

## Minimum contents of any handoff

Every handoff should answer these questions:

1. who is sending it
2. who should consume it
3. what became true now
4. what is still not true
5. what exact downstream action is now allowed or blocked
6. what files, plans, or artifacts must be read next
7. what verification exists behind the claim

## Recommended short format

```text
Status: [READY FOR CONSUME / BLOCKED / LOCKED / PREP ONLY / FYI ONLY]

From:
- [owner / track]

To:
- [owner / track]

What changed:
- ...

What remains out of scope or transitional:
- ...

Required next read:
- ...

Verification:
- ...

Next action:
- ...
```

## When a handoff is not enough

Use a lock note instead of a normal handoff when:

1. the change affects a shared contract or schema
2. the change affects ownership boundaries
3. the change affects future sequencing or gate decisions
4. downstream work would be unsafe if the decision later drifted

## Message-board note format

If your repo uses a short note relay doc like `[STATUS_DOC]`, use a compact durable entry:

```text
[YYYY-MM-DD][Owner] short title - impact - next step
```

Good note qualities:

- short
- durable outside current chat context
- clear about impact
- names the next owner or next step

Bad note qualities:

- too much story
- no stated impact
- no visible next step
- relies on current conversation memory

## Consumer obligations

The receiving track or role should do one of these explicitly:

1. acknowledge and proceed
2. say `NEEDS LOCK`
3. say `BLOCKED`
4. say the handoff is insufficient and list the missing artifact

Silence should not count as consume.

## Producer obligations

The sending track or role should not claim:

1. that consume happened if it only shipped the producer side
2. that a surface is canonical if it is still transitional
3. that downstream is unblocked without naming the exact reason

## Minimum re-entry bundle for handoff-heavy frontiers

If the current frontier depends on handoffs, re-read:

1. `[STATUS_DOC]`
2. latest lock note
3. latest producer closeout
4. latest consumer-ready handoff
5. the active plan for the current frontier

## Copyable examples

### Prep note

```text
Status: PREP ONLY

From:
- Track B

To:
- Meta Coordinator

What changed:
- baseline checklist for shared contract review is complete

What remains out of scope or transitional:
- no decision lock yet

Required next read:
- [contract prep doc]

Verification:
- repo scan complete

Next action:
- Meta review and either LOCKED or BLOCKED
```

### Consume handoff

```text
Status: READY FOR CONSUME

From:
- Track D

To:
- Track A

What changed:
- runtime snapshot now exports the agreed fields

What remains out of scope or transitional:
- wording cleanup deferred

Required next read:
- [handoff doc]

Verification:
- runtime tests and build green

Next action:
- Track A consume in HUD and close its slice
```

### Blocker note

```text
Status: BLOCKED

From:
- Track C

To:
- Meta Coordinator

What changed:
- implementation paused because the needed lock note for the shared context is missing

What remains out of scope or transitional:
- no code change should proceed

Required next read:
- [prep doc]

Verification:
- frontier checked against master plan

Next action:
- lock the consult or defer the epic
```
