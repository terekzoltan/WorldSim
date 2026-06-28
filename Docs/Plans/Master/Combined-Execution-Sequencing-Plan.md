# Combined Execution Sequencing Plan

Status: Active
Owner: Meta Coordinator
Last updated: 2026-06-16

This document interleaves the Director Integration Master Plan and the Combat-Defense-Campaign
Master Plan into a single wave-based execution schedule with per-item status tracking.

**Execution Steps** are provided per wave starting from Wave 3. They define the concrete session
launch order, prerequisites, and parallelism for each step. Waves 1–2.5 are fully complete and
their execution steps are not retroactively documented — see their proof links for verification.

---

## Reference Key

| Alias | Full Path |
|-------|-----------|
| **Director Plan** | `Docs/Plans/Master/Director-Integration-Master-Plan.md` |
| **Combat Plan** | `Docs/Plans/Master/Combat-Defense-Campaign-Master-Plan.md` |
| **Tools.Refinery Migration Plan** | `Docs/Plans/Master/Tools-Refinery-Migration-Plan.md` |
| **Tools.Refinery Agent Guide** | `Docs/Plans/Master/Tools-Refinery-Agent-Guide.md` |
| **Refinery Live SMR Plan** | `Docs/Plans/Master/Refinery-Live-SMR-Plan.md` |
| **Wave 8.6 Paid Live Director SMR Plan** | `Docs/Plans/Master/Wave8.6-Paid-Live-Director-SMR-Plan.md` |
| **Wave 8.7 Refinery Sidecar Stabilization Plan** | `Docs/Plans/Master/Wave8.7-Refinery-Sidecar-Stabilization-Plan.md` |
| **Wave 9-10 SMR Closeout Plan** | `Docs/Plans/Master/Wave9-10-SMR-Closeout-Plan.md` |
| **Wave 9 Runtime Campaign Hardening Plan** | `Docs/Plans/Master/Wave9-Runtime-Campaign-Hardening-Plan.md` |
| **Wave 10 Campaign Logistics Hardening Plan** | `Docs/Plans/Master/Wave10-Campaign-Logistics-Hardening-Plan.md` |
| **Wave 10.5 Refinery TR3 Audit Gates Plan** | `Docs/Plans/Master/Wave10.5-Refinery-TR3-Audit-Gates-Plan.md` |
| **Pre-W10.6 Refinery Model Fidelity Plan** | `Docs/Plans/Master/Pre-W10.6-Refinery-Model-Fidelity-And-Validation-Assurance-Plan.md` |
| **Wave 10.6 Coverage Baseline Plan** | `Docs/Plans/Master/Wave10.6-Coverage-Baseline-And-Test-Quality-Plan.md` |
| **Wave 10.6 Coverage Soft Policy** | `Docs/Plans/Master/Wave10.6-Coverage-Soft-Policy.md` |
| **Wave 11 Ecology Plan** | `Docs/Plans/Master/Wave11-Closed-Loop-Ecology-Redesign-Plan.md` |
| **Wave 11 Ecology Hardening Plan** | `Docs/Plans/Master/Wave11-Ecology-Hardening-Plan.md` |
| **Wave 12 Architecture Hardening Plan** | `Docs/Plans/Master/Wave12-Codebase-Architecture-Hardening-Plan.md` |

Epic codes (e.g., `S1-A`, `P0-B`) are unique and greppable in the respective master plan.

Reference format: `Director Plan > Phase X Sprint Y > S1-A` or `Combat Plan > Phase X Sprint Y > P0-A`.

Sequencing authority note: this Combined plan, `ops/PROJECT_STATE.md`, and accepted evidence notes are the active workflow authority. If a source plan's status header is stale, use the latest Combined execution rows, progress notes, and accepted evidence summaries for launch decisions.

Refinery pre-read rule: any implementation, review, planning, or closeout step that touches refinery/model artifacts, solver semantics, Java refinery-service behavior, bridge/runtime semantics, or refinery evidence policy must first read `Docs/Plans/Master/Tools-Refinery-Agent-Guide.md` and open every official external Refinery link listed in its `Official References` section. Do not rely on remembered summaries or this Combined note alone.

Wave insertion rule: when adding any new wave or post-wave slice to this Combined plan, optimize for maximum safe parallelism only after preserving sequential clarity. Every new step must state its prerequisites, owner, expected handoff, acceptance/evidence gate, and exactly which later step it unlocks. If parallelization would blur dependency truth, ownership, proof source-of-truth, or closeout responsibility, split the work into smaller steps, serialize it, or place it behind an explicit gate instead of forcing concurrency.

Step/parallelism interpretation rule: numbered execution steps are the sequence authority. Different numbered steps are sequential unless a later step explicitly says otherwise. Multiple `Session` rows inside the same execution step may run in parallel after their own prerequisites are satisfied, and they are expected to close independently before any later step that depends on all of their outputs starts. If one row depends on another row's output, they must not share the same step; split them into separate numbered steps or add an explicit intra-step dependency gate.

Status legend: `⬜` = pending, `🔄` = ongoing, `✅` = done, `❌` = cancelled.

Turn-gate legend (agent handoff safety):
- `NOT READY` = prerequisite epic(s) in the same sprint are not `✅` yet; agent must report blocked state and must not start implementation.
- `READY` = all declared prerequisite epic(s) are `✅`; agent can start and set its own epic to `🔄`.

---

## Parallelism Model

- Director and Combat touch **almost entirely different files** in early phases.
- Within each Wave, Director and Combat sprints can run **in parallel** unless noted.
- Combat sprints have internal ordering: **Track B first -> Track C + Track A after** (B owns snapshot, others consume it).
- Director sprints are often Track D only (especially Phases 2-3, which are Java-side).

### Global guiding constraint

- Reference: `Docs/Plans/Master/world_sim_low_cost_2_d_docs.md`
- Default implementation choices must preserve a low-cost, snapshot-driven, profile-aware baseline.
- Showcase/cinematic visual work is valid, but must remain additive on top of the cheap/stable default path.
- Graphics consumes visual-driving state from snapshot/read-model boundaries; renderer-side gameplay-state recomputation is not an accepted shortcut.

### Merge Risk Zones

| Zone | Wave | Risk | Mitigation |
|------|------|------|------------|
| MR-1 | 2 | Track B snapshot additions for both Director engines AND diplomacy | Same Track B agent handles both; becomes critical once Director state also enters render snapshot (Wave 3 S3-B) |
| MR-2 | 6 | Director budget in DirectorState + Combat siege in tick loop | Additive changes, low risk |
| MR-3 | 7 | Director causal chains + Combat DeclareWar both expand contracts AND runtime | Sequential sprints, not parallel |

### Operator Toggles (current implementation)

| Toggle | Layer | Purpose |
|--------|-------|---------|
| `REFINERY_DIRECTOR_DAMPENING=0.0..1.0` | Runtime | Scales applied director gameplay effects; `0.0` = narrative-only |
| `WORLDSIM_ENABLE_DIPLOMACY=true|false` | Runtime | Enables diplomacy relation updates in live app |
| `WORLDSIM_ENABLE_COMBAT_PRIMITIVES=true|false` | Runtime | Enables combat primitive runtime behavior |
| `WORLDSIM_ENABLE_PREDATOR_ATTACKS=true|false` | Runtime | Enables predator-human attack path |

### Wave Turn-Gate Protocol (all 4 track agents)

- Before starting any Meta/Track/review session, read `ops/PROJECT_STATE.md` first and continue from its recorded phase and next action unless it is stale, incomplete, missing, or contradicted by repo evidence.
- Before starting any epic, the active track agent must check current wave statuses and dependencies in this file.
- If prerequisites are not complete, explicitly report `NOT READY` to the coordinator/user and do not start coding for that epic.
- If prerequisites are complete, report `READY`, then switch the epic status from `⬜` to `🔄` when implementation begins.
- After acceptance + smoke/test gates pass, switch epic status from `🔄` to `✅` only after `ops/PROJECT_STATE.md` is updated, or the handoff explicitly states `No state change from this step.`.
- Do not mark another track's epic `✅` without explicit completion signal from that track owner (or coordinator confirmation).
- For Wave 1 Combat sprint ordering, Track A `P0-D` is blocked until Track B snapshot additions required by P0-D are completed.

### Project State Continuity Protocol

- Canonical state file: `ops/PROJECT_STATE.md`.
- Purpose: keep one compact project-level mental cache for the sequential workflow: `Meta Coordinator -> Track plan -> Meta plan review -> Track implementation -> Meta step review -> Track follow-up`.
- Start rule: every Meta Coordinator, Track, and relevant Swarm review session reads `ops/PROJECT_STATE.md` before this sequencing plan.
- End rule: every meaningful planning, implementation, review, fix, or handoff step updates `ops/PROJECT_STATE.md` with the current phase, last accepted decision, last actor, next concrete action, next expected role, do-not-reopen list, blockers, and evidence pointers.
- Size rule: the state file is a bootloader, not a full log; keep it short and link to detailed evidence instead of copying it.
- No separate per-track state files are required unless a future explicit repo decision changes this.
- Gate rule: a Meta Coordinator may not green-light the next step unless `ops/PROJECT_STATE.md` is fresh enough and points to the correct next role/action.

---

## Execution Refinement Rules (Wave 7+)

These rules refine future Wave 7+ step planning. They do not retroactively rewrite
already completed or in-flight waves.

**Dependency classification:**

| Type | Definition |
|------|------------|
| `contract-dependent` | Requires a stable schema/interface/boundary, but not full upstream implementation |
| `execution-dependent` | Requires an upstream implementation artifact or runtime output to exist first |
| `review-dependent` | Requires a produced artifact for validation/review, not for building upon |

**Parallel launch allowed only when:**

1. Required contract is already stable
2. No hidden upstream execution dependency
3. Work does not collide on the same file area, subsystem surface, or ownership boundary
4. Multiple tracks are not building on an actively moving architectural surface

If these conditions are not met, keep work sequential even if it appears parallelizable.

**Cross-track review steps:**

- Should appear as explicit later steps immediately after the relevant artifacts exist
- Should depend on concrete artifacts, not only on broad sprint completion

**Optimization target:** Maximum safe concurrency while preserving clean architectural boundaries.

---

## Wave 1 — Foundation (Director Phase 0 + Combat Phase 0)

### Sprint D1: Director Contract & Plumbing (Track D only)

> Director Plan > Phase 0 Sprint 1

- ✅ **S1-A** Contract v2 expansion — create `WorldSim.Contracts/v2/` namespace
- ✅ **S1-B** Java PatchOp/Goal expansion + mock director planner
- ✅ **S1-C** C# parser/applier expansion for v2 ops
- ✅ **S1-D** Adapter translation paths — addStoryBeat/setColonyDirective -> commands

Proof links:
- Tests: `WorldSim.RefineryAdapter.Tests/DirectorEndToEndTests.cs`, `WorldSim.RefineryAdapter.Tests/PatchCommandTranslationTests.cs`
- Manual: `Docs/Wave1-Manual-QA-Checklist.md` items 11-15

### Sprint C1: Combat Primitives (Track B -> C -> A)

> Combat Plan > Phase 0 Sprint 1

- ✅ **P0-A** Core damage model — Strength used, Defense added (Track B)
- ✅ **P0-B** Bidirectional predator combat — retaliation (Track B)
- ✅ **P0-C** AI threat response Fight/Flee (Track C, after P0-A/B)
- ✅ **P0-D** Snapshot + UI feedback — health bars, combat markers (Track B + Track A complete)
- ✅ **P0-E** Test harness + balance smoke tests (Track B)

Proof links:
- Tests: `WorldSim.Runtime.Tests/CombatPrimitivesTests.cs`, `WorldSim.Runtime.Tests/SimulationHarnessTests.cs`
- Manual: `Docs/Wave1-Manual-QA-Checklist.md` items 1-10

**Parallelism:** D1 and C1 are **fully parallel** (zero file overlap).

---

## Wave 2 — Runtime Engines + Diplomacy (Director Phase 1a + Combat Phase 1a)

### Sprint D2: Runtime Effects Core (Track B + C + D)

> Director Plan > Phase 1 Sprint 2

- ✅ **S2-A** Domain Modifier Engine — timed modifier engine in `WorldSim.Runtime` (Track B)
- ✅ **S2-B** Goal Bias Engine — timed bias engine + Track C integration (Track B + C)
- ✅ **S2-C** Director State + tick integration + command endpoints (Track B)

Proof links:
- Tests: `WorldSim.Runtime.Tests/DomainModifierEngineTests.cs`, `WorldSim.Runtime.Tests/GoalBiasEngineTests.cs`, `WorldSim.Runtime.Tests/SimulationRuntimeDirectorStateTests.cs`
- Manual: set `REFINERY_DIRECTOR_DAMPENING` and verify director snapshot/event feed state via runtime trigger path

### Sprint C2: Diplomacy & Territory (Track B -> C -> A)

> Combat Plan > Phase 1 Sprint 2

- ✅ **P1-A** Faction stance matrix + persistence (Track B)
- ✅ **P1-B** Relation dynamics triggers — tension/hostility/war (Track B)
- ✅ **P1-C** Territory influence + contested tiles (Track B)
- ✅ **P1-D** Enemy sensing in AI + role system (Track B + C)
- ✅ **P1-E** Diplomacy panel + territory overlay (Track A)

Proof links:
- Tests: `WorldSim.Runtime.Tests/RelationDynamicsTests.cs`, `WorldSim.Runtime.Tests/TerritoryMobilizationTests.cs`, `WorldSim.Runtime.Tests/RuntimeNpcBrainTests.cs`
- Manual: enable `WORLDSIM_ENABLE_DIPLOMACY=true`, then verify `Ctrl+F1` diplomacy panel and `Ctrl+F7` territory overlay

**Parallelism:** D2 and C2 are **parallel with caution** (MR-1).
Both add fields to `WorldSnapshotBuilder` / `WorldRenderSnapshot`.
Same Track B agent should handle both sprints' snapshot additions.

---

## Wave 2.5 — Closeout (Plan/Code Drift + Director Wiring)

Reference: `Docs/Plans/Wave-2.5-Closeout-Plan.md`

Purpose:
- Close Wave 2 gaps found during post-implementation review.
- Make Director v2 ops produce gameplay effects (effects/biases applied), and make diplomacy activatable in-app.
- Align master plan contract examples with actual v2 contract shape.

Classification:
- Post-review closeout wave, not a new feature-family wave.

### Sprint X1: Director Wiring Closeout (Track D + Track B)

- ✅ **W2.5-D1** Adapter translation carries v2 `effects`/`biases` payload into runtime commands
- ✅ **W2.5-B1** Runtime applies beat effects via `DomainModifierEngine` (dampening + deterministic expiry)
- ✅ **W2.5-B2** Runtime applies directive biases via `GoalBiasEngine` (dampening + deterministic expiry)
- ✅ **W2.5-B3** Add minimal observability: `[Director]` event feed entries + snapshot debug fields

### Sprint X2: Diplomacy Activation (Track B + Track A)

- ✅ **W2.5-B4** Add app/runtime activation path for diplomacy (safe default OFF; env or hotkey)
- ✅ **W2.5-A1** Fix UI/doc drift for diplomacy keybinds/legend

### Sprint X3: Plan Consistency (Meta)

- ✅ **W2.5-M1** Update Director master plan contract schema + wire examples to match current `WorldSim.Contracts/v2/`
- ✅ **W2.5-M2** Update Combat master plan notes for keybind drift (suggested vs implemented)

**Parallelism:** This wave is intentionally mostly sequential due to cross-file boundary wiring.

---

## Wave 3 — Beat Tiers + HUD + Fortifications (Director Phase 1b + Combat Phase 1b)

### Sprint D3: Beat Tiers + HUD + Smoke (Track D + A + B)

> Director Plan > Phase 1 Sprint 3

- ✅ **S3-A** Beat severity tier implementation (Track D: contract/adapter | Track B: runtime)
- ✅ **S3-B** HUD and event feed integration (Track A)
- ✅ **S3-C** Output mode matrix end-to-end (Track D)
- ✅ **S3-D** Fixture parity and smoke test (Track D)

### Sprint C3: Basic Fortifications + Pathfinding (Track B -> C -> A)

> Combat Plan > Phase 1 Sprint 3

- ✅ **P1-F** Defense domain scaffold — walls + watchtower (Track B)
- ✅ **P1-G** Navigation/pathfinding v1 — BFS when blocked (Track B)
- ✅ **P1-H** AI defense building + raid skeleton (Track C)
- ✅ **P1-I** Graphics for walls/towers/projectiles (Track A)

### Wave 3 — Execution Steps

**Step 1 — immediately launchable, fully parallel**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P1-F, P1-G, S3-A (runtime part) | — | Do P1-F first, then P1-G, then S3-A runtime |
| Track D agent | S3-A (contract/adapter part), S3-C, S3-D | — | Do S3-A contract first, then S3-C, then S3-D |

**Step 2 — opens when P1-F ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | P1-I (wall/tower graphics) | P1-F ✅ | Does NOT need P1-G; domain model is enough for rendering |

**Step 2b — opens when P1-F ✅ AND P1-G ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P1-H (AI defense + raid skeleton) | P1-F ✅ + P1-G ✅ | Raid pathfinding needs BFS from P1-G |

**Step 3 — opens when S3-A (both parts) ✅ AND Track A finished P1-I**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | S3-B (Director HUD + event feed) | S3-A ✅ (B+D) + P1-I ✅ | Track A bottleneck — last to complete in this wave |

**S3-A cross-track split note:** S3-A is one epic with two session rows. It is `✅` only when
BOTH the Track D part (contract/adapter severity mapping) AND the Track B part (runtime severity
multiplier + cooldown rules) are done. Either track can flag their half as ready in this file;
the epic itself goes `✅` when both halves are confirmed.

**Wave 3 critical path:**

```
Track B: ─ P1-F ──── P1-G ──── S3-A(rt) ─
Track D: ─ S3-A(ct) ──── S3-C ──── S3-D ─   (parallel with Track B)
Track A: ─ (wait) ── P1-I ──── (wait?) ── S3-B ─   (bottleneck)
Track C: ─ (wait) ──────── P1-H ─────────   (after P1-F+G, independent of Track A)
                     ↑           ↑
                P1-F READY   P1-F+G READY
```

**Parallelism:** D3 and C3 are **fully parallel** (zero merge risk).
Track A is the sequential bottleneck: P1-I first (needs P1-F), S3-B second (needs S3-A).
Track C is independent of Track A — can run P1-H while Track A works on P1-I.

---

## Wave 3.1 — Wave 3 Closeout Fixes

Purpose:
- Close the remaining correctness/perf drift discovered after Wave 3 review.
- Keep scope narrow: no new feature family, only state-sync, render correctness, and territory recompute hardening.

### Sprint X3.1: Director/HUD Sync + Beam Correctness + Territory Perf (Track D + B + A)

- ✅ **W3.1-D1** Director effective output mode handoff (Track D)
- ✅ **W3.1-B1** Runtime stores effective director execution status and snapshots it (Track B)
- ✅ **W3.1-A1** HUD consumes effective director mode/source from snapshot (Track A)
- ✅ **W3.1-A2** Watchtower beam target filtering uses faction stance, not "different faction" heuristic (Track A)
- ✅ **W3.1-B2** Territory ownership recompute moves from per-tick full scan to periodic cached recompute (Track B)

### Wave 3.1 — Execution Steps

**Step 1 — Director mode truth sync (Track D -> B -> A)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | W3.1-D1 | Wave 3 ✅ | Adapter computes final effective mode (`both/story_only/nudge_only/off`) and source (`env/response/fallback`) |
| Track B agent | W3.1-B1 | W3.1-D1 ✅ | Runtime stores last effective mode/source/stage and exports it via director render state |
| Track A agent | W3.1-A1 | W3.1-B1 ✅ | HUD must display applied mode, not env default |

**Step 2 — Beam correctness (Track A)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | W3.1-A2 | Wave 3 ✅ | Beam target search must use `FactionStances` + `FactionId`; neutral factions are not valid hostile targets |

**Step 3 — Territory perf hardening (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | W3.1-B2 | Wave 3 ✅ | First step only: periodic recompute + cached ownership/contested state; dirty-region remains future scope |

Acceptance notes:
- HUD `DirectorMode` matches the actual applied output mode, including `auto` cases.
- Watchtower beam only points at runtime-valid hostile/predator targets.
- Territory ownership no longer recomputes as a full-map scan every tick.

Proof targets:
- `W3.1-D1/B1/A1`: adapter/runtime tests for `auto -> story_only`, env override to `off`, and snapshot/HUD mode visibility.
- `W3.1-A2`: stance-based render verification (`Neutral` faction should not get a beam target).
- `W3.1-B2`: runtime test that ownership recompute is periodic/cached while diplomacy behavior remains stable.

**Parallelism:**
- W3.1-D1/B1/A1 is sequential by design.
- W3.1-A2 can run in parallel with W3.1-B2.
- No Track C work is required for this closeout wave.

---

## Wave 3.2 — NPC Clustering + Observability Closeout

Purpose:
- Close the remaining NPC clustering / idle-looking stack issues discovered during manual simulation review.
- Treat diplomacy/war as an amplifier, not the single root cause.
- Prioritize broad runtime correctness: actor overlap, no-progress loops, retreat collapse, and missing observability.

### Sprint X3.2: Occupancy + No-Progress + Retreat Fixes (Track B + A + C)

- ✅ **W3.2-B1** Actor occupancy lite / end-position deconfliction (Track B)
- ✅ **W3.2-B2** Soft reservation for shared targets (resource/build/retreat slots) (Track B)
- ✅ **W3.2-B3** No-progress detection + backoff for pseudo-success movement/action loops (Track B)
- ✅ **W3.2-B4** Replace single-tile flee-to-origin with safe-area / refuge ring behavior (Track B)
- ✅ **W3.2-B5** Export runtime observability for clustering diagnosis (war state, warrior quota, optional NPC cause/target debug) (Track B)
- ✅ **W3.2-C1** Audit planner/AI fallback so peaceful zero-signal states do not collapse into bad defensive loops (Track C)
- ✅ **W3.2-C2** Add crowd-aware fallback preferences where AI already chooses between equivalent actions (Track C)
- ✅ **W3.2-A1** Add stack visibility/debug rendering for overlapping people (Track A)
- ✅ **W3.2-A2** Rename diplomacy panel labels from `F0/F1/F2/F3` to faction names/abbreviations and improve panel title (Track A)
- ✅ **W3.2-A3** Replace placeholder combat overlay with useful debug content (Track A)

### Wave 3.2 — Execution Steps

**Step 1 — Runtime broad fixes first (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | W3.2-B1 | Wave 3.1 ✅ | Prevent arbitrary same-tile stacking as a baseline runtime rule |
| Track B agent | W3.2-B3 | W3.2-B1 ✅ | Detect loops where actions/movement report success without real position change |
| Track B agent | W3.2-B4 | W3.2-B1 ✅ | Retreat/home behavior must stop collapsing civilians onto a single colony origin tile |

**Step 2 — Shared-target deconfliction + observability (Track B, then Track A/C can consume it)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | W3.2-B2 | W3.2-B1 ✅ | Soft reservation only; avoid heavy fully-simultaneous path ownership in this wave |
| Track B agent | W3.2-B5 | W3.2-B3 ✅ | Export `ColonyWarState`, `ColonyWarriorCount`, and optional per-NPC reason/target/no-progress debug |

**Step 3 — AI + UI closeout (Track C + A, partially parallel)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | W3.2-C1 | W3.2-B5 ✅ | Verify peaceful states do not enter defensive/flee-biased fallback spuriously |
| Track C agent | W3.2-C2 | W3.2-B2 ✅ | Prefer alternate equivalent targets when tiles/slots are crowded |
| Track A agent | W3.2-A1 | W3.2-B5 ✅ | Surface stack count / overlap debug so manual QA can see whether overlap still exists |
| Track A agent | W3.2-A2 | Wave 3.1 ✅ | Pure naming/UX cleanup: use faction names/abbreviations instead of `F*` ids |
| Track A agent | W3.2-A3 | W3.2-B5 ✅ | Combat overlay should show meaningful combat/mobilization/contested diagnostics, not placeholder boxes |

### Wave 3.2 — Design Notes

- Diplomacy/war is considered an amplifier for clustering, not the sole root cause.
- The first fixes must target broad/systemic behavior: people overlap, shared target convergence, and no-progress loops.
- `TryMoveTowardsNearestResource(...)` is not treated as the sole driver for this wave; fixes should stay broad and occupancy-focused.
- Retreat/home collapse remains in scope because it worsens clusters even when it is not the original trigger.
- Naming cleanup: the diplomacy panel should move away from `F0/F1/F2/F3` and toward recognizable faction labels (for example `Syl`, `Obs`, `Aet`, `Chi`).

Acceptance notes:
- In peaceful runs, multiple civilians should no longer remain visually collapsed onto one tile for long periods.
- In hostile/war runs, civilian retreat should spread into a local safe area rather than a single origin pixel.
- Manual QA must be able to distinguish: peaceful crowding, diplomacy-driven retreat, contested-tile convergence, and no-progress loops.
- Combat overlay must provide real debugging value.

Proof targets:
- `W3.2-B1/B2/B3`: runtime tests proving overlap prevention, soft reservation, and no-progress backoff in peaceful scenarios.
- `W3.2-B4`: runtime test proving flee/home uses a safe-area distribution rather than a single origin tile.
- `W3.2-B5/A1/A3`: snapshot/HUD/overlay verification that stack counts, war state, contested status, and combat diagnostics are visible.
- `W3.2-C1/C2`: AI tests proving peaceful states avoid bad defensive fallback and crowded equivalent targets can be de-prioritized.

**Parallelism:**
- Step 1 is mostly sequential Track B work.
- After `W3.2-B5`, Track A and Track C can proceed in parallel.
- No Track D work is required for this closeout wave.

---

## Wave 3.5 — Local Worksite + Crowd Persistence Closeout

Purpose:
- Close the remaining post-W3.2 clustering issues where NPCs no longer overlap on one pixel, but still persist in dense local groups.
- Address local pseudo-work loops and local crowd reseeding instead of treating diplomacy/war as the primary cause.
- Focus on broad runtime fixes first: build-site correctness, local dispersal, spawn distribution, and peaceful no-progress coverage.

### Sprint X3.5: Worksite Correctness + Local Crowd Dissipation (Track B + C + A)

- ✅ **W3.5-B1** Align `BuildHouse` start conditions with actual completion cost (Track B)
- ✅ **W3.5-B2** Introduce explicit build-site targeting/state for house/defense construction (Track B)
- ✅ **W3.5-B3** Spawn births onto nearby free land tiles instead of the parent tile (Track B)
- ✅ **W3.5-B4** Upgrade deconfliction from exact-overlap removal to local crowd dissipation / spacing (Track B)
- ✅ **W3.5-B5** Extend no-progress detection/backoff to peaceful gather/build movement loops (Track B)
- ✅ **W3.5-C1** Audit planner warm-up / double-think and other peaceful pseudo-idle execution mismatches (Track C)
- ✅ **W3.5-A1** Improve tracked/debug UI so local cluster cause is visible (`decision cause`, `target key`, build-site intent) (Track A)

### Wave 3.5 — Execution Steps

**Step 1 — Runtime loop correctness first (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | W3.5-B1 | Wave 3.2 ✅ | Remove builder pseudo-work loop caused by half-cost start vs full-cost finish mismatch |
| Track B agent | W3.5-B2 | W3.5-B1 ✅ | Jobs must target a concrete build site before work begins; stop building "wherever the actor stands" |
| Track B agent | W3.5-B3 | Wave 3.2 ✅ | Reduce local crowd reseeding from births/spawn concentration |

**Step 2 — Local cluster dissipation (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | W3.5-B4 | W3.5-B2 ✅ | Do more than same-tile separation; spread persistent local groups over nearby free space |
| Track B agent | W3.5-B5 | W3.5-B2 ✅ | Peaceful gather/build travel also needs no-progress/backoff coverage |

**Step 3 — AI/Debug closeout (Track C + A, parallel after runtime base is ready)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | W3.5-C1 | W3.5-B5 ✅ | Verify peaceful execution does not burn a planner step or enter pseudo-idle loops spuriously |
| Track A agent | W3.5-A1 | W3.5-B5 ✅ | Surface cluster cause/debug intent clearly for manual QA and tracked NPC inspection |

### Wave 3.5 — Design Notes

- Diplomacy/war remains an amplifier, but is not treated as the primary root cause in this wave.
- The current main hypotheses are:
  - in-place/local work loops,
  - build jobs starting without a concrete site,
  - births/spawn re-densifying already crowded local areas,
  - overlap-only deconfliction not truly dispersing groups.
- `TryMoveTowardsNearestResource(...)` is not the focal bug for this wave.
- This wave should improve behavior in peaceful simulations first; war/retreat cases should benefit secondarily.

Acceptance notes:
- In peaceful runs, dense local NPC groups should decay over time instead of reforming around the same few tiles.
- Builders should not repeatedly "work" in place when construction is not actually affordable or no build site was selected.
- New births should not keep reseeding the same crowded tile.
- Manual QA should be able to tell whether a local cluster is caused by build intent, resource intent, retreat intent, or planner/no-progress fallback.

Proof targets:
- `W3.5-B1/B2`: runtime tests for build start/finish consistency and explicit build-site flow.
- `W3.5-B3/B4`: runtime tests showing births and crowd dissipation reduce persistent local clustering.
- `W3.5-B5/C1`: tests proving peaceful gather/build loops back off when no real movement/work progress occurs.
- `W3.5-A1`: tracked/debug UI exposes decision cause + target clearly enough for manual clustering diagnosis.

**Parallelism:**
- Step 1 and Step 2 are primarily sequential Track B work.
- After `W3.5-B5`, Track C and Track A can proceed in parallel.
- No Track D work is required for this closeout wave.

---

## Wave 3.6 — Clustering Evidence + Manual QA Control Closeout

Purpose:
- Close the remaining W3.5 review gaps before the project treats local grouping as "good enough" or naturally emergent.
- Distinguish natural short-lived grouping from pathological stuck clustering using repeatable evidence instead of one-off manual impressions.
- Add multi-run headless telemetry and lightweight in-app sim-speed control so future sessions can investigate clustering with less guesswork.

### Sprint X3.6: Clustering Validation + Telemetry Matrix (Track B + C + A)

- ✅ **W3.6-B1** Make birth spawn and build-site selection truly actor-free, not only structure-free (Track B)
- ✅ **W3.6-B2** Protect active peaceful work intents from crowd dissipation / false site invalidation (Track B)
- ✅ **W3.6-B3** Export clustering telemetry and stuckness counters from runtime/headless paths (Track B)
- ✅ **W3.6-B4** Extend `WorldSim.ScenarioRunner` into a structured multi-config + multi-planner clustering matrix runner (Track B)
- ✅ **W3.6-C1** Reconcile the W3.2 crowd-aware tie-break behavior with current code/tests/docs (Track C)
- ✅ **W3.6-A1** Add stable tracked-actor identity to AI debug snapshot/render plumbing (Track A)
- ✅ **W3.6-A2** Add simulation speed controls + HUD indicator for manual QA (`pause`, slower, faster, optional single-step) (Track A)

### Wave 3.6 — Execution Steps

**Step 1 — Runtime correctness gaps first (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | W3.6-B1 | Wave 3.5 ✅ | "Free tile" must mean actor-free for births and build placement unless an explicit hard fallback is taken |
| Track B agent | W3.6-B2 | W3.6-B1 ✅ | Crowd dissipation must not relocate active gather/build workers in a way that cancels or corrupts their current intent |

**Step 2 — Instrumentation for evidence, not anecdotes (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | W3.6-B3 | W3.6-B2 ✅ | Add counters/metrics for overlap resolves, local crowd moves, birth fallback, build-site resets, no-progress by cause, and dense-neighborhood persistence |
| Track B agent | W3.6-B4 | W3.6-B3 ✅ | ScenarioRunner should run many seeds/configs/planner modes and emit structured output (`json`/`jsonl` or equivalent) for later comparison |

**Step 3 — AI/UI/manual QA closeout (Track C + A, parallel after runtime telemetry is stable)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | W3.6-C1 | W3.6-B3 ✅ | Either restore crowd-aware equivalent-action preference with tests, or explicitly retire/update the W3.2 claim so code/tests/docs agree |
| Track A agent | W3.6-A1 | W3.6-B3 ✅ | Tracked NPC debug must resolve a stable actor identity, not whichever actor currently shares the tile |
| Track A agent | W3.6-A2 | Wave 3.5 ✅ | Manual QA needs direct runtime speed control without rebuilding or editing constants |

**Step 4 — Cross-mode evidence gate (Track B + A/C consumption)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Coordinator / QA session | W3.6 evidence pass | W3.6-B4 ✅ + W3.6-C1 ✅ + W3.6-A1/A2 ✅ | Run repeated `Simple/GOAP/HTN` matrices on the same seed sets; compare clustering metrics before drawing planner-quality conclusions |

### Wave 3.6 — Design Notes

- This wave does **not** assume that visible grouping is inherently wrong; the goal is to separate natural congregation from true stuckness.
- Manual testing remains useful, but one-off runs are treated as directional hints only. Wave 3.6 adds infrastructure so repeated runs can answer the question with evidence.
- `W3.6-B1` should tighten the semantics of "free tile":
  - actor-free for birth spawn,
  - actor-free for explicit build-site targeting,
  - explicit fallback only when no actor-free option exists.
- `W3.6-B2` should treat active peaceful work as protected state for dissipation purposes. Idle or loosely moving actors may still be spread, but workers should not be silently displaced off a valid work target mid-flow.
- `W3.6-B3/B4` should prefer lightweight structured telemetry over screenshots/log scraping. The goal is reproducible comparison across seeds, planners, and configs.
- The telemetry runner should support at least:
  - multiple seeds,
  - multiple planner modes (`Simple`, `Goap`, `Htn`),
  - optional config matrix (map size / pop / feature flags / ticks),
  - machine-readable output for later sessions.
- Suggested clustering metrics:
  - exact overlap resolve count,
  - local crowd dissipation move count,
  - average / max local neighbor density,
  - repeated dense-neighborhood dwell windows,
  - no-progress backoffs by cause (`resource`, `build`, `flee`, `combat`),
  - build-site reset count,
  - birth fallback-to-parent count.
- `W3.6-C1` exists because current code drifted from the previously documented W3.2 crowd-aware tie-break claim; this wave must resolve the mismatch explicitly.
- `W3.6-A2` is intentionally small but high-value: sim-speed control should make clustering/manual QA faster to inspect without changing game logic. Showing current speed in HUD/debug is part of the feature, not optional polish.

Acceptance notes:
- Births and build-site targeting no longer choose a person-occupied tile unless a clearly defined hard fallback path is active.
- Crowd dissipation reduces idle/local blobs without causing active workers to "teleport off" their job and enter new pseudo-failure loops.
- The project can run repeated headless clustering matrices and keep structured results for later analysis instead of relying only on memory or screenshots.
- Manual QA can pause, slow down, and speed up the sim while always seeing the current speed state.
- AI debug tracking stays pinned to the same actor even when multiple people temporarily share or cross the same tile.
- After W3.6, planner-mode comparison (`Simple` vs `Goap` vs `Htn`) should be evidence-backed, not inferred from one manual run.

Proof targets:
- `W3.6-B1/B2`: runtime tests for actor-free birth/build selection and for active build/gather flows surviving deconfliction/dissipation correctly.
- `W3.6-B3`: runtime/headless tests proving the new counters are exported deterministically.
- `W3.6-B4`: ScenarioRunner output sample + test coverage for multi-seed / multi-planner structured reporting.
- `W3.6-C1`: AI tests and doc alignment proving either restored crowd-aware preference or explicit retirement of that behavior.
- `W3.6-A1`: snapshot/UI verification that tracked NPC debug uses stable identity rather than tile-only lookup.
- `W3.6-A2`: manual smoke checklist item(s) proving pause/speed changes work and HUD reflects the active rate.

**Parallelism:**
- `W3.6-B1 -> W3.6-B2 -> W3.6-B3 -> W3.6-B4` is the main Track B critical path.
- After `W3.6-B3`, Track C (`W3.6-C1`) and Track A (`W3.6-A1`) can proceed in parallel.
- `W3.6-A2` can run in parallel with the later Track B work because it is primarily host/HUD level.
- No Track D work is required for this closeout wave.

---

## Wave 4 — Refinery Gate + Military Tech (Director Phase 2a + Combat Phase 2)

### Sprint D4: Formal Model + Validation Loop (Track D only — Java)

> Director Plan > Phase 2 Sprint 4

- ✅ **S4-A** Formal model layers in Java — all Phase 0-2 invariants (INV-01 through INV-14, INV-20)
- ✅ **S4-B** Validation/repair loop + fallback planner

### Sprint C4: Military Tech + Advanced Defenses (Track B -> C -> A)

> Combat Plan > Phase 2 Sprint 4

- ✅ **P2-A** Military + fortification techs in `technologies.json` (Track B)
- ✅ **P2-B** Colony equipment levels — weapon/armor (Track B)
- ✅ **P2-C** Advanced defenses — stone walls, gates, arrow/catapult towers (Track B)
- ✅ **P2-D** AI becomes tech-aware — avoid unwinnable fights (Track C)
- ✅ **P2-E** Graphics and HUD updates (Track A)

#### P2-D expanded spec (Track C agent input)

**Prereq:** P2-A ✅ + P2-B ✅ (equipment/tech domain must exist before AI can reason about it)

**Context fields to add to `NpcAiContext` (`WorldSim.AI/Abstractions.cs`):**

| Field | Type | Source |
|-------|------|--------|
| `HomeWeaponLevel` | `int` | `_home.WeaponLevel` |
| `HomeArmorLevel` | `int` | `_home.ArmorLevel` |
| `HomeMilitaryTechCount` | `int` | count of military techs unlocked (`weaponry`, `armor_smithing`, `military_training`, `war_drums`, `scouts`, `advanced_tactics`) |
| `HomeFortificationTechCount` | `int` | count of fortification techs unlocked (`fortification`, `advanced_fortification`, `siege_craft`) |

Enemy equipment is **not** observable at the individual AI level (no espionage mechanic yet). The AI uses its own equipment as a relative proxy.

**`BuildThreatContext` wiring (`Person.cs`):** Populate the four new fields from `_home` and `_home.UnlockedTechIds` (or equivalent colony accessor). Do not add enemy-tech sniffing.

**`ThreatDecisionPolicy.ShouldFight` changes (`WorldSim.AI/ThreatDecisionPolicy.cs`):**

- Add an equipment disadvantage check: if `HomeWeaponLevel == 0 && HomeArmorLevel == 0` and `LocalThreatScore >= 0.55f`, reduce effective fight willingness (return false earlier, or apply a penalty multiplier to `power`).
- The intent: un-upgraded colonies retreat rather than suicidally fighting into a high-threat scenario.
- Keep the existing warrior-role gate and health gate intact.
- Threshold values: tunable constants, not magic numbers inline.

**New AI goal: `UnlockMilitaryTech` (all planners):**

- Condition: `IsWarStance == true || (IsHostileStance && LocalThreatScore >= 0.4f)` AND `HomeMilitaryTechCount < 3`.
- Command maps to a new `NpcCommand.ResearchTech` (or reuse existing `CraftTools` goal if no new command slot is available — coordinate with Track B).
- Priority: higher than `BuildWall` when under active war pressure, lower than `Fight`/`Flee` direct threat response.
- Wire into `SimplePlanner`, `GoapPlanner`, and `HtnPlanner` consistently.

**Acceptance criteria:**
- AI test: a colony with `WeaponLevel=0, ArmorLevel=0` under high threat score (≥ 0.55) does NOT call `ShouldFight` = true (retreat bias confirmed).
- AI test: `UnlockMilitaryTech` goal fires when war stance + low military tech count.
- AI test: `UnlockMilitaryTech` does NOT fire when already at or above the threshold tech count.
- Full solution builds + all existing tests pass + new tests pass.
- No changes to `WorldSim.Runtime`, `WorldSim.Graphics`, or Java.

#### P2-E expanded spec (Track A agent input)

**Prereq:** P2-C ✅ (all 7 structure types + snapshot mapping must exist)

**StructureRenderPass changes (`WorldSim.Graphics/Rendering/StructureRenderPass.cs`):**

The current `DrawDefensiveStructures` method has a two-branch switch (`WoodWall` → `DrawWoodWall`, everything else → `DrawWatchtower`). Replace with a full switch covering all 7 `DefensiveStructureKindView` values:

| Kind | Render approach | Color palette suggestion |
|------|----------------|--------------------------|
| `WoodWall` | existing `DrawWoodWall` | brown (125, 91, 61) — keep as-is |
| `StoneWall` | thicker/taller rectangle with stone texture lines | grey (140, 140, 150) |
| `ReinforcedWall` | stone wall + iron edge highlight | grey + steel accent (170, 180, 195) |
| `Gate` | gap/archway shape; draw as two half-wall pillars with a gap center | same grey as StoneWall, with a dark center gap |
| `Watchtower` | existing `DrawWatchtower` | blue-grey (85, 94, 112) — keep as-is |
| `ArrowTower` | taller watchtower body + notched top | blue-grey + lighter parapet (150, 165, 185) |
| `CatapultTower` | widest footprint + distinct dark cap | dark slate (60, 65, 80) with orange accent dot |

Add an **inactive structure indicator**: when `structure.IsActive == false`, overlay the structure with a semi-transparent red tint or a small `!` marker (pixel-art style). This makes upkeep failures visible.

**Tower projectile events — extend event matching (`DrawTowerProjectiles`):**

Current code only matches `"watchtower fired"` and `"tower hit predator"` event strings. Extend to also match:
- `"arrow tower fired"` (ArrowTower shot)
- `"catapult fired"` (CatapultTower shot)

For catapult: draw a larger/thicker beam or a filled circle at the AoE center to suggest splash; color suggestion: orange-red (230, 130, 60).

**Tech menu additions (`TechMenuPanelRenderer.cs` + `HudRenderer.cs`):**

The current tech menu lists all locked techs as a flat numbered list. Add a **section header** for the military/fortification branch:

- When the list contains any of the military tech IDs (`weaponry`, `armor_smithing`, `military_training`, `war_drums`, `scouts`, `advanced_tactics`, `fortification`, `advanced_fortification`, `siege_craft`), render a "-- Military & Fortification --" section header before those entries.
- The existing civilan/economy branch entries keep their existing display.
- `TechMenuView` may need a small extension to carry branch grouping data, or the renderer can classify by name string — coordinate with Track B if a model change is needed.

**Colony HUD — equipment level indicator:**

`ColonyHudData` already has `WeaponLevel` and `ArmorLevel`. Add a short line to the colony HUD section (near the resource row) showing:
```
Wpn: 1  Arm: 0
```
Only show when at least one level > 0, or always show for clarity (designer preference).

**Acceptance criteria:**
- All 7 structure kinds render with visually distinct colors/shapes.
- Inactive structures show a visible inactive indicator.
- Arrow tower and catapult projectile events produce beams/splash markers.
- Tech menu shows military branch header when those techs are present.
- Colony HUD shows weapon/armor level.
- F1 tech menu open/close and Ctrl+F1–F4 flows do not regress.
- Build + arch tests green.

### Wave 4 — Execution Steps

**Step 1 — immediately launchable, fully parallel (zero file overlap)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | S4-A, S4-B | Wave 3 D3 ✅ | Java-only; no C# changes |
| Track B agent | P2-A, P2-B, P2-C | Wave 3 C3 ✅ | Sequential: P2-A → P2-B → P2-C |

**Step 2 — opens when P2-A + P2-B + P2-C ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P2-D | P2-A ✅ + P2-B ✅ | AI needs equipment/tech domain to evaluate fights |
| Track A agent | P2-E | P2-C ✅ | Graphics for advanced structures |

**Critical path:** Track B (3 epics) → Track C + A (parallel). Track D fully independent.

---

## Wave 4.5 — SMR Headless Validation Infrastructure

Purpose:
- Promote the `W3.6-B4` Scenario Matrix Runner into an agent-grade headless validation system that later waves can use for regression, balance, and performance evidence.
- Replace anecdotal clustering/manual QA conclusions with reproducible artifact bundles, anomaly detection, and baseline comparison.
- Keep the visual `SMR Lab` explicitly out of the critical path until the late campaign/UI waves; this wave is headless-first by design.

### Sprint X4.5: Scenario Matrix Runner Hardening (Track B + C + Meta)

- ✅ **SMR-B1** Artifact bundle contract + output directory layout (`manifest`, runs, summaries, anomalies, logs) (Track B)
- ✅ **SMR-B2** Assertion + anomaly engine with explicit exit codes for agent/CI use (Track B)
- ✅ **SMR-B3** Baseline comparison + delta threshold policy (Track B)
- ✅ **SMR-B4** Unified CLI surface for clustering, balance, and perf evidence modes; SimStats headless infra (Track B)
- ✅ **SMR-B5** Lightweight evidence export hooks (event/sample timeline, worst-run drilldown, replay-oriented data without a viewer yet) (Track B)
- ✅ **SMR-B6** CI integration — `.github/workflows/smr-headless.yml` with assert + perf modes, triggered on push/PR to `main` (Track B)
- ✅ **SMR-C1** AI/planner anomaly signals exposed to SMR only where runtime counters are insufficient (Track C)
- ✅ **SMR-M1** Absorb `Session-Balance-QA-Plan.md` + `Session-Perf-Profiling-Plan.md` expectations into one Combined-plan sequencing/evidence workflow (Meta)
- ✅ **SMR-M2** Baseline update policy, artifact retention policy, and evidence-review protocol (Meta)

### Wave 4.5 — Execution Steps

**Step 1 — define the contract before adding more modes**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | SMR-B1 | Wave 3.6 ✅ | Stabilize output bundle first so later assertions/perf/reporting write into one schema |
| Meta coordinator | SMR-M1 | Wave 3.6 ✅ | Merge the existing balance/perf session expectations into one operational storyline |

**Step 2 — make SMR agent-usable, not just human-readable**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | SMR-B2 | SMR-B1 ✅ | Exit codes, invariant/anomaly catalog, and machine-readable failure reporting |
| Track C agent | SMR-C1 | SMR-B2 🔄 or ✅ | Only additive AI/planner signals; keep scope narrow and evidence-driven |

**Step 3 — turn one-off runs into comparable evidence**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | SMR-B3 | SMR-B2 ✅ | Baseline deltas and regression thresholds build directly on assertion/anomaly output; interim compare path is acceptable before the final unified CLI lands |
| Meta coordinator | SMR-M2 | SMR-B3 🔄 or ✅ | Define when baselines are updated, how artifacts are kept, and how evidence is reviewed |

**Step 4 — unify the tool surface and export drilldown evidence**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | SMR-B4 | SMR-B3 ✅ | One CLI/tool surface for `assert`, `compare`, `perf`, and standard matrix runs |
| Track B agent | SMR-B5 | SMR-B4 ✅ | Add worst-run drilldown bundles and replay-oriented evidence data; still no graphical lab |

**Step 5 — CI hardening + adoption gate before Wave 5+ complexity**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | SMR-B6 | SMR-B2 ✅ (exit codes must exist first) | CI workflow: `.github/workflows/smr-headless.yml` — push/PR trigger, assert mode, perf mode optional |
| Coordinator / QA session | SMR evidence gate | SMR-B4 ✅ + SMR-B5 ✅ + SMR-B6 ✅ + SMR-C1 ✅ + SMR-M1/M2 ✅ | Before Wave 5 formations/group combat starts, prove SMR can run repeatable evidence sweeps and identify suspicious runs without manual guesswork |

### Wave 4.5 — Design Notes

- `W3.6-B4` is treated as **SMR Phase 0**, not unfinished work. Wave 4.5 hardens that base into a project-level tool.
- This wave intentionally lands **before** Wave 5 group combat and Wave 6 siege/LLM complexity so that those later systems inherit a stronger debugging and regression workflow.
- The existing session plans are **absorbed/superseded** by Wave 4.5 SMR; their deliverables are fully consolidated here:
  - `Docs/Plans/Session-Balance-QA-Plan.md` → SMR-B2 (invariant catalog), SMR-B3 (multi-config matrix + baseline comparison), SMR-B6 (CI workflow)
- `Docs/Plans/Session-Perf-Profiling-Plan.md` → SMR-B4 (SimStats headless infra + `--perf` mode + headless perf budgets), SMR-A-infra (RenderStats FPS + render-frame HUD, explicitly deferred beyond Wave 4.5)

#### Invariant catalog (from `Session-Balance-QA-Plan.md`) — input to SMR-B2

SMR-B2 must implement and register these invariant IDs in the assertion engine:

| Category | ID | Invariant | Threshold | Phase |
|----------|----|-----------|-----------|-------|
| Survival | `SURV-01` | At least 1 colony survives | `livingColonies >= 1` | A |
| Survival | `SURV-02` | Population does not collapse to zero | `people > 0` | A |
| Survival | `SURV-03` | No mass starvation (>50% deaths from starvation) | `starvDeaths / totalDeaths < 0.5` | A |
| Survival | `SURV-04` | Average food per person above subsistence | `avgFpp >= 1.0` | A |
| Survival | `SURV-05` | Starvation-with-food anomaly is rare | `starvWithFood <= 2` | A |
| Combat | `COMB-01` | Combat deaths exist (combat is happening) | `combatDeaths > 0` | Phase 0+ |
| Combat | `COMB-02` | Combat is not annihilating population | `combatDeaths / totalDeaths < 0.7` | Phase 0+ |
| Combat | `COMB-03` | Combat engagements proportional to population | `engagements > 0` | Phase 0+ |
| Economy | `ECON-01` | Total food is positive at end of run | `totalFood > 0` | A |
| Economy | `ECON-02` | No degenerate colony (food=0 && people=0 is expected, not a bug) | informational | A |
| Scaling | `SCALE-01` | Larger maps produce more colonies | `large.colonies >= small.colonies` | Phase C / SMR-B3 |
| Scaling | `SCALE-02` | Population scales roughly with initial pop | `people >= initialPop * 0.3` | Phase C / SMR-B3 |

Combat counters required from Track B for `COMB-*` invariants: `TotalCombatDeaths`, `TotalCombatKills`, `TotalCombatEngagements` on `World`. If not yet exposed, assertion engine must gracefully **skip** combat invariants (not fail with a hard error).

#### Headless perf budget (from `Session-Perf-Profiling-Plan.md`) — input to SMR-B4

| Metric | Green (target) | Yellow (warning) | Red (block) |
|--------|----------------|------------------|-------------|
| Sim tick time (avg) | ≤ 4 ms | 4–8 ms | > 8 ms |
| Sim tick p99 | ≤ 8 ms | 8–12 ms | > 12 ms |
| Snapshot build time | ≤ 2 ms | 2–5 ms | > 5 ms |
| Peak entity count | ≤ 5 000 | 5 000–10 000 | > 10 000 |

SMR `--perf` mode must report: `avgTickMs`, `maxTickMs`, `p99TickMs`, `peakEntities` per seed (JSON line). Red-zone violations in any mode should produce anomaly warnings, not hard assertion failures (perf regressions are evidence, not blockers by default).

Render/FPS perf remains a later Track A concern and is intentionally not part of the Wave 4.5 headless acceptance gate.

#### SimStats / FPS infra assignment

`Session-Perf-Profiling-Plan.md` Phase A specifies two runtime measurement files not yet implemented:

- **`WorldSim.Runtime/Diagnostics/SimStats.cs`** (new file): `LastTickMs`, `LastSnapshotBuildMs`, `EntityCountSnapshot` — Track B responsibility. Must be exposed via `SimulationRuntime` property, **no** dependency from Runtime → Graphics.
- **`RenderStats` FPS extension** (`WorldSim.Graphics/Rendering/RenderStats.cs`): rolling 1-second `Fps`, `TotalEntitiesRendered`, 60-frame time history — Track A responsibility, lower priority, outside Wave 4.5 headless DoD.
- **F3 HUD extension** (`WorldSim.Graphics/UI/HudRenderer.cs`): `FPS | Tick Xms | Snap Xms | Render Xms | Entities N` — Track A, same later trigger.
- **ScenarioRunner `--perf` mode** (`WorldSim.ScenarioRunner/Program.cs`): `WORLDSIM_SCENARIO_PERF=true` env var — Track B, part of SMR-B4.

These items are **not** new epics but are callouts within SMR-B4 (Track B) and a pending Track A subtask (low priority, Wave 5+ trigger).

#### CI workflow spec (input to SMR-B6)

File: `.github/workflows/smr-headless.yml`

```yaml
name: SMR Headless
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:

jobs:
  smr-headless:
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        mode: [assert, perf]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet build WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj -c Release
      - name: Run SMR headless gate
        env:
          WORLDSIM_SCENARIO_MODE: ${{ matrix.mode }}
          WORLDSIM_SCENARIO_SEEDS: "101,202,303,404,505"
          WORLDSIM_SCENARIO_TICKS: "1200"
          WORLDSIM_SCENARIO_PLANNERS: "simple,goap,htn"
          WORLDSIM_SCENARIO_OUTPUT: "json"
          WORLDSIM_SCENARIO_ARTIFACT_DIR: ${{ runner.temp }}/smr-${{ matrix.mode }}-${{ github.run_id }}
        run: dotnet run --project WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj -c Release
```

Prereq: SMR-B2 exit codes must exist before CI is meaningful. SMR-B6 is therefore Step 5, not Step 1.

#### Existing design notes

- `SMR-B1` should standardize a durable artifact layout so OpenCode/LLM sessions can run SMR in isolation and later sessions can re-open the artifacts without ambiguity.
- `SMR-B2` should separate:
  - hard assertion failures,
  - anomaly warnings,
  - infra/config errors,
  - and stable exit codes for automation.
- `SMR-B3` should make "compare against yesterday / compare against known-good" a first-class workflow, not a manual spreadsheet task.
- `SMR-B4` should unify the currently separate headless directions into one tool contract:
  - standard matrix run,
  - assertion mode,
  - comparison mode,
  - perf mode,
  - machine-readable summary/report mode.
- `SMR-B5` is deliberately **not** the visual lab. Its purpose is to export richer evidence bundles (sampled snapshots, event trails, anomaly context, worst-run drilldown) that a later UI can consume.
- `SMR-C1` exists to avoid overloading Track B with planner-specific interpretation logic when a small additive AI signal would make anomaly classification far more useful.
- There is **no Track A epic in Wave 4.5**. This is intentional: Track A bandwidth remains focused on battle/siege/campaign UI in later waves, and the visual SMR Lab is deferred.
- Visual `SMR Lab` target:
  - not before Wave 10 closeout,
  - should consume the artifact bundles defined here,
  - should start as replay/drilldown UI before any 16-window live mosaic experiment.

Acceptance notes:
- SMR can be launched by a coding agent with a stable CLI contract and deterministic artifact output directory.
- A run can fail with a machine-meaningful reason (`assert fail`, `anomaly gate fail`, `bad config`, etc.) instead of only printing human text.
- Baseline comparison and regression deltas are stored as artifacts, not reconstructed manually from terminal logs.
- Perf, balance, and clustering evidence share one compatible schema/tool surface rather than separate ad hoc outputs.
- The project has a documented evidence-review workflow before the heavier Wave 5+ runtime/AI complexity lands.

Operational follow-up note (post-Wave 4.5, non-blocking):
- Use `Docs/Plans/Master/SMR-Minimum-Ops-Checklist.md` as the lightweight operating standard for run naming, artifact placement, baseline discipline, and report shape.
- Larger SMR improvements around reporting/workflow/baseline tooling/interpretation are intentionally deferred and should not interrupt the main wave critical path unless they become a proven blocker.

Proof targets:
- `SMR-B1`: sample artifact bundle checked into docs/example or captured in test fixtures.
- `SMR-B2`: tests for assertion/anomaly classification and exit-code policy.
- `SMR-B3`: tests and/or fixtures for baseline delta reporting.
- `SMR-B4`: smoke commands proving standard/assert/compare/perf modes all emit compatible outputs.
- `SMR-B5`: at least one worst-run drilldown artifact example with enough data for later replay UI.
- `SMR-C1`: AI/runtime tests proving planner/anomaly signals are exported deterministically.
- `SMR-M1/M2`: documented operator workflow for baseline refresh, artifact retention, and evidence review.

**Parallelism:**
- Main critical path: `SMR-B1 -> SMR-B2 -> SMR-B3 -> SMR-B4 -> SMR-B5`.
- Meta work (`SMR-M1`, `SMR-M2`) can overlap with the corresponding Track B phases but should not outrun the real tool contract.
- `SMR-C1` should start only after `SMR-B2` reveals which AI/planner signals are truly missing.
- No Track A work is planned in this wave.

---

## Wave 5 — Runtime Hardening + Group Combat (Director Phase 2b + Combat Phase 3a)

### Sprint D5: Hardening + Invariant Completeness (Track D only — Java)

> Director Plan > Phase 2 Sprint 5

- ✅ **S5-A** Runtime hardening — dedupe, counters, diagnostic logging
- ✅ **S5-B** Invariant pack completeness — INV-20, fuzzing (1000 random candidates)

### Sprint C5: Formations + Morale (Track B -> C -> A)

> Combat Plan > Phase 3 Sprint 5

- ✅ **P3-A** Formation system + group combat resolver (Track B)
- ✅ **P3-B** Combat morale + routing (Track B)
- ✅ **P3-C** Commander bonus — Intelligence-based (Track B + C)
- ✅ **P3-D** Graphics for battles (Track A)

### Wave 5 — Execution Steps

**Step 1 — immediately launchable, fully parallel (zero file overlap)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | S5-A, S5-B | Wave 4 D4 ✅ | Java-only; S5-A → S5-B sequential |
| Track B agent | P3-A, P3-B, P3-C (B part) | Wave 4 C4 ✅ | P3-A → P3-B → P3-C sequential |

**Step 2 — opens when P3-A + P3-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P3-C (C part — AI commander logic) | P3-A ✅ + P3-B ✅ | Needs formation + morale model |
| Track A agent | P3-D | P3-A ✅ | Formation rendering needs group model |

**Critical path:** Track B (3 epics) → Track C + A (parallel). Track D fully independent.

---

## Wave 5.1 — Combat Observability + Engagement Realization Closeout

Purpose:
- Treat Wave 5 as functionally successful combat foundation work, then immediately close the most important post-release gaps before Wave 6 siege work adds more complexity.
- Make the new combat slice measurable in SMR and readable in the live app, so later reviews do not depend on proxy metrics alone.
- Separate three concerns cleanly:
  - combat really exists and engages,
  - combat evidence is still incomplete,
  - medium combat lanes show persistent crowd/backoff friction that may be a real runtime/AI issue.

### Sprint C5.1: Combat Readability + SMR Evidence Closeout (Track B -> C -> A)

> Post-Wave-5 closeout sprint

- ✅ **W5.1-B1** Combat counter parity in ScenarioRunner -- export death/kill counters so `COMB-01/02` no longer skip on combat-enabled runs (Track B)
- ✅ **W5.1-B2** Morale/routing/battle telemetry export -- summary + drilldown visibility for active battles, combat groups, routing counts, morale, battle ticks (Track B)
- ✅ **W5.1-B3** Combat engagement/congestion runtime closeout -- battle-local spacing / routed egress / contact realization improvements for the medium combat lane (Track B)
- ✅ **W5.1-B4** Predator-human toggle semantics fix -- if predator-human attacks are disabled, predator vs human combat should not silently remain active on one side (Track B)
- ✅ **W5.1-C1** AI re-engage / congestion audit -- routed or fleeing actors should not thrash back into congested fights without clear intent (Track C)
- ✅ **W5.1-A1** Battle readability cleanup -- clarify battle-state marker semantics without turning this into a full visual redesign (Track A)

### Wave 5.1 — Execution Steps

**Step 1 — Track B closes the evidence gap first**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | W5.1-B1, W5.1-B2, W5.1-B4 | Wave 5 C5 ✅ | Same runtime/runner ownership; treat as one closeout batch before broader diagnosis |

**Step 2 — runtime combat behavior closeout**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | W5.1-B3 | W5.1-B1 ✅ + W5.1-B2 ✅ | Fix should target combat-specific congestion/engagement realization, not reopen the full peaceful clustering problem |

**Step 3 — AI and graphics consume the stabilized state**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | W5.1-C1 | W5.1-B3 ✅ | Re-engage/routing logic should reflect the actual post-fix combat model |
| Track A agent | W5.1-A1 | W5.1-B2 ✅ | Improve readability after battle/routing semantics are visible enough to label correctly |

**Critical path:** `W5.1-B1/B2/B4 -> W5.1-B3 -> W5.1-C1`, while `W5.1-A1` can overlap after telemetry semantics are stable.

### Wave 5.1 — Design Notes

- Wave 5 already delivered real combat. This closeout wave should not be framed as "combat does not work"; it should be framed as "combat now needs to be measurable, interpretable, and less friction-heavy before siege/campaign layers build on it."
- Manual screenshot evidence plus written operator notes are valid inputs for this wave, alongside SMR artifacts. Use both:
  - manual/live app evidence answers: "can a human read the battle?"
  - SMR evidence answers: "what actually happens across seeds/planners/configs?"
- Manual notes so far suggest that individual combat definitely occurs, while large-scale grouped battle readability is still inconsistent and may depend heavily on local trigger geometry.
- This wave should remain narrow:
  - no siege mechanics,
  - no major campaign/supply work,
  - no large visual overhaul,
  - no broad rebalance pass beyond combat-local closeout.
- `W5.1-B4` is intentionally in scope because the predator-human toggle semantics currently produce confusing evidence and manual behavior. The runtime policy should match the operator expectation.

### Wave 5.1 — Evidence Expectations

- Peaceful regression guard:
  - `small-default` compare baseline remains clean.
- Combat evidence minimum:
  - combat-enabled runs evaluate `COMB-01/02/03` rather than skipping `COMB-01/02`.
  - SMR summary/drilldown exposes enough combat fields that morale/routing/battle-state can be reasoned about directly.
- Combat lane quality target:
  - medium combat lane still may be noisy, but post-fix evidence should show either improvement or much clearer attribution of the remaining problem.
- Manual verification target:
  - a human should be able to distinguish at a glance between active combat, routing state, contested pressure, and non-combat damage markers more reliably than in raw Wave 5.

### Wave 5.1 — Risks And Mitigations

- **Risk: this turns into an SMR feature wave instead of a combat closeout.**
  - Mitigation: only add the minimum combat observability required to validate Wave 5 claims.
- **Risk: combat congestion fixes accidentally reopen the broader peaceful clustering problem.**
  - Mitigation: keep `W5.1-B3` scoped to battle-local spacing / routed egress / contact realization.
- **Risk: screenshots drive premature visual over-polish.**
  - Mitigation: use manual images as evidence inputs, not as justification for a full art/UI redesign inside this closeout wave.
- **Risk: predator-human behavior remains inconsistent across manual app vs SMR.**
  - Mitigation: fix the toggle semantics and ensure the resulting counters/fields are visible in SMR output.

### Wave 5.1 — Proof Targets

- `W5.1-B1/B2`: combat-enabled SMR runs no longer produce `ANOM-COMB-COUNTERS-MISSING`, and artifacts contain morale/routing/battle-state evidence fields.
- `W5.1-B3/C1`: medium combat lane shows reduced or better-explained combat backoff/crowding without breaking the peaceful smoke baseline.
- `W5.1-B4`: predator-human attack toggle behavior is semantically aligned in runtime and visible enough to verify in SMR/manual tests.
- `W5.1-A1`: manual screenshots + notes show more readable battle-state cues without requiring internal code knowledge to interpret the overlay.

---

## Wave 6 — LLM Integration + Siege (Director Phase 3a + Combat Phase 3b)

Wave turn-gate:
- Wave 6 is `READY` only after Wave 5.1 closeout is `✅`.
- Reason: Wave 5.1 explicitly closes the combat observability / engagement gaps that would otherwise make Wave 6 siege debugging noisier.

### Sprint D6: LLM Creativity + Budget (Track D + B)

> Director Plan > Phase 3 Sprint 6

- ✅ **S6-A** LLM director proposal stage — OpenRouter (Track D — Java)
- ✅ **S6-B** LLM + Refinery iterative correction loop (Track D — Java)
- ✅ **S6-C (D part)** Influence budget semantics (Track D — Java)
- ✅ **S6-C (B part)** Runtime budget mirror + checkpoint reset + snapshot export (Track B — C#)

### Sprint C6: Siege Mechanics (Track B -> C -> A)

> Combat Plan > Phase 3 Sprint 6

- ✅ **P3-E** Siege state + breach logic (Track B)
- ✅ **P3-F** Structure damage integration (Track B)
- ✅ **P3-G** AI siege tactics — attack vs retreat vs sortie (Track C)
- ✅ **P3-H** Siege UI/overlays (Track A)

### Wave 6 — Execution Steps

**Step 1 — immediately launchable, parallel with caution (MR-2)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | S6-A, S6-B | Wave 5 D5 ✅ | Java-only; S6-A → S6-B sequential |
| Track B agent | P3-E, P3-F | Wave 5 C5 ✅ + Wave 5.1 ✅ | P3-E → P3-F first; keep S6-C runtime blocked until the Java budget semantics are stable |

**MR-2 caution:** S6-C adds budget tracking to `DirectorState`; P3-E/F add siege state to tick loop.
Both are additive, but the clean order is: Track B finishes P3-E/F first, then picks up the S6-C runtime slice after Track D stabilizes the budget model.

**Step 2 — opens when P3-E + P3-F ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P3-G | P3-E ✅ + P3-F ✅ | AI siege needs breach + damage models |
| Track A agent | P3-H | P3-E ✅ | Siege overlay needs siege state model |

**Step 3 — S6-C sequencing (Track D -> Track B, then optional Track A consume)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | S6-C (D part) | S6-A ✅ + S6-B ✅ | Define budget costs, validator invariant, and prompt-context budget fields first |
| Track B agent | S6-C (B part) | S6-C (D part) ready + P3-E ✅ + P3-F ✅ | Mirror budget usage into `DirectorState`, checkpoint reset, and snapshot export after the Java semantics are stable |
| Track A agent | S6-C (consume, if needed) | S6-C (B part) ready | Minimal HUD/debug consume only if manual verification needs same-wave visibility |

Wave 6 closeout note:
- ✅ `S6-C (consume, if needed)` Track A consume visibility finalized in HUD/debug: director budget line shows remaining/max/used and debug adds checkpoint tick + used percentage from snapshot budget fields.

**S6-C split note:** S6-C is not "Track B first, then Track D". The intended order is `Track D first -> Track B second` because the runtime budget state should mirror the already-defined Java validator/prompt budget semantics, not invent them independently.

**Critical path:** Track D `S6-A -> S6-B -> S6-C (D)` → Track B `S6-C (B)`. Combat side: Track B `P3-E -> P3-F`, then Track C + A parallel.

---

## Wave 6.1 — Director Live Contract Alignment + Apply Observability

Purpose:
- Convert the first real live `F6` evidence into a small stabilization wave before Wave 7 expands the director model further.
- Freeze the current Phase 3a story-beat contract to the simpler runtime-safe rule: every `domain_modifier.durationTicks` must equal the parent `addStoryBeat.durationTicks`.
- Treat the existing C# runtime rule as the source of truth for Wave 6.1; defer richer per-effect durations to a later director-model wave.
- Make live smoke easier to read by separating "Java director pipeline validated/fallback completed" from "C# adapter/runtime apply succeeded/failed".
- Document that one manual `F6` checkpoint may legitimately consume multiple OpenRouter completions because the LLM retry loop runs inside a single director request.

Wave turn-gate:
- Wave 6.1 is `READY` only after Wave 6 closeout is `✅`.
- Reason: this wave is a hardening response to real live evidence, not a parallel feature track.

### Sprint D6.1: Contract Freeze + Live Hardening (Track D primary, Track A/B consume as needed)

- ✅ **D6.1-A** Director story-beat duration contract alignment (Track D — Java)
  - Enforce/repair `effect.durationTicks == beat.durationTicks` in the Java sanitize + validator path.
  - Add regression tests so Java-validated director output is always acceptable to the current C# runtime apply path.
- ✅ **D6.1-B** Director apply observability hardening (Track D — C# adapter, with minimal runtime/HUD consume if needed)
  - Preserve response-level `stage`, `mode`, `source`, and `budgetUsed` even when runtime apply throws.
  - Distinguish "director pipeline succeeded but apply failed" from "live request failed before response" in local manual smoke.
- ✅ **D6.1-C** Live smoke recipe + ops/docs refresh (Track D docs)
  - Record the recommended local live profile (`REFINERY_TIMEOUT_MS` high, `REFINERY_RETRY_COUNT=0`, `REFINERY_APPLY_TO_WORLD=true`).
  - Document that one `F6` may cause `1..(maxRetries+1)` LLM completions because iterative correction is inside one `/patch` request.

### Wave 6.1 — Execution Steps

**Step 1 — D6.1-A first (contract fix before more live smoke)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | D6.1-A | Wave 6 ✅ | Java contract must stop generating C#-invalid story beats before any further live validation is trusted |

**Step 2 — opens when D6.1-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | D6.1-B | D6.1-A ✅ | Adapter/runtime status should reflect the now-aligned response/apply path, including apply failures |

**Step 3 — opens when D6.1-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | D6.1-C | D6.1-A ✅ + D6.1-B ✅ | Finalize the practical smoke recipe after the behavior and HUD/status wording are stable |

Wave 6.1 non-goal note:
- Per-effect story modifier durations that differ from the parent beat duration are explicitly deferred.
- If the project later chooses the richer model from the Director master plan examples, that should be treated as a separate design/contract wave, not silently folded into this hotfix.

**Critical path:** `D6.1-A -> D6.1-B -> D6.1-C`.

Wave 6.1 closeout note:
- Real post-closeout review found one remaining reliability blocker and several observability inconsistencies in the live apply path, so Wave 6.1.1 is required before Wave 7 kickoff.

---

## Wave 6.1.1 — Director Apply Atomicity + Status Semantics Closeout

Purpose:
- Close the remaining reliability gap after Wave 6.1 so the live director path is not just diagnosable, but recoverable and internally consistent.
- Make the adapter/runtime apply flow atomic enough that failed multi-op director responses do not leave the world partially mutated while the dedupe state has already accepted those ops.
- Align all default/not-triggered/request-failure status semantics so the HUD, runtime snapshot, and adapter all tell the same truth (`not_triggered` / `unknown`) instead of falling back to `idle` / `both`.
- Preserve richer request-failure diagnostics in the top status line so local live smoke can distinguish timeout, connection, HTTP, and apply-level failures without digging through code.
- Clarify LLM retry accounting and silent Java-side repair semantics so one `F6` can be interpreted correctly in OpenRouter telemetry and local logs.

Wave turn-gate:
- Wave 6.1.1 is `READY` only after Wave 6.1 closeout is `✅`.
- Reason: this is a post-review stabilization wave driven by defects found in the just-landed Wave 6.1 implementation.

### Sprint D6.1.1: Reliability + Semantic Consistency (Track D primary, Track B/A consume as needed)

- ✅ **D6.1.1-A** Atomic director apply boundary + dedupe safety (Track D — C# adapter/runtime)
  - Rework the adapter/runtime apply sequence so a failed multi-op director response cannot permanently consume opIds before the world apply is known-good.
  - Define explicit rollback or commit-order policy for `_patchState` vs runtime command execution, and cover partial-failure scenarios with regression tests.
  - Acceptance: a response that fails on command N does not leave unrecoverable partial world/apply-state divergence.
- ✅ **D6.1.1-B** Canonical status semantics for `not_triggered` / `unknown` / `request_failed` (Track D — C# adapter/runtime/HUD)
  - Remove the misleading `unknown -> both` and `not_triggered -> idle` drift in the director status path.
  - Keep request-failure snapshots truthful all the way from adapter state to runtime snapshot to HUD line.
  - Acceptance: before first `F6`, and after a request failure before any response, all director surfaces agree on the same non-applied semantic state.
- ✅ **D6.1.1-C** Request-failure diagnostics + budget attempt policy (Track D — C# adapter/runtime/docs)
  - Surface the real underlying live-request failure cause in the top status line instead of only `Live refinery request failed`.
  - Decide and document the intended budget semantics for "checkpoint attempt started but no usable response arrived"; then make adapter/runtime/HUD behavior match that policy.
  - Acceptance: local smoke can distinguish timeout vs refused connection vs HTTP error, and budget behavior on request failure is intentional and documented.
- ✅ **D6.1.1-D** Java retry/repair observability cleanup (Track D — Java + docs)
  - Split or clarify `llmRetries` vs total completion count so one `F6` can be mapped unambiguously to OpenRouter usage.
  - Expose when planner-side duration repair/normalization happened, so prompt-quality regressions are not silently hidden behind a successful validated output.
  - Clean up docs to distinguish `directorStage:*` from generic `refineryStage:*` where the live Season Director path is specifically being discussed.
  - Acceptance: manual smoke + logs can answer "how many completions happened?" and "did Java have to repair the LLM candidate before validation?" without code inspection.
- ✅ **D6.1.1-E** Live regression matrix + closeout docs (Track D docs, optional minimal Track A consume)
  - Refresh the manual smoke checklist and ops guidance around the new atomicity/status semantics.
  - Include a regression matrix for: success, apply failure after response, request failure before response, deterministic fallback, and retry-heavy validated output.

### Wave 6.1.1 — Execution Steps

**Step 1 — D6.1.1-A first (reliability blocker)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | D6.1.1-A | Wave 6.1 ✅ | Atomic apply/dedupe correctness is the remaining blocker; no later semantic cleanup should proceed on top of a non-recoverable apply path |

**Step 2 — opens when D6.1.1-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | D6.1.1-B | D6.1.1-A ✅ | Canonical status semantics depend on the final apply pipeline shape |
| Track D agent | D6.1.1-C | D6.1.1-A ✅ | Request-failure and budget-attempt semantics should be finalized after the apply boundary is stable |

**Step 3 — opens when D6.1.1-B + D6.1.1-C ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | D6.1.1-D | D6.1.1-B ✅ + D6.1.1-C ✅ | Retry/repair telemetry and terminology should reflect the final local status model |

**Step 4 — opens when D6.1.1-D ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | D6.1.1-E | D6.1.1-A ✅ + D6.1.1-B ✅ + D6.1.1-C ✅ + D6.1.1-D ✅ | Freeze the final smoke/ops matrix only after the behavior and terminology stop moving |

Wave 6.1.1 non-goal note:
- This wave still does not introduce the richer per-effect story modifier duration model from the broader Director master plan examples.
- This wave also does not broaden director outputs beyond the current story beat + colony directive checkpoint surface; it only makes the existing live path safer and clearer.

**Critical path:** `D6.1.1-A -> {D6.1.1-B + D6.1.1-C} -> D6.1.1-D -> D6.1.1-E`.

---

## Wave 6.2 — Tools.Refinery Migration Foundation (Director TR1)

Purpose:
- Start the actual migration from the current validator-centric transition state toward versioned `tools.refinery` artifacts as the primary formal truth.
- Keep the external Spring Boot + current patch wire boundary (`v1` today) stable while the Java internals move to layered Refinery artifacts and solver-backed thinking.
- Define the designated output area, structured assertion-candidate ingest shape, and bridge-contract policy before later director waves expand capability further.
- Make `Docs/Plans/Master/Tools-Refinery-Agent-Guide.md` the mandatory pre-read for any refinery/model artifact work.

Wave turn-gate:
- Wave 6.2 is `READY` only after Wave 6.1.1 closeout is `✅`.
- Reason: the migration should build on the now-stabilized live director path rather than replacing a moving target.

### Sprint TR1: Tools.Refinery foundation (Track D primary, Track B consult on runtime-fact boundary)

- ✅ **TR1-A** Java / `tools.refinery` integration spike (Track D)
- ✅ **TR1-B** Artifact layout + shared/common vocabulary strategy (Track D)
- ✅ **TR1-C** Director problem family skeleton (`design/model/runtime/output`) (Track D, Track B consult)
- ✅ **TR1-D** Structured assertion-candidate ingest design (Track D)
- ✅ **TR1-E** Bridge contract policy (validated symbolic facts -> current patch wire output, `v1` today) (Track D)

### Wave 6.2 — Execution Steps

**Step 1 — TR1-A first**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR1-A | Wave 6.1.1 ✅ | Prove the Java service can load actual Refinery artifacts and invoke solver components before larger planning assumptions harden |

**Step 2 — opens when TR1-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR1-B | TR1-A ✅ | Lock repository artifact layout and shared/common vocabulary ownership before authoring real family files |

**Step 3 — opens when TR1-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR1-C | TR1-B ✅ | Create the first real director family artifact set and define designated output-area boundaries |
| Track B agent | TR1-C consult (runtime-fact boundary) | TR1-B ✅ | Confirm snapshot/runtime fact ownership and keep the C# bridge boundary stable while Java internals shift |

**Step 4 — opens when TR1-C ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR1-D | TR1-C ✅ | Move LLM ingest toward an assertion-oriented candidate shape instead of treating patch-like output as internal ontology |

**Step 5 — opens when TR1-D ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR1-E | TR1-D ✅ | Freeze the transition policy: validated symbolic facts inside, current patch wire output (`v1` today) only as bridge output |

Wave 6.2 policy note:
- Any task in this wave that creates or edits refinery/model artifacts requires reading `Docs/Plans/Master/Tools-Refinery-Agent-Guide.md` first, including the external official links referenced there.

**Critical path:** `TR1-A -> TR1-B -> TR1-C -> TR1-D -> TR1-E`.

---

## Wave 7 — Causal Chains + Director x Combat Intersection (Director Phase 3b + Combat Phase 4)

Wave turn-gate:
- Wave 7 is `READY` only after Wave 6.2 closeout is `✅`.
- Reason: causal chains should build on a live-stable director path whose formal-model migration direction and bridge-contract policy are already fixed.

### Sprint D7: Causal Chains + Operational UX (Track D + A + B)

> Director Plan > Phase 3 Sprint 7

- ✅ **S7-A** Causal chain layer — v2 contracts, monitoring, condition evaluation (Track D + B)
- ✅ **S7-B** Operational UX — profiles, debug toggles, env var cleanup (Track D + A)

### Sprint C7: Refinery / Director Integration for Combat (Track D — optional)

> Combat Plan > Phase 4 Sprint 7 (optional)

- ✅ **P4-A** Contracts v2 for diplomacy/campaign ops — DeclareWar, ProposeTreaty (Track D)
- ✅ **P4-B** Adapter translation to runtime commands (Track D)
- ✅ **P4-C** Runtime command endpoints — DeclareWar, ProposeTreaty (Track B)
- ✅ **P4-D** Java service beats — mock + gated director for war/diplomacy (Track D)

### Wave 7 — Execution Steps

**MR-3 — Sequential recommended.** D7 first, then C7.
Both sprints expand v2 contracts and runtime command endpoints in overlapping namespaces.

**Step 1 — D7 first**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | S7-A (D part: contracts + monitoring) | Wave 6.2 ✅ | Java + C# contract changes |
| Track B agent | S7-A (B part: condition evaluation runtime) | Wave 6.2 ✅ | Parallel with Track D on different files (contract-dependent only) |

**Step 2 — opens when S7-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | S7-B (D part) | S7-A ✅ | Profiles/env cleanup build on the causal-chain contract and monitoring shape |
| Track A agent | S7-B (A part) | S7-A ✅ | In-game debug toggles and consume-side UX build on the same stabilized monitoring shape |

Wave 7 Step 2 progress note:
- ✅ `S7-B` operator/debug UX closed: restartless operator seam (`Ctrl+F6` mode, `Ctrl+Shift+F6` preset), pending causal-chain HUD consume, settings-overlay director block, smoke helper/docs cleanup, and manual fixture/live_mock/live_director smoke all verified without gameplay-semantics regressions.

**Step 3 — opens when D7 fully ✅ (S7-A + S7-B). C7 is sequential after D7.**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | P4-A | S7-A ✅ + S7-B ✅ | Expand contracts for combat-facing director/runtime ops first |

Wave 7 Step 3 progress note:
- ✅ `P4-A` closed: additive C#/Java campaign contract ops (`declareWar`, `proposeTreaty`) wired at bridge/parser/schema level with `schemaVersion=v1` root unchanged, deterministic unsupported full-apply behavior explicitly test-covered until later C7 steps, and stale combat-plan P4-A wording aligned to current scope.

**Step 4 — opens when P4-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | P4-B | P4-A ✅ | Adapter translation depends on the new contract ops existing first |

Wave 7 Step 4 progress note:
- ✅ `P4-B` closed: adapter translation now maps campaign ops (`declareWar`, `proposeTreaty`) to runtime command shapes with deterministic translator-side validation (faction range/self-target/treaty kind), patch-state/apply path supports additive campaign bookkeeping (war pair normalized, treaty proposal directional), and executor emits explicit P4-C handoff unsupported diagnostics instead of generic unknown-op failures.

**Step 5 — opens when P4-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | P4-D | P4-B ✅ | Java service beat generation should target the already-mapped adapter contract |
| Track B agent | P4-C | P4-A ✅ + P4-B ✅ | Runtime endpoints need the contract and adapter semantics defined first |

Wave 7 Step 5 progress note:
- ✅ `P4-C` closed: runtime endpoints now apply campaign ops (`declareWar`, `proposeTreaty`) via world/relation-manager helpers (no direct runtime-side stance writes), treaty semantics locked to conservative ladder (`ceasefire`: War→Hostile only, `peace_talks`: one-step toward Neutral), and adapter executor validate/execute paths switched from explicit P4-C handoff failures to runtime calls; runtime+adapter test gates and full solution build are green.
- ✅ `P4-D` closed: Java director campaign emit is now optional and default-OFF (`planner.director.campaignEnabled`), stays on `SEASON_DIRECTOR_CHECKPOINT` + `schemaVersion=v1`, extends assertion-oriented designated output with `campaignSlot`, maps through parser/sanitize/bridge/validator, treats campaign ops as nudge-side output (`nudge_only` keeps directive+campaign), keeps campaign ops budget-neutral, and adds dedicated campaign-enabled fixtures/tests while default fixture/API behavior remains unchanged.

**Critical path:** D7 (S7-A → S7-B) → C7 (P4-A → P4-B → {P4-D + P4-C}). Strictly sequential across waves, with only the final C7 step parallelized.
**Director Phase 3 roadmap is COMPLETE after this wave. Tools.Refinery migration continues later through the Director sidecar waves (Wave 8.5 / TR2 and Wave 10.5 / TR3).**

---

## Wave 7.1 — Director Telemetry / Operator UX Cleanup

Purpose:
- Clean up the Wave 7 operator/debug UX now that the restartless preset/mode seam is proven in manual smoke.
- Reduce telemetry text density and improve readability without changing gameplay semantics.
- Add low-cost visual widgets only where they materially improve operator diagnosis.

Wave turn-gate:
- Wave 7.1 is `READY` only after Wave 7 closeout is `✅`.
- Wave 7.1 is a small post-wave cleanup slice, not a new major gameplay wave.
- By default, Wave 7.1 does **not** gate Wave 7.5, Wave 8, or Wave 8.5 unless a coordinator explicitly serializes Track A / D bandwidth around it.

### Sprint TU1: Telemetry Readability + Visual Widgets (Track A + D)

- ✅ **TU1-A1** Information architecture cleanup -- split always-visible operator summary, debug detail, and failure-only diagnostics (Track A)
- ✅ **TU1-D1** Operator wording + failure taxonomy alignment -- keep preset/mode/source labels short, stable, and docs-consistent (Track D)
- ✅ **TU1-A2** HUD/settings cleanup -- shorten top status line, restructure director block, and improve failure prominence (Track A)
- ✅ **TU1-A3** Tier-1 widgets -- status badges/chips and progress bars for budget/cooldown/pending-chain progress (Track A)
- ⬜ **TU1-A4** Tier-2 widget experiment (optional) -- tiny sparklines and/or event timeline strip only if Tier-1 smoke still leaves readability gaps (Track A)

### Wave 7.1 — Execution Steps

**Step 1 — Track A/D define the readability contract first**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | TU1-A1 | Wave 7 ✅ | Establish the visibility split before touching specific HUD sections |
| Track D agent | TU1-D1 | Wave 7 ✅ | Operator wording/taxonomy should settle in parallel with the Track A information split |

Wave 7.1 Step 1 progress note:
- ✅ `TU1-A1` closed: Track A locked the visibility split in code (always-visible operator summary vs debug-only detail vs failure-only diagnostics), kept pending causal chains in debug-only consume (max 3 rows), and introduced settings-overlay section grouping contract without final taxonomy freeze or widget/polish expansion.
- ✅ `TU1-D1` closed: Track D froze terminology (`preset/profile/lane/requested mode/effective mode/apply/request failure kind`), normalized adapter status copy so control-state lines use `lane=` + `requested=` while outcome lines keep `mode=`, and aligned runtime/smoke/operator docs without pre-writing TU1-A2 layout semantics.

**Step 2 — opens when TU1-A1 ✅ + TU1-D1 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | TU1-A2 | TU1-A1 ✅ + TU1-D1 ✅ | HUD/settings cleanup should implement the finalized wording and visibility tiers |

Wave 7.1 Step 2 progress note:
- ✅ `TU1-A2` closed: top operator summary wording locked to `requested=` and remains the sole always-visible summary, director block shifted to contextual directive/chain/budget/detail consume (no duplicate mode/apply summary row), failure diagnostics kept taxonomy-preserving and visually prominent, and settings overlay readability upgraded with grouped sections + wrapped long rows while hiding green-path failure `none` noise.

**Step 3 — opens when TU1-A2 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | TU1-A3 | TU1-A2 ✅ | Add the cheapest/highest-value visual aids only after the textual layout is stable |

Wave 7.1 Step 3 progress note:
- ✅ `TU1-A3` closed: Tier-1 glance widgets landed with snapshot-safe scope only -- HUD got apply/mode/profile chips plus budget bar and directive-duration bar as the primary surface, while Settings received only a minimal secondary mirror (apply chip + budget mini-bar). Cooldown/pending-chain progress bars were intentionally deferred (no new runtime/read-model export) to preserve snapshot-boundary correctness and avoid dashboard-heavy UI.

**Step 4 — optional; opens when TU1-A3 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | TU1-A4 | TU1-A3 ✅ | Only continue if manual smoke still shows real readability gaps after badges/bars land |

Acceptance notes:
- Operator state is readable at a glance during fixture, `live_mock`, and `live_director` smoke.
- Failure diagnostics are visually distinct from normal green-path telemetry.
- At least one low-cost visual aid (badge or bar) improves readability without creating a dashboard-heavy default.
- Any sparkline/timeline work remains additive, snapshot-driven, and low-cost.

Policy note:
- `Docs/Plans/Master/Post-Wave7-Telemetry-Operator-UX-Cleanup-Plan.md` is the detailed source of truth for this follow-up slice.

---

## Wave 7.5 — Low-Cost Visual Systems Baseline

Purpose:
- Convert the low-cost 2D strategy into an explicit execution wave after Director completion and before supply/campaign expansion.
- Lock the default runtime/render path to a cheap, profile-aware baseline so later campaign growth does not silently assume showcase-only costs.
- Keep visual polish, post-fx, and cinematic capture additive on top of the baseline instead of redefining it.

Wave 7.5 alignment note:
- Track A should treat `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Visual-Overhaul-Plan.md` and `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Sprint3-Execution-Plan.md` as reference inputs for profile naming, terrain/atmosphere direction, HUD/settings surface shape, and additive polish boundaries.
- Wave 7.5 remains the authority for sequencing and baseline policy: `DevLite` first, `Showcase` additive, `Headless` preserved.
- If the older Track A Phase 1 docs conflict with Wave 7.5 low-cost constraints or current post-Wave7 Track A reality, Wave 7.5 wins and the older docs must not be executed literally without a later refresh.

### Sprint LC1: Profiles + Visual Driver Boundary (Track B -> A -> C)

- ✅ **LC1-B1** Snapshot visual-driver field audit + minimal additive export set for state-driven rendering (Track B)
- ✅ **LC1-B2** Runtime/profile plumbing for `Showcase`, `DevLite`, and `Headless` defaults (Track B)
- ✅ **LC1-A1** Terrain state-driven variation baseline -- palette/tint/noise/culling-friendly rendering (Track A)
- ✅ **LC1-A2** Atmosphere + ambient-life baseline under explicit quality gates (Track A)
- ✅ **LC1-A3** Settings/HUD/profile visibility + low-cost regression smoke checklist updates (Track A)
- ✅ **LC1-C1** AI/planner telemetry + profile-compatibility audit for headless/devlite determinism (Track C, additive only)

### Wave 7.5 — Execution Steps

**Step 1 — Track B sets the baseline contract first**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | LC1-B1 | Wave 7 ✅ | Define the visual-driver boundary before any profile or rendering follow-up starts |

Wave 7.5 Step 1 progress note:
- ✅ `LC1-B1` closed: Track B locked a minimal visual-driver contract (`OwnershipStrength`, `FoodRegrowthProgress`) with explicit fallback/range policy (including `secondId < 0` ownership fallback) in `Docs/Plans/Master/Wave7.5-LC1-B1-Track-B-Visual-Driver-Contract.md`, implemented runtime-owned caches/accessors and additive `TileRenderData` export without new top-level snapshot blocks or Graphics-side changes, and added controlled runtime tests (range/default, determinism, regrowth progression, ownership ordering); runtime+adapter test gates and full solution build are green.

**Step 2 — opens when LC1-B1 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | LC1-B2 | LC1-B1 ✅ | Profile plumbing builds on the agreed snapshot/visual-driver boundary |
| Track A agent | LC1-A1 | LC1-B1 ✅ | Terrain/state-driven variation can start once the snapshot driver contract is stable |

Wave 7.5 Step 2 progress note:
- ✅ `LC1-B2` closed: Track B introduced a minimal canonical visual-lane plumbing seam (`Showcase` / `DevLite` / `Headless`) with requested/effective/source resolution, locked `Headless` as runner-only default (no app-side fake headless), switched app interactive lane defaults/cycle to `DevLite` ↔ `Showcase`, and exported effective lane metadata on ScenarioRunner run/summary/manifest artifacts; scope remained plumbing-only (no render-pass logic or future render-knob package), runtime+adapter tests, targeted ScenarioRunner artifact tests, and full solution build are green.

Wave 7.5 Step 2 progress note:
- ✅ `LC1-A1` closed as strict minimal Track A slice: render context now carries centralized visible tile bounds from camera+viewport, `TerrainRenderPass` consumes `OwnershipStrength` + `FoodRegrowthProgress` with deterministic CPU-side tile variation (no time animation, no neighbor scan), and terrain/resource passes are now culling-aware on the shared bounds seam. No runtime/read-model changes, no profile/UI wording scope, no atmosphere layer, and no resource-side regrowth marker were added. This is terrain/resource culling baseline only (not full renderer-culling closure).

**Step 3 — opens when LC1-B2 ✅ + LC1-A1 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | LC1-A2 | LC1-B2 ✅ + LC1-A1 ✅ | Atmosphere/ambient-life tuning should respect the finalized low-cost profile wiring |
| Track C agent | LC1-C1 | LC1-B2 ✅ + LC1-A1 ✅ | Only additive telemetry/guardrails; no renderer scope or profile-owned game logic |

Wave 7.5 Step 3 progress note:
- ✅ `LC1-A2` closed as a strict render-behavior Track A slice: `WorldRenderer` now applies an internal lane-aware visual policy from `RequestedVisualLane` (not from postfx state), the existing `FogHazeRenderPass` is reused and wired directly after terrain (`Terrain -> FogHaze -> Resources -> Structures -> Actors`) with deterministic season/drought intensity gating, and `TerrainRenderPass` received a small CPU-side lane-aware ambient modulation tune. Scope remained guarded: no `GameHost`/UI wording work, no runtime/read-model export changes, no particle/weather system, and no separate ambient-life pass.
- ✅ `LC1-C1` closed as a strict Track C additive audit slice: alignment note documented low-cost compatibility guardrails (`Docs/Plans/Master/Wave7.5-LC1-C1-Track-C-Alignment-Note.md`), and focused ScenarioRunner compatibility tests now verify `Headless` vs `DevLite` lane metadata may differ while AI/planner/contact evidence remains exact-match deterministic across `simple`/`goap`/`htn` planners under a nontrivial combat-enabled config (`EnableCombatPrimitives=true`, `EnableDiplomacy=true`, `EnableSiege=true`, `Ticks=300`) with exit-code-`0` enforcement (`WorldSim.ScenarioRunner.Tests/LowCostProfileCompatibilityTests.cs`). Scope stayed guarded (no renderer scope, no compare-mode identity changes, no profile-owned gameplay logic).

**Step 4 — opens when LC1-A2 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | LC1-A3 | LC1-B2 ✅ + LC1-A2 ✅ | Settings/HUD/profile visibility and checklist updates should document the final baseline, not an intermediate one |

Wave 7.5 Step 4 progress note:
- ✅ `LC1-A3` closed as a strict Track A visibility/documentation slice: `GameHost` now preserves full app-side visual lane resolution (`requested/effective/source`) without changing shared runtime resolver taxonomy, HUD keeps minimal lane visibility (`Lane:<effective>`) on the main status line, and settings overlay now documents the final low-cost policy (`requested/effective/source` + `Headless` as `ScenarioRunner/batch only`) while keeping app cycle locked to `DevLite <-> Showcase`. `WorldSim.Graphics/Docs/Plans/Phase1-Sprint3-Smoke-Checklist.md` was updated to current core `Ctrl+...` hotkeys (including operator controls), explicit HUD lane-label verification, three-lane policy proof (`DevLite`/`Showcase` app smoke + `Headless` ScenarioRunner artifact verification), and control-state wording alignment for `Ctrl+F3/F4` (no renderer-behavior proof claim). Scope stayed guarded: no runtime/read-model export changes, no renderer behavior expansion, and no app-side interactive headless mode.

Acceptance notes:
- `DevLite` becomes the default development baseline; `Showcase` is explicit/opt-in and `Headless` remains available for SMR and batch runs.
- New visual work remains snapshot-driven; Graphics does not compute gameplay-state stand-ins.
- Viewport culling, cheap terrain variation, and quality-gated atmosphere are baseline concerns, not optional late polish.
- Later combat/campaign waves consume the low-cost baseline instead of redefining the default rendering cost profile.

Proof targets:
- Runtime/snapshot docs and tests for the chosen visual-driver fields.
- Track A smoke checks covering `Showcase`, `DevLite`, and `Headless` profile behavior.
- Track A smoke/checklist updates should absorb any still-valid checks from `WorldSim.Graphics/Docs/Plans/Phase1-Sprint3-Smoke-Checklist.md`, but only after updating hotkeys and expectations to the post-Wave7 baseline.
- Perf/QA evidence showing the default path stays cheap enough for multi-instance/dev workflows.

**Parallelism:** `LC1-B1` is the gate. After that, `LC1-B2` and `LC1-A1` run in parallel. After `LC1-B2 + LC1-A1`, `LC1-A2` and `LC1-C1` can run in parallel, and `LC1-A3` closes the wave after the final Track A baseline is stable.

---

## Pre-Wave8 Coordinator Addendum - Visual-L Hybrid + Ecology Stabilization

Purpose:
- Resolve the two known pre-Wave8 gaps that are not covered by the current Wave 8 inventory plan:
  - a broader Track A visual readability/overhaul slice on top of the Wave 7.5 low-cost baseline
  - an ecology observability-first then stabilization pass for the current plant/herbivore/predator model
- Make Wave 8 inventory work start on a cleaner visual baseline and a better-measured ecology baseline.

Source of truth:
- `Docs/Plans/Master/Pre-Wave8-Visual-L-Hybrid-And-Ecology-Stabilization-Plan.md`

Wave 8 turn-gate:
- Wave 8 is `NOT READY` until `PW8-A1`, `PW8-B1`, and `PW8-B2` are all `✅` and the `SMR Analyst` evidence steps after `PW8-B1` and `PW8-B2` are complete.
- Reason: inventory/supply work should not begin while major visual readability debt and ecology-balance blind spots are still intentionally queued ahead of it.

Approved slices:
- ✅ **PW8-A1** Visual-L Hybrid readability/overhaul pass (Track A)
- ✅ **PW8-B1** Ecology observability for ScenarioRunner/SMR (Track B)
- ✅ **PW8-B2** Ecology stabilization on the current model (Track B primary, SMR Analyst evidence)

### Pre-Wave8 - Execution Steps

**Step 1 - parallel kickoff after Wave 7.5**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | PW8-A1 | Wave 7.5 ✅ | Visual-L Hybrid pass may start immediately on top of the low-cost baseline |
| Track B agent | PW8-B1 | Wave 7.5 ✅ | Observability must land before ecology tuning starts |

**Step 2 - ecology baseline evidence**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst | PW8-B1 evidence | PW8-B1 ✅ | Run baseline + drilldown package and confirm the ecology artifact surface is sufficient for tuning |

Pre-Wave8 Step 2 progress note:
- ✅ `PW8-B1 evidence` closed: baseline + stress-focused ecology drilldown artifacts were reviewed (`.artifacts/smr/baseline-candidate-pw8-b1-ecology-001/`, `.artifacts/smr/baseline-candidate-pw8-b1-ecology-stress-focus-001/`), both Headless + `exitCode=0`; checked-in report: `Docs/Evidence/SMR/pre-wave8-pw8-b1-ecology/README.md`; decision: ecology artifact surface is sufficient for `PW8-B2`.

**Step 3 - ecology stabilization**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | PW8-B2 | PW8-B1 ✅ + PW8-B1 evidence ✅ | Stabilize the current model only; do not pre-implement the later true closed-loop redesign |

Pre-Wave8 Step 3 progress note:
- ✅ `PW8-B2` implementation-side stabilization candidate wired: runtime current-model ecology defaults changed to `AnimalReplenishmentChancePerSecond=0.04`, `PredatorReplenishmentChance=1.0`, `FoodRegrowthMinSeconds=18`, `FoodRegrowthJitterSeconds=18`; predator rescue fairness added for extinct-predator + viable-prey state without touching predator speed/vision/capture/energy or initial predator multiplier; ScenarioRunner config accepts nullable ecology balance overrides and exports clamp-effective `ecologyBalance`; compare baseline paths for evidence must point at `summary.json`, while ecology improvement remains manual review via `ecology` run/timeline fields.

**Step 4 - stabilization verification**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst | PW8-B2 evidence | PW8-B2 ✅ | Validate the tuned state across the agreed matrix before Wave 8 starts |

Pre-Wave8 Step 4 progress note:
- ✅ `PW8-B2 evidence` SMR Analyst package captured in `Docs/Evidence/SMR/pre-wave8-pw8-b2-ecology/README.md`: primary + stress-focus compare packages ran with explicit env reset and `Headless`; compare identity is clean (`18/18` and `9/9`, no current-only/baseline-only keys), no compare regressions or threshold breaches; ecology review shows predator collapse materially improved (default zero-predator ticks `4704 -> 67`, stress `10554 -> 24`) while herbivore zero-species pressure does not regress. Caveat: stress clustering warnings increased `3 -> 5`; SMR Analyst recommends Meta Coordinator close Pre-Wave8 and unblock Wave 8 after review.

Parallelism:
- `PW8-A1` may run in parallel with `PW8-B1` and does not need to wait for ecology evidence.
- `PW8-B2` depended on `PW8-B1` and the first SMR evidence pass; this dependency is now satisfied.
- Wave 8 remains blocked until the full pre-wave addendum closes.

---

## Wave 8 — Supply & Inventory (Combat Phase 5a)

### Sprint C8: Personal Inventory + Storage (Track B -> C -> A)

> Combat Plan > Phase 5 Sprint 8

- ✅ **P5-A** Person inventory data model (Track B)
- ✅ **P5-B** Storehouse integration — withdraw/deposit (Track B)
- ✅ **P5-C** Consumption from inventory first (Track B)
- ✅ **P5-D** Snapshot and UI indicators (Track B -> A)
- ✅ **P5-E** Supply-related tech entries — backpacks, rationing (Track B)
- ✅ **Wave 8 SMR supply prep** ScenarioRunner supply/inventory evidence surface (Track B / SMR Analyst)
- ✅ **Wave 8 SMR evidence** Supply/inventory scenario evidence package (SMR Analyst)

### Wave 8 — Execution Steps

**Step 1 — inventory data first (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-A | Pre-Wave8 addendum ✅ | Inventory data model is the base for storage, consumption, tech hooks, and UI |

Wave 8 Step 1 progress note:
- ✅ `P5-A` closed in `fb7dd49 feat(wave8): add person inventory model`: `PersonInventory`, `ItemType.Food`, and `Person.Inventory` landed with focused inventory tests, runtime gate, and full solution build green.

**Step 2 — opens when P5-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-B | P5-A ✅ | Storehouse withdraw/deposit rules depend on the inventory model existing |

Wave 8 Step 2 progress note:
- ✅ `P5-B` closed in `0bdb281 feat(wave8): add storehouse inventory refill`: `RefillInventory` command/job mapping, owned-storehouse access, refill/deposit transfer helpers, and fixed-brain refill integration landed; focused, AI, runtime, and full build gates were green.

**Step 3 — opens when P5-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-C | P5-B ✅ | Consumption should switch to inventory only after refill/storehouse semantics are stable |

Wave 8 Step 3 progress note:
- ✅ `P5-C` closed in `7e6743f feat(wave8): consume inventory food first`: human food consumption now uses inventory before colony stock across eating/critical hunger paths, runtime-only inventory consumption counter landed, starvation-with-food telemetry counts carried food, and focused/runtime/full build gates were green.

**Step 4 — opens when P5-C ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-E | P5-C ✅ | Tech effects should target the stabilized inventory/carry rules |

Wave 8 Step 4 progress note:
- ✅ `P5-E` closed: `backpacks` and `rationing` tech entries landed with colony-specific, idempotent runtime effects (`+2` inventory slots, `1.25x` inventory-only carried-food hunger efficiency), existing/future colony people receive backpack capacity, and focused/runtime/full build gates were green.

**Step 5 — opens when P5-E ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-D(B_part) ✅ | P5-E ✅ | Snapshot/export side should reflect the final inventory + tech shape |

Wave 8 Step 5 progress note:
- ✅ `P5-D(B_part)` closed: runtime read-model export now carries supply/inventory state for Track A consume (`PersonRenderData.InventoryFood/InventoryUsedSlots/InventoryCapacitySlots/HasFood`, `ColonyHudData.InventoryCapacityBonusSlots/InventorySupplyEfficiencyMultiplier`, `EcoHudData.InventoryFoodConsumed`). `HasFood` means carried inventory food only; `InventoryFoodConsumed` is a global cumulative counter. `WorldSnapshotInterpolator` preserves the new person carry fields from the current snapshot. Top-level `P5-D` remains open until Track A P5-D(A_part) completes.

**Step 6 — opens when P5-D (B part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | P5-D(A_part) ✅ | P5-D(B_part) ✅ | UI indicators consume the finalized carry/supply snapshot fields |

Wave 8 Step 6 progress note:
- ✅ `P5-D(A_part)` closed: Track A consumes the finalized supply snapshot fields without reopening Runtime/ReadModel boundaries. In-world carried-food badges now show only for `HasFood` actors, AI debug exposes tracked actor carried food and slots, colony HUD conditionally shows non-default backpack/rationing supply state, and ecology diagnostics label global cumulative inventory food consumption. Build/test gates were green. Manual smoke did not visibly catch a carried-food badge; this is accepted as non-blocking because the smoke run did not prove a `HasFood` carrier was present, and organic gameplay does not guarantee a refill/carry moment. Step 7A must provide deterministic ScenarioRunner supply evidence for carried food and inventory consumption before final Wave 8 SMR evidence.

**Step 7A — opens when P5-D (A part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | Wave 8 SMR supply prep - export/config ✅ | P5-D (A part) ✅ | Add the ScenarioRunner supply/inventory artifact fields, deterministic supply-focused lane/config surface, and focused tests |
| SMR Analyst | Wave 8 SMR supply prep - validation ✅ | Track B export/config ✅ | Validate that the new artifact surface and supply-focused lane are sufficient before the final Wave 8 SMR evidence run |

Wave 8 SMR supply prep requirements - Track B export/config:
- Add a supply/inventory block to ScenarioRunner run-level artifacts (`summary.json` / per-run result) with at least:
  - `inventoryFoodConsumed` from `World.TotalInventoryFoodConsumed`.
  - `carriersWithFood` = living people with `InventoryFood > 0`.
  - `totalCarriedFood` = sum of carried inventory food.
  - `avgInventoryUsedSlots` and `avgInventoryCapacitySlots` across living people.
  - `coloniesWithBackpacks` = colonies with `InventoryCapacityBonusSlots > 0` or `backpacks` unlocked.
  - `coloniesWithRationing` = colonies with `InventorySupplyEfficiencyMultiplier > 1f` or `rationing` unlocked.
- Add compact drilldown timeline fields for supply evidence if drilldown is enabled:
  - `inventoryFoodConsumed`, `carriersWithFood`, `totalCarriedFood`, and optional average used/capacity slots.
- Add a supply-focused ScenarioRunner config/lane or env/config surface that can deterministically exercise Wave 8 supply behavior:
  - enable/free-unlock `backpacks` and `rationing`, or otherwise document the unlock/setup path in the run config;
  - include a storehouse/refill/carry setup or deterministic condition likely to produce non-zero carried food and inventory consumption;
  - keep the lane Headless-compatible and deterministic across seeds/planners.
- Add focused ScenarioRunner tests proving:
  - run artifacts contain the supply block;
  - old baselines without the supply block still parse when compare mode is used;
  - drilldown timeline contains compact supply fields when enabled;
  - the supply-focused lane produces non-zero supply evidence in at least one deterministic smoke case.
- Do not change runtime gameplay rules, Graphics UI, or Track A presentation during this prep step.

Wave 8 Step 7A Track B progress note:
- ✅ Track B export/config closed: ScenarioRunner run-level and per-run artifacts now emit nullable/default-safe `supply` blocks with `inventoryFoodConsumed`, `carriersWithFood`, `totalCarriedFood`, `avgInventoryUsedSlots`, `avgInventoryCapacitySlots`, `coloniesWithBackpacks`, and `coloniesWithRationing`; drilldown timeline samples emit compact `supply` fields for inventory consumption, carriers, carried food, and average slots. Deterministic supply lane contract is `SupplyScenario = "storehouse_refill_consumption"`, which prepares the primary colony through actual `backpacks`/`rationing` `TechTree` unlocks, places an owned storehouse, invokes the existing refill path, and starts inventory consumption with spare carried food. Focused ScenarioRunner test gate: `dotnet test "WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj" --filter "SupplyTelemetryArtifactTests"` passed 4/4. Example SMR Analyst smoke env: `WORLDSIM_SCENARIO_CONFIGS_JSON=[{"Name":"supply-storehouse-refill-consumption","Width":32,"Height":20,"InitialPop":12,"Ticks":8,"Dt":0.25,"EnableCombatPrimitives":false,"EnableDiplomacy":false,"StoneBuildingsEnabled":false,"BirthRateMultiplier":0.0,"MovementSpeedMultiplier":1.0,"EnableSiege":true,"SupplyScenario":"storehouse_refill_consumption"}]`, with `WORLDSIM_SCENARIO_DRILLDOWN=true`, `WORLDSIM_SCENARIO_DRILLDOWN_TOP=1`, and `WORLDSIM_SCENARIO_SAMPLE_EVERY=1`. Full Wave 8 SMR supply prep remains open until SMR Analyst validation returns sufficient-for-Step-7B.
- ✅ Post-review fix note: `AllowFreeTechUnlocks` is restored after supply-lane tech setup, existing storehouse setup no longer duplicates buildings, and the stale low-intensity combat assertion fixture was retargeted to a deterministic low-intensity run. Full gates after fix: `WorldSim.ScenarioRunner.Tests` 57/57, `WorldSim.Runtime.Tests` 251/251, and `dotnet build "WorldSim.sln"` all green.

Wave 8 SMR supply prep requirements - SMR Analyst validation:
- Run a narrow Headless validation package against the Track B supply-focused lane after export/config lands.
- Inspect `manifest.json`, `summary.json`, `anomalies.json`, and drilldown output if enabled.
- Confirm the artifact surface can answer at least:
  - whether carried food exists in the run (`carriersWithFood`, `totalCarriedFood`);
  - whether inventory food was consumed (`inventoryFoodConsumed`);
  - whether backpack/rationing tech state is visible (`coloniesWithBackpacks`, `coloniesWithRationing`);
  - whether old compare baselines without the new supply block remain parse-compatible.
- Produce a short validation note or handoff stating either:
  - `supply prep sufficient for Step 7B`, or
  - the exact missing field/lane/test that Track B must fix before Step 7B.

Wave 8 Step 7A SMR Analyst validation note:
- ✅ SMR validation closed: `SupplyScenario = "storehouse_refill_consumption"` was validated in `.artifacts/smr/wave8-step7a-supply-prep-validation-001/` with `Headless`, seed `101`, planners `simple,goap,htn`, and drilldown `sampleEvery=1`. Manifest exit `0`, anomaly count `0`, run-level `supply` block and compact timeline `supply` fields are present. All three planner runs produced `inventoryFoodConsumed=2`, `carriersWithFood=1`, `totalCarriedFood=3`, `coloniesWithBackpacks=1`, and `coloniesWithRationing=1`; timelines also show transient carried-food proof (`totalCarriedFood` max `4`). Old-baseline compatibility is covered by `SupplyTelemetryArtifactTests.Compare_OldBaselineWithoutSupplyBlock_StillParses`; focused no-build supply test rerun passed `4/4`. Evidence note: `Docs/Evidence/SMR/wave8-step7a-supply-prep-validation/README.md`. Decision: `supply prep sufficient for Step 7B`.

**Step 7B — opens when Wave 8 SMR supply prep ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst | Wave 8 SMR evidence | Wave 8 SMR supply prep ✅ | Run and review SMR packages for the completed supply/inventory wave before Wave 8 closeout |

Wave 8 SMR evidence requirements:
- Use `Docs/Plans/Master/SMR-Minimum-Ops-Checklist.md` for artifact naming, manifest/summary/anomaly review, worst-run ranking, and report format.
- Minimum expected package: a Headless `all-around-smoke` or equivalent multi-seed, multi-planner run plus the supply-focused lane from Step 7A.
- Required matrix target: seeds `101,202,303`, planners `simple`, `goap`, and `htn`, unless SMR Analyst records a narrower smoke-only exception with a follow-up run recommendation.
- Evidence review must explicitly inspect the supply block and state whether supply behavior was actually exercised:
  - `inventoryFoodConsumed` non-zero in supply-focused runs, or a documented explanation if zero;
  - `carriersWithFood` / `totalCarriedFood` observed where refill/carry setup should create carriers;
  - backpack/rationing effective fields present when the lane enables those techs;
  - no new `starvation_with_food`, clustering/backoff, survival, or economy regression that blocks Wave 8 closeout.
- Run `compare-baseline` only when a valid comparable baseline path exists; otherwise mark baseline compare as unavailable rather than manufacturing one.
- Closeout artifact/report should state healthy signals, suspicious signals, unknowns, and the recommended next run or baseline decision.
- Wave 8 is not fully closeable from a generic SMR smoke alone; the report must include supply/inventory-specific evidence from Step 7A fields.

Wave 8 Step 7B SMR Analyst evidence note:
- ✅ SMR evidence closed: two Headless packages were captured and reviewed. Peaceful all-around package `.artifacts/smr/all-around-smoke-wave8-001/` ran `27/27` (`small-default`, `medium-default`, `standard-default` x seeds `101,202,303` x planners `simple,goap,htn`) with `exitCode=0`, `assertionFailures=0`, and supply blocks present in every run; it produced `12` reviewed `ANOM-CLUSTER-HIGH-BACKOFF` warnings, but no survival/economy/supply blocker (`minLivingColonies=4`, `minPeople=24`, `minFood=894`, `starvationWithFood=0`). Supply-focused package `.artifacts/smr/wave8-step7b-supply-focused-001/` ran `9/9` with `SupplyScenario="storehouse_refill_consumption"`, `exitCode=0`, `anomalyCount=0`, and every run produced `inventoryFoodConsumed=2`, `carriersWithFood=1`, `totalCarriedFood=3`, `coloniesWithBackpacks=1`, and `coloniesWithRationing=1`; drilldown timelines show transient carried food (`totalCarriedFood` max `4`). Evidence note: `Docs/Evidence/SMR/wave8-step7b-supply-inventory-evidence/README.md`. Decision: `Wave 8 SMR evidence accepted; recommend Wave 8 closeout`.

**Parallelism:** Wave 8 is intentionally mostly sequential Track B work; only the final Track A consume step, the ScenarioRunner supply-evidence prep, and the SMR Analyst closeout evidence step are separate.

---

## Wave 8.5 — First Solver-Backed Director Slice (Director TR2)

Purpose:
- Convert the TR1 foundation into the first actual solver/refinement-backed director slice while keeping the current C# bridge contract stable.
- Lock runtime snapshot -> runtime assertions ownership before solve-path work starts.
- Preserve live/manual diagnosability while the internal Java path shifts from validator-centric repair toward model-backed refinement.
- Establish the first headless refinery evidence lane on top of the real runtime/adapter path without pulling paid live runs into the critical path.

Wave turn-gate:
- Wave 8.5 is `READY` only after Wave 7 closeout is `✅` and the TR1-C Track B consult boundary decisions (D1-D5) are locked in a short consult note (`AGENTS.md` or `Docs/Plans/Master/`).
- Reason: the first solver-backed slice should build on post-D7 director semantics and a closed runtime-fact boundary, not on a half-implicit consult checklist.

### Sprint TR2: First solver-backed director slice (Track D primary, Track B consult on runtime assertions handoff)

> Tools-Refinery Migration Plan > Phase TR2

- ✅ **TR2-A** Runtime snapshot -> runtime assertions mapper (Track D, Track B consult)
- ✅ **TR2-B** Solve/refinement path for minimal director output (Track D)
- ✅ **TR2-C** Validated facts -> bridge mapping (Track D)
- ✅ **TR2-D** Solver-path observability + headless refinery evidence lane foundation (Track D primary, Track B owns the `ScenarioRunner` lane implementation)

### Wave 8.5 — Execution Steps

**Sidecar note:** Wave 8.5 is a Director-only migration wave. Once its turn-gate is met, it may run in parallel with Combat Waves 8-10 unless a later explicit cross-track handoff says otherwise.

**Step 1 — runtime assertions boundary first**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR2-A | Wave 7 ✅ + TR1-C consult note locked | Normalize checkpoint facts into the runtime assertion layer before solve/refinement work starts |

**Step 2 — opens when TR2-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR2-B | TR2-A ✅ | The first solve/refinement pass should target the finalized runtime-assertion input shape |

**Step 3 — opens when TR2-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR2-C | TR2-B ✅ | Bridge mapping should extract facts from actual solve/refinement output, not a placeholder path |

**Step 4 — opens when TR2-C ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR2-D (D part: solver markers + evidence semantics) | TR2-C ✅ | Define the solver-path observability contract and operator-facing marker semantics; detailed execution shape lives in the Refinery Live SMR Plan |
| Track B agent | TR2-D (B part: `ScenarioRunner` refinery fixture + live_mock foundation) | TR2-C ✅ | Build the first headless refinery evidence lane against the stabilized bridge semantics; detailed defaults and guardrails live in the Refinery Live SMR Plan |

Wave 8.5 Step 4 closeout note:
- ✅ `TR2-D` joint closeout completed: Track D D-part and Track B B2 + fixture/live_mock evidence are GREEN; the normalized `directorSolver*` marker contract is consumed by `ScenarioRunner`, `core` remains the default lane, and `refinery_live_validator` / `refinery_live_paid` remain deferred/config-error for TR2-D.

Wave 8.5 policy note:
- Any task in this wave that creates, edits, or reviews refinery/model artifacts or solver semantics requires reading `Docs/Plans/Master/Tools-Refinery-Agent-Guide.md` first, including the external official links referenced there.
- `Docs/Plans/Master/Refinery-Live-SMR-Plan.md` is the detailed source of truth for refinery headless lane behavior, guardrails, and artifact policy; Combined remains high level.

Proof targets:
- Java tests proving runtime assertions feed the solver-backed director slice.
- Existing C# bridge tests remain green without contract churn.
- Manual/live director smoke still diagnoses stage/mode/source/apply/budget semantics clearly.
- The first headless refinery evidence slice (`fixture + live_mock`) proves the real runtime/adapter path can be exercised without the app under the detailed plan's guardrails.

**Critical path:** `TR2-A -> TR2-B -> TR2-C -> TR2-D`.

---

## Wave 8.6 — Guardrailed Paid LLM Director SMR Pilot

Source of truth:
- `Docs/Plans/Master/Wave8.6-Paid-Live-Director-SMR-Plan.md`

Purpose:
- Pull a small, safe part of paid-live Refinery evidence forward before Wave 9.
- Prove that `ScenarioRunner` can run no-cost validator rehearsal and tightly capped paid LLM Director pilots through the real runtime/adapter path.
- Keep paid behavior local-only, explicit opt-in, advisory, and excluded from default `core`, generic `all`, CI, and Wave 9 closeout gates.

Wave turn-gate:
- Wave 8.6 is `READY` only after Wave 8.5 `TR2-D` is `✅`.
- Wave 9 Step 1 is intentionally serialized behind Wave 8.6 closeout by current Meta decision.
- Reason: paid-live Director evidence can inform SMR/balance workflow design before the supply/campaign growth wave starts.

### Sprint D8.6: Paid-live Director evidence pilot (Track D -> Track B -> SMR Analyst)

- ✅ **W8.6-D1** Paid/validator LLM policy lock + scorecard semantics (Track D)
- ✅ **W8.6-B1** ScenarioRunner validator rehearsal + paid preset guardrails (Track B)
- ✅ **W8.6-SMR1** No-cost rehearsal + `paid_micro_total2` evidence review (SMR Analyst / Meta, YELLOW accepted)
- ❌ **W8.6-SMR2** Optional `paid_probe_2x2x2` evidence review (skipped for this closeout)

### Wave 8.6 — Execution Steps

**Step 1 — Track D policy lock**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | W8.6-D1 | TR2-D ✅ | Lock Java LLM policy, completion/retry semantics, marker/telemetry meaning, scorecard taxonomy, and Track B handoff without enabling paid by default |

**Step 2 — opens when W8.6-D1 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | W8.6-B1 | W8.6-D1 ✅ | Enable no-cost validator rehearsal and paid presets only behind explicit confirm, cost estimate, hard cap, rehearsal gate, and safe artifact policy |

**Step 3 — opens when W8.6-B1 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst / Meta | W8.6-SMR1 | W8.6-B1 ✅ | Run/review mandatory no-cost rehearsal and `paid_micro_total2` local paid pilot; write evidence summary and Wave 9 go/no-go recommendation |

**Optional Step 4 — only after W8.6-SMR1 GREEN and explicit user approval**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst / Meta | W8.6-SMR2 | W8.6-SMR1 GREEN + explicit approval | Not run in this closeout: W8.6-SMR1 was YELLOW accepted, so the optional 8-completion probe is deferred |

Wave 8.6 policy notes:
- `paid_micro_total2`: 2 seeds, 1 checkpoint per run, max 1 completion per checkpoint, total estimated completions 2, concurrency 1.
- `paid_probe_2x2x2`: 2 seeds, 2 checkpoints per run, max 2 completions per checkpoint, total estimated completions 8, concurrency 1.
- Paid runs require local manual API-key setup and explicit confirmation; no API key may be committed or captured in artifacts.
- No-cost rehearsal is mandatory before paid.
- Default model remains Java default `openai/gpt-5.4-mini` unless W8.6-D1 changes it explicitly.

Wave 8.6 Step 1 closeout note:
- ✅ `W8.6-D1` Track D policy lock completed: Java paid/validator defaults remain safe, `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED` is explicitly mapped to the Java solver-observability property, paid preset retry semantics and marker/telemetry meanings are locked, `PLANNER_DIRECTOR_MAX_RETRIES=2` is explicitly not a paid preset default, telemetry endpoint capture is recommended but not universally mandatory, and Track B handoff lives in `Docs/Plans/Master/Wave8.6-W8.6-D1-Track-D-Policy-Lock-Handoff.md`. `W8.6-B1` is unblocked; SMR steps and full Wave 8.6 remain open.

Wave 8.6 closeout note:
- ✅ `W8.6-B1` Track B guardrails completed and committed (`e423fff`): `refinery_live_validator` and `refinery_live_paid` are enabled only through explicit ScenarioRunner lanes, paid requires confirmation + GREEN rehearsal + preset/cap checks, observed completion cap is enforced, and focused ScenarioRunner tests/build were green.
- ✅ `W8.6-SMR1` evidence completed and accepted as YELLOW: no-cost validator rehearsal was GREEN; `paid_micro_total2` stayed within the 2-completion cap with clean request/apply/no-secret evidence; the only caveat is solver-sidecar observability (`directorSolverStatus=load_failure`, coverage `none`) on paid checkpoints. Evidence summary: `Docs/Evidence/SMR/wave8.6-paid-live-director-pilot/README.md`.
- ❌ `W8.6-SMR2` was intentionally not run: the optional `paid_probe_2x2x2` would spend up to 8 completions and would not clarify the already identified Track D sidecar extraction issue. Track D follow-up before future paid probes/TR3: add no-paid major/epic sidecar extraction coverage and fix severity-node/load-failure reporting.
- ✅ Wave 9 may start with the W8.6 YELLOW caveat accepted; paid behavior remains local-only/advisory and is not part of Wave 9 CI/default gates.

Proof targets:
- Paid cannot run without explicit confirmation and GREEN rehearsal proof.
- Estimated completion cap is printed, persisted, and enforced.
- `core` and generic `all` remain no-paid paths.
- Scorecard covers balance stability, director creativity, failure hardening, and formal/refinery quality.
- Paid evidence remains advisory and local-only.

**Critical path:** `W8.6-D1 -> W8.6-B1 -> W8.6-SMR1`. `W8.6-SMR2` is optional.

---

## Wave 8.7 — Refinery Sidecar Stabilization Mini-Wave

Source of truth:
- `Docs/Plans/Master/Wave8.7-Refinery-Sidecar-Stabilization-Plan.md`

Purpose:
- Resolve the W8.6 paid micro solver-sidecar caveat before Wave 9 without running more paid LLM calls.
- Reproduce and fix the likely major/epic severity extraction/load-failure issue in the Java Tools.Refinery sidecar.

Turn-gate:
- Wave 8.7 is optional from a paid guardrail perspective, but currently preferred before Wave 9 by Meta/user decision.
- No paid probe is allowed in Wave 8.7.

### Sprint D8.7: Director Solver-Sidecar Stabilization (Track D)

- ✅ **W8.7-D1** No-paid major/epic sidecar reproducer (Track D)
- ✅ **W8.7-D2** Minimal sidecar severity/extraction/status fix (Track D)
- ✅ **W8.7-D3** No-paid Java gate and optional validator artifact check (Track D / SMR Analyst)

### Wave 8.7 — Execution Steps

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | W8.7-D1/D2 | W8.6-SMR1 YELLOW accepted | Add no-paid reproducer, then apply smallest correct fix; stop for human/formal-model review before changing `.problem` constraints broadly |
| SMR Analyst | W8.7-D3 optional | Track D fix ✅ | Optional no-paid validator artifact only if Meta requests artifact-level confirmation |

Closeout target:
- Valid `minor`/`major`/`epic` core story/directive sidecar paths do not report `directorSolverStatus=load_failure`.
- Existing `minor`/mock/validator tests remain green.
- No paid requests are made.

Wave 8.7 closeout note:
- ✅ `W8.7-D1/D2` Track D fix completed and committed (`dfef555`): major/epic no-paid sidecar reproducers added, severity node extraction made safe, extraction exceptions classified as `NON_SUCCESS` extraction failures instead of true `LOAD_FAILURE`, and focused/full Java gates passed.
- ✅ `W8.7-D3` SMR validator artifact completed: `.artifacts/smr/wave8.7-sidecar-validator-001/` ran `refinery_live_validator` no-paid with fixed 30000/60000ms timeouts, `exitCode=0`, request/apply/fallback failures `0`, observed completions `0`, `directorSolverStatusHistogram success=2`, coverage `story_core=2` and `directive_core=2`, anomalies empty, and secret/auth scan clean.
- Evidence summary: `Docs/Evidence/SMR/wave8.7-sidecar-validator/README.md`.
- ✅ Wave 9 P5-F may start; no paid probe was run or needed.

---

## Wave 9 — Army Supply + Campaign Start (Combat Phase 5b + 6a)

SMR closeout source of truth:
- `Docs/Plans/Master/Wave9-10-SMR-Closeout-Plan.md`

Audit hardening source:
- `Docs/Plans/Master/Wave9-Runtime-Campaign-Hardening-Plan.md`

P6-A boundary mini-fix source:
- `Docs/Plans/Master/Wave9-P6-A1-Campaign-Query-Boundary-Mini-Fix-Plan.md`

Wave turn-gate:
- Wave 9 is `READY` after Wave 8.7 closeout completed the no-paid sidecar validator artifact.
- Original Wave 8.6 paid guardrail closeout is accepted with a YELLOW solver-sidecar observability caveat, documented in `Docs/Evidence/SMR/wave8.6-paid-live-director-pilot/README.md`; Wave 8.7 no-paid validation closes the local sidecar/load-failure follow-up before Wave 9.

### Sprint C9: Army Supply Model (Track B -> C)

> Combat Plan > Phase 5 Sprint 9

- ✅ **P5-F** Army supply model — aggregate + consumption (Track B)
- ✅ **P5-G** Supply carrier role + AI behaviors (Track B + C)
- ✅ **P5-H** Foraging behavior (Track B + C)
- ✅ **P5-I** Fallback supply budget for early prototypes (Track B)

Split-status note:
- `P5-G` and `P5-H` are aggregate B+C epics and are now complete after both runtime and AI parts were accepted. Current frontier is Sprint C10 campaign skeleton work; `P6-A` opened the runtime entity baseline, `P6-A1` hardens the runtime query boundary before assembly/rally, and `P6-B`/`P6-C`/`P6-D` remain sequential follow-ups.

### Sprint C10: Campaign Skeleton (Track B -> C -> A)

> Combat Plan > Phase 6 Sprint 10

- ✅ **P6-A** Campaign and army entities (Track B)
- ✅ **P6-A1** Campaign query boundary hardening (Track B mini-fix)
- ✅ **P6-B** Assembly and rally points (Track B)
- ✅ **P6-C** March system + encounters (Track B)
- ✅ **P6-D** Snapshot + overlays (Track B + A)
- ✅ **Wave 9 SMR campaign/supply prep** ScenarioRunner army supply + campaign skeleton evidence surface (Track B / SMR Analyst validation)
- ✅ **Wave 9 SMR evidence** Army supply + campaign skeleton closeout package (SMR Analyst)

**Parallelism:** C9 and C10 are **sequential** (C10 depends on supply model from C9).

### Wave 9 — Execution Steps

**Step 1 — army supply foundation (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-F | Wave 8.7 ✅ | Aggregate army supply and consumption rules are the base for every later campaign step |

Wave 9 Step 1 progress note:
- ✅ `P5-F` closed: minimal model-first army supply foundation landed without persistent Army/Campaign entities or `World.Update` wiring. `ArmySupplyModel` consumes aggregate member-carried inventory food deterministically, tracks caller-owned `ArmySupplyState`, reports low/out-of-supply separately, applies conservative sustained out-of-supply morale/stamina attrition and routing, and includes focused runtime tests for no-op safety, fractional demand, fractional zero-supply integer-demand semantics, no-dupe consumption, attrition, routing, and stamina clamp. Focused runtime tests, full runtime tests, full solution build, and diff check were green.

**Step 2 — opens when P5-F ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-I | P5-F ✅ | The fallback budget should mirror the already-defined supply model instead of competing with it |

Wave 9 Step 2 progress note:
- ✅ `P5-I` closed: temporary caller-owned ration pool fallback landed without persistent Army/Campaign entities or `World.Update` wiring. `ArmyRationPoolSupplyModel` reserves colony food into `ArmyRationPoolState`, consumes ration-pool food using the same `ArmySupplyState` fractional/out-of-supply semantics as `P5-F`, leaves member inventories untouched in fallback mode, and returns remaining rations idempotently. Focused Wave9 army tests, full runtime tests, full solution build, and diff check were green.

**Step 3 — opens when P5-I ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-G (B part) | P5-F ✅ + P5-I ✅ | Runtime role/state hooks for supply carriers build on the settled supply model |

P5-G Track B requirement note:
- Runtime carrier/caller hooks must choose exactly one army supply mode per army tick: either carried-inventory consumption (`ArmySupplyModel`) or fallback ration-pool consumption (`ArmyRationPoolSupplyModel`), never both. This is a caller-level guard to implement/test once the P5-G caller hook exists; do not push this into Track C AI behavior.

Wave 9 Step 3 progress note:
- ✅ `P5-G (B part)` closed: runtime-owned supply carrier hook landed without `World.Update` army supply ticking, Track C AI changes, foraging state, or persistent Army/Campaign entities. `ArmySupplyCarrierModel` now provides the caller seam over carried-inventory and ration-pool modes, with first-source-per-tick selection, same-source `AlreadyProcessed` no-op behavior, and mixed-source `RejectedMixedSupplySource` rejection that does not mutate food, pool, fractional demand, morale/stamina, routing, or counters. Durable `PersonRole.SupplyCarrier` helpers and minimal structured snapshot visibility (`PersonRenderData.IsSupplyCarrier`) were added. Focused carrier tests, Wave9 army regression, targeted snapshot tests, full runtime tests, full solution build, and diff/scope checks were green.

Wave 9 audit hardening notes:
- Detailed execution plan: `Docs/Plans/Master/Wave9-Runtime-Campaign-Hardening-Plan.md`.
- `P5-G (B part)` must add durable supply-carrier role/state hooks, not only debug strings or profession behavior.
- Before march-heavy `P6-C`, Track B must either add or explicitly defer with evidence: tile-indexed blockage/occupancy maps, world-topology-aware path cache invalidation, path request/blocked-check counters, and a large-lane performance baseline.
- `P6-D (B part)` should expose structured `CampaignRenderData`, `ArmyRenderData`, `ArmySupplyRenderData`, supply source mode, route progress, carrier/resupply/forage counters, and campaign outcome fields. Track A must not infer campaign/supply visuals from event strings.
- Wave 9 evidence must use dedicated counters for carrier, forage, and campaign behavior; generic `GatherFood`, movement, or `RaidBorder` telemetry is not enough.

**Step 4 — opens when P5-G (B part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-H (B part) | P5-G (B part) ✅ | Foraging runtime behavior should layer onto the carrier/resupply baseline |

Wave 9 Step 4 progress note:
- ✅ `P5-H (B part)` closed: runtime-only foraging primitive landed without Track C AI changes, ScenarioRunner artifact export, Graphics consume, organic `World.Update` wiring, or persistent Army/Campaign entities. `ArmyForagingModel` now provides caller-owned `ArmyForagingState` counters and deterministic `ArmyRationPoolState` destination for map-food foraging, with strict consumer-key validation, Chebyshev adjacent-or-same source range, explicit failure reasons, capped per-attempt/per-consumer yield, and exact conservation (`FoodGained == ration-pool delta == source-node decrease`). Focused Wave9 foraging tests, Wave9 runtime regression, full runtime tests, full solution build, and scope checks were green. `P5-H` top-level remains pending until the later Track C part closes.
- Deferred follow-up: `ArmyForageFailureReason.HarvestFailed` remains a defensive branch under the current `World.TryHarvest(...)` contract. If a mockable harvest seam, concurrent harvest path, or changed harvest contract is introduced later, add focused `HarvestFailed` regression coverage then.

**Step 5 — opens when P5-H (B part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P5-G (C part) | P5-H (B part) ✅ | AI carrier behavior should target the actual runtime hooks, not placeholders |

Wave 9 Step 5 progress note:
- ✅ `P5-G (C part)` closed: Track C AI carrier decision baseline landed with explicit carrier commands (`AssignSupplyCarrier`, `DeliverSupply`, `AbortSupplyDelivery`), safe-default supply context fields, `MaintainArmySupply` goal/planner handling across Simple/GOAP/HTN, and runtime context fill from existing role/inventory/storehouse facts. Initial review blocker was fixed before closeout: trace-only carrier commands remain mapped to `Job.Idle`, but explicit `HasArmySupplyDemand=false` runtime default gating prevents no-demand `MaintainArmySupply -> AssignSupplyCarrier -> Job.Idle` loops. Direct planner no-demand/threat paths return `Idle`, multi-tick runtime regression proves non-carrier progress, and focused AI/runtime, Wave9 runtime, full runtime, solution build, and diff checks were green. `P5-G` top-level is now complete after B+C acceptance.

**Step 6 — opens when P5-H (B part) ✅ + P5-G (C part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P5-H (C part) | P5-H (B part) ✅ + P5-G (C part) ✅ | Foraging decision logic depends on the runtime forage command/state and the carrier AI baseline existing |

Wave 9 Step 6 progress note:
- ✅ `P5-H (C part)` closed: Track C foraging AI behavior landed with explicit `ForageArmySupply` command support across Simple/GOAP/HTN, safe-default no-demand runtime context, demand/capability/threat/routing gates, and trace-only runtime mapping to `Job.Idle`. Review blocker was fixed before closeout: zero-score trace-only support goals (`ForageArmySupply`, `MaintainArmySupply`) no longer become `Trace.SelectedGoal`, while `GoalScores` debug visibility is preserved and null selection returns a fresh `None`/`Idle` trace. Focused AI tests, runtime brain tests, Wave9 army regression tests, full runtime tests, solution build, diff check, scope search, and parallel Swarm review were green. `P5-H` top-level is now complete after B+C acceptance.

**Step 7 — campaign entities (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-A | P5-F ✅ + P5-G (B+C parts) ✅ | Start campaign work after the carrier runtime+AI hook is stable; `P5-H` organic foraging is not an execution dependency, but `P6-A` must reserve forage telemetry/state extension points |

Wave 9 Step 7 progress note:
- ✅ `P6-A` closed: Track B runtime campaign/army entity baseline landed without actor assignment/rally, march/pathfinding/encounter execution, render snapshot/export, ScenarioRunner, AI, Graphics, Refinery, or `World.Update` organic campaign/supply/forage ticking. `ArmyState` and `CampaignState` provide persistent runtime state with deterministic runtime-local campaign/army IDs, `TryCreateCampaign(...)` returns deterministic `CampaignCreationResult` domain statuses, initial phase is `AssemblingPending`, member roster starts empty, and route/supply/carrier/forage counters/states are zeroed extension points only. Focused campaign runtime tests, Wave9 runtime regression, full runtime tests, full solution build, diff check, and scope searches were green.

**Step 7B — campaign query boundary mini-fix before assembly/rally**

Detailed execution plan:
- `Docs/Plans/Master/Wave9-P6-A1-Campaign-Query-Boundary-Mini-Fix-Plan.md`

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-A1 | P6-A ✅ | Replace the temporary live runtime-state `SimulationRuntime.Campaigns` query seam with detached immutable campaign/army runtime snapshots before P6-B mutates assembly/rally state |

P6-A1 requirement note:
- `SimulationRuntime.Campaigns` must not expose live `CampaignState`, `ArmyState`, supply, ration, carrier, or foraging state objects to downstream consumers. Keep mutable campaign state runtime-owned; P6-D/SMR must later consume explicit immutable read-model/export DTOs, not live runtime entities.

Wave 9 Step 7B progress note:
- ✅ `P6-A1` closed: `SimulationRuntime.Campaigns` now returns detached `CampaignRuntimeSnapshot`/`ArmyRuntimeSnapshot` runtime-query DTOs instead of live `CampaignState`/`ArmyState` objects. Nested supply, ration pool, carrier, foraging, route-counter, and member-roster data are copied value snapshots; retained query results do not grow or mutate after later campaign creation or positive-dt ticks. No assembly/rally, march/pathfinding, encounter, `World.Update`, ScenarioRunner, Graphics, AI, Refinery/Java, or P6-D render/read-model scope was introduced. Focused campaign tests, Wave9 runtime regression, full runtime tests, full solution build, diff check, and scope searches were green.

**Step 8 — opens when P6-A1 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-B | P6-A1 ✅ | Assembly/rally depends on campaign and army entities after the runtime query boundary is hardened |

P6-B requirement note:
- Close the deferred P6-A1 roster-boundary finding when assembly creates non-empty army rosters: add a regression that captures a `CampaignRuntimeSnapshot` with at least one `MemberActorId`, mutates/advances roster state through runtime-owned assembly/rally methods, and asserts the retained snapshot keeps the original copied member IDs. This proves the P6-A1 detached query boundary under real roster mutation, not only empty-roster state.

Wave 9 Step 8 progress note:
- ✅ `P6-B` closed: runtime-owned campaign assembly/rally is now wired through `SimulationRuntime.AdvanceTick(...)` orchestration, with deterministic rally point selection near the origin colony, incremental roster fill (max one member per campaign/tick, including after pruning), strict eligible/assigned-member lifecycle filtering, and final post-update visible member movement toward the rally point before the P6-C march handoff. Campaign membership remains `ArmyState.MemberActorIds` only; no automatic `PersonRole.Warrior` assignment, Track C AI command, Track A UI, ScenarioRunner export, Refinery/Java, P6-D read-model, or `World.Update` campaign ticking was introduced. P6-B now prunes dead/missing/routing/combat/hard-job assigned members deterministically, clears stale supply-carrier binding when needed, and only completes assembly when all current members are valid and within Manhattan radius <= 1. P6-C route/march/encounter counters and supply/forage ticking remain untouched during assembly. The routed P6-A1 non-empty roster snapshot finding is closed by a retained `CampaignRuntimeSnapshot` regression that proves copied member IDs stay stable after later roster mutation.

**Step 9 — opens when P6-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-C | P6-B ✅ | March and encounters need the assembly/rally flow to exist first |

P6-C requirement note:
- Before the first march movement/counter update, revalidate the assembled campaign roster using the P6-B lifecycle contract. Do not assume roster permanence after `CampaignPhase.Marching`; handle newly invalid members (health-zero/dead, missing, routing, in-combat, active battle/group, hard combat job) deterministically via prune/replacement/non-complete behavior before march semantics proceed. Add focused guard coverage for at least health-zero, isolated in-combat, world-update-induced invalidation, and max-one replacement after pruning with multiple candidates.

Wave 9 Step 9 progress note:
- ✅ `P6-C` closed after Meta + Swarm re-review: march and encounter runtime now runs through `SimulationRuntime.AdvanceTick(...)` with lifecycle-safe roster pruning, route cache/counter instrumentation, persistent `World.NavigationTopologyVersion` route-cache validity, fallback objective alignment, and non-resolving encounter ticks. For positive-dt march ticks, march supply and post-supply prune/recompute run before route/path/movement/encounter decisions, including the already-at-encounter-objective branch; supply-induced routing returns an understrength campaign to `Assembling` with `IsAssembled=false` and `AssemblyCompletedTick=-1`; no same-tick movement, encounter, route/path counters, or march/encounter progress counters occur after supply-induced invalidation. Zero-dt encounter transition remains intentionally supported. Focused campaign tests, Wave9 runtime regression, full runtime tests, full solution build, diff check, and scope checks were green. `.gitignore` remains unrelated dirty local state and must not be staged with P6-C.

**Step 10 — opens when P6-C ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-D(B-part) | P6-C ✅ | Snapshot/export should reflect the actual campaign loop, not an incomplete placeholder |

Wave 9 Step 10 progress note:
- ✅ `P6-D(B-part)` closed after Meta re-review: Track B runtime read-model fixes landed with stable lower_snake_case mapping helpers for campaign phase/supply/forage strings, route intent/resolved objective/next-waypoint assertions, and `GetSnapshot()` no-mutation coverage. The authorized first-gate Graphics handoff fix also landed: `WorldSnapshotInterpolator.Interpolate(...)` preserves `current.Campaigns` with focused regression coverage so populated campaign read-model data is not dropped before Track A consume. Normal Track A overlay/UI implementation, ScenarioRunner/SMR export, campaign resolution, and `World.Update` campaign orchestration remain out of scope; `.gitignore` remains unrelated dirty local state and must not be staged with P6-D.

**Step 11 — opens when P6-D (B part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | P6-D(A_part) ✅ | P6-D (B part) ✅ | Overlays consume the campaign snapshot once it is stable |

Wave 9 Step 11 progress note:
- ✅ `P6-D(A-part)` closed after manual smoke PASS: Track A campaign consume uses the structured `WorldRenderSnapshot.Campaigns` read model only. `Ctrl+F2` intentionally toggles both the Campaign panel and the campaign map overlay for this slice; empty-state panel/overlay smoke passed, and the invalid-anchor sentinel fix is in place (`AnchorActorId < 0` displays `anchor:none` and no bogus anchor-tied map marker is drawn at `(-1,-1)`). The low-cost map overlay uses no custom/content texture assets; it uses only the shared pixel texture for primitive drawing, without parsing `RecentEvents`, querying `SimulationRuntime.Campaigns`, or adding runtime/ScenarioRunner/AI/Refinery scope. Exact ETA and supply percentage remain out of scope until a future read-model expansion. Top-level `P6-D` is closed; Wave 9 SMR prep may open next.

**Step 12A — SMR evidence surface before closeout**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | Wave-9-SMR-prep-export/config | P5-G (B part) ✅ + P5-H (B part) ✅ + P6-D (B+A parts) ✅ | Add ScenarioRunner artifact fields, drilldown fields, deterministic lanes, and focused tests for army supply, carrier/resupply, foraging, and campaign skeleton evidence per `Wave9-10-SMR-Closeout-Plan.md` |
| SMR Analyst | Wave 9 SMR prep - validation | Track B export/config ✅ + P5-G (C part) ✅ + P5-H (C part) ✅ | Validate that the new artifact surface and deterministic lanes can prove runtime and AI-side Wave 9 behavior before the final closeout package |

Wave 9 Step 12A progress note:
- ✅ `Wave-9-SMR-prep-export/config` targeted review fix accepted by Meta + Swarm re-review: ScenarioRunner keeps the normalized `Wave9Scenario` config surface (underscore canonical names plus documented aliases including `campaign-foraging`), a nullable/default-safe run-level `wave9` artifact block, and deterministic prep lanes for army supply depletion, carrier/resupply, campaign foraging, and campaign assembly/march/encounter. This remains Track B prep evidence only; SMR Analyst validation and final Wave 9 closeout evidence are still separate steps.
- ✅ Step-review fix gate closed: run-level Wave9 deterministic probes are explicitly labeled with `evidenceKind=deterministic_probe` and `timelineSemantics=not_tick_sampled`; drilldown timeline `wave9` samples remain empty/default unless tick-accurate sampling exists, so final run-level counters are not retroactively stamped onto every sample. Commit/staging must exclude the unrelated `.gitignore` dirty diff.
- ✅ SMR Analyst prep validation closed GREEN: local raw artifact `.artifacts/smr/wave9-campaign-supply-focused-001/` ran the 36-run matrix (3 seeds x 3 planners x 4 Wave9 configs) with `exitCode=0`, `anomalyCount=0`, metadata mismatches `0`, positive lane evidence failures `0`, selected timeline bad Wave9 samples `0`, and secrets findings `0`. This unblocks Step 12B final Wave 9 closeout evidence, but is not final Wave 9 acceptance.

**Step 12B — final Wave 9 closeout evidence**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst | Wave-9-SMR-evidence | P5-G (C part) ✅ + P5-H (B+C parts) ✅ + P6-D (A part) ✅ + Wave 9 SMR prep ✅ | Run and review Wave 9 all-around + targeted campaign/supply packages before Wave 10 kickoff; generic smoke alone is not sufficient |

Wave 9 Step 12B closeout note:
- ✅ `Wave-9-SMR-evidence` accepted by Meta after SMR Analyst GREEN recommendation: Package A `.artifacts/smr/all-around-smoke-wave9-001/` ran 27 peaceful all-around runs (`exitCode=0`, `assertionFailures=0`) and Package B `.artifacts/smr/wave9-campaign-supply-final-001/` ran 36 targeted Wave9 campaign/supply runs (`exitCode=0`, `assertionFailures=0`, `anomalyCount=0`, positive counters for supply depletion, carrier/resupply, bounded foraging, and campaign assembly/march/encounter). Evidence taxonomy is locked: Package A is organic peaceful regression pressure, Package B is deterministic Wave9 feature proof, and organic tick-sampled campaign/supply behavior remains outside the current evidence surface. Wave 9 closeout was accepted at Step 12B, and the subsequent deep-review preflight gate below is now closed GREEN.
- Residual monitoring route: Package A reported 12 non-blocking `ANOM-CLUSTER-HIGH-BACKOFF` warnings concentrated in medium/standard peaceful lanes. This did not block Wave 9 because survival/economy/AI health and targeted Wave9 proof were green, but Wave 10 SMR packages must continue ranking clustering/backoff signals and treat worsening movement/occupancy regressions as in-scope review evidence.

Post-Wave9 deep-review preflight gate for Wave 10/P6-E:
- ✅ Preflight fix accepted by Meta re-review: strict requested-strength semantics are now the Wave9/Wave10 preflight invariant, so campaigns with `requestedMemberCount > eligible candidates` remain in assembly and cannot mark assembly complete, enter marching, increment returned/aborted churn, or record route progress until the requested roster is filled. Focused runtime regression covers `AssemblyCompletedCount`, `MarchStartedCount`, `CampaignsReturnedOrAborted`, and `MarchProgressTicks` staying zero for incomplete rosters.
- ✅ Preflight evidence semantics cleanup accepted by Meta re-review: Wave 9 carrier/resupply evidence is documented and tested as carrier assignment plus model-level supply-source application (`carrierSupplyApplications` / compatibility `carrierDeliveries`), not actual actor command/path delivery. Actual carrier delivery command/path remains unproven and out of Wave9 scope unless separately implemented. `P6-E` proper is unblocked.

---

## Wave 10 — Campaign Resolution + Advanced Warfare (Combat Phase 6b + 7)

SMR closeout source of truth:
- `Docs/Plans/Master/Wave9-10-SMR-Closeout-Plan.md`

Audit hardening source:
- `Docs/Plans/Master/Wave10-Campaign-Logistics-Hardening-Plan.md`
- `Docs/Plans/Master/Wave10-Campaign-Launch-Catalyst-Plan.md`

Wave 10 SMR evidence guardrail:
- Every Wave 10 SMR prep surface must include a lane manifest: for each lane/config, document purpose, proof type (`organic` vs `deterministic_probe`), required counters, expected positive/zero assertions, and explicit non-claims. Do not use deterministic probes as organic campaign/siege proof.

### Sprint C11: Campaign Siege + Resolution (Track B -> C -> A)

> Combat Plan > Phase 6 Sprint 11

- ✅ **P6-E** Siege integration in campaign flow (Track B)
- ✅ **P6-F** Resolution — loot, war score, peace (Track B)
- ✅ **P6-G** Strategic campaign AI (Track C)
- ✅ **P6-H** Campaign UI polish (Track A)
- ✅ **P6-I** Manual/operator campaign launch catalyst (Track B + App routing)
- ✅ **P6-J(B)** Organic campaign launch runtime application (Track B)
- ✅ **P6-J(C)** Organic campaign strategy follow-up N/A - existing P6-G strategy contract sufficient (Track C)

### Sprint C12: Supply Lines + Forward Bases (Track B -> C -> A)

> Combat Plan > Phase 7 Sprint 12

- ✅ **P7-A** Supply line convoy entities (Track B)
- ✅ **P7-B** Forward bases / camps (Track B)
- ✅ **P7-C** Scout role + intel (Track B + C)
- ✅ **P7-D** UI for supply lines and forward bases (Track A)

### Sprint C13: Siege Units + Multi-Front (Track B -> C -> A)

> Combat Plan > Phase 7 Sprint 13

- ✅ **P7-E** Dedicated siege units — ram, siege tower, mobile catapult (Track B)
- ✅ **P7-F** Siege unit AI deployment (Track C)
- 🔄 **P7-G** Multi-front war — bounded (Track B)
- 🔄 **P7-H** Graphics for siege units (Track A)
- ✅ **Wave 10 SMR advanced campaign prep** ScenarioRunner campaign resolution + logistics + siege-unit evidence surface (Track B / SMR Analyst validation)
- ✅ **Wave 10 SMR evidence** Campaign resolution + advanced warfare closeout package (SMR Analyst)
- 🔄 **Step10B.2** Organic/manual campaign lifecycle long-run SMR evidence (SMR Analyst)

**Parallelism:** C11 -> C12 -> C13 are **sequential** (each builds on previous).

### Wave 10 — Execution Steps

**Step 1 — campaign siege/runtime resolution first (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-E | Wave 9 ✅ + post-Wave9 deep-review preflight findings closed/downgraded | Campaign siege flow must exist before resolution or campaign-AI/UI follow-ups |

- ✅ `P6-E` accepted GREEN by Meta + external Swarm deep-review synthesis. Campaign encounters integrate with World siege state while preserving Track B boundaries: non-breached suppression clears campaign target identity (`TargetStructureId`, `DefenderColonyId`, `ObservedSiegeId`), breached state remains sticky, same-pair driver/takeover behavior is deterministic, and focused regressions cover disabled-after-target-acquired -> breach while disabled -> re-enable plus same-pair suppressed prior driver inheritance. P6-F is now unblocked for Track B kickoff. Unrelated `AGENTS.md` and `.opencode-router/` must not be staged with the scoped P6-E commit.

**Step 2 — opens when P6-E ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-F | P6-E ✅ | Resolution rules depend on campaign-owned siege/engagement outcomes; do not treat raw pair-keyed World active siege `Status` as independent target-level campaign truth |

- ✅ `P6-F` accepted GREEN by Meta re-review. Track B closed the same-pair contradictory scoring path, historical resolved-breach suppression bug, pair-scoped signed war-score contract, and direct read-model resolution export coverage while keeping campaign-owned siege truth and pair-keyed World siege identity unchanged. Step 3 (`P6-G`/`P6-H`) and Step 4 (`P7-A`) are now READY. ScenarioRunner event/counter-key evidence remains deferred to Wave 10 SMR prep Step 10A.

**Step 3 — opens when P6-F ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P6-G | P6-E ✅ + P6-F ✅ | Strategic campaign AI should target the finalized campaign state machine |
| Track A agent | P6-H | P6-E ✅ + P6-F ✅ | UI polish should visualize the full resolution flow, not only partial siege state |

P6-G boundary note:
- Detailed execution plan: `Docs/Plans/Master/Wave10-Campaign-Logistics-Hardening-Plan.md`.
- Strategic campaign AI should be a faction/campaign strategist surface over finalized campaign state, not another per-person `RuntimeNpcBrain` branch.

P6-G closeout note:
- ✅ Track C AI-only strategist slice accepted with explicit advisory scope: `WorldSim.AI/CampaignStrategy.cs` defines a campaign/faction strategist contract + deterministic default strategy, and `WorldSim.AI.Tests/CampaignStrategyTests.cs` covers launch/hold/tie-break/clamp plus capability-gated abort/convoy/reinforce decisions. Step-review fixups reject self-target and zero-force launch outputs and gate reinforcement against low home defense. Runtime application/context mapping is not part of P6-G and is deferred to P6-J(B) Track B. Track C only re-enters through P6-J(C) if P6-J(B) identifies a concrete advisory contract gap. Do not claim runtime-integrated strategic campaign AI from this slice alone. `RequestConvoy` and `ReinforceCampaign` remain advisory until Track B/P7 hooks exist.

P6-H closeout note:
- ✅ Track A campaign UI polish accepted with limited manual-smoke caveat: campaign panel surfaces compact P6-F resolution results from `WorldRenderSnapshot.Campaigns` including treaty kind, campaign overlay adds minimal resolved-objective markers plus coarse encounter activity cues, and event feed changes are display-only classifier keyword coverage with Director tag precedence preserved. Scope stayed Graphics-only: no Runtime/read-model, ScenarioRunner, AI, Refinery, Java, retention-policy, exact siege-progress, or runtime event-emission changes. Automated build/test/scope checks are green, and manual smoke verified empty-state/toggle coverage. Populated/resolved app smoke could not be exercised because no interactive path creates real campaigns; that missing catalyst is deferred to P6-I and organic gameplay proof to P6-J(B), so it is not a Track A closeout blocker.

P6-H deferred smoke route:
- P6-H is closed as a snapshot-render UI slice, but not as organic app-smoke proof. P6-I proved `Ctrl+Q` -> `Ctrl+F2` populated campaign panel/overlay from the running app. P6-J(B) must later prove organic gameplay campaign launch; manual/operator launch evidence must not be overclaimed as organic proof.

**Step 3A — campaign launch catalyst for manual smoke (opens when P6-F ✅; required for full P6-H closeout)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-I | P6-F ✅ | Add runtime-owned manual/operator campaign launch path with minimal App hotkey routing. Use `Ctrl+Q` as the manual launch shortcut. App may route input and show toast/status, but must call a runtime-owned command/API; Graphics remains snapshot-only. Detailed plan: `Docs/Plans/Master/Wave10-Campaign-Launch-Catalyst-Plan.md`. |

P6-I acceptance:
- From the running app, `Ctrl+Q` deterministically attempts a real `TryCreateCampaign(...)`-backed launch with explicit owner faction, target faction, and requested member count.
- Success/failure is visible through toast/status, including `CampaignCreationStatus` and readable message.
- `Ctrl+Q` followed by `Ctrl+F2` shows populated campaign panel/overlay when runtime gates allow launch.
- No Graphics-owned state synthesis, no direct `World` mutation from App, and no runtime validation bypass.

P6-I closeout note:
- ✅ Track B manual/operator campaign launch catalyst accepted GREEN as `manual_operator` proof only: runtime-owned `ManualCampaignLaunchCommand.DefaultOperatorSmoke` (`Obsidari -> Aetheri`, requested members `1`) delegates through `SimulationRuntime.TryCreateManualCampaign(...)` -> `TryCreateCampaign(...)`, App `Ctrl+Q` only routes to Runtime and shows toast, and no Graphics/AI/ScenarioRunner scope was added. Automated wrapper/boundary/build gates are green, including the review follow-up arch test targeting the active `GameHost.cs`. Manual smoke passed: gates ON (`WORLDSIM_ENABLE_DIPLOMACY=true`, `WORLDSIM_ENABLE_COMBAT_PRIMITIVES=true`) `Ctrl+Q` created real campaigns and `Ctrl+F2` showed populated campaign panel/overlay (`Obs->Aet`, `assembling_pending` / `pending_assembly`, `Result pending`); gates OFF `Ctrl+Q` showed visible `CampaignRuntimeUnavailable`. `Army 0/1` / `anchor:none` is accepted as non-blocking assembly state, not a P6-I failure. Organic campaign launch remains P6-J(B).

**Step 3B — organic campaign launch runtime application (Track B; opens when P6-I ✅)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-J(B) | P6-G ✅ + P6-I ✅ | Wire the already-accepted P6-G strategist output into runtime-owned validation/application so civilizations can organically launch bounded campaigns during normal gameplay. Track B owns runtime fact mapping, cadence, caps, validation, suppression reasons, and `TryCreateCampaign(...)` application. Do not change AI strategy internals unless a concrete contract gap is found; if a gap is found, hand it off to P6-J(C). Detailed plan: `Docs/Plans/Master/Wave10-Campaign-Launch-Catalyst-Plan.md`. |

P6-J(B) acceptance:
- Under hostile/war conditions and sufficient eligible members, at least one civilization can autonomously launch a bounded campaign in normal app/runtime flow.
- Peaceful, disabled, insufficient-force, same-faction, missing-colony, home-defense, active-campaign-cap, and route/path budget cases suppress launches deterministically.
- The implementation is faction/campaign-level, not a new per-person `RuntimeNpcBrain` branch.
- `RequestConvoy`/`ReinforceCampaign` remain advisory until Track B/P7 hooks exist, unless P7 logistics hooks are already available and explicitly consumed.
- ScenarioRunner/SMR evidence must distinguish organic launches from deterministic/manual operator probes.

P6-J(B) handoff requirement:
- Track B must explicitly state one of: `P6-J(C) not needed - existing P6-G strategy contract was sufficient`, or `P6-J(C) needed - Track C must adjust/extend advisory strategy because <specific contract gap>`.

P6-J(B) closeout note:
- ✅ Track B organic campaign launch runtime application accepted GREEN after Meta+Swarm review/fix loop. Runtime now evaluates organic campaign launches on a private faction-level cadence, consumes the existing P6-G `DefaultCampaignStrategist`, and applies only `LaunchCampaign` decisions through runtime-owned validation/application. Accepted review blockers were fixed: launch-time route/path preflight uses `CampaignPathMaxExpansions` before creation, preflight and creation share the same resolved target colony, selected `TargetColonyId` is preserved through a private resolved-colony creation helper while public/manual `TryCreateCampaign(Faction, Faction, int)` remains compatible, unresolved same-pair cap is unordered across faction pairs, `AvailableWarriors` means actual `PersonRole.Warrior`, carrier-only/hunter-only pools suppress, and injected same-faction launch decisions are rejected with `CampaignCreationStatus.SameFaction` before target lookup/application. Scope stayed Track B Runtime + Runtime tests: no App/Graphics/ScenarioRunner/AI/Track C implementation drift, no `RuntimeNpcBrain` campaign branch, and `RequestConvoy`/`ReinforceCampaign`/`AbortCampaign` remain advisory. Verification passed via focused organic launch tests (23/23), campaign runtime/regression tests, AI strategy tests, arch boundary tests, full solution build, diff/static/security checks. Proof type for this step is `organic` runtime-test proof only; durable ScenarioRunner/SMR proof-type export remains Wave 10 SMR prep Step 10A. Campaign runtime availability remains the existing diplomacy+combat gate for this slice. `P6-J(C) not needed - existing P6-G strategy contract was sufficient`.

**Step 3C — campaign strategy follow-up (Track C; opens only after P6-J(B) handoff)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P6-J(C) | P6-J(B) ✅ + explicit B handoff requiring C work | Only adjust the advisory campaign strategist if P6-J(B) proves the existing P6-G contract is insufficient. Track C owns advisory scoring/intent shape only; Runtime application, caps, validation, and `TryCreateCampaign(...)` remain Track B-owned. If P6-J(B) says no Track C changes are needed, mark P6-J(C) N/A in the closeout note rather than opening a no-op implementation session. |

P6-J(C) acceptance, if opened:
- The advisory strategy change is isolated to `WorldSim.AI` / AI tests and does not reference Runtime/App/Graphics.
- The change directly addresses the named P6-J(B) handoff gap and does not invent new campaign runtime policy.
- Runtime application remains owned by the already-closed P6-J(B) path.

**Step 4 — supply lines foundation (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P7-A | P6-E ✅ + P6-F ✅ | Start Phase 7 after siege + resolution runtime ready; P6-G/P6-H/P6-I/P6-J(B)/P6-J(C) are not true execution-dependencies for this step and may proceed in parallel as downstream / non-blocking work. If P7-A lands before P6-J(B), P6-J(B) must consume P7 logistics caps instead of inventing parallel launch constraints. |

P7-A closeout note:
- ✅ Track B supply line convoy foundation accepted GREEN after Meta+Swarm re-review. Runtime now owns non-actor `SupplyConvoyState` entities, `CampaignLogisticsOptions`/counters, organic-path-only campaign cap support, convoy cap/throttle/home-defense/route-budget validation, target-resolved failure, recipient-gated one-time ration-pool delivery, and minimal runtime/read-model snapshot export. The RED-review blocker was fixed: convoy delivery now requires a live assigned target-army recipient adjacent to the convoy before adding rations; static campaign-objective arrival without a recipient stalls/no-progress instead of remote delivery. The telemetry minor was fixed with `ConvoySpawnBlockedByHomeDefense`. Scope stayed Track B runtime/read-model/test plus Graphics interpolator pass-through regression only: no `ForwardBaseState`, no Track C AI contract changes, no App/operator controls, no Graphics rendering/UI, and no ScenarioRunner/SMR export. Verification passed via focused P7-A logistics tests (11/11), campaign regressions, arch/interpolator tests, full solution build, diff/static/security checks. Step 10A still owns durable ScenarioRunner/SMR logistics proof export.

**Step 5 — opens when P7-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P7-B | P7-A ✅ | Forward bases depend on convoy/supply-line structure existing first |

P7-B closeout note:
- ✅ Track B forward-base runtime foundation accepted GREEN after Meta+Swarm review/fix loop. Runtime now owns non-actor `ForwardBaseState` entities with active/expired/abandoned lifecycle, live assigned army-anchor placement, Manhattan home-distance gate, deterministic passable fallback, active-base cap, placement/route/cap/rest counters, TTL expiry, resolved/no-live-member abandonment, rally-point state foundation, and bounded stamina-only rest. The RED review blockers were fixed: liveness/abandonment is split from strict rest eligibility, and production `AdvanceTick(...)` pruning now preserves live assigned transient actors near active same owner/campaign/army forward bases while keeping combat/routing/transient actors rest-ineligible. Scope stayed Track B runtime/read-model/test plus Graphics interpolator pass-through regression only: no Track C AI, no App/operator controls, no Graphics rendering/UI, no ScenarioRunner/SMR export, no ration creation, and no supply-readiness contract changes. Verification passed via focused P7-B logistics tests (25/25), arch/interpolator tests, full solution build, diff/static/security checks. Step 10A still owns durable ScenarioRunner/SMR forward-base evidence export.

**Step 6 — opens when P7-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P7-C (B part) | P7-B ✅ | Scout role runtime hooks should build on the supply-line/forward-base layer |

P7-C (B part) closeout note:
- ✅ Track B scout-intel runtime foundation accepted GREEN after Meta+Swarm review/fix loop. Runtime now owns non-actor `ScoutIntelState` records with hostile/war target-colony observation only, generated by live `Health > 0` colony/faction-owned `PersonRole.Scout` actors, deterministic owner-faction + observed-colony + kind refresh identity, TTL expiry, confidence/radius options, scout counters, detached runtime snapshot export, read-model `ScoutIntelRenderData`, and interpolator pass-through. Review gaps were fixed before closeout: scout freshness is exported explicitly as `TicksSinceRefresh`, and multi-owner same-target observation is covered so owner-dimensional records do not collapse. Scope stayed Track B runtime/read-model/test plus Graphics interpolator pass-through only: no `WorldSim.AI`, no organic campaign `IsKnown` behavior, no scout movement/AI, no App/UI/rendering behavior, no ScenarioRunner/SMR export. Verification passed via focused `Wave10ScoutIntelTests` (9/9), arch/interpolator tests, full solution build, diff/static/security checks. Step10A still owns durable ScenarioRunner/SMR scout evidence export.

**Step 7 — opens when P7-C (B part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P7-C (C part) | P7-C (B part) ✅ | Scout AI consumes the actual runtime scout/intel hooks |
| Track A agent | P7-D | P7-A ✅ + P7-B ✅ | UI for convoys and forward bases can proceed once those runtime entities exist |

P7-C (C part) active consume gate:
- P7-C(C) must classify stale-intel semantics as `in-scope now` at kickoff and explicitly decide how AI/strategy consumers treat runtime scout intel freshness: TTL-only, current-stance filtering, `TicksSinceRefresh` thresholding, or a documented combination. Track C must not assume that all active P7-C(B) intel is strategically actionable without this consume policy.

P7-D progress note:
- ✅ Track A snapshot-only logistics UI implementation is ready for review: the existing `Ctrl+F2` campaign overlay/panel now consumes `WorldRenderSnapshot.SupplyConvoys` and `WorldRenderSnapshot.ForwardBases` to draw convoy current markers, target lines, payload badges, forward-base markers, and radius/crosshair cues, plus a bounded Logistics panel section with summary counts and top-N convoy/base rows. Scope stayed Graphics/ArchTests/docs-only: no Runtime/read-model, App hotkey/control, AI, ScenarioRunner/SMR, Refinery, `Docs/Architecture/`, event-string parsing, or scout-intel UI changes. Manual smoke remains visual-consume proof only and must not be overclaimed as logistics runtime correctness or SMR evidence.

P7-D closeout / residual evidence route:
- ✅ Track A P7-D is closed as a snapshot-only UI/visual-consume slice. Manual smoke (`WORLDSIM_VISUAL_PROFILE=Showcase`, diplomacy/combat/siege enabled, `Ctrl+Q`, `Ctrl+F2`) confirmed that the campaign panel/overlay opens, the compact Logistics section renders, dense rows degrade with top-N fallback, and forward-base active/abandoned rows are visible when runtime reaches those states. This is visual consume evidence only: it must not be overclaimed as logistics runtime correctness, organic campaign proof, or SMR evidence.
- Manual evidence log: `Docs/Evidence/Manual/P7-D-Manual-Smoke-Followup.md`. This captures the observed `Ctrl+Q` / `Ctrl+F2` attempts, including successful `Syl->Obs` / `Aet->Syl` campaigns, intermittent fresh-start launch failures, repeated-campaign spam, one-person-probe behavior, forward-base active/abandoned rows, and why Step 10A/SMR should later provide durable non-interactive proof types for campaign/logistics visualization.
- Deferred residual route: Track B/operator-smoke stability and durable proof are routed to Wave 10 SMR prep Step 10A or later Track B campaign hardening. Do not block Step 8 (`P7-E`) on more manual P7-D smoke chasing.

**Step 8 — dedicated siege units first (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P7-E | P7-A ✅ + P7-B ✅ | Multi-front and siege-unit work needs supply + bases runtime; P7-C/P7-D are not true execution-dependencies for this step and may proceed in parallel as downstream / non-blocking work |

P7-E implementation note:
- ✅ Track B dedicated siege-unit runtime foundation accepted: Runtime now owns campaign-scoped, non-actor `SiegeUnitState` records for ram/siege tower/mobile catapult, spawned idempotently at encounter/siege relevance only when the attacker has `siege_craft`. Scope stayed Track B runtime/read-model/test plus snapshot interpolator pass-through: no AI deployment, no Graphics rendering, no ScenarioRunner/SMR export, no App controls, no independent unit pathfinding, and no second siege truth model. Unit effects remain bounded through the existing siege pressure/damage/breach path (`ram_wall_gate_pressure`, `siege_tower_access_pressure`, `mobile_catapult_ranged_pressure`), resolved campaigns mark units inactive, and `SiegeUnitRenderData` exposes stable unit kind/phase/inactive reason/target/capability-effect fields for P7-F/P7-H consume. Meta+Swarm closeout accepted the lifecycle fix passes: post-world sync uses full pressure-capable validation, same-tick sync validates active dedicated siege-unit campaigns before the `LastPressureTick == Tick` skip, and focused regressions cover invalid/incomplete roster, alive-but-pressure-invalid same-tick cleanup, no-target reporter cleanup, resolver-disabled-after-spawn cleanup, runtime inactive state, and snapshot inactive state. Verification passed via focused `Wave10SiegeUnitTests` (12/12), `WorldSnapshotInterpolatorTests` (5/5), `Wave10CampaignResolutionTests` (22/22), full solution build, and diff hygiene. Step 9 (`P7-F`, `P7-G`, `P7-H`) is unblocked by P7-E; Step10A still owns durable ScenarioRunner/SMR siege-unit evidence export.

**Step 9 — opens when P7-E ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P7-F | P7-E ✅ | Siege-unit AI needs the dedicated unit types and behaviors first |
| Track B agent | P7-G | P7-E ✅ | Multi-front war should be bounded using the finalized siege-unit/runtime constraints |
| Track A agent | P7-H | P7-E ✅ | Graphics consume the siege-unit snapshot once the runtime entity set is stable |

P7-G implementation progress note:
- ✅ Track B bounded multi-front runtime policy accepted for the focused cap/filter/war-score slice: organic runtime launch/application paths now centralize owner cap, unordered pair cap, home reserve, and route budget semantics through `CampaignLogisticsOptions`; target-option filtering and apply-boundary validation share unresolved owner/pair cap helpers; pair-cap filtering allows a second distinct front while duplicate same-pair launches remain blocked; a small deterministic/clamped war-score pressure modifier shapes target choice without bypassing stance, scout, home-defense, route, owner, or pair gates; public/manual campaign creation remains an uncapped compatibility path. Scope stayed Track B runtime/tests/docs only: no Track C strategy contract changes, no Track A/App changes, no `SiegeUnitRenderData`/snapshot contract changes, and no ScenarioRunner/SMR export. Step10A still owns durable multi-front evidence export and must not overclaim these focused tests as durable ScenarioRunner proof.

P7-F closeout note:
- ✅ Track C/Runtime siege-unit AI deployment is accepted GREEN after production-capacity fix. Runtime now applies only `ReinforceCampaign` decisions with `CampaignSiegeUnitProtectionNeeded` through a protection-specific helper; generic `CampaignAdvantageForReinforcement` remains advisory/no-op. `ArmyState.TryAddProtectionReinforcementMemberActorId(...)` provides a narrow internal post-assembly reinforcement path that rejects invalid/duplicate actor ids, bypasses only the initial requested-count cap, and does not modify `RequestedMemberCount` or initial assembly semantics. Focused regression proves a full active campaign with damaged active siege units can gain a reserve warrior (`MemberCount == RequestedMemberCount + 1`) without reflection-expanded capacity, while inactive/history units, home-defense reserve, and generic advantage reinforcement remain no-op. Scope stayed P7-F AI + narrow Runtime/ArmyState/test only: no siege-unit lifecycle, read-model/render shape, App, ScenarioRunner/SMR, P7-G, or P7-H ownership. Verification passed via focused Runtime tests (18/18), focused AI tests (21/21), AI boundary grep, full solution build, and diff hygiene. Step10A still owns durable ScenarioRunner/SMR siege-unit evidence.

P7-H implementation progress note:
- 🔄 Track A overlay-only siege-unit visualization code/automated review accepted, but final visual closeout remains gated on manual smoke: `CombatOverlayPass` consumes `WorldRenderSnapshot.SiegeUnits` in the existing `Ctrl+F8` combat overlay and renders non-actor siege-unit glyphs for ram, siege tower, and mobile catapult with active/inactive styling, damaged-health cue, target cue, and bounded recent-action cues for the known P7-E effects. Scope stayed Graphics/docs-only: no Runtime/read-model/App/AI/ScenarioRunner/SMR/`Docs/Architecture` changes, no new hotkey, no new asset pipeline, and no actor-like gameplay rendering. Before P7-H can be marked ✅, manual app smoke must record distinct kind readability, active/inactive contrast, target cue visibility, at least one recent-action cue, and unchanged battle/siege/breach overlay readability; if no interactive siege-unit scene is available, this must remain an explicit active gate rather than a clean closeout. Step10A still owns durable siege-unit evidence export.

Manual smoke follow-up note (2026-06-07):
- User manual smoke looked healthy at a baseline level (`Ctrl+Q`, `Ctrl+F2`, sampled campaign/logistics states, multi-faction conflict), but dedicated siege units were not directly observed, so P7-H direct siege-unit visual proof is still incomplete. The same smoke also surfaced three accepted future-fix candidates: (1) operator/manual campaigns tending to remain one-member probes when larger squads would be more representative, (2) wood wall icon scale/readability looking too large, and (3) fragmented/random wall placement giving poor defensive value. These are recorded in `Docs/Evidence/Manual/Wave10-Step9-Manual-Smoke-Followup.md` and routed to Step10A/10B classification plus conditional Step10C follow-up, not treated as Step 9 blockers.

P7 logistics cap note:
- Detailed execution plan: `Docs/Plans/Master/Wave10-Campaign-Logistics-Hardening-Plan.md`.
- `P7-A`, `P7-B`, and `P7-G` must define caps/guards before multi-front work: max active campaigns/convoys per faction, home garrison minimum, route/path budget, and convoy spawn throttles.

**Step 10A — SMR evidence surface before closeout**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | Wave 10 SMR prep - export/config | P6-I ✅ + P6-J(B) ✅ + P6-J(C) ✅/N/A + P7-C (B part) ✅ + P7-E ✅ + P7-G ✅ | ✅ Accepted after re-review: normal `runs/*.json.wave10` is main-world-run/default-safe truth only, side-probe evidence is separated into `wave10-probes.json` with explicit `simulation_runtime_probe` provenance, manifest `wave10*` fields summarize probe evidence, drilldown does not claim probe counters as main timeline, and all required Wave10 lanes are represented as positive or explicit `proof_unavailable` probe entries. Deterministic/helper evidence must not be overclaimed as same-run or organic proof; Step10B must classify unavailable lanes before closeout. |
| SMR Analyst | Wave 10 SMR prep - validation | Track B export/config ✅ after re-review | Validate that the new artifact surface and deterministic lanes can prove Wave 10 behavior before the final closeout package |

Step10A validation result:
- SMR prep validation artifact `.artifacts/smr/wave10-smr-prep-validation-001/` completed 72 runs with exit code 0, no assertions, and no anomalies; artifact/provenance surface is valid (`runs[].wave10` main-world truth, probe evidence in `wave10-probes.json`, drilldown non-overclaim intact).
- Track B unavailable-lane fix artifact `.artifacts/smr/wave10-unavailable-lane-fix-001/` completed the 3 seed x 3 planner x 8 config probe matrix. Results: `manual_operator_launch` 9/9 positive, `multi_front_bounded` 9/9 positive as deterministic active multi-front proof (not organic proof), `campaign_siege_resolution` partial 3/9 positive, `forward_base_long_campaign` partial 5/9 positive, and `organic_campaign_launch` / `supply_line_convoy` / `scout_intel_campaign_choice` / `siege_unit_breach` remain explicit `proof_unavailable` with targeted Step10C/Meta-YELLOW routes.
- Mini-review result: Track B evidence/probe fix pass is accepted as commit-safe YELLOW. The fix pass improved evidence classification and one core lane, but clean Step10B is still blocked by default; open Step10C-B/C next unless the user explicitly accepts a YELLOW Step10B with the recorded non-claims.
- ✅ Step10C-B Track B follow-up accepted and committed in `6bc6fd9` (`feat(wave10): close step10c evidence gaps`). Evidence artifact `.artifacts/smr/wave10-step10c-b-runtime-evidence-002/` completed the 3 seed x 3 planner x 8 config probe matrix with exit code 0, no assertions, no anomalies, and 8/8 positive lanes. Meta artifact-only review confirmed `probe.unavailable=0`, all lanes `9/9 positive`, `mainWorldRuns=72`, `probeSources=72`, and scout fresh-intel counters positive (`scoutIntelObserved=1`, `activeScoutIntel=1`, `freshScoutIntel=1`, `campaignTargetsWithScoutIntel=1`).

Step10A follow-up triage plan:
- Active handoff source: `Docs/Plans/Master/Wave10-Unavailable-Lane-Triage-Plan.md`.
- Locked policy: core proof positive before clean Step10B where feasible; expensive/rare/visual gaps may be explicitly deferred to Step10C; Track B first pass is evidence/probe-only and must not use gameplay tuning to make lanes green.
- Core Track B fix-now lanes before Step10B: `organic_campaign_launch`, `campaign_siege_resolution`, `supply_line_convoy`, `multi_front_bounded`.
- Conditional/defer lanes: `forward_base_long_campaign`, `scout_intel_campaign_choice`, `siege_unit_breach` may be fixed if evidence-only setup is enough, otherwise they must be routed to Step10C-B/A/C or future-wave accepted limitation with explicit non-claims.
- User-approved next route: Step10C-B/C first pass is now opened before clean Step10B. Track B receives the first implementation handoff and must classify the remaining unavailable/partial lanes as deterministic setup gap, telemetry export gap, Track B runtime behavior gap, Track C strategist/advisory gap, or accepted low-incidence/deferred limitation.
- Active Track B result: supply, campaign siege/resolution, siege-unit action, forward-base lifecycle, scout-intel target-with-intel, multi-front, and organic launch proof are now positive in the Step10C-B follow-up artifact while preserving `runs[].wave10` main-world truth and `wave10-probes.json` side-probe provenance. Supply remains request-bound outcome proof unless delivered/failed counters become positive.
- Track C remains closed from the current evidence; the previous scout-intel consume blocker was resolved in Track B.
- Next gate: Step10B SMR Analyst closeout evidence can start from the 8/8-positive Track B package. Step10C-C is not needed from current evidence; keep supply wording as request-bound outcome proof unless delivered/failed counters are positive.

**Step 10B — final Wave 10 closeout evidence**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst | Wave 10 SMR evidence | All Wave 10 implementation epics ✅ (`P6-G`/`P6-H`/`P6-I`/`P6-J(B)`/`P6-J(C) ✅ or N/A`/`P7-C B+C`/`P7-D`/`P7-F`/`P7-G`/`P7-H`) + Wave 10 SMR prep ✅ + Step10C-B Track B evidence pass reviewed/accepted ✅ (`6bc6fd9`) | ✅ Accepted GREEN after the repaired closeout rerun. Broad package `.artifacts/smr/all-around-smoke-wave10-001/` remains green, and exact standard-seed targeted closeout `.artifacts/smr/wave10-campaign-resolution-focused-002/` completes the 3 seed x 3 planner x 8 lane matrix with `exitCode=0`, `assertionFailures=0`, `anomalyCount=0`, and `wave10ProbeEvidence.unavailableLaneNames=[]`. The prior failing `multi__goap__seed303__multi_front_bounded` surface is now positive through deterministic owner-cap proof prep (`maxActiveCampaignsForAnyFaction=2`, `campaignLaunchBlockedByCap=1`). Provenance stays intact: `runs[].wave10` remains `main_world_run` truth, `wave10-probes.json` stays `simulation_runtime_probe`, and drilldown main-run timelines remain `not_configured` / `not_sampled`. `assertionSkipped=216` is accepted as benign because the assert-compatible companion main-world profile disables combat primitives for Wave 10 proof configs; combat proof still comes only from the side-probe lanes. Manual/operator launch evidence must not be overclaimed as organic campaign proof, and supply wording remains request-bound unless delivered/failed convoy counters become positive. |

Step10B manual evidence input:
- `Docs/Evidence/Manual/Wave10-Step9-Manual-Smoke-Followup.md` must be loaded during SMR closeout triage. Step10B should explicitly classify whether "no dedicated siege unit seen manually" is expected low-incidence behavior or evidence of a real runtime/visibility gap, and whether the accepted manual candidate issues belong in a post-SMR fix bucket.

Step10B closeout result:
- ✅ SMR Analyst closeout review accepted the repaired rerun as GREEN. The targeted package no longer has unavailable lanes or hard assertion failures, and the previous `multi_front_bounded` Goap/303 regression is covered by focused regressions plus the green `-002` artifact.
- Manual residuals remain post-SMR candidates only: direct siege-unit visual low incidence, wall/watchtower readability, fragmented wall placement, and one-member operator probes are not Step10B blockers from current evidence.
- Track C remains closed from current evidence; no strategist/advisory gap was newly proven by the repaired Step10B rerun.

**Step 10B.2 — organic/manual campaign lifecycle long-run evidence**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst | Step10B.2 organic/manual lifecycle SMR | Step10B ✅ + user request for deeper organic proof | 🔴 RED evidence accepted for routing. Dedicated plan: `Docs/Plans/Master/Wave10-Step10B2-Organic-Campaign-Lifecycle-SMR-Plan.md`. Evidence note: `Docs/Evidence/SMR/wave10-step10b2-organic-manual-lifecycle/README.md`. Runtime-backed packages proved that manual/operator launch works (`90/90` launch, partial encounter/resolution), but hostile organic (`0/90`), pure organic (`0/90`), and stress hostile (`0/240`) did not launch. Stress also failed `SURV-01/02/04` in three seed-606 small-topology runs. Do not route directly to Track C from this evidence; next owner is Track B recovery planning unless a later diagnostic pass proves strategist/advisory ownership. |

Step10B.2 required packages:
- `wave10-organic-pure-soak-001`: pure organic long-run package; no manual launch or deterministic campaign creation.
- `wave10-organic-hostile-soak-001`: hostile/war/tension precondition package; no direct campaign creation.
- `wave10-manual-operator-lifecycle-001`: runtime-owned manual/operator launch at a configured tick, then long lifecycle observation.
- `wave10-organic-lifecycle-stress-001`: broader seed/planner/config stress matrix for stuckness, no-progress, perf, clustering, and lifecycle variability.

Step10B.2 decision policy:
- GREEN if hostile organic and manual lifecycle packages show meaningful campaign lifecycle without systemic regressions.
- YELLOW if pure organic is rare but hostile/manual lifecycle paths work and remaining gaps are clearly routed.
- RED if hostile organic cannot launch, manual lifecycle systematically stalls, or runtime-backed lifecycle evidence cannot be produced reliably.

Step10B.2 result:
- RED. The decisive blockers are hostile organic no-launch, stress hostile no-launch plus survival failures, and incomplete downstream convoy/scout/siege-unit natural activation under manual lifecycle. This opens Step10B.5 before any Step10C residual disposition or Wave10.5 readiness decision.

**Step 10B.5 — organic campaign RED recovery (Track B primary)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Meta Coordinator | Step10B.5-F0 evidence acceptance + prep-slice disposition | Step10B.2 RED evidence | ✅ Closed. Step10B.2 RED evidence is accepted for routing, Step10B.2-A evidence-surface prep slice is accepted as committed prep in `e4bb0a1`, and no behavior fix is bundled into F0. Detail: `Docs/Plans/Master/Wave10-Step10B5-F0-Evidence-Acceptance-And-Prep-Disposition.md`. |

| Track B agent | Step10B.5-F1 organic launch decision-trail diagnostics | F0 ✅ | ✅ Accepted/committed in `cf34de6`. Runtime-owned `organicLaunchDiagnostics` classifies main-run no-launch reason and exports score/count semantics without launch behavior changes. Current smoke `.artifacts/smr/wave10-step10b5-f1-hostile-diagnostics-smoke-003/` reports `no_available_warriors_after_home_defense`; controlled runtime war-with-prepared-warriors coverage still exposes the scout/known-target blocker. Detail: `Docs/Plans/Master/Wave10-Step10B5-F1-Organic-Launch-Decision-Trail-Diagnostics.md`. |

| Track B agent | Step10B.5-F2-A runtime war mobilization / launchable warrior availability | F1 ✅ | ✅ Accepted. `SimulationRuntime.EvaluateOrganicCampaignLaunches(...)` synchronizes persistent `PersonRole.Warrior` roles from existing `ColonyWarState.Tense/War` mobilization before `BuildOrganicCampaignStrategyContext(...)`, targeting the narrow campaign-launch minimum `max(World.GetColonyWarriorCount(...), MinimumHomeDefenseWarriors + 1)` while excluding assigned, blocked, and transient assembly-blocked actors from the sync quota. No direct campaign creation, score-threshold tuning, scout-policy change, or AI/App/Graphics changes. Focused runtime tests and blocker regression pass. Detail: `Docs/Plans/Master/Wave10-Step10B5-F2A-Runtime-War-Mobilization-Launchability.md`. |

| Track B agent | Step10B.5-F2-B mini-SMR / hostile lifecycle harness confidence | F2-A focused tests/build green | ✅ Accepted. Local mini-SMR `.artifacts/smr/wave10-step10b5-f2b-hostile-lifecycle-mini-001/` ran 2 seeds x 2 planners x 1 hostile lifecycle config with `exitCode=0`, `anomalyCount=0`, no `wave10-probes.json`, and all 4 run-level `dominantNoLaunchReason=missing_scout_intel`. This proves the main-run hostile lifecycle moved past `no_available_warriors_after_home_defense`; it is mini-SMR confidence only, not final SMR GREEN/YELLOW/RED recommendation. Detail: `Docs/Plans/Master/Wave10-Step10B5-F2B-Mini-SMR-Harness-Confidence.md`. |

| Track B agent | Step10B.5-F2-C target-knowledge / scout gate policy fix | F2-A/F2-B show target knowledge or scout gate as next blocker | ✅ Accepted. Runtime target-knowledge policy is centralized for target-option construction and apply-boundary validation: `War` targets are baseline-known, `Hostile` remains scout-gated, Neutral/Tense remain non-launchable, scout metadata remains actual scout metadata, and `CountCampaignTargetsWithScoutIntel(...)` remains a fresh actionable scout metric. Local mini-SMR `.artifacts/smr/wave10-step10b5-f2c-target-knowledge-mini-001/` ran 1 seed x 1 planner x 1 hostile lifecycle config for 1200 ticks with `exitCode=0`, `anomalyCount=0`, no `wave10-probes.json`, `runtimeSource=main_world_run`, `campaignLaunches=5`, and `dominantNoLaunchReason=launch_applied`. This is Track B mini-SMR confidence only, not final SMR GREEN/YELLOW/RED recommendation. Detail: `Docs/Plans/Master/Wave10-Step10B5-F2-Target-Knowledge-Policy-Fix.md`. |

| Track B agent, then SMR Analyst artifact review | Step10B.5-F3 hostile organic pilot/confirm | F2-A/F2-B ✅ and F2-C if needed ✅ | ✅ Routing accepted. Track B execution handoff and SMR Analyst review are complete: hostile zero-launch recovery is GREEN, medium confirm is clean enough for medium-backed diagnostics, but standard confirm remains validity-questionable because effective `movementSpeedMultiplier=0` materially affects the `exitCode=2` survival/economy collapse and no downstream lifecycle progression. Primary routing decision: Route C, a new narrow Track B scenario/config diagnostic, before treating standard as a valid F5 survival bug or running any full 90-run hostile package. No next Track B implementation step is opened by this row. Detail: `Docs/Plans/Master/Wave10-Step10B5-F3-Hostile-Organic-Pilot-And-Confirm.md`. |

| Track B agent | Step10B.5-Route C scenario/config diagnostic | F3 routing accepted ✅ | ✅ Accepted. Meta step-review and SMR Analyst routing review approved Route C as `config_bug_confirmed` + one-run `standard_recovered`. ScenarioRunner lifecycle configs now safe-normalize non-positive `MovementSpeedMultiplier` to `1f` at the evidence config boundary while preserving `BirthRateMultiplier=0`; no Runtime/AI/App/Graphics changes. Sentinel `.artifacts/smr/wave10-step10b5-routec-standard-movement-sentinel-001/` used explicit `MovementSpeedMultiplier=1`, exited `0`, had `assertionFailures=0`, no anomalies, no `wave10-probes.json`, and recovered launch/march/encounter/resolution with main-world organic provenance. The old F3 standard `movementSpeedMultiplier=0` collapse is not valid F5 evidence. F4 is explicitly unblocked next; full hostile package remains blocked until later Meta/SMR decision. Detail: `Docs/Plans/Master/Wave10-Step10B5-RouteC-Scenario-Config-Diagnostic.md`. |

| Track B agent | Step10B.5-F4 manual downstream diagnostics | Route C accepted ✅ | ✅ Accepted. Additive nested `manualDownstreamDiagnostics` distinguishes convoy request/spawn/delivery/failure, scout relation/radius/live-actor absence, and siege-unit tech-lock/no-target/action absence without behavior tuning. Local 1x1 manual lifecycle pilot `.artifacts/smr/wave10-step10b5-f4-manual-downstream-diagnostics-001/` exited `0` with `assertionFailures=0`, `anomalyCount=0`, and no `wave10-probes.json`. F4 explains absence; it does not prove positive scout-role or tech-enabled siege-unit lifecycle. Detail: `Docs/Plans/Master/Wave10-Step10B5-F4-Manual-Downstream-Diagnostics.md`. |

| Track B agent | Step10B.5-F5 stress seed-606 survival repro/fix | F4 accepted ✅ + Meta keeps stress seed-606 active | ✅ Accepted as no-fix `no_longer_reproducible` evidence for the three targeted seed-606 lanes only. The three known Step10B.2 stress seed-606 survival lanes were rerun as separate single-lane ScenarioRunner assert+drilldown commands under `.artifacts/smr/wave10-step10b5-f5-seed606-repro-001/`; all passed before any fix, so no runtime/ScenarioRunner behavior change was made. No-fix sentinel rerun under `.artifacts/smr/wave10-step10b5-f5-seed606-postfix-001/` also passed all three lanes with `exitCode=0`, `assertionFailures=0`, `anomalyCount=0`, hard `SURV-01/02/04` pass status, and no `wave10-probes.json` proof claim. Full hostile/pure/stress/perf packages remain blocked until F6 Meta/SMR decision. Detail: `Docs/Plans/Master/Wave10-Step10B5-F5-Stress-Seed606-Survival-Repro-Fix.md`. |

| SMR Analyst + Meta Coordinator | Step10B.5-F6 full recovery rerun + closeout | F3/F4/F5 accepted | ✅ Accepted YELLOW. Staged evidence only, not broad matrix: hostile organic core `.artifacts/smr/wave10-organic-hostile-soak-002/` recovered strongly (`18/18` launch runs, `277` launches, `17/18` march/encounter/resolution, clean assertions/anomalies, `18/18 main_world_run|organic|tick_sampled`). Manual control `.artifacts/smr/wave10-manual-operator-lifecycle-002/` is meaningful but partial (`18/18` lifecycle progression, clean assertions/anomalies, `16/18 Created`, `2/18 CampaignRuntimeUnavailable`). Step10B closes as YELLOW accepted evidence; the manual-command residual is routed to Step10C residual/manual gap triage. Persistent note: `Docs/Evidence/SMR/wave10-step10b5-f6-full-recovery-closeout/README.md`. Detail: `Docs/Plans/Master/Wave10-Step10B5-F6-Full-Recovery-Rerun-And-Closeout.md`. |

Step10B.5 sequencing rules:
- F1 must be diagnostics-only. Do not change launch behavior before the no-launch reason is visible.
- F2-A is now first because F1 proved the main-run hostile lifecycle currently stops at `no_available_warriors_after_home_defense`; target-knowledge policy is F2-C and remains conditional.
- F2 slices must not broadly tune campaign scores; they may only implement the proven runtime blocker or the later accepted target-knowledge policy.
- Track B may run mini-SMR only for local fix confidence/routing proof; full matrix closeout remains SMR Analyst ownership.
- Full SMR packages are not allowed until pilot/confirm runs show meaningful signals.
- Hostile + manual are the recovery decision core; pure/stress/perf are staged escalation, not mandatory default packages.
- F6 is accepted YELLOW: Step10B is closed for Wave10 sequencing, but the manual-command residual (`2/18 CampaignRuntimeUnavailable`) is routed into Step10C residual/manual gap triage.
- Full hostile/pure/stress/perf packages remain blocked unless Meta opens a separate evidence question.
- Track C remains closed unless F1/F2 diagnostics prove a strategy-only contract gap.
- Track A remains optional and evidence-driven after F6; Step10C closeout did not reopen it without fresh manual screenshots.

**Step 10C — post-SMR / manual gap closure (conditional)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Meta Coordinator | Wave 10 gap triage | Step10A unavailable-lane YELLOW + user approval to open Step10C-B/C before clean Step10B | ✅ Closed in docs-only triage (`Docs/Plans/Master/Wave10-Step10C-Residual-Manual-Gap-Triage.md`). The F6 `CampaignRuntimeUnavailable` residual is accepted as a known YELLOW limitation / future Track B-only diagnostic if fresh evidence turns it into a blocker. |
| Track B agent | Step10C-B runtime/evidence gap first pass | Meta triage ✅ + Track B handoff sent | ✅ Accepted/committed in `6bc6fd9`. The `-002` artifact preserves `runs[].wave10` main-world truth and `wave10-probes.json` side-probe provenance and reports 8/8 positive lanes; the former scout-intel timing blocker is cleared inside a fresh-intel probe window. |
| Track A agent | Step10C-A fortification readability pass | Meta triage ✅ | Not opened in the Step10C closeout. Wall/watchtower readability and siege/manual clarity remain Track A candidates only if fresh manual evidence promotes them. |
| Track C agent | Step10C-C advisory follow-up (conditional) | Explicit Track B or SMR handoff | Not opened. No Step10B/Step10C-B/F6 evidence proves a strategist/advisory gap rather than a runtime/operator/visual issue. |

Step10C policy note:
- Step10C is evidence-driven. Every item must come from Step10A/10B evidence or an explicit manual evidence doc such as `Docs/Evidence/Manual/Wave10-Step9-Manual-Smoke-Followup.md`.
- Step10C closeout result (2026-06-21): the F6 manual runtime-command residual is classified as an accepted YELLOW limitation / `not-yet-in-scope` Track B-only follow-up, and the remaining manual/readability candidates stay deferred until fresh evidence promotes them. No new Step10C Track B/A/C slice opened from this closeout.
- Current seeded candidates:
  - F6 manual runtime-command residual: decide whether `CampaignRuntimeUnavailable` under already-active organic campaign pressure is acceptable as a known limitation or needs a narrow Track B diagnostic/fix; classify this first as `in-scope now`, `not-yet-in-scope`, or `already resolved`,
  - operator/manual launch should stay at `1` only as a fallback; if more viable members exist, prefer a larger bounded squad for more representative campaign smoke,
  - classify low manual siege-unit visibility after SMR before deciding whether to change runtime incidence or visual/debug observability,
  - wall/watchtower icon scale/readability,
  - wall placement coherence / defensive usefulness,
  - noisy repeated `Army 0/1` / `anchor:none` assembly rows if SMR/manual evidence shows the issue persists.
- Do not silently absorb SMR tooling/process ideas from `Docs/Ideas/Meta-Ideas-Inbox.md` into Step10C; the current inbox items are mainly SMR/process/tooling and should stay separate unless explicitly promoted.

**Parallelism:** Wave 10 stays sequential across major phases (`C11 -> C12 -> C13`), but inside each phase the final consumer steps are grouped into the same step whenever cross-track work can proceed in parallel.

---

## Wave 10.5 — Convergence + Family Expansion Prep (Director TR3)

Purpose:
- Converge the post-TR2 director path by shrinking imperative validator/fallback responsibilities into clearly transitional boundaries.
- Prepare shared/common vocabulary and future combat/campaign family expansion without reopening the stable C# bridge contract.
- Keep fallback conservative and operationally explicit while solver-backed behavior becomes the primary migration direction.
- Generalize refinery evidence policy carefully so later families can reuse it without making paid live behavior part of the default critical path.

Wave turn-gate:
- Wave 10.5 is `READY` only after Wave 8.5 closeout is `✅` and Wave 10 closeout is `✅`.
- Reason: convergence work should build on a proven solver-backed slice and the matured late combat/campaign surface before shared-family expansion prep begins.
- Wave 10 closeout includes the Wave 10 SMR prep + SMR evidence gates defined in `Docs/Plans/Master/Wave9-10-SMR-Closeout-Plan.md`; implementation-only completion is not enough to unblock Wave 10.5. The user-requested Step10B.2 organic/manual lifecycle SMR gate is closed through Step10B.5-F6 as YELLOW accepted evidence, and Step10C docs-only residual/manual gap triage closeout is now accepted, so Wave 10.5 is unblocked.

Audit hardening source:
- `Docs/Plans/Master/Wave10.5-Refinery-TR3-Audit-Gates-Plan.md`

### Sprint TR3: Convergence + Expansion Prep (Track D primary, Track B/C consult on shared vocabulary touchpoints)

> Tools-Refinery Migration Plan > Phase TR3

- ✅ **TR3-A** Imperative validator deprecation plan (Track D)
- ✅ **TR3-B** Fallback boundary cleanup + paid-live guardrail hardening (Track D)
- ✅ **TR3-C** Shared vocabulary + family expansion prep + evidence-schema generalization (Track D, Track B/C consult)

### Wave 10.5 — Execution Steps

**Step 1 — audit the transitional Java surface first**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR3-A | Wave 8.5 ✅ + Wave 10 ✅ | Audit/classification should happen after the first solver-backed slice and the current combat/campaign surface both stop moving; mandatory pre-read: `Tools-Refinery-Agent-Guide.md` plus its official Refinery links |

TR3-A closeout:
- ✅ `TR3-A` accepted GREEN: `Docs/Plans/Master/Refinery-TR3-Validator-Responsibility-Matrix.md` classifies `INV-01` through `INV-20` plus planner/fallback orchestration responsibilities without retiring validators or changing behavior. `DirectorDesign.java` only gained a comment breadcrumb. Focused validator/planner tests and full Java suite passed; forbidden C#/Runtime/ScenarioRunner/AI/App/Graphics scope check was clean. Next step: `TR3-B` fallback boundary cleanup + paid-live guardrail hardening.

**Step 2 — opens when TR3-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR3-B | TR3-A ✅ | Fallback cleanup should build on the explicit validator-role classification, tighten paid-live guardrails, and keep operational policy explicit per the Refinery Live SMR Plan; re-read the official Refinery links before changing fallback/formal semantics |

TR3-B implementation closeout:
- ✅ `TR3-B` implementation completed: deterministic fallback construction moved behind `DirectorDeterministicFallbackPlanner`, `DirectorRefineryPlanner` keeps retry/exhaustion/telemetry responsibility, existing `directorStage:fallback-deterministic` + `directorFallback` warning semantics remain unchanged, campaign fallback behavior remains behind `campaignEnabled`, and `Docs/Plans/Master/Refinery-TR3-Fallback-Boundary-Policy.md` documents the Java vs ScenarioRunner paid-live guardrail boundary. Focused Java fallback/planner/marker tests and full Java suite passed; no C#/Runtime/ScenarioRunner/AI/App/Graphics changes, paid runs, `.problem` migration, `season_boundary` implementation, or new fallback marker vocabulary.
- ✅ `TR3-B` accepted GREEN after Meta step-review + external Swarm review: no findings discovered, forbidden scope/no-paid/no-marker checks were clean, and TR3-C is now unblocked.

**Step 3 — opens when TR3-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | TR3-C | TR3-B ✅ | Shared/common vocabulary prep should happen after the transitional boundaries and fallback responsibilities are explicit, including how refinery evidence can generalize beyond director-only families; re-read the official Refinery links before family/vocabulary design |

TR3-C implementation closeout:
- ✅ `TR3-C` implementation completed in the narrowed approved scope: symbolic Java/C# `RefineryVocabulary` surfaces and parity tests were added, shared output modes remain `both/story_only/nudge_only/off` while `auto` stays adapter/operator-local, numeric bounds and behavior policy remain in existing owner classes, common/combat/campaign `.problem` files are non-enforcing parse/load skeletons with catalog/resource tests, and `Docs/Plans/Master/Refinery-TR3-Shared-Vocabulary-And-Family-Policy.md` records policy-only family-neutral evidence guidance. No ScenarioRunner, Runtime, AI, App, Graphics, validator-retirement, paid-run, production solver-routing, or new fallback marker vocabulary scope was taken. Focused Java/C# gates, full Java suite, client/adapter tests, solution build, forbidden-scope, no-paid, marker-compatibility, and docs-overclaim checks passed.

Wave 10.5 policy note:
- Any task in this wave that creates, edits, or reviews refinery/model artifacts or convergence policy requires reading `Docs/Plans/Master/Tools-Refinery-Agent-Guide.md` first, including the external official links referenced there. This is mandatory for implementers and reviewers, and must be refreshed in the current session before relying on Refinery semantics.
- `Docs/Plans/Master/Refinery-Live-SMR-Plan.md` remains the detailed operational source of truth for refinery headless lanes; Combined only records ownership, gates, and wave placement.

TR3 audit gates:
- Detailed execution plan: `Docs/Plans/Master/Wave10.5-Refinery-TR3-Audit-Gates-Plan.md`.
- Java/C# output-mode parity matrix must cover story, directive, and campaign ops across `both`, `story_only`, `nudge_only`, and `off`.
- Each `INV-*` must be classified as formal artifact responsibility, bridge guard, runtime adapter guard, or retired.
- Bridge contract roundtrip/parity must include v2 director and campaign ops.
- Solver coverage evidence must distinguish `validated_core` from unsupported effects, biases, causal fields, and campaign fields.
- Snapshot mapper parity must prove C# refinery snapshots are consumed by the Java mapper without shape drift.
- Shared vocabulary must cover faction ids, treaty kinds, goal categories, domains, and severities; mirrored constants should not keep expanding.
- Paid/live guardrails remain opt-in and local; no default CI or generic evidence path may require paid completions.

**Critical path:** `TR3-A -> TR3-B -> TR3-C`.
**Parallelism:** Wave 10.5 is intentionally sequential Track D convergence work; the optimization target here is boundary clarity, not concurrency.

---

## Pre-W10.6 - Refinery Model Fidelity And Validation Assurance (Hard Governance Gate)

Purpose:
- Treat Refinery model fidelity as a first-class governance concern before more quality/tooling work or new runtime waves continue.
- Expose which director semantics are really solver-backed and which remain transitional Java validation.
- Prevent false confidence about the formal validity of LLM-directed world interventions.

Source of truth:
- `Docs/Plans/Master/Pre-W10.6-Refinery-Model-Fidelity-And-Validation-Assurance-Plan.md`

Turn-gate:
- This interrupt gate activates after Wave10.5 TR3-C is accepted GREEN and committed, and after W10.6-M1 planning is already complete.
- `RFM-M2` is closed; W10.6-Q1 and W10.6-Q2 are unblocked for their own turn-gates.
- The gate is hard governance, not a demand to finish the ultimate solver-complete model before any other work.

Priority policy:
- Marker-only `.problem` rows do not count as real formal coverage.
- Transitional Java validator behavior must not be described as the final formal truth layer.
- New director/model semantics may not expand casually without explicit reviewed transitional/formal classification.

Human/user assistance:
- User/manual help was considered during `RFM-V1`; fixture/live_mock evidence closed the required behavior proof, while manual app smoke remains optional.

### Sprint RFM: Director-First Refinery Fidelity Hardening

- ✅ **RFM-M1** Governance lock and language correction (Meta Coordinator)
- ✅ **RFM-D1** Formal coverage inventory (Track D)
- ✅ **RFM-D2** Runtime-fact authority and fixture corpus (Track D primary, Track B consult)
- ✅ **RFM-D3** Director-first predicate promotion pack (Track D)
- ✅ **RFM-D4** Differential solver-vs-validator harness (Track D)
- ✅ **RFM-V1** Wave10.5 behavior proof pass (Track D primary, Meta/user assist optional)
- ✅ **RFM-M2** Closeout and W10.6 unblock decision (Meta Coordinator)

### Pre-W10.6 - Execution Steps

**Step 1 - governance interrupt insertion (Meta)**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Meta Coordinator | RFM-M1 | Wave10.5 TR3-C ✅ + commit + W10.6-M1 ✅ | Accepted plan-review, Combined insertion, state route, mandatory-doc priority note | Plan reviewed, hard governance gate inserted, W10.6-Q1/Q2 explicitly blocked, state/docs updated | RFM-D1 |

RFM-M1 closeout:
- ✅ `RFM-M1` accepted GREEN: this fidelity concern became a pre-W10.6 hard governance gate. W10.6-M1 remained complete, and W10.6-Q1/Q2 were paused until `RFM-M2`; `RFM-M2` is now closed.

**Step 2 - truth inventory first (Track D)**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Track D agent | RFM-D1 | RFM-M1 ✅ | Formal coverage matrix showing real formal predicates vs marker-only vs transitional Java guards vs bridge/runtime guards | New matrix doc accepted; highest-risk gaps explicit; no marker-only rule overclaimed as formal | RFM-D2 + RFM-V1; RFM-D3 only after RFM-D2 ✅ |

RFM-D1 implementation closeout:
- ✅ `RFM-D1` implementation completed as docs/inventory evidence only: `Docs/Plans/Master/Refinery-Formal-Coverage-And-Fidelity-Matrix.md` classifies director semantic families by real formal predicate/error predicate, marker-only artifact row, transitional Java guard, bridge/parser/extractor guard, runtime application guard, observability-only marker, and unsupported solver-sidecar status. The matrix explicitly includes `DirectorCorePatchAssertionsMapper` and `DirectorSolverObservability`, separates mapped runtime facts from authoritative runtime facts, and records highest-risk gaps for RFM-D2/RFM-D3 without changing Java, `.problem`, C#, runtime, ScenarioRunner, AI, App, or Graphics semantics. Next gate: RFM-D1 review/acceptance, then RFM-D2 + RFM-V1 can open per sequencing.

**Step 3 - runtime-fact authority after inventory (Track D + Track B consult)**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Track D agent | RFM-D2 | RFM-D1 ✅ | Runtime-fact authority note, C#-originated fixture/sample corpus, mapper parity tests | Java runtime layer is tied to C#-authority fixtures; drift cases fail clearly; Track B consult deliverable is explicit | RFM-D3 + RFM-D4 |

RFM-D2 closeout:
- ✅ `RFM-D2` accepted GREEN by Meta step-review: Track B consult was consumed, `Docs/Plans/Master/Refinery-RFM-D2-Runtime-Fact-Authority-And-Fixtures.md` records canonical C# authority fields, request budget precedence, transitional fallbacks, campaign config ownership, must-fail drift cases, and verification scope. Java full `PatchRequest` fixture corpus was added under `refinery-service-java/src/test/resources/fixtures/director-runtime-facts/`, with dedicated mapper fixture tests in `DirectorRuntimeFactsFixtureTest` covering canonical current-shape facts, budget precedence, active beat/directive preservation, legacy fallback compatibility, and canonical drift detection. The previous severity-casing review blocker was fixed (`Major`/`Epic` C# snapshot casing with normalized Java facts), focused mapper/fixture tests and full Java suite are green, and no C# runtime, `.problem`, ScenarioRunner, AI, App, or Graphics changes were introduced.

**Step 4 - promote real formal rules, not easy markers (Track D)**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Track D agent | RFM-D3 | RFM-D1 ✅ + RFM-D2 ✅ | Predicate/error-predicate promotion on highest-risk director rule families | At least one non-trivial multi-rule family beyond cooldown becomes real formal enforcement with negative solver tests and parity/mismatch classification | RFM-D4 |

RFM-D3 closeout:
- ✅ `RFM-D3` accepted GREEN by Meta/operator approval: active major/epic exclusivity is now a narrow real formal predicate/error-predicate promotion for explicit core story severity against RFM-D2-authorized active beat facts. Java validator guards remain transitional/backstop, and budget/effect/domain/campaign/causal/ScenarioRunner/C#/Runtime/AI/App/Graphics scope remains unopened.

**Step 5 - differential drift harness (Track D)**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Track D agent | RFM-D4 | RFM-D2 ✅ + RFM-D3 ✅ | Deterministic solver-vs-validator-vs-bridge mismatch report | Unsupported and divergent regions are explicit; gate no longer relies on Java-only self-confirmation | RFM-M2 |

RFM-D4 closeout:
- ✅ `RFM-D4` accepted GREEN by Meta re-review: `DirectorRefineryDifferentialHarnessTest` is a Track D Java test-owned deterministic differential harness comparing Java validator, core solver, and validated-output bridge outcomes. It covers accepted core parity, covered formal+validator rejections, formal-rejects-validator-accepts drift, validator-rejects-formal-accepts drift, validator repair-only/transitional drift, and explicit unsupported-by-formal categories for nested effects, directive biases, campaign ops, and causal chains. The prior YELLOW gap was fixed with an invalid-directive baseline case plus branch precondition asserts. No C# runtime, ScenarioRunner/SMR, paid/live, runtime-visible diagnostic, or new predicate-promotion scope was opened.

**Step 6 - focused behavior proof for current Wave10.5 slice**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Track D agent | RFM-V1 | RFM-D1 ✅ | Fixture/live_mock/manual-safe behavior evidence note | Current Wave10.5 director behavior is re-grounded; residuals are routed without overclaiming solver completeness | RFM-M2 |

RFM-V1 closeout:
- ✅ `RFM-V1` accepted GREEN by Meta step-review: focused Java and C# fixture support checks passed, mandatory `refinery_fixture` and `refinery_live_mock` ScenarioRunner evidence lanes both exited `0`, reached one applied checkpoint, and recorded zero request/apply failures. Checked-in evidence summary: `Docs/Evidence/SMR/pre-w10.6-rfm-v1-behavior-proof/README.md`. Manual app smoke and `refinery_live_validator` were intentionally not run and are not required for RFM-V1 GREEN; paid/live LLM behavior remains out of scope. This proof re-grounds fixture/live_mock behavior only and does not overclaim solver-backed parity for effects/biases/campaign/causal-chain semantics.

**Step 7 - unblock or hold W10.6**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Meta Coordinator | RFM-M2 | RFM-D1 ✅ + RFM-D2 ✅ + RFM-D3 ✅ + RFM-D4 ✅ + RFM-V1 ✅ | Closeout verdict with explicit transitional/formal boundary statement | W10.6-Q1/Q2 either unblocked or held with named blocker(s) | W10.6-Q1 + W10.6-Q2 |

RFM-M2 closeout:
- ✅ `RFM-M2` accepted GREEN: the pre-W10.6 refinery model fidelity interrupt gate is closed. RFM-D1 exposed formal coverage truth, RFM-D2 tied runtime facts to C#-originated fixtures, RFM-D3 promoted active major/epic explicit core story conflicts into real formal error predicates, RFM-D4 made validator/solver/bridge drift and unsupported regions explicit, and RFM-V1 re-grounded current fixture/live_mock behavior with no-paid evidence. Boundary statement: the current director stack is not solver-complete; effects/biases/campaign/causal-chain solver validation, paid/live LLM behavior, manual app smoke, and validator retirement remain unproven/out of scope. Closeout evidence: `Docs/Evidence/SMR/pre-w10.6-rfm-m2-closeout/README.md`. W10.6-Q1/Q2 are unblocked for baseline coverage work under the existing no-threshold/no-generated-artifact policy.

**Critical path:** `RFM-M1 -> RFM-D1 -> RFM-D2 -> RFM-D3 -> RFM-D4 -> RFM-V1 -> RFM-M2`.
**Parallelism:** `RFM-V1` may run alongside `RFM-D2`/`RFM-D3` once `RFM-D1` exists, but must close before `RFM-M2`.

---

## Wave 10.6 - Coverage Baseline And Test Quality (Quality Gate)

Purpose:
- Create the first project-level line/branch coverage baseline across the C# simulation/refinery stack and the Java refinery service.
- Measure test protection before Wave11 ecology runtime work starts, without turning coverage percentage into a premature hard gate.
- Separate line/branch coverage from SMR/manual evidence: coverage identifies blind spots, while SMR/manual evidence still proves scenario behavior and balance.

Source of truth:
- `Docs/Plans/Master/Wave10.6-Coverage-Baseline-And-Test-Quality-Plan.md`

Wave turn-gate:
- W10.6-M1 is already complete.
- W10.6-Q1 and W10.6-Q2 are unblocked now that `RFM-M2` is `✅`.
- Wave10.6 resumes after the pre-W10.6 fidelity interrupt gate closeout.
- Wave11 `E11-A` is not blocked by reaching a numeric coverage percentage.
- Wave11 `E11-A` opens after W10.6-Q4 baseline evidence review, unless Q4 classifies a direct ecology/runtime test-debt item as `blocked-before-wave11`.
- W10.6-Q5 and optional W10.6-Q6 do not block `E11-A` by default unless Q4/Meta explicitly promotes a finding.

Coverage policy:
- Baseline only in Wave10.6: no hard CI threshold and no PR/push coverage fail.
- Later soft thresholds may be designed per module or changed-code surface after stable baseline evidence exists.
- Raw coverage artifacts live under `.artifacts/coverage/<run-name>/` and are not committed by default.
- Checked-in evidence should be short README-style summaries, not generated HTML/XML reports.

Human/user assistance:
- The user can help during W10.6-Q4 by classifying the first coverage gaps, especially whether any ecology-adjacent runtime blind spot should block Wave11.
- Human/CI-owner approval is required before any W10.6-Q6 CI artifact workflow is added.

### Sprint W10.6-Q: Coverage Baseline Infrastructure (Meta strategy lock, then Track B/D implementation; Track C consult where noted)

- ✅ **W10.6-M1** Coverage strategy lock and plan-review (Meta Coordinator)
- ✅ **W10.6-Q1** C# coverage infrastructure - coverlet collector alignment and coverage collection across C# test projects (Track B primary, Track C/D consult)
- ✅ **W10.6-Q2** Java coverage infrastructure - JaCoCo report generation for `refinery-service-java` (Track D)
- ✅ **W10.6-Q3** Unified local coverage runbook and summary artifact contract (Track B technical prep, then Meta governance lock; Track D consult)
- ✅ **W10.6-Q4** First coverage baseline evidence review and gap classification (SMR Analyst review, then Meta classification; user assistance encouraged)
- ✅ **W10.6-Q5** Soft threshold and changed-code policy design (Meta Coordinator)
- ✅ **W10.6-Q6** Optional manual non-blocking CI coverage artifact upload (`workflow_dispatch` only; no PR/push/scheduled trigger)

### Wave 10.6 - Execution Steps

**Step 1 - strategy lock (Meta)**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Meta Coordinator | W10.6-M1 | Wave10.5 TR3-C ✅ + commit | Accepted plan-review, Combined insertion, state route | Plan reviewed with one critic subagent, baseline-only policy locked, no-hard-gate/no-paid/no-generated-artifact policy explicit | Pre-W10.6 RFM gate; W10.6-Q1/Q2 only after RFM-M2 ✅ |

W10.6-M1 closeout:
- ✅ `W10.6-M1` accepted GREEN: coverage plan reviewed by Meta Coordinator plus one critic subagent. Required edits were folded into `Docs/Plans/Master/Wave10.6-Coverage-Baseline-And-Test-Quality-Plan.md`: explicit `WorldSim.RefineryClient.Tests` coverage-policy alignment, first-run restore caveat, Track C consult for `WorldSim.AI.Tests`, and Q6 restricted to optional/manual `workflow_dispatch` only.
- The pre-W10.6 fidelity interrupt (`RFM-D1` through `RFM-M2`) is closed. W10.6-Q1/Q2 are the next executable coverage infrastructure lanes.

**Step 2 - C# and Java coverage infrastructure in parallel**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Track B agent | W10.6-Q1 | W10.6-M1 ✅ + RFM-M2 ✅ | C# coverage collection command/result, package changes, exclusions if any, no generated artifacts staged | Coverlet coverage collection works for relevant C# test projects or exclusions documented; no hard threshold; build/test/`git diff --check` green | W10.6-Q3 |
| Track D agent | W10.6-Q2 | W10.6-M1 ✅ + RFM-M2 ✅ | Java JaCoCo config/result, report paths, no generated artifacts staged | `refinery-service-java` test + `jacocoTestReport` works; XML generated locally; no hard threshold; no paid/live dependency | W10.6-Q3 |

W10.6-Q1 closeout:
- ✅ `W10.6-Q1` accepted GREEN by Meta step-review: C# test projects now have aligned `coverlet.collector` `6.0.2` metadata, per-project coverage fallback passed for all six test projects and produced Cobertura XMLs under `.artifacts/coverage/w10-6-q1-dotnet-001/`, build and diff checks passed, and no production/Java/CI/schema/runbook change was introduced. The solution-wide coverage timeout is routed to `W10.6-Q3`: the unified runbook must document the per-project fallback path instead of assuming solution-wide coverage is the stable default. `WorldSim.ArchTests` coverage remains test/tooling-only and should not be interpreted as production module coverage in `W10.6-Q4`.

W10.6-Q2 closeout:
- ✅ `W10.6-Q2` accepted GREEN by Meta step-review: `refinery-service-java` now has minimal Gradle JaCoCo report generation, `test jacocoTestReport` passed, XML and HTML report paths exist under ignored `build/`, and no threshold, CI, paid/live, `.problem`, validator/fallback/planner, bridge mapping, runtime, ScenarioRunner, C#, App, Graphics, or AI semantic change was introduced. `W10.6-Q3` must define how the native Gradle JaCoCo output is referenced or copied into the unified `.artifacts/coverage/<run-name>/java/` artifact contract.

**Step 3a - technical coverage runbook preparation after both infrastructures exist**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Track B agent | W10.6-Q3 | W10.6-Q1 ✅ + W10.6-Q2 ✅ | Technical coverage runbook draft, local command sequence, artifact paths, and any tool fallback notes | Stable local C# + Java coverage command sequence documented; output paths under `.artifacts/coverage/<run-name>/`; no threshold gate | W10.6-Q3 Meta lock |

**Step 3b - governance lock on the unified runbook**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Meta Coordinator | W10.6-Q3 | W10.6-Q3 Track B handoff ✅ | Approved artifact contract and governance wording for the unified coverage runbook | Runbook semantics are accepted; generated artifacts remain local-only by policy; no threshold gate is implied | W10.6-Q4 evidence review |

W10.6-Q3 Track B handoff:
- ✅ `W10.6-Q3` technical prep produced `Docs/Plans/Master/Wave10.6-Coverage-Runbook.md` with the stable per-project `.NET` fallback sequence, the local summary artifact contract under `.artifacts/coverage/<run-name>/summary.md`, the test/tooling-only interpretation of `WorldSim.ArchTests`, and the v1 Java policy of referencing native JaCoCo XML/HTML outputs from ignored `build/` paths rather than copying generated reports by default.

W10.6-Q3 Meta closeout:
- ✅ `W10.6-Q3` accepted GREEN: runbook semantics are governance-locked, generated artifacts remain local-only by policy, and no threshold gate or CI/push trigger was introduced.

**Step 4a - baseline evidence review before Wave11 runtime work**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| SMR Analyst | W10.6-Q4 | W10.6-Q3 ✅ | Coverage evidence review, gap findings, and recommendation packet | First baseline artifacts are inspected; major gaps are enumerated with evidence and recommended classification | W10.6-Q4 Meta classification |

**Step 4b - gap classification and Wave11 unblock decision input**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Meta Coordinator | W10.6-Q4 | W10.6-Q4 SMR review ✅ | Checked-in coverage evidence note plus final gap classification | First baseline evidence note under `Docs/Evidence/Coverage/...`; raw reports local-only; every major gap classified as `accepted-for-now`, `future-soft-gate`, `test-debt-risk`, or `blocked-before-wave11` | Wave11 `E11-A` unless Q4 blocks; W10.6-Q5 |

W10.6-Q4 SMR Analyst handoff:
- ✅ Step 4a review packet recorded in `Docs/Evidence/Coverage/w10-6-baseline-001/SMR-REVIEW.md`: first baseline artifacts were inspected, major gaps were enumerated, `WorldSim.ScenarioRunner` zero collector output was recommended as `test-debt-risk`, and lane-first `.NET` interpretation limits were recommended as `future-soft-gate` rather than an immediate Wave11 block.

W10.6-Q4 Meta closeout:
- ✅ `W10.6-Q4` accepted GREEN: Meta consumed the Step 4a SMR packet and the unified baseline run `w10-6-unified-baseline-001`, then recorded the final classification in `Docs/Evidence/Coverage/w10-6-baseline-001/README.md` without committing raw generated XML/HTML artifacts. No item was classified as `blocked-before-wave11`: ecology-adjacent `WorldSim.Runtime` and `WorldSim.AI` lanes show meaningful first-baseline coverage, while the `WorldSim.ScenarioRunner` zero collector lane and the missing merged/module-filtered `.NET` summary remain routed as `test-debt-risk` and `future-soft-gate` follow-up inputs for `W10.6-Q5`.

**Step 5 - soft policy design after baseline exists**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Meta Coordinator | W10.6-Q5 | W10.6-Q4 ✅ | Soft-threshold/changed-code policy proposal | No hard CI fail; future promotion criteria explicit; module-specific or changed-code warning policy defined | Optional W10.6-Q6 |

W10.6-Q5 closeout:
- ✅ `W10.6-Q5` accepted GREEN as docs-only policy design: `Docs/Plans/Master/Wave10.6-Coverage-Soft-Policy.md` defines advisory changed-code evidence prompts, future trusted-module delta prompt criteria, and deep-review escalation prompts without enabling any hard CI fail, PR/push/scheduled trigger, generated artifact commit flow, or numeric threshold. The policy explicitly keeps current `.NET` coverage as lane-first evidence until merged/module-filtered interpretation exists, excludes `WorldSim.ScenarioRunner` from early production line-coverage targets while preserving it as an evidence-tooling trust surface, keeps `WorldSim.ArchTests` test/tooling-only, and leaves App/Graphics under manual/architecture smoke evidence. Optional `W10.6-Q6` remains deferred unless the user/CI owner explicitly wants a manual `workflow_dispatch` artifact lane.

**Step 6a - optional CI policy approval**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| Meta Coordinator | W10.6-Q6 | W10.6-Q4 ✅ + human/CI approval | Explicit go/no-go decision for manual CI artifact lane | CI artifact lane is either approved as optional `workflow_dispatch` only or explicitly deferred | W10.6-Q6 CI implementation |

W10.6-Q6 approval:
- ✅ User/CI owner explicitly approved the optional manual artifact lane after `W10.6-Q5`. Scope is limited to a non-blocking `workflow_dispatch` workflow: artifact upload only, no coverage threshold fail, and no PR/push/scheduled trigger.

**Step 6b - optional non-blocking CI artifact lane implementation**

| Session | Epic(s) | Prereq | Expected handoff | Acceptance/evidence | Unlocks |
|---------|---------|--------|------------------|---------------------|---------|
| CI owner | W10.6-Q6 | W10.6-Q6 Meta approval ✅ | Manual CI artifact workflow or explicit defer decision | `workflow_dispatch` only if implemented; artifact upload works; no PR/push/scheduled trigger; no coverage threshold fail | Future coverage trend workflow |

W10.6-Q6 closeout:
- ✅ `W10.6-Q6` implemented and operationally proven: `.github/workflows/coverage-baseline.yml` adds a manual `Coverage Baseline Artifact` workflow with `workflow_dispatch` only, `windows-latest`, `.NET` per-project coverage lanes, Java `test jacocoTestReport`, generated `.artifacts/coverage/<run-name>/summary.md`, and GitHub artifact upload with operator-selected retention. The first CI-owner run `w10-6-ci-manual-001` (`28326071232`) succeeded in `9m 7s`, uploaded `1` artifact, reported `6` .NET Cobertura XML files, and confirmed Java JaCoCo XML + HTML presence. It intentionally has no PR/push/scheduled trigger and no coverage percentage threshold. Test failures can fail the manual workflow, but coverage percentages cannot. Evidence note: `Docs/Evidence/Coverage/w10-6-ci-manual-001/README.md`.

Post-W10.6 quality note:
- No pre-Wave11 test-hardening interrupt is recommended from the coverage baseline alone. A later `test quality ratchet` / quality hardening wave is useful after Wave11 exposes concrete ecology behavior and SMR evidence, focusing on merged/module-filtered .NET reporting, changed-code evidence prompts, and targeted ecology branch/edge tests rather than vanity percentage chasing.

**Critical path:** `W10.6-M1 -> RFM-M2 -> (W10.6-Q1 + W10.6-Q2) -> W10.6-Q3 -> W10.6-Q4 -> E11-A`.
**Parallelism:** Q1 and Q2 may run in parallel after M1 because they touch separate C# and Java tooling surfaces; Q3/Q4 must remain sequential because they consume both outputs.

---

## Wave 11 - Closed-Loop Ecology Redesign (Ecology Phase 2)

Purpose:
- Replace the Pre-Wave8 current-model ecology stabilization with an emergent plant/herbivore/predator loop.
- Move normal viability away from replenishment/respawn and toward lifecycle state, plant availability, hunting pressure, reproduction, starvation, and carrying capacity.
- Keep emergency rescue only as debug/safety fallback with explicit counters, not as the normal balancing mechanism.
- Introduce staged plant/meat supply coupling without pulling domestication, farms, milk, eggs, or full food taxonomy into this wave.

Source of truth:
- `Docs/Plans/Master/Wave11-Closed-Loop-Ecology-Redesign-Plan.md`

Audit hardening source:
- `Docs/Plans/Master/Wave11-Ecology-Hardening-Plan.md`

Wave turn-gate:
- Wave 11 is `READY` only after Wave10.6-Q4 first coverage baseline evidence review is `✅`, unless Meta/user explicitly waives the quality lane.
- Reason: the Pre-Wave8 ecology patch was intentionally a current-model stabilizer; the full closed-loop redesign was deferred until after the Wave 10.5 convergence point, and Wave10.6 now adds a baseline-only test-quality checkpoint before new ecology runtime work.
- Wave11 readiness is not gated on a numeric coverage percentage. It is gated only on whether Q4 finds direct ecology/runtime test-debt that must be classified as `blocked-before-wave11`.

E11 runtime performance note:
- Detailed execution plan: `Docs/Plans/Master/Wave11-Ecology-Hardening-Plan.md`.
- `E11-A`/`E11-B` must define region/tile ecology caches and land-safe spawn/migration policy up front; animal lifecycle work should not add new per-animal full-map scans as the normal path.

### Sprint E11-A: Runtime Ecology Core (Track B primary)

> Wave 11 Ecology Plan > Runtime model

- ✅ **E11-A** Ecology state contract - tile fertility, region capacity, initial/default plant biomass, aggregate passive lifecycle counters, and snapshot/export shape (Track B). Per-animal lifecycle fields/state are explicitly deferred to E11-C/E11-D.
- ✅ **E11-B** Plant growth + region carrying capacity - deterministic growth, overgrazing, season/drought modifiers, and bounded caches (Track B)
- ✅ **E11-C** Herbivore lifecycle - energy, grazing, starvation, reproduction, migration pressure, and no normal respawn dependency (Track B)
- ⬜ **E11-D** Predator lifecycle - energy, hunting, capture gain, starvation, reproduction, and prey-linked capacity (Track B)
- ⬜ **E11-E** Emergency rescue demotion - existing replenish/rescue becomes explicit debug/safety policy with separate counters (Track B)

### Sprint E11-B: Behavior + Supply, then SMR Gates (Track C and Track B first, then SMR Analyst)

> Wave 11 Ecology Plan > Integration and evidence

- ⬜ **E11-F** Animal/AI behavior alignment - animals and NPC reactions consume ecology context; predator-human baseline ON gets bounded behavior (Track C, Track B consult)
- ⬜ **E11-G** Plant/meat supply bridge - staged plant/meat production counters and minimal Wave 8+ supply hooks, no domestication/farming scope (Track B)
- ⬜ **E11-H** SMR ecology invariant pack - hard ecology invariants, ecology-aware drilldown, compare policy, and multi-lane scenario matrix (SMR Analyst, Track B)

### Sprint E11-C: Debug UX, then Evidence Closeout (Track A first, then SMR Analyst/Meta closeout)

> Wave 11 Ecology Plan > Visualization and closeout

- ⬜ **E11-I** Ecology snapshot + debug overlay - fertility/overgrazing, region pressure, animal lifecycle markers, and HUD counters from snapshots (Track A after Track B)
- ⬜ **E11-J** Evidence closeout + baseline decision - full ecology matrix, manual app smoke, hard invariant gate, and baseline promotion decision (SMR Analyst review, then Meta closeout)

### Wave 11 - Execution Steps

**Step 1 - runtime contract first (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | E11-A | W10.6-Q4 ✅ | Contract locks the runtime/snapshot fields before model logic or consumers start; no numeric coverage threshold is required |

E11-A closeout:
- ✅ `E11-A` accepted GREEN after Meta + Swarm step-review synthesis: runtime-owned ecology contract types, deterministic fixed 16x16 region chunks, bounded tile fertility/capacity/default biomass fields, region aggregate snapshots, passive aggregate lifecycle counters, and snapshot/interpolator preservation are implemented with focused runtime tests, PW8 telemetry regression tests, ArchTests, full Runtime tests, solution build, and diff hygiene passing. Scope clarification is now locked: E11-A does not add per-animal lifecycle fields/behavior; E11-C/E11-D own those fields/state/tests. E11-B must convert the E11-A initial/default `PlantBiomass` contract into dynamic plant-model truth or explicitly document any remaining static/default semantics. Pre-check SAST `Random` findings in `World.cs` are accepted as pre-existing/non-E11-A caveats.

**Step 2 - plant capacity foundation (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | E11-B | E11-A ✅ | Plant availability and region capacity must exist before animal lifecycle can use them; E11-B must turn E11-A initial/default `PlantBiomass` into dynamic plant-model truth or document any remaining static/default semantics explicitly |

E11-B closeout:
- ✅ `E11-B` accepted GREEN after Meta + Swarm synthesis and fix pass: dynamic plant biomass model added for the existing food-node/regrowth mutation surface, partial and depleted food harvest now enter bounded plant recovery, regrowth completion restores node+biomass consistency, region biomass/pressure totals update by delta, and overgrazing/drought/season growth behavior is covered by focused tests. Remaining static/default semantics are explicit: background non-food land biomass remains seeded/default until a later plant/lifecycle step introduces broader region/tile growth. No `Animal.cs`, Track C, Track A overlay, ScenarioRunner artifact/invariant, rescue demotion, or food taxonomy scope was introduced. Targeted E11-B/E11-A/PW8/low-cost tests and solution build passed; pre-check SAST `Random` findings in `World.cs` remain accepted as pre-existing/non-E11-B caveats.

**Step 3 - herbivore lifecycle before predator lifecycle (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | E11-C | E11-B ✅ | Herbivores depend on plant capacity; predators depend on viable prey behavior; E11-C owns per-animal herbivore lifecycle fields/state/tests deferred from E11-A |

E11-C closeout:
- ✅ `E11-C` accepted GREEN after second fix-pass Meta step-review: herbivore lifecycle state/model is runtime-owned, grazing uses the E11-B `TryHarvest(... Food ...)` biomass seam, starvation/reproduction/migration counters are aggregate snapshot-safe, and lifecycle births are queued after animal iteration with target-region capacity/reservation/counter authority in `QueueHerbivoreBirth(...)`. The two review blockers were fixed: queued same-tick births now reserve capacity and `Herbivore.TryReproduce(...)` no longer parent-region pre-checks capacity before target-region selection. Focused tests cover same-region reservation, parent-region-full/neighboring-target-region-available success, all-targets-full clean failure with unchanged parent energy/cooldown, failed birth no-mutation, starvation-on-current-food before death, negative migration counter, and non-zero birth/migration snapshot parity. No Track A/C/ScenarioRunner, predator lifecycle, rescue demotion, food taxonomy, or animal snapshot-shape scope was introduced. Verification accepted: focused E11-C tests, full runtime tests, arch tests, solution build, optional full solution tests, syntax/diff/pre-check gates; SAST `World.cs` Random findings remain known pre-existing caveat.

**Step 4a - predator lifecycle (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | E11-D | E11-C ✅ | Predator reproduction/starvation should be tied to the stabilized prey loop; E11-D owns per-animal predator lifecycle fields/state/tests deferred from E11-A |

**Step 4b - rescue demotion after lifecycle paths stand alone (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | E11-E | E11-D ✅ | Rescue demotion should happen after both lifecycle paths can stand on their own |

**Step 5a - behavior and supply bridge open after lifecycle core**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | E11-F | E11-C ✅ + E11-D ✅ | AI/behavior consumes lifecycle context and must bound predator-human baseline behavior |
| Track B agent | E11-G | E11-C ✅ + E11-D ✅ | Plant/meat supply bridge should use real lifecycle outputs, not respawn counters |

**Step 5b - SMR invariant pack after rescue demotion**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst | E11-H | E11-E ✅ + E11-G ✅ | Hard ecology invariants must distinguish lifecycle births from emergency rescues and consume the real plant/meat supply bridge outputs |

**Step 6 - visual/debug consume after snapshot and invariants are stable**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | E11-I | E11-A ✅ + E11-H ✅ | Debug overlay consumes stable snapshot fields and SMR terminology |

**Step 7a - ecology evidence review**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst | E11-J | E11-F ✅ + E11-G ✅ + E11-H ✅ + E11-I ✅ | Review the full ecology matrix first and prepare the closeout recommendation; do not promote baseline or close the wave in the same row |

**Step 7b - ecology closeout and baseline decision**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Meta Coordinator | E11-J | E11-J SMR review ✅ | Close only if accepted lanes pass hard ecology invariants without depending on emergency rescue |

Wave 11 policy note:
- Predator-human interaction is ON in the Wave 11 baseline lanes, but it must be bounded and observable so the ecology gate does not become an uncontrolled colony-wipe test.
- Domestication/farms/milk/eggs are explicitly later work; Wave 11 is wild ecology first.
- Emergency rescue may remain as a debug/safety fallback, but normal acceptance lanes must not rely on it.

E11 hard evidence gate:
- Detailed execution plan: `Docs/Plans/Master/Wave11-Ecology-Hardening-Plan.md`.
- Required invariants: `ECO-SPECIES`, `ECO-PLANT`, `ECO-OSC`, `ECO-RESCUE`, `ECO-SUPPLY`, and `ECO-HUMAN`.
- Required seeds: `101`, `202`, `303`.
- Required planner lanes: `simple`, `goap`, and `htn`.
- Required scenario lanes: default, medium-stress, drought, predator-human, and long-run.
- Closeout must show lifecycle births/survival without depending on emergency rescue counters.

**Critical path:** `E11-A -> E11-B -> E11-C -> E11-D -> E11-E -> E11-H -> E11-J`.
**Parallelism:** Track C `E11-F` and Track B `E11-G` can proceed after lifecycle runtime is available; Track A `E11-I` waits for stable snapshot + SMR terminology.

## Wave 12 Parking Lot - Codebase Architecture Hardening (Not Launchable)

Detailed source plan:
- `Docs/Plans/Master/Wave12-Codebase-Architecture-Hardening-Plan.md`

This is a planning parking lot, not an executable wave. It cannot launch until a source plan defines epics, ownership, gates, and evidence.

Candidate scope from the 2026-05-12 deep audit:
- Align `SimulationRuntime` and `ScenarioRunner` so headless evidence exercises the same command/runtime boundary as app/refinery paths unless a test deliberately bypasses it.
- Add real `GameHost` boundary arch tests; current app-host checks should not rely on legacy shims alone.
- Add snapshot caching or dirty-slice/static-layer separation so `GameHost.Draw()` does not rebuild all read-model data on every draw.
- Add spatial indexes for actor occupancy, structure/blockage lookup, local threat scans, and crowd deconfliction.
- Add structured render data for tactical effects, tower beams, campaign entities, convoys, supply routes, and ecology overlays; avoid renderer-side event-string parsing.
- Harden CI/test matrix around C# runtime/app architecture tests, Java refinery tests, ScenarioRunner smoke/perf lanes, and artifact hygiene.

---

## Summary Table

| Wave | Director Sprint | Combat Sprint(s) | Parallel? |
|------|----------------|-------------------|-----------|
| 1 | D1 (Phase 0 S1) | C1 (Phase 0 S1) | Fully parallel |
| 2 | D2 (Phase 1 S2) | C2 (Phase 1 S2) | MR-1: snapshot merge caution |
| 3 | D3 (Phase 1 S3) | C3 (Phase 1 S3) | Fully parallel |
| 4 | D4 (Phase 2 S4) | C4 (Phase 2 S4) | Fully parallel |
| 5 | D5 (Phase 2 S5) | C5 (Phase 3 S5) | Fully parallel |
| 5.1 | — | C5.1 (Combat closeout) | Track B -> C + A |
| 6 | D6 (Phase 3 S6) | C6 (Phase 3 S6) | MR-2: tick loop caution |
| 7 | D7 (Phase 3 S7) | C7 (Phase 4 S7) | MR-3: sequential |
| 7.1 | TU1 (Telemetry/operator cleanup) | — | Post-Wave7 sidecar cleanup; non-gating by default |
| 7.5 | — | LC1 (Low-Cost baseline) | Staged parallel after `LC1-B1` |
| 8 | — | C8 (Phase 5 S8) | Mostly sequential; final Track A consume |
| 8.5 | TR2 (Tools.Refinery Phase TR2) | — | Director-only sidecar; sequential inside wave, parallel-eligible with Combat Waves 8-10 once Wave 7 + consult gate hold |
| 8.6 | Paid LLM Director SMR Pilot | — | Guardrailed local-only paid pilot before Wave 9; Track D -> Track B -> SMR Analyst |
| 8.7 | Refinery Sidecar Stabilization Mini-Wave | — | No-paid sidecar validator closeout before Wave 9 |
| 9 | — | C9-C10 (Phase 5-6 S9-10) | Mostly sequential; Track A only at final campaign overlay consume |
| 10 | — | C11-C13 (Phase 6-7 S11-13) | Sequential by phase, parallel consumer steps inside phases |
| 10.5 | TR3 (Tools.Refinery Phase TR3) | — | Director-only convergence after Wave 10 and Wave 8.5 |
| Pre-W10.6 | RFM (Refinery model fidelity gate) | — | Director-first hard governance interrupt before W10.6-Q1/Q2 |
| 10.6 | — | W10.6 (Coverage baseline) | Baseline-only quality wave; Q1/Q2 blocked until Pre-W10.6 RFM-M2 closes |
| 11 | — | E11 (Closed-loop ecology redesign) | Runtime-first; Track C/B parallel only after lifecycle core; SMR invariant pack after rescue demotion |

**Totals:** 20 summary rows after adding Pre-W10.6 and Wave 10.6. Full document contains many sprint/addendum headings; epic counts are approximate and should be regenerated before use.

---

## Session Triggers

| Trigger condition | Session to launch | Plan doc |
|-------------------|-------------------|----------|
| Track A Sprint 3 complete + Phase 0 green-lit | Combat Coordinator | `Docs/Plans/Session-Combat-Coordinator-Plan.md` |
| Manual app/SMR testing questions, commands, or env setup help | Manual Test Helper | `Docs/Plans/Session-Manual-Test-Helper-Plan.md` |
| FPS < 60 or Combat Phase 3 reached | Performance Profiling | `Docs/Plans/Session-Perf-Profiling-Plan.md` |
| Combat Phase 0 end or balance regressions | Balance/QA Agent | `Docs/Plans/Session-Balance-QA-Plan.md` |
| Wave 7 complete + telemetry/operator readability follow-up desired | Wave 7.1 telemetry/operator cleanup kickoff | `Docs/Plans/Master/Post-Wave7-Telemetry-Operator-UX-Cleanup-Plan.md` |
| Wave 7 complete | Low-Cost 2D alignment / Wave 7.5 kickoff | `Docs/Plans/Master/world_sim_low_cost_2_d_docs.md` |
| Wave 7 complete + TR1-C consult note locked | Tools.Refinery TR2 kickoff | `Docs/Plans/Master/Tools-Refinery-Migration-Plan.md` |
| Wave 8.5 complete + paid LLM Director pilot desired before Wave 9 | Wave 8.6 paid-live SMR pilot kickoff | `Docs/Plans/Master/Wave8.6-Paid-Live-Director-SMR-Plan.md` |
| Current Wave 9 audit hardening desired before P6-C | Wave 9 runtime/perf hardening review | `Docs/Plans/Master/Wave9-Runtime-Campaign-Hardening-Plan.md` |
| Wave 10 complete + Track A polish bandwidth available | Track A visual overhaul refresh triage | `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Visual-Overhaul-Plan.md` |
| Wave 10 complete + Wave 8.5 complete | Tools.Refinery TR3 kickoff | `Docs/Plans/Master/Tools-Refinery-Migration-Plan.md` |
| W10.6-Q4 ✅ with no `blocked-before-wave11` ecology/runtime test-debt, or explicit Meta/user waiver | Wave 11 closed-loop ecology kickoff | `Docs/Plans/Master/Wave11-Closed-Loop-Ecology-Redesign-Plan.md` |
| Architecture hardening promoted by Meta | Wave 12 architecture hardening planning | `Docs/Plans/Master/Wave12-Codebase-Architecture-Hardening-Plan.md` |

Track A deferred-reference note:
- The Wave 10 visual-overhaul trigger is a refresh/triage point, not approval to execute the old Track A Phase 1 docs as-is.
- When this trigger is used, also read `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Sprint3-Execution-Plan.md` and `WorldSim.Graphics/Docs/Plans/Phase1-Sprint3-Smoke-Checklist.md`.
- Those docs should be treated as reference-only until refreshed against the completed Wave 7.5 baseline and the post-Wave10 combat/campaign Track A surface.

---

*End of Combined Execution Sequencing Plan*
