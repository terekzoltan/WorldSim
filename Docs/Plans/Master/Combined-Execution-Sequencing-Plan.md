# Combined Execution Sequencing Plan

Status: Active
Owner: Meta Coordinator
Last updated: 2026-03-19

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

Epic codes (e.g., `S1-A`, `P0-B`) are unique and greppable in the respective master plan.

Reference format: `Director Plan > Phase X Sprint Y > S1-A` or `Combat Plan > Phase X Sprint Y > P0-A`.

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

- Before starting any epic, the active track agent must check current wave statuses and dependencies in this file.
- If prerequisites are not complete, explicitly report `NOT READY` to the coordinator/user and do not start coding for that epic.
- If prerequisites are complete, report `READY`, then switch the epic status from `⬜` to `🔄` when implementation begins.
- After acceptance + smoke/test gates pass, switch epic status from `🔄` to `✅`.
- Do not mark another track's epic `✅` without explicit completion signal from that track owner (or coordinator confirmation).
- For Wave 1 Combat sprint ordering, Track A `P0-D` is blocked until Track B snapshot additions required by P0-D are completed.

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

## Wave 7 — Causal Chains + Director x Combat Intersection (Director Phase 3b + Combat Phase 4)

### Sprint D7: Causal Chains + Operational UX (Track D + A + B)

> Director Plan > Phase 3 Sprint 7

- ⬜ **S7-A** Causal chain layer — v2 contracts, monitoring, condition evaluation (Track D + B)
- ⬜ **S7-B** Operational UX — profiles, debug toggles, env var cleanup (Track D + A)

### Sprint C7: Refinery / Director Integration for Combat (Track D — optional)

> Combat Plan > Phase 4 Sprint 7 (optional)

- ⬜ **P4-A** Contracts v2 for diplomacy/campaign ops — DeclareWar, ProposeTreaty (Track D)
- ⬜ **P4-B** Adapter translation to runtime commands (Track D)
- ⬜ **P4-C** Runtime command endpoints — DeclareWar, ProposeTreaty, ApplyMilitaryEvent (Track B)
- ⬜ **P4-D** Java service beats — mock + gated director for war/diplomacy (Track D)

### Wave 7 — Execution Steps

**MR-3 — Sequential recommended.** D7 first, then C7.
Both sprints expand v2 contracts and runtime command endpoints in overlapping namespaces.

**Step 1 — D7 first**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | S7-A (D part: contracts + monitoring) | Wave 6 D6 ✅ | Java + C# contract changes |
| Track B agent | S7-A (B part: condition evaluation runtime) | Wave 6 C6 ✅ | Parallel with Track D on different files |

**Step 2 — opens when S7-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | S7-B (D part) | S7-A ✅ | Profiles/env cleanup build on the causal-chain contract and monitoring shape |
| Track A agent | S7-B (A part) | S7-A ✅ | In-game debug toggles and consume-side UX build on the same stabilized monitoring shape |

**Step 3 — opens when D7 fully ✅ (S7-A + S7-B). C7 is sequential after D7.**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | P4-A | S7-A ✅ + S7-B ✅ | Expand contracts for combat-facing director/runtime ops first |

**Step 4 — opens when P4-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | P4-B | P4-A ✅ | Adapter translation depends on the new contract ops existing first |

**Step 5 — opens when P4-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | P4-D | P4-B ✅ | Java service beat generation should target the already-mapped adapter contract |
| Track B agent | P4-C | P4-A ✅ + P4-B ✅ | Runtime endpoints need the contract and adapter semantics defined first |

**Critical path:** D7 (S7-A → S7-B) → C7 (P4-A → P4-B → {P4-D + P4-C}). Strictly sequential across waves, with only the final C7 step parallelized.
**Director pipeline is COMPLETE after this wave.**

---

## Wave 7.5 — Low-Cost Visual Systems Baseline

Purpose:
- Convert the low-cost 2D strategy into an explicit execution wave after Director completion and before supply/campaign expansion.
- Lock the default runtime/render path to a cheap, profile-aware baseline so later campaign growth does not silently assume showcase-only costs.
- Keep visual polish, post-fx, and cinematic capture additive on top of the baseline instead of redefining it.

### Sprint LC1: Profiles + Visual Driver Boundary (Track B -> A -> C)

- ⬜ **LC1-B1** Snapshot visual-driver field audit + minimal additive export set for state-driven rendering (Track B)
- ⬜ **LC1-B2** Runtime/profile plumbing for `Showcase`, `DevLite`, and `Headless` defaults (Track B)
- ⬜ **LC1-A1** Terrain state-driven variation baseline -- palette/tint/noise/culling-friendly rendering (Track A)
- ⬜ **LC1-A2** Atmosphere + ambient-life baseline under explicit quality gates (Track A)
- ⬜ **LC1-A3** Settings/HUD/profile visibility + low-cost regression smoke checklist updates (Track A)
- ⬜ **LC1-C1** AI/planner telemetry + profile-compatibility audit for headless/devlite determinism (Track C, additive only)

### Wave 7.5 — Execution Steps

**Step 1 — Track B sets the baseline contract first**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | LC1-B1 | Wave 7 ✅ | Define the visual-driver boundary before any profile or rendering follow-up starts |

**Step 2 — opens when LC1-B1 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | LC1-B2 | LC1-B1 ✅ | Profile plumbing builds on the agreed snapshot/visual-driver boundary |
| Track A agent | LC1-A1 | LC1-B1 ✅ | Terrain/state-driven variation can start once the snapshot driver contract is stable |

**Step 3 — opens when LC1-B2 ✅ + LC1-A1 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | LC1-A2 | LC1-B2 ✅ + LC1-A1 ✅ | Atmosphere/ambient-life tuning should respect the finalized low-cost profile wiring |
| Track C agent | LC1-C1 | LC1-B2 ✅ | Only additive telemetry/guardrails; no renderer scope or profile-owned game logic |

**Step 4 — opens when LC1-A2 ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | LC1-A3 | LC1-B2 ✅ + LC1-A2 ✅ | Settings/HUD/profile visibility and checklist updates should document the final baseline, not an intermediate one |

Acceptance notes:
- `DevLite` becomes the default development baseline; `Showcase` is explicit/opt-in and `Headless` remains available for SMR and batch runs.
- New visual work remains snapshot-driven; Graphics does not compute gameplay-state stand-ins.
- Viewport culling, cheap terrain variation, and quality-gated atmosphere are baseline concerns, not optional late polish.
- Later combat/campaign waves consume the low-cost baseline instead of redefining the default rendering cost profile.

Proof targets:
- Runtime/snapshot docs and tests for the chosen visual-driver fields.
- Track A smoke checks covering `Showcase`, `DevLite`, and `Headless` profile behavior.
- Perf/QA evidence showing the default path stays cheap enough for multi-instance/dev workflows.

**Parallelism:** `LC1-B1` is the gate. After that, `LC1-B2` and `LC1-A1` run in parallel. After `LC1-B2 + LC1-A1`, `LC1-A2` and `LC1-C1` can run in parallel, and `LC1-A3` closes the wave after the final Track A baseline is stable.

---

## Wave 8 — Supply & Inventory (Combat Phase 5a)

### Sprint C8: Personal Inventory + Storage (Track B -> C -> A)

> Combat Plan > Phase 5 Sprint 8

- ⬜ **P5-A** Person inventory data model (Track B)
- ⬜ **P5-B** Storehouse integration — withdraw/deposit (Track B)
- ⬜ **P5-C** Consumption from inventory first (Track B)
- ⬜ **P5-D** Snapshot and UI indicators (Track B -> A)
- ⬜ **P5-E** Supply-related tech entries — backpacks, rationing (Track B)

### Wave 8 — Execution Steps

**Step 1 — inventory data first (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-A | Wave 7.5 ✅ | Inventory data model is the base for storage, consumption, tech hooks, and UI |

**Step 2 — opens when P5-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-B | P5-A ✅ | Storehouse withdraw/deposit rules depend on the inventory model existing |

**Step 3 — opens when P5-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-C | P5-B ✅ | Consumption should switch to inventory only after refill/storehouse semantics are stable |

**Step 4 — opens when P5-C ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-E | P5-C ✅ | Tech effects should target the stabilized inventory/carry rules |

**Step 5 — opens when P5-E ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-D (B part) | P5-E ✅ | Snapshot/export side should reflect the final inventory + tech shape |

**Step 6 — opens when P5-D (B part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | P5-D (A part) | P5-D (B part) ✅ | UI indicators consume the finalized carry/supply snapshot fields |

**Parallelism:** Wave 8 is intentionally mostly sequential Track B work; only the final Track A consume step is separate.

---

## Wave 9 — Army Supply + Campaign Start (Combat Phase 5b + 6a)

### Sprint C9: Army Supply Model (Track B -> C)

> Combat Plan > Phase 5 Sprint 9

- ⬜ **P5-F** Army supply model — aggregate + consumption (Track B)
- ⬜ **P5-G** Supply carrier role + AI behaviors (Track B + C)
- ⬜ **P5-H** Foraging behavior (Track B + C)
- ⬜ **P5-I** Fallback supply budget for early prototypes (Track B)

### Sprint C10: Campaign Skeleton (Track B -> C -> A)

> Combat Plan > Phase 6 Sprint 10

- ⬜ **P6-A** Campaign and army entities (Track B)
- ⬜ **P6-B** Assembly and rally points (Track B)
- ⬜ **P6-C** March system + encounters (Track B)
- ⬜ **P6-D** Snapshot + overlays (Track B + A)

**Parallelism:** C9 and C10 are **sequential** (C10 depends on supply model from C9).

### Wave 9 — Execution Steps

**Step 1 — army supply foundation (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-F | Wave 8 ✅ | Aggregate army supply and consumption rules are the base for every later campaign step |

**Step 2 — opens when P5-F ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-I | P5-F ✅ | The fallback budget should mirror the already-defined supply model instead of competing with it |

**Step 3 — opens when P5-I ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-G (B part) | P5-F ✅ + P5-I ✅ | Runtime role/state hooks for supply carriers build on the settled supply model |

**Step 4 — opens when P5-G (B part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P5-H (B part) | P5-G (B part) ✅ | Foraging runtime behavior should layer onto the carrier/resupply baseline |

**Step 5 — opens when P5-H (B part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P5-G (C part) | P5-H (B part) ✅ | AI carrier behavior should target the actual runtime hooks, not placeholders |

**Step 6 — opens when P5-G (C part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P5-H (C part) | P5-H (B part) ✅ | Foraging decision logic depends on the runtime forage command/state existing |

**Step 7 — campaign entities (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-A | P5-F ✅ + P5-I ✅ + P5-G ✅ + P5-H ✅ | Start campaign work only after the supply baseline is usable |

**Step 8 — opens when P6-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-B | P6-A ✅ | Assembly/rally depends on campaign and army entities |

**Step 9 — opens when P6-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-C | P6-B ✅ | March and encounters need the assembly/rally flow to exist first |

**Step 10 — opens when P6-C ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-D (B part) | P6-C ✅ | Snapshot/export should reflect the actual campaign loop, not an incomplete placeholder |

**Step 11 — opens when P6-D (B part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | P6-D (A part) | P6-D (B part) ✅ | Overlays consume the campaign snapshot once it is stable |

---

## Wave 10 — Campaign Resolution + Advanced Warfare (Combat Phase 6b + 7)

### Sprint C11: Campaign Siege + Resolution (Track B -> C -> A)

> Combat Plan > Phase 6 Sprint 11

- ⬜ **P6-E** Siege integration in campaign flow (Track B)
- ⬜ **P6-F** Resolution — loot, war score, peace (Track B)
- ⬜ **P6-G** Strategic campaign AI (Track C)
- ⬜ **P6-H** Campaign UI polish (Track A)

### Sprint C12: Supply Lines + Forward Bases (Track B -> C -> A)

> Combat Plan > Phase 7 Sprint 12

- ⬜ **P7-A** Supply line convoy entities (Track B)
- ⬜ **P7-B** Forward bases / camps (Track B)
- ⬜ **P7-C** Scout role + intel (Track B + C)
- ⬜ **P7-D** UI for supply lines and forward bases (Track A)

### Sprint C13: Siege Units + Multi-Front (Track B -> C -> A)

> Combat Plan > Phase 7 Sprint 13

- ⬜ **P7-E** Dedicated siege units — ram, siege tower, mobile catapult (Track B)
- ⬜ **P7-F** Siege unit AI deployment (Track C)
- ⬜ **P7-G** Multi-front war — bounded (Track B)
- ⬜ **P7-H** Graphics for siege units (Track A)

**Parallelism:** C11 -> C12 -> C13 are **sequential** (each builds on previous).

### Wave 10 — Execution Steps

**Step 1 — campaign siege/runtime resolution first (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-E | Wave 9 ✅ | Campaign siege flow must exist before resolution or campaign-AI/UI follow-ups |

**Step 2 — opens when P6-E ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P6-F | P6-E ✅ | Resolution rules depend on the campaign reaching siege/engagement outcomes |

**Step 3 — opens when P6-F ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P6-G | P6-E ✅ + P6-F ✅ | Strategic campaign AI should target the finalized campaign state machine |
| Track A agent | P6-H | P6-E ✅ + P6-F ✅ | UI polish should visualize the full resolution flow, not only partial siege state |

**Step 4 — supply lines foundation (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P7-A | P6-E ✅ + P6-F ✅ + P6-G ✅ + P6-H ✅ | Start Phase 7 only after the end-to-end campaign loop is complete |

**Step 5 — opens when P7-A ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P7-B | P7-A ✅ | Forward bases depend on convoy/supply-line structure existing first |

**Step 6 — opens when P7-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P7-C (B part) | P7-B ✅ | Scout role runtime hooks should build on the supply-line/forward-base layer |

**Step 7 — opens when P7-C (B part) ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P7-C (C part) | P7-C (B part) ✅ | Scout AI consumes the actual runtime scout/intel hooks |
| Track A agent | P7-D | P7-A ✅ + P7-B ✅ | UI for convoys and forward bases can proceed once those runtime entities exist |

**Step 8 — dedicated siege units first (Track B)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | P7-E | P7-A ✅ + P7-B ✅ + P7-C ✅ + P7-D ✅ | Multi-front and siege-unit follow-ups should build on the completed logistics layer |

**Step 9 — opens when P7-E ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P7-F | P7-E ✅ | Siege-unit AI needs the dedicated unit types and behaviors first |
| Track B agent | P7-G | P7-E ✅ | Multi-front war should be bounded using the finalized siege-unit/runtime constraints |
| Track A agent | P7-H | P7-E ✅ | Graphics consume the siege-unit snapshot once the runtime entity set is stable |

**Parallelism:** Wave 10 stays sequential across major phases (`C11 -> C12 -> C13`), but inside each phase the final consumer steps are grouped into the same step whenever cross-track work can proceed in parallel.

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
| 7.5 | — | LC1 (Low-Cost baseline) | Staged parallel after `LC1-B1` |
| 8 | — | C8 (Phase 5 S8) | Mostly sequential; final Track A consume |
| 9 | — | C9-C10 (Phase 5-6 S9-10) | Mostly sequential; Track A only at final campaign overlay consume |
| 10 | — | C11-C13 (Phase 6-7 S11-13) | Sequential by phase, parallel consumer steps inside phases |

**Totals:** 12 waves, 21 sprints (7 Director + 15 Combat/closeout), ~94 epics.

---

## Session Triggers

| Trigger condition | Session to launch | Plan doc |
|-------------------|-------------------|----------|
| Track A Sprint 3 complete + Phase 0 green-lit | Combat Coordinator | `Docs/Plans/Session-Combat-Coordinator-Plan.md` |
| Manual app/SMR testing questions, commands, or env setup help | Manual Test Helper | `Docs/Plans/Session-Manual-Test-Helper-Plan.md` |
| FPS < 60 or Combat Phase 3 reached | Performance Profiling | `Docs/Plans/Session-Perf-Profiling-Plan.md` |
| Combat Phase 0 end or balance regressions | Balance/QA Agent | `Docs/Plans/Session-Balance-QA-Plan.md` |
| Wave 7 complete | Low-Cost 2D alignment / Wave 7.5 kickoff | `Docs/Plans/Master/world_sim_low_cost_2_d_docs.md` |

---

*End of Combined Execution Sequencing Plan*
