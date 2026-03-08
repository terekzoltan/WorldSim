# Combined Execution Sequencing Plan

Status: Active
Owner: Meta Coordinator
Last updated: 2026-03-06

This document interleaves the Director Integration Master Plan and the Combat-Defense-Campaign
Master Plan into a single wave-based execution schedule with per-item status tracking.

**Execution Steps** are provided per wave starting from Wave 3. They define the concrete session
launch order, prerequisites, and parallelism for each step. Waves 1–2.5 are fully complete and
their execution steps are not retroactively documented — see their proof links for verification.

---

## Reference Key

| Alias | Full Path |
|-------|-----------|
| **Director Plan** | `Docs/Plans/Director-Integration-Master-Plan.md` |
| **Combat Plan** | `Docs/Plans/Combat-Defense-Campaign-Master-Plan.md` |

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

## Wave 4 — Refinery Gate + Military Tech (Director Phase 2a + Combat Phase 2)

### Sprint D4: Formal Model + Validation Loop (Track D only — Java)

> Director Plan > Phase 2 Sprint 4

- ⬜ **S4-A** Formal model layers in Java — all Phase 0-2 invariants (INV-01 through INV-14, INV-20)
- ⬜ **S4-B** Validation/repair loop + fallback planner

### Sprint C4: Military Tech + Advanced Defenses (Track B -> C -> A)

> Combat Plan > Phase 2 Sprint 4

- ⬜ **P2-A** Military + fortification techs in `technologies.json` (Track B)
- ⬜ **P2-B** Colony equipment levels — weapon/armor (Track B)
- ⬜ **P2-C** Advanced defenses — stone walls, gates, arrow/catapult towers (Track B)
- ⬜ **P2-D** AI becomes tech-aware — avoid unwinnable fights (Track C)
- ⬜ **P2-E** Graphics and HUD updates (Track A)

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

## Wave 5 — Runtime Hardening + Group Combat (Director Phase 2b + Combat Phase 3a)

### Sprint D5: Hardening + Invariant Completeness (Track D only — Java)

> Director Plan > Phase 2 Sprint 5

- ⬜ **S5-A** Runtime hardening — dedupe, counters, diagnostic logging
- ⬜ **S5-B** Invariant pack completeness — INV-20, fuzzing (1000 random candidates)

### Sprint C5: Formations + Morale (Track B -> C -> A)

> Combat Plan > Phase 3 Sprint 5

- ⬜ **P3-A** Formation system + group combat resolver (Track B)
- ⬜ **P3-B** Combat morale + routing (Track B)
- ⬜ **P3-C** Commander bonus — Intelligence-based (Track B + C)
- ⬜ **P3-D** Graphics for battles (Track A)

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

## Wave 6 — LLM Integration + Siege (Director Phase 3a + Combat Phase 3b)

### Sprint D6: LLM Creativity + Budget (Track D + B)

> Director Plan > Phase 3 Sprint 6

- ⬜ **S6-A** LLM director proposal stage — OpenRouter (Track D — Java)
- ⬜ **S6-B** LLM + Refinery iterative correction loop (Track D — Java)
- ⬜ **S6-C** Influence budget system (Track D Java + Track B runtime tracking)

### Sprint C6: Siege Mechanics (Track B -> C -> A)

> Combat Plan > Phase 3 Sprint 6

- ⬜ **P3-E** Siege state + breach logic (Track B)
- ⬜ **P3-F** Structure damage integration (Track B)
- ⬜ **P3-G** AI siege tactics — attack vs retreat vs sortie (Track C)
- ⬜ **P3-H** Siege UI/overlays (Track A)

### Wave 6 — Execution Steps

**Step 1 — immediately launchable, parallel with caution (MR-2)**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | S6-A, S6-B | Wave 5 D5 ✅ | Java-only; S6-A → S6-B sequential |
| Track B agent | P3-E, P3-F, S6-C (B part) | Wave 5 C5 ✅ | P3-E → P3-F first, then S6-C runtime |

**MR-2 caution:** S6-C adds budget tracking to `DirectorState`; P3-E/F add siege state to tick loop.
Both are additive — low risk if Track B agent handles both C6 combat epics AND S6-C runtime part.

**Step 2 — opens when P3-E + P3-F ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track C agent | P3-G | P3-E ✅ + P3-F ✅ | AI siege needs breach + damage models |
| Track A agent | P3-H | P3-E ✅ | Siege overlay needs siege state model |

**Step 3 — opens when S6-A + S6-B ✅**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D + Track B | S6-C (both parts) | S6-A ✅ + S6-B ✅ | Budget system wiring after LLM pipeline is stable |

**Critical path:** Track D S6-A/B (LLM pipeline) → S6-C. Combat side: Track B → Track C + A parallel.

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
| Track D + Track A | S7-B | S7-A ✅ | UX/debug toggles build on causal chain monitoring |

**Step 3 — opens when D7 fully ✅ (S7-A + S7-B). C7 is sequential after D7.**

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track D agent | P4-A, P4-B, P4-D | S7-A ✅ + S7-B ✅ | v2 contract expansion for combat ops |
| Track B agent | P4-C | P4-A ✅ + P4-B ✅ | Runtime endpoints need contract + adapter first |

**Critical path:** D7 (S7-A → S7-B) → C7 (P4-A/B/D → P4-C). Strictly sequential.
**Director pipeline is COMPLETE after this wave.**

---

## Wave 8 — Supply & Inventory (Combat Phase 5a)

### Sprint C8: Personal Inventory + Storage (Track B -> C -> A)

> Combat Plan > Phase 5 Sprint 8

- ⬜ **P5-A** Person inventory data model (Track B)
- ⬜ **P5-B** Storehouse integration — withdraw/deposit (Track B)
- ⬜ **P5-C** Consumption from inventory first (Track B)
- ⬜ **P5-D** Snapshot and UI indicators (Track B -> A)
- ⬜ **P5-E** Supply-related tech entries — backpacks, rationing (Track B)

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

---

## Summary Table

| Wave | Director Sprint | Combat Sprint(s) | Parallel? |
|------|----------------|-------------------|-----------|
| 1 | D1 (Phase 0 S1) | C1 (Phase 0 S1) | Fully parallel |
| 2 | D2 (Phase 1 S2) | C2 (Phase 1 S2) | MR-1: snapshot merge caution |
| 3 | D3 (Phase 1 S3) | C3 (Phase 1 S3) | Fully parallel |
| 4 | D4 (Phase 2 S4) | C4 (Phase 2 S4) | Fully parallel |
| 5 | D5 (Phase 2 S5) | C5 (Phase 3 S5) | Fully parallel |
| 6 | D6 (Phase 3 S6) | C6 (Phase 3 S6) | MR-2: tick loop caution |
| 7 | D7 (Phase 3 S7) | C7 (Phase 4 S7) | MR-3: sequential |
| 8 | — | C8 (Phase 5 S8) | N/A |
| 9 | — | C9-C10 (Phase 5-6 S9-10) | Sequential |
| 10 | — | C11-C13 (Phase 6-7 S11-13) | Sequential |

**Totals:** 10 waves, 19 sprints (7 Director + 13 Combat), ~82 epics.

---

## Session Triggers

| Trigger condition | Session to launch | Plan doc |
|-------------------|-------------------|----------|
| Track A Sprint 3 complete + Phase 0 green-lit | Combat Coordinator | `Docs/Plans/Session-Combat-Coordinator-Plan.md` |
| FPS < 60 or Combat Phase 3 reached | Performance Profiling | `Docs/Plans/Session-Perf-Profiling-Plan.md` |
| Combat Phase 0 end or balance regressions | Balance/QA Agent | `Docs/Plans/Session-Balance-QA-Plan.md` |

---

*End of Combined Execution Sequencing Plan*
