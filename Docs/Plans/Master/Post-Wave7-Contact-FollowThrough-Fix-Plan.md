# Post-Wave7 Contact Follow-Through Fix Plan

Status: implemented and validated with runtime tests plus SMR reruns
Owner: Meta Coordinator
Last updated: 2026-04-12

## 1. Goal

This slice targets the residual post-Wave7 combat problem after the broad threat/arbitration fix:

- contact now happens,
- but in the bad lane the follow-through from contact -> battle -> damage/death is still weaker than desired,
- especially in `standard-default / htn / seed 101`.

This is **not** another broad threat rewrite, planner rewrite, or Java-side change.

## 2. Working Diagnosis

The remaining issue is concentrated in runtime contact persistence:

1. `ExecuteFightAction()` falls back too quickly when no hostile remains in the immediate radius.
2. `ExecuteRaidBorderAction()` does not convert strongly enough from border pressure to actor-vs-actor engagement.
3. Combat group pairing in `World` still uses a narrow local window for larger-topology follow-through.

## 3. Scope

### 3.1 In scope

- short-lived recent-hostile pursuit memory in `Person`
- fight-action stickiness before contested/home fallback
- raid-to-fight conversion when hostile actors are nearby
- combat-group eligibility/pairing that respects recent combat intent
- focused runtime tests for the above

### 3.2 Out of scope

- another threat-layer rewrite
- planner-specific HTN hacks
- Java/director changes
- campaign-march feature work
- broad lethality/routing retuning in phase 1

## 4. File Targets

- `WorldSim.Runtime/Simulation/Person.cs`
- `WorldSim.Runtime/Simulation/World.cs`
- `WorldSim.Runtime.Tests/ContactFollowThroughTests.cs`
- `WorldSim.Runtime.Tests/Wave5FormationCombatTests.cs` only if a gap remains

## 5. Planned Runtime Changes

### 5.1 Fight stickiness

- remember recent hostile actor/position for a short window
- pursue immediate hostile first
- then pursue recent hostile memory
- only after that fall back to contested/frontier or home/origin behavior

### 5.2 Raid -> fight conversion

- when a raid actor detects a nearby hostile actor, prefer converting to `Fight`
- avoid treating raid as effectively structure-only once contact is available

### 5.3 Combat-group follow-through

- treat recent combat intent as combat-group eligibility
- allow slightly wider pairing distance for groups that still have active combat follow-through intent

## 6. Test Plan

Required focused runtime coverage:

1. fight action pursues a recent hostile before home fallback
2. raid converts to fight when hostile actor is nearby
3. combat groups can pair across the extended follow-through distance window

## 7. Verification Plan

1. `dotnet test WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj`
2. `dotnet build WorldSim.sln`
3. HTN-only standard micro SMR rerun
4. full contact-realization rerun if the micro lane is healthy

## 8. Acceptance Criteria

The slice is accepted when all of the following hold:

1. the runtime follow-through tests pass,
2. the solution build stays green,
3. the residual `standard-default / htn / 101` lane no longer shows the same weak contact follow-through signature,
4. the lane does not regress back to zero-contact,
5. the previous broad threat-fix gains remain intact.

## 9. Outcome

Implementation landed in the runtime layer with:

- recent-hostile pursue stickiness in `Fight`,
- raid-to-fight actor conversion when hostile actors are nearby,
- and wider combat-group pairing for active/recent combat intent.

Validation status:

- focused runtime follow-through tests green,
- `dotnet test WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj` green,
- `dotnet build WorldSim.sln` green,
- final reruns green:
  - `planner-compare-wave7-contact-realization-medium-005` -> `exitCode=0`
  - `planner-compare-wave7-contact-realization-standard-005` -> `exitCode=0`

Observed result:

- the former `standard-default / htn / 101` residual lane no longer stays deathless,
- contact and battle persistence improved,
- and the hard failure mode shifted from a reproducible blocker to a softer retreat-heavy quality issue on larger topology.
