# Wave 4 P2 Fixup — Track B Agent Prompt

**Session type:** Track B implementation agent  
**Prereqs:** P2-A ✅, P2-B ✅, P2-C ✅ (all marked done in AGENTS.md, but review found gaps — this session closes them)  
**Goal:** Fix three runtime-side gaps identified in the P2-A/B/C post-implementation review, then create the missing `Wave4AdvancedDefenseTests.cs` test file.

---

## Context

Read `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` and `Docs/Plans/Master/Combat-Defense-Campaign-Master-Plan.md` for background.  
Read `AGENTS.md` for the wave/turn-gate protocol.  
All fixes are in `WorldSim.Runtime` and `WorldSim.Runtime.Tests`. No other projects are touched.

---

## Fixes to implement (ordered by severity)

### Fix 1 — CRITICAL: Create `Wave4AdvancedDefenseTests.cs`

AGENTS.md claims P2-C ✅ with "uj runtime tesztek (Wave4AdvancedDefenseTests)" but this file does not exist. P2-C has no automated test coverage at all.

**File to create:** `WorldSim.Runtime.Tests/Wave4AdvancedDefenseTests.cs`

Required test cases (minimum):

1. `StoneWall_RequiresFortificationTech_BlockedWithout` — `World.TryAddStoneWall` returns false / does not add when `fortification` tech is not unlocked.
2. `StoneWall_AllowedAfterFortificationTech` — succeeds after unlocking `fortification`.
3. `ReinforcedWall_RequiresAdvancedFortification` — `TryAddReinforcedWall` blocked without `advanced_fortification`, allowed after.
4. `CatapultTower_RequiresSiegeCraft` — `TryAddCatapultTower` blocked without `siege_craft`, allowed after.
5. `Gate_BlocksHostileMovement_AllowsFriendlyMovement` — place a gate; hostile faction person movement is blocked; same-faction person movement is not blocked (use `World.IsMovementBlocked`).
6. `Tower_BecomesInactiveWhenUpkeepUnpaid` — drain the colony's resources to zero, call `DefenseManager.Tick`; tower's `IsActive` becomes false.
7. `Tower_ReactivatesWhenUpkeepPaid` — after deactivation above, replenish stock; next `DefenseManager.Tick` reactivates the tower.
8. `CatapultTower_AoE_DamagesNearbyHostiles` — place a catapult tower, place two hostile-faction persons adjacent to each other within splash radius; fire tick applies damage to both.

Write tests using the same harness pattern as `Wave4MilitaryTechTests.cs` and `Wave4ColonyEquipmentTests.cs`.  
Use `HeadlessSimulationHarness` or `WorldBuilder` helpers as appropriate for the existing test project setup.

---

### Fix 2 — Moderate: Apply `FortificationHpMultiplier` during wall construction

**Problem:** `Colony.FortificationHpMultiplier` is set to `1.2f` by the `advanced_fortification` tech effect, but wall construction ignores it. `World.TryAddStoneWall` and `World.TryAddReinforcedWall` use the concrete class's hardcoded `DefaultHp`.

**Files to change:**
- `WorldSim.Runtime/Simulation/World.cs` — `TryAddStoneWall`, `TryAddReinforcedWall`
- Possibly `TryAddGate`, `TryAddArrowTower` if those also have HP affected by fortification

**Change:** When constructing a wall/structure belonging to a colony, multiply the initial `Hp` by `colony.FortificationHpMultiplier`.

Pattern (pseudocode):
```csharp
var hp = StoneWallSegment.DefaultHp * colony.FortificationHpMultiplier;
var segment = new StoneWallSegment(colony) { Hp = hp, MaxHp = hp };
```

Both `Hp` and `MaxHp` must be set to the scaled value so HP bars display correctly.

**Test to add** (inside `Wave4AdvancedDefenseTests.cs` or as a new test):  
`StoneWall_HpScaled_WhenAdvancedFortificationUnlocked` — verify that after unlocking `advanced_fortification`, a newly-built stone wall's `MaxHp` equals `StoneWallSegment.DefaultHp * 1.2f`.

---

### Fix 3 — Minor: Wire `ScoutRadiusBonus` into threat sensing

**Problem:** `Colony.ScoutRadiusBonus` is set to 2 by the `scouts` tech, but `BuildThreatContext` in `Person.cs` hardcodes `radius: 4` for both `CountNearbyHostilePeople` and `CountNearbyPredators`.

**File to change:** `WorldSim.Runtime/Simulation/Person.cs` — `BuildThreatContext` method (around line 1599)

**Change:** Replace `radius: 4` with `radius: 4 + _home.ScoutRadiusBonus` in both calls.

**No new test required** for this fix (minor/observability improvement); existing headless smoke should still pass.

---

### Fix 4 — Minor: Wire `CombatMoraleBonus` into colony morale

**Problem:** `Colony.CombatMoraleBonus` is set to `max(8f)` by the `war_drums` tech but is never consumed in `Colony.Update()`.

**File to change:** `WorldSim.Runtime/Simulation/Colony.cs` — `Update` method (find where morale target is computed)

**Change:** Add `CombatMoraleBonus` to the morale target when war/hostile stance is active, e.g.:
```csharp
if (warState == ColonyWarState.War || warState == ColonyWarState.Tense)
    moraleTarget += CombatMoraleBonus;
```

Clamp accordingly so morale cannot exceed its existing cap.

**No new test required** (minor improvement); integration visible in headless runs.

---

## Non-goals for this session

- Do NOT change `GateStructure.IsOpen` — it is vestigial dead code, not a bug.
- Do NOT change upkeep payment ordering — non-deterministic ordering is a design smell, not a correctness bug.
- Do NOT touch `FormationsUnlocked` — it is an intentional stub for Phase 3.
- Do NOT touch `WorldSim.AI`, `WorldSim.Graphics`, or Java — those are Track C/A/D respectively.

---

## Acceptance criteria

1. `Wave4AdvancedDefenseTests.cs` exists with all 8 required tests (plus the HP scaling test = 9 total).
2. `World.TryAddStoneWall` / `TryAddReinforcedWall` scale initial HP by `colony.FortificationHpMultiplier`.
3. `BuildThreatContext` uses `4 + _home.ScoutRadiusBonus` as the sensing radius.
4. `Colony.Update()` adds `CombatMoraleBonus` to morale target when stance is War or Tense.
5. Full solution builds with zero warnings (treat new warnings as errors if the project already does so).
6. All existing tests pass (zero regressions).
7. New tests all pass.

---

## Turn-gate protocol reminder

- Check prereq status in `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` before starting.
- On completion: mark this session's fixes as part of P2-C closeout in AGENTS.md cross-track notes.
- P2-C status remains ✅ (the fixes are retroactive); update the cross-track notes entry.
- When done, signal the Meta Coordinator that P2-D (Track C) and P2-E (Track A) are unblocked.
