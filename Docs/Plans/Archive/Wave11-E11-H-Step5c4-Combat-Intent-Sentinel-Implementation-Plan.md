# Wave 11 E11-H Step 5c4 Combat-Intent Sentinel Implementation Plan

Status: HISTORICAL - implementation already present in commit `3e77d92e6b8e4d3febb9f1aa2fbd37c2ffcee077`.
Archived 2026-09-23; not an active implementation request.
The two planned candidate blobs match that committed fix on master. This archive
records implementation provenance; it does not rerun verification, supply missing
historical review evidence, or accept the separate ecology lifecycle.
The original local plan was retained in a hash-verified private backup; only this
status preface and the machine-specific isolated path were normalized here.
Plan identity: `worldsim-wave11-e11-h-step5c4-combat-intent-sentinel-final-v1@base-0022c273`
Accountable lane: Track B / TRACK / track-b
Wave/Epic: Wave 11 / E11-H / Step 5c4

## Goal

Remove unchecked sentinel arithmetic from recent-combat intent bookkeeping so a fresh
actor cannot receive phantom follow-through intent, while preserving genuine recent
contact and active `Fight`, `RaidBorder`, and `AttackStructure` follow-through.

This is a standalone correctness package. It does not repair or prove the E11-H
predator-prey lifecycle.

## Prerequisites And Frozen Identity

- Accepted base commit: `0022c273b2a0dbdb62a64b6255290e8187d77452`.
- Accepted base tree: `a211ce419a96d7dfcbead4c302ed821f25e8e690`.
- The implementation index must start empty.
- `WorldSim.Runtime/Simulation/Person.cs`:
  - base blob: `3d04ef0c362dd8fa7057cc432663760fb4245aa9`;
  - candidate blob: `ca0be8a718d05d1edf8de97efe56b3741289f3f7`;
  - exact numstat: `+6/-2`.
- `WorldSim.Runtime.Tests/ContactFollowThroughTests.cs`:
  - base blob: `19c87b6bc49e277ed37ad07456587cd92f1e9cc6`;
  - candidate blob: `97ebb4a6d24549a3305440ced9427a5df90810e9`;
  - exact numstat: `+10/-0`.
- Isolated candidate root:
  `<historical isolated worktree; runtime-local>`.

Any base, tree, blob, hunk, ownership, or dependency drift is a hard stop and routes
back to Meta/Orchestrator. Do not reconstruct or absorb unrelated work silently.

## Scope And Non-Goals

The candidate may change only:

- `Person.HasRecentCombatIntent(int)` initialized and monotonic timestamp guards;
- `ContactFollowThroughTests.FreshPerson_HasNoRecentCombatIntentBeforeAnyContact`.

The final plan document and `ops/PROJECT_STATE.md` are non-candidate continuity and
governance surfaces. They must not enter the two-file Runtime candidate identity.

The candidate must not change:

- any other `Person.cs` behavior;
- `RecentHostileMemoryTicks`, contact recording, pursuit, damage, or callers;
- active `Fight`, `RaidBorder`, or `AttackStructure` follow-through;
- `AGENTS.md`, `CombatPrimitivesTests.cs`, or `Wave11AnimalLifecycleTests.cs`;
- animal seeding or lifecycle, predator-human pressure, predator-prey recruitment or
  mortality, combat constants, rescue, or replenishment;
- AI, ScenarioRunner, App, Graphics, refinery, assertions, or evidence thresholds;
- Meta-owned findings/evidence documents or raw `.artifacts/**`;
- expanded 9-run or full 45-run matrices, E11-H acceptance, E11-I, or E11-J.

No public API, DTO, schema, persistence format, configuration, module dependency, or
compatibility layer is added.

## Interfaces And Ownership

Track B owns the internal Runtime methods `Person.HasRecentCombatIntent(int)` and
`Person.HasCombatFollowThroughIntent(int)` and their focused Runtime tests.

The recency contract is:

- an uninitialized timestamp is not recent;
- a timestamp later than `currentTick` is not recent;
- subtraction occurs only after initialization and monotonicity checks;
- a nonnegative timestamp within `RecentHostileMemoryTicks` remains recent;
- active `Fight`, `RaidBorder`, and `AttackStructure` jobs retain follow-through.

Future-timestamp rejection and unchanged `AttackStructure` behavior are
inspection-backed for this frozen candidate. They are not separately test-backed.
Adding tests for them requires a newly frozen candidate identity.

Meta owns Combined-plan state, the findings registry, final acceptance review, and
closeout governance. Predator-prey diagnostics remain a separate Track B route.

## Feature, User Story, And Tasks

Feature F1: Safe recent-combat intent bookkeeping.

User Story US1: As the Runtime combat pipeline, a fresh actor with no hostile or
combat observation must not receive phantom recent intent, while genuine recent
contact and active combat jobs retain follow-through.

Tasks:

1. Materialize this final plan as non-candidate documentation.
2. Create the exact isolated candidate and verify frozen identity before mutation.
3. Retain only the accepted `HasRecentCombatIntent` guard hunk.
4. Retain only the accepted fresh-person regression hunk.
5. Run focused test, compile, identity, index, scope, and hygiene gates.
6. Freeze the two-file implementation evidence and route it to Meta `/step-review`.

## Ordered Implementation

1. Use a detached isolated worktree rooted at the accepted base. Never reset, stash,
   normalize, or package the primary dirty worktree.
2. Verify the base commit/tree, base blobs, primary candidate blobs, exact two-path
   diff, numstat, and empty index.
3. Apply only the accepted two-file patch to the isolated root.
4. Confirm `_recentHostileContactTick >= 0` and
   `currentTick >= _recentHostileContactTick` precede subtraction.
5. Confirm `LastCombatTick >= 0` and `currentTick >= LastCombatTick` precede
   subtraction.
6. Confirm `RecentHostileMemoryTicks`, the active-job expression, contact recording,
   pursuit, combat damage, and callers are unchanged.
7. Confirm the new test asserts both `HasRecentCombatIntent` and
   `HasCombatFollowThroughIntent` are false for a fresh person.
8. Keep all six existing `ContactFollowThroughTests` unchanged. Preserve the recorded
   RED-before-fix evidence rather than reverting a shared file to recreate RED.
9. Run the exact verification commands below.
10. Recompute blobs, numstat, path list, index state, and diff hygiene.
11. Write the implementation evidence with exact commands, results, identity,
    inspection-backed claims, exclusions, and lifecycle non-claims.
12. Update `ops/PROJECT_STATE.md` separately or explicitly record
    `No state change from this step`.
13. Route to Meta `/step-review`. Do not stage, commit, push, or request another
    `/terv-review`.

## Verification

Focused regression:

```powershell
dotnet test "WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj" --filter "FullyQualifiedName~ContactFollowThroughTests" --no-restore -m:1 -p:UseSharedCompilation=false
```

Required result: `7 passed, 0 failed, 0 skipped`.

Runtime warning-as-error build:

```powershell
dotnet build "WorldSim.Runtime\WorldSim.Runtime.csproj" --no-restore -m:1 -p:UseSharedCompilation=false -p:TreatWarningsAsErrors=true
```

Required result: zero warnings and zero errors.

Solution build:

```powershell
dotnet build "WorldSim.sln" --no-restore -m:1 -p:UseSharedCompilation=false
```

Required result: zero candidate-attributable warnings and errors.

Git gates:

- path-limited `git diff --check` has no whitespace errors;
- exact candidate blobs and numstat match the frozen identity;
- the isolated index is empty;
- only the two authorized candidate paths differ from the base.

LF-to-CRLF advisories are non-failing. Actual whitespace errors fail the gate.

## Acceptance And Evidence

- Fresh actor direct recency is false: test-backed by the new regression.
- Fresh actor higher-level follow-through is false: test-backed by the new regression.
- Genuine recent pursuit, raid conversion, combat grouping, and topology behavior are
  preserved: test-backed by the six unchanged focused tests.
- Sentinel wraparound is impossible: inspection-backed by guard-before-subtraction.
- Future timestamps are rejected: inspection-backed only.
- `AttackStructure` follow-through is unchanged: inspection-backed only.
- Public/module contracts are unchanged: exact diff and contract inventory.
- Package attribution is exact: base/tree/blob/numstat/path/index manifest.
- Compilation is clean: focused test plus Runtime and solution build outputs.
- No lifecycle authority changes: implementation evidence retains the canonical ON
  `4/5` and OFF `1/5` RED results and states that expanded/full matrices, E11-H,
  E11-I, and E11-J remain blocked.

## Risks And Stops

- Unrelated dirty work can contaminate the package: use the isolated root and exact
  path/blob gates.
- Concurrent diagnostics can touch `Person.cs`: serialize writes or use separate roots.
- A third candidate path, changed blob, or changed hunk is a hard stop.
- A need to alter combat policy, lifecycle behavior, public contracts, dependencies,
  ownership, or sequencing routes to Meta/Orchestrator for a new plan.
- No implementation subagent is planned. Any bounded discovery helper may read only
  the two candidate files and cannot review, approve, or mutate them.

## Handoff And Closeout

The implementation output must contain the isolated root, exact identity, checks,
scope audit, and explicit non-claims. Independent acceptance belongs to Meta
`/step-review` over the frozen candidate. Only a later allowed `/step-review-utan`
response may route the exact package to `/closeout-commit`.

No second plan review is permitted.
