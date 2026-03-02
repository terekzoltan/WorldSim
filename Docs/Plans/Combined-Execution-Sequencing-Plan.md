# Combined Execution Sequencing Plan

Status: Active
Owner: Meta Coordinator
Last updated: 2026-03-02

This document interleaves the Director Integration Master Plan and the Combat-Defense-Campaign
Master Plan into a single wave-based execution schedule with per-item status tracking.

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
| MR-1 | 2 | Track B snapshot additions for both Director engines AND diplomacy | Same Track B agent handles both |
| MR-2 | 6 | Director budget in DirectorState + Combat siege in tick loop | Additive changes, low risk |
| MR-3 | 7 | Director causal chains + Combat DeclareWar both expand contracts AND runtime | Sequential sprints, not parallel |

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

### Sprint C1: Combat Primitives (Track B -> C -> A)

> Combat Plan > Phase 0 Sprint 1

- ✅ **P0-A** Core damage model — Strength used, Defense added (Track B)
- ✅ **P0-B** Bidirectional predator combat — retaliation (Track B)
- ✅ **P0-C** AI threat response Fight/Flee (Track C, after P0-A/B)
- ✅ **P0-D** Snapshot + UI feedback — health bars, combat markers (Track B + Track A complete)
- ✅ **P0-E** Test harness + balance smoke tests (Track B)

**Parallelism:** D1 and C1 are **fully parallel** (zero file overlap).

---

## Wave 2 — Runtime Engines + Diplomacy (Director Phase 1a + Combat Phase 1a)

### Sprint D2: Runtime Effects Core (Track B + C + D)

> Director Plan > Phase 1 Sprint 2

- ⬜ **S2-A** Domain Modifier Engine — timed modifier engine in `WorldSim.Runtime` (Track B)
- ⬜ **S2-B** Goal Bias Engine — timed bias engine + Track C integration (Track B + C)
- ⬜ **S2-C** Director State + tick integration + command endpoints (Track B)

### Sprint C2: Diplomacy & Territory (Track B -> C -> A)

> Combat Plan > Phase 1 Sprint 2

- ⬜ **P1-A** Faction stance matrix + persistence (Track B)
- ⬜ **P1-B** Relation dynamics triggers — tension/hostility/war (Track B)
- ⬜ **P1-C** Territory influence + contested tiles (Track B)
- ⬜ **P1-D** Enemy sensing in AI + role system (Track B + C)
- ⬜ **P1-E** Diplomacy panel + territory overlay (Track A)

**Parallelism:** D2 and C2 are **parallel with caution** (MR-1).
Both add fields to `WorldSnapshotBuilder` / `WorldRenderSnapshot`.
Same Track B agent should handle both sprints' snapshot additions.

---

## Wave 3 — Beat Tiers + HUD + Fortifications (Director Phase 1b + Combat Phase 1b)

### Sprint D3: Beat Tiers + HUD + Smoke (Track D + A + B)

> Director Plan > Phase 1 Sprint 3

- ⬜ **S3-A** Beat severity tier implementation (Track D + B)
- ⬜ **S3-B** HUD and event feed integration (Track A)
- ⬜ **S3-C** Output mode matrix end-to-end (Track D)
- ⬜ **S3-D** Fixture parity and smoke test (Track D)

### Sprint C3: Basic Fortifications + Pathfinding (Track B -> C -> A)

> Combat Plan > Phase 1 Sprint 3

- ⬜ **P1-F** Defense domain scaffold — walls + watchtower (Track B)
- ⬜ **P1-G** Navigation/pathfinding v1 — BFS when blocked (Track B)
- ⬜ **P1-H** AI defense building + raid skeleton (Track C)
- ⬜ **P1-I** Graphics for walls/towers/projectiles (Track A)

**Parallelism:** D3 and C3 are **fully parallel**.
Track A has tasks from both sprints (S3-B + P1-I) — schedule sequentially within Track A.

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

**Parallelism:** D4 and C4 are **fully parallel** (D4 is Java-only, C4 is C# runtime/AI/graphics).

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

**Parallelism:** D5 and C5 are **fully parallel** (D5 is Java-only).

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

**Parallelism:** D6 and C6 are **parallel with caution** (MR-2).
S6-C touches `DirectorState` budget tracking; C6 adds siege state to tick loop.
Both are additive to runtime tick — low risk if same Track B agent coordinates.

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

**Parallelism:** MR-3 — **Sequential recommended.**
Both sprints expand v2 contracts and runtime command endpoints.
Run D7 first, then C7 (C7 builds on the v2 namespace established in earlier Director sprints).

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
