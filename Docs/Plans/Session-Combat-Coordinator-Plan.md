# Session: Combat Implementation Coordinator

> Operational playbook for a dedicated chat session that coordinates the Combat-Defense-Campaign
> Master Plan across Tracks A/B/C/D. This session is to combat phases what the Meta Coordinator
> is to the overall project -- a persistent coordination hub opened at each phase/sprint boundary.
>
> **Primary reference:** `Docs/Plans/Combat-Defense-Campaign-Master-Plan.md` (1958 lines, 8 phases, 13 sprints)

Status: Ready to launch (trigger: Track A Sprint 3 complete + Phase 0 green-lit)
Last updated: 2026-02-26

---

## 1. When to open this session

| Trigger | Action |
|---------|--------|
| Track A Sprint 3 completes (combat preflight stubs in place) | First session opening; Phase 0 kickoff |
| Every combat sprint boundary (~2 weeks) | Sprint gate + next sprint kickoff |
| Mid-sprint if conflict-scan finds shared-type overlap | Ad-hoc conflict resolution |
| Phase boundary (multi-sprint end) | Phase gate (mini full-sweep) |

---

## 2. Session responsibilities

| Responsibility | Description |
|---|---|
| **Pre-sprint snapshot contract review** | Before each combat sprint starts, review the `WorldRenderSnapshot` changes planned by Track B. Produce a "Snapshot Delta" checklist listing new fields and every downstream file (interpolator, render passes) that must be updated in the same PR. |
| **Cross-track sync check** | At sprint start, verify each track knows its deliverables. Produce a 4-row table: Track / Sprint deliverable / Blocking dependency / Status. |
| **Mid-sprint conflict scan** | ~1 week in, check if any track is touching shared types (`WorldRenderSnapshot`, `PersonRenderData`, AI context fields, Contracts). Flag overlaps early. |
| **Sprint gate checklist** | At sprint end, verify per the Combat Master Plan DoD: `dotnet build`, `dotnet test WorldSim.ArchTests`, headless balance smoke (ScenarioRunner), manual smoke if applicable. Record pass/fail. |
| **Phase gate** | At the end of each phase (multi-sprint boundary), run a mini full-sweep: arch-audit, dod-check for combat-specific DoD criteria, risk-update for combat-related risks. Report to Meta Coordinator session. |
| **Uzenofal relay** | Write combat-specific uzenofal entries to AGENTS.md. Also relay important entries from the Meta Coordinator that affect combat work. |

---

## 3. Per-sprint workflow (playbook)

### Sprint start (Day 1)

```
1. Read Combat Master Plan section for this sprint (Phase X, Sprint Y)
2. Read current AGENTS.md uzenofal for context
3. Produce "Sprint Y Kickoff" brief:
   a. Track A tasks (snapshot rendering, overlays, UI)
   b. Track B tasks (runtime rules, snapshot fields, domain model)
   c. Track C tasks (AI planning, combat decisions)
   d. Track D tasks (if Phase 4, Refinery integration)
   e. Snapshot Delta checklist:
      - New/changed fields in WorldRenderSnapshot / PersonRenderData / etc.
      - Downstream files that MUST be updated in the same PR:
        * WorldSnapshotInterpolator
        * Affected render passes (ActorRenderPass, StructureRenderPass, etc.)
        * RenderFrameContext (if new context needed)
        * WorldSnapshotBuilder
   f. Balance assertion targets (what ScenarioRunner should check after this sprint)
4. Write uzenofal entry: "[date][Combat-Coord] Sprint Y started - [scope summary]"
```

### Mid-sprint check (Day ~7)

```
1. Conflict scan: grep for changes to shared types since sprint start
2. Check if any track is blocked or behind
3. If snapshot shape changed mid-sprint, verify all downstream renderers updated
4. If issues found, write uzenofal warning entry
```

### Sprint end (Day ~14)

```
1. Sprint gate checklist:
   - [ ] dotnet build WorldSim.sln
   - [ ] dotnet test (all test projects)
   - [ ] dotnet test WorldSim.ArchTests/WorldSim.ArchTests.csproj
   - [ ] ScenarioRunner balance smoke passes (assert mode)
   - [ ] Combat Master Plan sprint-specific DoD items met
   - [ ] No new ArchTest violations
   - [ ] Snapshot shape stable (no orphan fields)
2. Write uzenofal entry with results
3. If phase boundary: run phase gate (see section 4)
4. Report summary to Meta Coordinator session
```

---

## 4. Phase gate protocol

At each phase boundary (Phase 0 end, Phase 1 end, etc.), run a focused audit:

1. **Arch-audit** -- verify no new dependency violations from combat code
2. **DoD-check** -- combat-specific DoD from the Master Plan for this phase
3. **Risk-update** -- new combat risks (entity explosion, AI thrashing, balance collapse)
4. **Perf checkpoint** -- if Phase 3+, trigger Performance Profiling session (see below)
5. **Balance checkpoint** -- run ScenarioRunner with combat flags ON, compare to baseline
6. Produce phase gate report and relay to Meta Coordinator

---

## 5. Phase-specific coordination notes

| Phase | Sprints | Special coordination needs |
|---|---|---|
| **Phase 0** | Sprint 1 | First combat snapshot fields (`IsInCombat`, `LastCombatTick`, `Health`). Establish the pattern for snapshot delta reviews. ScenarioRunner must be extended with `TotalCombatDeaths` counter before this sprint ends. Track C C-R1 (contract freeze) should complete before or during this sprint. |
| **Phase 1** | Sprints 2-3 | Diplomacy + territory data enters snapshot. Track A gets territory overlay. Track C gets enemy sensing + C-R2 (GOAP combat hardening starts). HIGH overlap risk on snapshot. Sprint 3 adds walls + pathfinding -- first structural entities in snapshot. |
| **Phase 2** | Sprint 4 | Tech tree changes affect Track D (tech ID mapping in RefineryAdapter). Colony equipment levels added. Track C C-R3 (HTN method quality) is appropriate here. |
| **Phase 3** | Sprints 5-6 | Entity explosion starts (formations, morale, siege). **Trigger Performance Profiling session.** First major perf check. Track C C-R4 (runtime integration) appropriate after Sprint 5. |
| **Phase 4** | Sprint 7 (optional) | Track D heavy. Contracts v2 decision needed BEFORE this sprint. Track C C-R5 (directive integration). |
| **Phase 5** | Sprints 8-9 | Supply model adds inventory + convoys. Second perf checkpoint. |
| **Phase 6** | Sprints 10-11 | Campaign entities (armies, rally points, march columns). Balance/QA critical. |
| **Phase 7** | Sprints 12-13 | Supply lines, forward bases, siege units. Maximum entity count. Perf profiling critical. |

---

## 6. Cross-track dependency rule (enforced by this session)

> Track B (Runtime) must ship snapshot changes first.
> Track A (Graphics) renders only what the snapshot exposes.
> Track C (AI) can develop in parallel once Track B exposes the required context fields.
> Track D (Refinery) depends on runtime command endpoints and must never be a mandatory runtime dependency.

Any deviation from this rule must be flagged in the uzenofal with a justification.

---

## 7. Snapshot Delta checklist template

Use this template at each sprint kickoff when snapshot changes are expected:

```markdown
## Snapshot Delta -- Sprint Y

### New/changed snapshot types
- [ ] `WorldRenderSnapshot`: [describe new fields]
- [ ] `PersonRenderData`: [describe new fields]
- [ ] `TileRenderData`: [describe new fields]
- [ ] `ColonyHudData`: [describe new fields]
- [ ] New record types: [list]

### Downstream update checklist (same PR)
- [ ] `WorldSnapshotBuilder` -- populate new fields
- [ ] `WorldSnapshotInterpolator` -- interpolate if applicable
- [ ] `RenderFrameContext` -- add if render passes need it
- [ ] `WorldRenderer` -- update context construction
- [ ] Affected render passes: [list]
- [ ] `HudRenderer` / panel renderers -- display new HUD data
- [ ] `ColonyPanelRenderer` -- if ColonyHudData changed

### Test update checklist
- [ ] `TerritoryMobilizationTests` -- if territory/war state changed
- [ ] `SimulationHarnessTests` -- if new invariants needed
- [ ] `BoundaryRulesTests` -- if new project references added
- [ ] ScenarioRunner output -- if new counters available
```

---

## 8. Relationship to other sessions

| Session | Relationship |
|---------|-------------|
| **Meta Coordinator** | Reports to at phase boundaries. Receives strategic direction. |
| **Performance Profiling** | Triggers at Phase 3 start, or earlier if FPS < 60. |
| **Balance/QA Agent** | Triggers at Phase 0 end (ScenarioRunner extended). Uses at every sprint gate. |
| **Track-specific sessions** (A/B/C/D) | Coordinates between them; does not do their implementation work. |
