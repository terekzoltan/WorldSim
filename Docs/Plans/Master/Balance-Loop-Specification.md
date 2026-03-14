# Balance Loop Specification

## 1. Overview

This document defines a future balance workflow built on the existing SMR infrastructure.
It is event-driven, not continuous, and it does not introduce a new architecture track.

The workflow is:

1. `SMR Analyst` runs the right SMR profile for the current trigger.
2. `Meta Coordinator` reviews the artifacts and classifies the finding.
3. `Balancer` runs as a process lane only for approved tuning cases.
4. `Meta Coordinator` performs verification review and decides merge / reject / iterate.

Important boundaries:

- Code ownership stays with Track B / C / D plus Meta.
- The "Balancer" is an ops lane / shorthand, not a real track beside A-D.
- `Config/balance-surface.json` starts as an unwired draft skeleton.
- That file becomes authoritative only for the subset that is explicitly wired into runtime code.

## 2. Trigger Model

This workflow should start from signals, not from a permanent autonomous loop.

Primary triggers:

- wave-boundary health check
- anomaly or suspicious signal from a normal SMR run
- pre-demo / pre-milestone confidence check
- explicit user / coordinator request

Recommended initial rollout:

- Spec now.
- First diagnostic around Wave 5 kickoff timeframe.
- First real wiring batch only after the first evidence loop proves the workflow is worth operationalizing.

## 3. Agent Responsibilities

### 3.1 SMR Analyst

Responsibilities:

- choose the most relevant SMR profile for the change or suspicion
- run ScenarioRunner and produce the artifact bundle
- write a structured report with:
  - run config
  - healthy signals
  - suspicious signals
  - worst runs (top 3-5)
  - unknowns
  - suggested next action

Profile selection guidance:

| Situation | Preferred profile(s) |
|---|---|
| General health check | `all-around-smoke` |
| AI / planner regression suspicion | `planner-compare`, `clustering-deep` |
| Combat balance suspicion | `combat-smoke` |
| Perf / clustering suspicion | `perf-long`, `clustering-deep` |
| Post-balance verification | same trigger profile + holdout profile |

### 3.2 Meta Coordinator

Responsibilities:

- review analyst report plus artifacts
- compare against baseline and prior evidence when relevant
- classify every finding into exactly one category
- document the result for cross-session / cross-agent context

Classification outcomes:

1. `Bug / systemic issue`
   - hand off to the owning track as a normal fix
   - not a balance patch
2. `Balance tuning candidate`
   - approved for the Balancer lane
3. `Threshold / policy update`
   - goes to Track B / C with explicit spec
4. `Baseline refresh candidate`
   - handled only by Meta Coordinator
5. `Not enough evidence`
   - request another SMR run with a better profile or wider matrix

Output:

- `Balance Brief` in `Docs/Evidence/SMR/` or another explicitly chosen evidence folder

### 3.3 Balancer (process lane)

Responsibilities:

- act only on items classified as `Balance tuning candidate`
- modify only the approved wired surface
- keep changes small and reviewable
- run verification on the triggering profile
- run holdout validation on a different profile / matrix

Non-responsibilities:

- no new assembly ownership
- no architectural refactors
- no structural bug fixing disguised as tuning
- no baseline refresh authority

### 3.4 Meta Coordinator Verification Review

Responsibilities:

- compare original vs verification vs holdout results
- decide one of:
  - approve merge
  - reject and revert
  - iterate once more
- decide whether a baseline refresh is warranted

The baseline decision always stays with Meta Coordinator.

## 4. Balance Surface Policy

### 4.1 Scope Statement

`Docs/Plans/Master/balance-surface.md` is a curated candidate surface map.
It is not a promise that every listed knob is already first-class or safely tunable.

`Config/balance-surface.json` starts as a draft skeleton.
Until runtime code reads a subset of it, that file is not authoritative configuration.

### 4.2 Surface Classes

#### Safe

Low-coupling data / threshold style knobs.
Examples:

- resource yields
- building costs
- hunger / starvation thresholds
- housing capacity style values

These are the best initial wiring candidates.

#### Guarded

Higher-risk knobs that still look tunable, but can distort behavior quickly.
Examples:

- combat damage and defense multipliers
- AI threat thresholds
- crowd penalties
- diplomacy pressure thresholds

These require holdout validation.

#### Blocked

Structural logic and coupled behavior.
Examples:

- planner control flow
- pathing logic
- runtime tick ordering
- structure targeting logic

Blocked items are not balance patches. They go through normal feature / bug workflows.

### 4.3 Initial Scope

Initial scope is a curated candidate subset of roughly 30 parameters.

Initial practical policy:

- `balance-surface.json` first
- `Tech/technologies.json` only later, explicitly approved surface
- `Config/ai-policy.json` only if it becomes a proven first-class tuning surface

## 5. Convergence Protocol

Balance cycles must be bounded.

Rules:

- maximum 3 tuning iterations per cycle
- if a patch introduces a new regression, reject or revert instead of drifting further
- a cycle is considered converged only after verification and holdout both stay healthy
- concrete tolerance bands are defined only after the first diagnostic evidence is available

Tolerance bands are intentionally deferred for now because the current codebase still has significant hardcoded coupling.

## 6. Delta Guards

Early cycles should avoid large parameter swings.

Default rule:

- maximum parameter delta per cycle: about 15-20% of current value

If a larger shift seems necessary:

- split it across multiple cycles, or
- escalate it back to Meta review as a likely non-tuning issue

## 7. Holdout Validation

Optimization on a single matrix is not enough.

Every approved tuning patch should verify on:

1. the original trigger profile / matrix
2. a holdout profile / matrix that the Balancer did not optimize against

Examples:

- issue found on `combat-smoke` -> verify on `combat-smoke` and hold out on `all-around-smoke`
- issue found on `planner-compare` -> verify on `planner-compare` and hold out on `clustering-deep`

## 8. Branch and Review Workflow

Branch model:

- `balance/<context>`

Examples:

- `balance/wave5-combat-tuning`
- `balance/clustering-food-economy`

Review model:

- human-in-the-loop is mandatory in early phases
- each balance cycle should stay small enough for targeted review
- baseline refresh is never coupled automatically to a tuning merge

## 9. Rollout Phases

### Phase 0 - Specification and Diagnostics

- write the workflow spec
- build the candidate surface map
- keep `balance-surface.json` as draft only
- run diagnostic SMR around Wave 5 timeframe
- no balance patching yet

### Phase 1 - First Proven Cycle

- wire a curated safe subset into runtime code
- run the first real tuning cycle only after Phase 0 evidence justifies it
- keep the surface intentionally narrow

### Phase 2 - Guarded Expansion

- expand into guarded parameters only after 1-2 successful cycles
- keep holdout validation mandatory

### Phase 3 - Mature Operations

- allow stronger operational use after the workflow is proven in real cycles
- only then consider deeper Combined-plan integration

## 10. Current Limitations

- balance logic is not data-driven enough yet for an "autonomous balancer" to be safe
- many useful knobs are still embedded in runtime and AI code
- `RuntimeBalanceOptions` exists but is currently not wired
- `CombatConstants` still contains compile-time constants
- the candidate surface map will evolve after source audits and real SMR evidence

## 11. Immediate Next Step

Near-term next step:

- treat this as a documented future workflow
- run the first diagnostic near Wave 5 kickoff timeframe
- only after that decide whether the first curated wiring batch is justified
