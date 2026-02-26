# WorldSim Combat + Fortification + Campaign Master Plan

Status: Draft (future feature; not scheduled)
Owner: TBD (track leads A/B/C/D)
Scope: Track A (Graphics/UI), Track B (Runtime), Track C (AI), Track D (Refinery boundary)
Timebox: Not scheduled. Reference plan: 13 sprints x 2 weeks (+1 optional Sprint 7) covering Phases 0-7.
Last updated: 2026-02-23

This document is a detailed, implementation-oriented master plan for introducing:

- Combat (predators + faction warfare)
- Fortifications with a tower-defense vibe (autonomous towers, tile-based walls, gates)
- Siege (breach/structure damage; later dedicated siege units)
- Supply/logistics (personal inventory + storage interaction)
- Campaigns (hadjaratok) including cross-map expeditions with supply lines

The plan is written to be track-parallelizable while respecting the boundary rules from `AGENTS.md`.

## 1. Goal

- Add conflict as an emergent pressure system that interacts with economy/ecology, not a separate game mode.
- Keep the sim fully AI-driven (no player control), but highly observable via snapshot/overlays/event feed.
- Introduce tower-defense feel early: walls have HP, towers auto-target and auto-fire, sieges are readable.
- Scale from local skirmishes to cross-map campaigns once supply/logistics exists.
- Keep module boundaries clean: Runtime owns rules, AI owns decisions, Graphics renders snapshot only, Refinery stays optional and gated.

## 2. Current State Baseline (as of 2026-02-23)

This section anchors the plan in the code that exists today, so future work can be scoped and verified.

### 2.1 Simulation tick and dev hotkeys

`WorldSim.App/Game1.cs`:

- Simulation tick duration is fixed-step: `SimulationTickDuration = 0.25f`.
- Current dev hotkeys:
  - F1: toggle Tech menu
  - F3: toggle Render stats
  - F6: trigger Refinery (fixture/live depending on integration mode)
  - F7: (DEBUG only) cycle planner mode
  - F8: toggle AI debug panel
  - F9/F10: cycle render theme presets
  - F11: toggle fullscreen
  - T: toggle telemetry HUD

The plan proposes new overlays/panels; keybinds should avoid the above.

### 2.2 Person combat-related state exists but is not functional

`WorldSim.Runtime/Simulation/Person.cs` contains `Strength` and `ApplyDamage`, but there is no combat system.

Strength assignment exists but is not used mechanically:

```csharp
// Person.cs (private constructor)
Strength = _rng.Next(3, 11);       // range [3, 10] inclusive
Intelligence = _rng.Next(3, 11);   // range [3, 10] inclusive
```

Newborn bonus exists:

```csharp
// Person.cs (SpawnWithBonus)
person.Strength = Math.Min(20, person.Strength + world.StrengthBonus);
person.Intelligence = Math.Min(20, person.Intelligence + world.IntelligenceBonus);
person.Health += world.HealthBonus;
```

Damage application exists but only marks Predator/Other death reasons:

```csharp
public void ApplyDamage(float amount, string source)
{
    if (amount <= 0f || Health <= 0f)
        return;

    Health -= amount;
    if (Health <= 0f)
    {
        LastDeathReason = source.Contains("Predator", StringComparison.OrdinalIgnoreCase)
            ? PersonDeathReason.Predator
            : PersonDeathReason.Other;
    }
}
```

Current death reasons:

```csharp
public enum PersonDeathReason { None, OldAge, Starvation, Predator, Other }
```

Current professions (no Warrior):

```csharp
public enum Profession { Generalist, Lumberjack, Miner, Farmer, Hunter, Builder }
```

Runtime jobs exist and are mirrored by AI commands:

```csharp
public enum Job
{
    Idle,
    GatherWood,
    GatherStone,
    GatherIron,
    GatherGold,
    GatherFood,
    EatFood,
    Rest,
    BuildHouse,
    CraftTools
}
```

The current `Person.Update(...)` priority cascade is heavily hardcoded, and the AI brain is a late fallback.

Key takeaways from the existing cascade (high level):

- Hunger and starvation are handled first (including emergency eating).
- Children (Age < 16) short-circuit most adult behavior.
- If a job is in progress, it completes before any new decision.
- If idle, the person follows a long sequence: rest -> eat -> seek food -> profession-directed actions -> gather on tile -> move to nearby resource -> AI brain fallback -> loiter/wander.

This matters because combat response must be inserted into the cascade at a high priority (below starvation rescue, above economy).

### 2.3 Predator->human attack exists but is disabled (one-way)

`WorldSim.Runtime/Simulation/World.cs`:

```csharp
// Disabled by default until bidirectional combat/retaliation exists.
public bool EnablePredatorHumanAttacks { get; set; } = false;
public float PredatorHumanDamage { get; set; } = 10f;
```

`WorldSim.Runtime/Simulation/Animal.cs` predator harassment:

```csharp
private bool TryHarassNearbyPerson(World w)
{
    Person? nearest = null;
    int best = int.MaxValue;
    foreach (var person in w._people)
    {
        if (person.Health <= 0f)
            continue;

        int d = Manhattan(Pos, person.Pos);
        if (d < best)
        {
            best = d;
            nearest = person;
        }
    }

    if (nearest == null || best > 2)
        return false;

    StepTowards(w, nearest.Pos, Speed);
    if (Pos == nearest.Pos)
    {
        nearest.ApplyDamage(w.PredatorHumanDamage, "Predator");
        w.ReportPredatorHumanHit();
        return true;
    }

    return false;
}
```

Predator update only calls it when enabled:

```csharp
if (w.EnablePredatorHumanAttacks && TryHarassNearbyPerson(w))
{
    _energy = Math.Clamp(_energy + 4f, 0f, 120f);
}
```

There is currently no human retaliation, no predator kill attribution, and no NPC-vs-NPC combat.

### 2.4 World counters exist; there are no combat counters

`WorldSim.Runtime/Simulation/World.cs` counters:

```csharp
public int TotalAnimalStuckRecoveries { get; private set; }
public int TotalPredatorDeaths { get; private set; }
public int TotalPredatorHumanHits { get; private set; }
public int TotalDeathsOldAge { get; private set; }
public int TotalDeathsStarvation { get; private set; }
public int TotalDeathsPredator { get; private set; }
public int TotalDeathsOther { get; private set; }
public int RecentDeathsStarvation60s => _recentStarvationDeaths.Count;
public int TotalStarvationDeathsWithFood { get; private set; }
```

No counters for combat deaths, raids, sieges, wall/tower destructions, campaign outcomes, etc.

### 2.5 Tile model has no ownership, occupancy, or wall concept

`WorldSim.Runtime/Simulation/Tile.cs`:

- Tile has `Ground` and an optional `ResourceNode`.
- No tile ownership, no tile occupancy beyond houses/specialized buildings tracked elsewhere.

```csharp
public class Tile
{
    public Ground Ground { get; }
    public ResourceNode? Node { get; private set; }
    ...
}
```

Walls will require an explicit obstacle/occupancy model (either tile overlay, or a separate grid) plus pathfinding.

### 2.6 Colonies/factions exist but there is no diplomacy

`WorldSim.Runtime/Simulation/Colony.cs` has 4 factions:

```csharp
public enum Faction
{
    Sylvars,
    Obsidari,
    Aetheri,
    Chirita
}
```

Faction assignment is ID-based:

```csharp
(Faction, Name) = id switch
{
    0 => (Faction.Sylvars, "Sylvars"),
    1 => (Faction.Obsidari, "Obsidari"),
    2 => (Faction.Aetheri, "Aetheri"),
    3 => (Faction.Chirita, "Chirita"),
    _ => (Faction.Sylvars, $"Colony{id}")
};
```

Economic bonuses only:

```csharp
case Faction.Sylvars:
    FoodGatherMultiplier = 1.2f;
    WoodGatherMultiplier = 1.05f;
    break;
case Faction.Obsidari:
    StoneGatherMultiplier = 1.2f;
    IronGatherMultiplier = 1.1f;
    WoodGatherMultiplier = 0.95f;
    break;
```

Morale baseline is faction-dependent (but still economy-driven):

```csharp
float factionBaseline = Faction switch
{
    Faction.Sylvars => 58f,
    Faction.Obsidari => 54f,
    _ => 52f
};
```

There is no stance/relationship model, no war declarations, and no territory.

### 2.7 Buildings: specialized buildings exist; no defenses exist

`WorldSim.Runtime/Simulation/SpecializedBuilding.cs`:

```csharp
public enum SpecializedBuildingKind
{
    FarmPlot,
    Workshop,
    Storehouse
}
```

Specialized buildings are auto-constructed by `World.TryAutoConstructSpecializedBuildings()` on a timer.
Defensive buildings (walls/towers) do not exist and should not be forced into `SpecializedBuildingKind` because:

- Defenses have HP and active behavior (targeting, firing).
- They affect navigation/occupancy.

### 2.8 AI is economy-only; perception has Danger types but no sensors

AI contracts today:

`WorldSim.AI/Abstractions.cs`:

```csharp
public enum NpcCommand
{
    Idle,
    GatherWood,
    GatherStone,
    GatherIron,
    GatherGold,
    GatherFood,
    EatFood,
    Rest,
    BuildHouse,
    CraftTools
}
```

Current AI context has no health/threat/combat fields:

```csharp
public readonly record struct NpcAiContext(
    float SimulationTimeSeconds,
    float Hunger,
    float Stamina,
    int HomeWood,
    int HomeStone,
    int HomeIron,
    int HomeGold,
    int HomeFood,
    int HomeHouseCount,
    int HouseWoodCost,
    int ColonyPopulation,
    int HouseCapacity,
    bool StoneBuildingsEnabled,
    bool CanBuildWithStone,
    int HouseStoneCost);
```

Current goals in `WorldSim.AI/GoalLibrary.cs` (economy only):

- GatherWood
- GatherStone
- SecureFood
- RecoverStamina
- BuildHouse
- ExpandHousing
- StabilizeResources

Current GOAP actions in `WorldSim.AI/GoapPlanner.cs` are 6 economy actions:

- GatherWood
- GatherStone
- GatherFood
- EatFood
- Rest
- BuildHouse

Perception types exist but only `ResourceHere` is actually emitted:

`WorldSim.Runtime/Simulation/Perception/EventTypes.cs`:

```csharp
public static class EventTypes
{
    public const string ResourceHere = "ResourceHere";
    public const string LowResource = "LowResource";
    public const string HousingNeed = "HousingNeed";
    public const string Danger = "Danger";
    public const string Encounter = "Encounter";
}
```

Sensor base type exists:

```csharp
public abstract class Sensor
{
    public abstract void Sense(World world, Person person, Blackboard blackboard);
}
```

Only `EnvironmentSensor` is installed by default and emits `ResourceHere`.

### 2.9 Snapshot/render model is minimal for actors

`WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`:

- People only expose position and colony id:

```csharp
public sealed record PersonRenderData(int X, int Y, int ColonyId);
```

- There is no health/combat/profession info in the snapshot.
- Eco HUD already has some counters and a predator-human toggle:
  - `PredatorHumanAttacksEnabled`
  - `PredatorHumanHits`
  - Death counters

This plan requires expanding the read model in an incremental, phase-gated way.

### 2.10 Refinery boundary exists (Track D), but only addTech is wired through

- `WorldSim.Contracts/v1` contains patch ops like `addTech`.
- `WorldSim.RefineryAdapter/Translation/PatchCommandTranslation.cs` currently supports `addTech -> UnlockTech` only.
- F6 triggers refinery request; output is applied through runtime commands.

War/diplomacy/campaign ops would be a v2 contract and must remain optional and gated.

## 3. Design Principles

- Track boundaries are hard rules (see `AGENTS.md`): Runtime owns rules, Graphics reads snapshot only, AI emits decisions only, Refinery is an optional boundary.
- Combat is a pressure that competes with economic needs. Starvation avoidance remains top priority.
- Systems are layered by phase: each phase ships a coherent, testable slice.
- Determinism and observability matter more than early balance: add counters, event feed messages, and overlays first.
- "Tower defense vibe" comes from autonomous defenses + readable sieges, not player control.

## 4. Target Architecture

### 4.1 Responsibility split (Track A/B/C/D)

| Track | Owns | Must not do |
| --- | --- | --- |
| Track B (Runtime) | Combat rules, damage model, defensive structures, diplomacy/war state, territory, supply model, campaigns, snapshot builder | Rendering, UI state, direct Java contract branching |
| Track C (AI) | Sensors (decision inputs), goals/considerations, planning (GOAP/HTN), strategic evaluators (war/campaign) | Directly mutate world state, render anything |
| Track A (Graphics/UI) | Rendering passes, overlays, panels, event feed UI, tech menu display for new branches | Read/modify mutable simulation state |
| Track D (Refinery) | Optional story beats + planner nudges for diplomacy/campaign, formal validation gate | Mandatory runtime dependency, per-frame decision making |

### 4.2 Proposed folder layout (incremental)

Runtime additions (under `WorldSim.Runtime/Simulation/`):

```text
Combat/
  CombatResult.cs
  CombatConstants.cs
  CombatResolver.cs
  Formation.cs
  SiegeState.cs
Diplomacy/
  Stance.cs
  FactionRelations.cs
  RelationManager.cs
  WarManager.cs
Defense/
  DefenseManager.cs
  DefensiveStructure.cs
  WallSegment.cs
  Gate.cs
  DefensiveTower.cs
  TowerTargeting.cs
  DefenseBlueprints.cs
Military/
  Army.cs
  Campaign.cs
  CampaignManager.cs
  MarchSystem.cs
  SupplyModel.cs
  SupplyLine.cs
  ForwardBase.cs
Navigation/
  NavigationGrid.cs
  Pathfinding.cs
Inventory/
  ItemType.cs
  InventorySlot.cs
  PersonInventory.cs
```

AI additions (under `WorldSim.AI/`):

```text
Sensors/
  ThreatSensing.cs
Strategy/
  WarStrategist.cs
  CampaignPlanner.cs
  SiegeTactics.cs
Combat/
  CombatConsiderations.cs
  CombatGoals.cs
```

Graphics additions (under `WorldSim.Graphics/`):

```text
Rendering/Passes/
  TerritoryOverlayPass.cs
  DiplomacyOverlayPass.cs
  ProjectileRenderPass.cs
  SiegeOverlayPass.cs
UI/
  Panels/
    DiplomacyPanelRenderer.cs
    CampaignPanelRenderer.cs
```

Notes:

- The exact folder structure can be adjusted; the plan assumes new concepts get dedicated folders to avoid bloating existing files.
- Each new folder should come with small, focused types rather than a single mega-file.

### 4.3 New core types (proposed)

Combat result value object:

```csharp
namespace WorldSim.Runtime.Simulation.Combat;

public sealed record CombatResult(
    bool AttackerWon,
    float DamageDealt,
    float DamageReceived,
    string Outcome);
```

Defensive structures should be runtime-owned and actively updated:

```csharp
namespace WorldSim.Runtime.Simulation.Defense;

public interface IDefensiveStructure
{
    int Id { get; }
    int ColonyId { get; }
    (int x, int y) Pos { get; }
    float Health { get; }
    float MaxHealth { get; }
    bool IsDestroyed { get; }

    void Update(World world, int tick, float dt);
    void ApplyDamage(float amount, string source);
}
```

Diplomacy stance model:

```csharp
namespace WorldSim.Runtime.Simulation.Diplomacy;

public enum Stance
{
    Friendly,
    Neutral,
    Tense,
    Hostile,
    War
}
```

Campaign state machine (runtime-owned, AI-triggered):

```csharp
namespace WorldSim.Runtime.Simulation.Military;

public enum CampaignState
{
    Planning,
    Assembling,
    Marching,
    Engaging,
    Sieging,
    Resolving,
    Returning,
    Done,
    Failed
}
```

These are reference targets; exact names can vary, but the responsibilities must stay the same.

### 4.4 Feature flags / toggles (recommended)

To keep the sim debuggable during rollout, implement phase-gated toggles.
Recommended runtime-level toggles (defaults OFF except where noted):

- `World.EnablePredatorHumanAttacks` (exists, default OFF)
- `World.EnableCombatPrimitives`
- `World.EnableDiplomacy`
- `World.EnableFortifications`
- `World.EnableSiege`
- `World.EnableSupply`
- `World.EnableCampaigns`

Graphics overlay toggles should be separate UI state.
Suggested new hotkeys (avoid existing bindings):

- F2: toggle Diplomacy panel
- F5: toggle Territory overlay
- F12: toggle Siege overlay (or cycle overlays)

Keybinds are not a shipped UX requirement; they are primarily for dev/test observability.

### 4.5 Resolved design decision: Roles vs Professions (Warrior representation)

This plan resolves the "Warrior" representation up front so Phase 0/1 work is not blocked.

- `Profession` remains an economy specialization (existing: Generalist/Lumberjack/Miner/Farmer/Hunter/Builder).
- Military participation is modeled as an additive role layer, not as a new `Profession` value.
- Reason: the current `Person.Update(...)` and profession-weighted initial assignment are tightly coupled to economy.
  A separate role avoids rewriting/retuning the entire profession system and lets roles evolve (Warrior, SupplyCarrier,
  Scout, Commander) without disturbing the economy distribution.

Proposed runtime-side role representation (reference target):

```csharp
[Flags]
public enum PersonRole
{
    None = 0,
    Warrior = 1 << 0,
    SupplyCarrier = 1 << 1,
    Scout = 1 << 2,
    Commander = 1 << 3
}
```

Rules:

- Role assignment is Runtime-owned (Track B), triggered by diplomacy/war pressure and colony directives.
- AI (Track C) uses role flags as inputs for decisions (e.g., Warrior patrols; civilians flee).
- Roles are reversible (demobilize after peace cooldown).

## 5. Formulas, Parameters, and State Machines

This section defines "knobs" and the initial parameter values so the system can be implemented consistently.

### 5.1 Damage model (Phase 0)

Base requirements:

- `Person.Health` is 100 by default.
- `Strength` exists today and is in [3, 10] for spawned adults, with possible world tech bonus up to 20.

Proposed damage formula (human attacker):

```text
rawDamage = baseDamage * (1.0 + strength / 20.0) * randomFactor
randomFactor in [0.85, 1.15]

effectiveDamage = rawDamage * (1.0 - defensePct)
defensePct = clamp(defense / 100.0, 0.0, 0.75)

minDamage = 1.0
effectiveDamage = max(minDamage, effectiveDamage)
```

Defaults (tuning knobs):

- `baseDamageHuman = 8`
- `baseDamagePredator = World.PredatorHumanDamage` (existing default 10)
- `combatCooldownSeconds = 0.75` for persons, `0.9` for predators

### 5.2 Defensive structures: initial parameters

Tile-based walls (occupy a tile):

| Structure | HP | Build cost | Build time | Notes |
| --- | ---: | --- | ---: | --- |
| WoodWall | 100 | 4 wood | 1.0s | Phase 1; blocks movement |
| StoneWall | 300 | 3 stone + 1 wood | 1.5s | Phase 2; requires `fortification` |
| ReinforcedWall | 500 | 5 stone + 2 iron | 2.0s | Phase 2; requires `advanced_fortification` |
| Gate | 150 | 6 wood + 2 stone | 2.0s | Phase 2; friendly passable |

Autonomous towers:

| Structure | HP | Range | Damage | Cooldown | Build cost | Upkeep | Notes |
| --- | ---: | ---: | ---: | ---: | --- | --- | --- |
| Watchtower | 120 | 3 | 5 | 1.0s (4 ticks) | 18 wood + 4 stone | 1 wood / 30s | Phase 1 |
| ArrowTower | 180 | 5 | 12 | 0.5s (2 ticks) | 24 wood + 8 stone + 2 iron | 1 wood / 20s | Phase 2 |
| CatapultTower | 220 | 8 | 25 (AoE) | 2.0s (8 ticks) | 18 wood + 18 stone + 6 iron | 1 stone / 30s | Phase 2 |

Notes:

- Range is Manhattan distance (consistent with current movement distance usage).
- Catapult AoE applies damage to all hostile combatants on the target tile.
- Upkeep is colony-paid; if unpaid, structure becomes inactive (still blocks if wall).

### 5.3 Territory influence model (Phase 1)

Goal: derive tile ownership as a simple, periodic computation.

Definitions:

- Each colony has an origin `(x,y)`.
- Each watchtower contributes influence.
- Warriors near a tile contribute presence.

Proposed influence function for a tile `t` from a colony `c`:

```text
influence(c, t) = baseColonyInfluence / (1 + dist(origin(c), t))
               + sum_over_towers (towerInfluence / (1 + dist(towerPos, t)))
               + presenceInfluence * nearbyWarriorsCount(c, t, presenceRadius)
```

Suggested defaults:

- `baseColonyInfluence = 12`
- `towerInfluence = 8`
- `presenceInfluence = 2`
- `presenceRadius = 3`
- Recompute period: every 10 ticks (2.5s) or every 20 ticks (5s) depending on perf.

Ownership rules:

- OwnerFaction(t) is the faction with the max influence.
- Contested(t) if `maxInfluence - secondInfluence <= contestedThreshold`.
- Suggested `contestedThreshold = 2.0`.

### 5.4 Diplomacy state machine (Phase 1)

Stance progression is a state machine with timers and triggers.

```text
Friendly <-> Neutral <-> Tense <-> Hostile <-> War

Triggers (examples):
- Contested border sustained -> Tense
- Raid success / repeated skirmishes -> Hostile
- Hostile sustained + war score delta -> War
- War cooldown + war score stabilization -> Hostile
- Peace time + no contested tiles -> Tense/Neutral
```

Hard rules (recommended):

- Stance matrix is symmetric (A->B equals B->A) for Phase 1; asymmetric stances can be a later extension.
- Enforce cooldowns to prevent rapid stance flapping.

### 5.5 Siege state machine (Phase 3)

```text
Approaching -> Breaching -> Breached -> (Victory | Repelled) -> Recovery

Approaching: attacker enters defender influence radius.
Breaching: attacker targets walls/gates/towers.
Breached: wall tile is destroyed OR gate is destroyed/open.
Victory: defenders routed OR defender morale collapses OR loot extracted.
Repelled: attacker morale collapses OR supply runs out.
Recovery: forced peace cooldown adjustments + structure repair window.
```

### 5.6 Campaign state machine (Phase 6)

```text
Planning -> Assembling -> Marching -> (Engaging)* -> (Sieging)* -> Resolving -> Returning -> Done

*Engaging and Sieging can repeat multiple times depending on encounters and target defenses.
Failed path: any state -> Failed (e.g., total rout, commander death, no supply).
```

## 6. Technology Additions (proposed)

Tech additions must follow the existing `Tech/technologies.json` schema (id, name, effect, prerequisites, cost).
Effects must be implemented in `WorldSim.Runtime/Simulation/TechTree.cs`.

### 6.1 Military combat techs

Proposed entries (IDs only; details are required in the JSON):

- `weaponry` -> effect: `damage_bonus`
- `armor_smithing` -> effect: `defense_bonus`
- `military_training` -> effect: `unlock_warrior_role`
- `war_drums` -> effect: `combat_morale_bonus`
- `scouts` -> effect: `scout_radius`
- `advanced_tactics` -> effect: `unlock_formations`

Suggested prereq anchors:

- `weaponry` requires `mining` (iron access)
- `armor_smithing` requires `weaponry`
- `military_training` requires `construction`
- `advanced_tactics` requires `education` (command/intelligence tie-in)

### 6.2 Fortification and siege techs

- `fortification` -> effect: `unlock_fortifications`
- `advanced_fortification` -> effect: `fortification_hp_bonus`
- `siege_craft` -> effect: `siege_damage`

Suggested prereqs:

- `fortification` requires `construction` + `mining`
- `advanced_fortification` requires `fortification` + `masonry`
- `siege_craft` requires `fortification` + `tools`

### 6.3 Supply/logistics techs

Supply is planned as personal inventory + storage.
Proposed supply techs (in addition to existing `logistics`):

- `backpacks` -> effect: `carry_slots_bonus`
- `rationing` -> effect: `supply_efficiency`

Suggested prereqs:

- `backpacks` requires `logistics`
- `rationing` requires `medicine` + `logistics`

Implementation note:

- Prefer checking unlocked tech IDs for unlock-gates (e.g., `fortification` allows building stone walls)
  and reserve effect strings for numeric modifiers (damage/defense/supply efficiency).

## 7. AI Additions (Track C)

Combat/campaign AI must integrate with the existing utility + planner architecture and the runtime fallback cascade.

### 7.1 New commands and runtime job mapping

AI `NpcCommand` and runtime `Job` must remain aligned.

Proposed additions (incremental by phase):

- Phase 0: `Fight`, `Flee`
- Phase 1: `PatrolBorder`, `GuardColony`, `BuildWall`, `BuildTower`, `Raid`, `AttackStructure`
- Phase 5+: `RefillInventory`, `DeliverSupply`, `Forage`

Runtime requirements:

- Add new `Job` values (runtime) for each command.
- Update `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs` mapping from decision -> job.

### 7.2 Expand NpcAiContext for combat and war

Add fields (by phase) to `WorldSim.AI/Abstractions.cs`:

- Phase 0: `Health`, `Strength`, `Defense`, `NearbyPredators`, `NearbyHostilePeople`
- Phase 1: `WarState`, `TileContestedNearby`, `IsWarrior`, `ColonyWarriorCount`
- Phase 5: `InventoryFood`, `InventorySlotsUsed`, `NearestStorehouseDistance`
- Phase 6: `IsInArmy`, `ArmySupplyRatio`, `ArmyState`

Keep the context as a pure data record (no references to runtime types).

### 7.3 New sensors (inputs)

Two layers exist today:

- Runtime perception sensors that populate `Blackboard`/`Memory` (EventTypes).
- AI decision inputs built by `RuntimeNpcBrain` using runtime scanning.

Recommended approach:

- Use `RuntimeNpcBrain` to compute numeric threat fields for AI (stable, deterministic, no event parsing).
- Add runtime perception sensors for narrative/debugging (`EventTypes.Danger`, `EventTypes.Encounter`) but do not couple AI correctness to them.

### 7.4 New goals and considerations

Goals (utility-scored) should follow existing patterns in `GoalLibrary.cs`:

- `DefendSelf` (Phase 0)
- `FleeToSafety` (Phase 1)
- `PatrolBorder` / `GuardColony` (Phase 1)
- `RaidEnemy` (Phase 1)
- `UnlockMilitaryTech` (Phase 2)
- `SupplyArmy` / `RefillInventory` / `Forage` (Phase 5)
- `LaunchCampaign` / `AbortCampaign` (Phase 6)

New considerations (examples):

- ThreatNearbyConsideration: `min(1, nearbyThreats / threatCap)`
- HealthLowConsideration: `min(1, (100 - health) / 100)`
- CombatAdvantageConsideration: compare own squad strength vs enemy
- SupplyLowConsideration: `1 - armySupplyRatio`

### 7.5 GOAP/HTN integration notes

- GOAP action count must remain small; use HTN for multi-step siege/campaign sequences.
- Combat actions should be designed as "high-level" commands (FightTarget) rather than micro-steps.
- Formation and siege tactics can be HTN methods (Phase 3+).

## 8. Snapshot and UI Contract (Track A depends on this)

Snapshot changes must be staged to avoid breaking Graphics mid-phase.

### 8.1 Snapshot additions by phase

Phase 0:

- Expand `PersonRenderData` with `Health` and `IsInCombat` and `LastCombatTick`.

Proposed shape:

```csharp
public sealed record PersonRenderData(
    int X,
    int Y,
    int ColonyId,
    float Health,
    bool IsInCombat,
    int LastCombatTick);
```

Phase 1:

- Add `FactionView` and tile ownership to `TileRenderData` (or a parallel tile ownership list).
- Add defensive structure render data list.
- Add diplomacy stance matrix view.

Phase 3:

- Add optional combat morale fields for render.
- Add siege overlay state snapshots.

Phase 6:

- Add campaign/army snapshots.

### 8.2 New render snapshot types (proposed)

```csharp
public enum FactionView { Sylvars, Obsidari, Aetheri, Chirita }

public enum DefensiveStructureKindView
{
    WoodWall,
    StoneWall,
    ReinforcedWall,
    Gate,
    Watchtower,
    ArrowTower,
    CatapultTower
}

public sealed record DefensiveStructureRenderData(
    int X,
    int Y,
    int ColonyId,
    DefensiveStructureKindView Kind,
    float Health,
    float MaxHealth,
    int LastFireTick);

public sealed record DiplomacyRenderData(
    FactionView[] Factions,
    int[] StanceMatrix); // 4x4 flattened, values map to StanceView

public enum StanceView { Friendly, Neutral, Tense, Hostile, War }
```

Implementation note:

- Keep view enums inside `WorldSim.Runtime/ReadModel` as today.
- Graphics should not reference runtime domain enums directly.

## 9. Phase 0 - Combat Primitives (Sprint 1)

Phase goal: ship safe, bidirectional "micro-combat" with predators plus AI fight/flee response and UI feedback.

### Sprint 1 - Combat primitives (predators + self-defense)

#### Epic P0-A: Core damage model (Strength used + Defense added)

Tasks:

- In `WorldSim.Runtime/Simulation/Person.cs`, introduce `Defense` stat (default 0) and use Strength in a new combat damage formula.
- Add `PersonDeathReason.Combat` to `PersonDeathReason`.
- Create `WorldSim.Runtime/Simulation/Combat/CombatResult.cs`.
- Add new world/colony counters: combat deaths, predator kills by humans.
- Add unit tests in `WorldSim.Runtime.Tests` covering the damage formula (Strength scaling + Defense reduction + clamps).

Acceptance:

- Strength affects damage (higher Strength -> higher average damage).
- Defense reduces damage with a clamp (no invulnerability).
- Death reason `Combat` is set deterministically for NPC-vs-NPC or NPC-vs-predator fights.

#### Epic P0-B: Bidirectional predator combat (retaliation)

Tasks:

- Enable predator->human attacks safely by adding human retaliation logic.
- Add a small combat cooldown to avoid per-tick instant death loops.
- Add a simple targeting rule for retaliation (attack predator that just hit me).
- Ensure predator death is recorded and removed from world.

Acceptance:

- With `EnablePredatorHumanAttacks = true`, predators can hit humans, and humans can kill predators.
- Predator-human combat produces visible counters (predator hits, predator kills).
- No regression: economy loop continues when attacks are OFF.

#### Epic P0-C: AI threat response (Fight/Flee)

Tasks:

- Expand `WorldSim.AI/NpcAiContext` with: health, strength, defense, nearby predators.
- Add new commands `NpcCommand.Fight` and `NpcCommand.Flee` and mirror them in runtime `Job`.
- Add `DefendSelf` goal + threat/health considerations.
- Update `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs` to produce Fight/Flee jobs.
- Insert combat response into `Person.Update(...)` above economy actions but below starvation rescue.

Acceptance:

- When predators are nearby, some NPCs fight, weaker NPCs flee.
- Combat response preempts gathering/building, but starvation avoidance still wins.
- The AI debug panel shows the selected goal and resulting command.

#### Epic P0-D: Snapshot + UI feedback (health bar, combat markers)

Tasks:

- Expand `PersonRenderData` in `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs` with HP + combat flags.
- Update `WorldSim.Runtime/ReadModel/WorldSnapshotBuilder.cs` to populate the new fields.
- In `WorldSim.Graphics`, add:
  - health bar rendering for persons when HP < 100
  - combat indicator when `IsInCombat` is true
  - event feed strings for predator fights and combat deaths

Acceptance:

- You can see injured NPCs via HP bars.
- Combat is visible (indicator) and traceable (event feed).
- Snapshot changes do not break the renderer (no missing fields).

#### Epic P0-E: Test harness + balance smoke tests (headless)

Tasks:

- Add deterministic unit tests in `WorldSim.Runtime.Tests` for:
  - combat damage math (Strength/Defense/randomFactor clamped)
  - predator retaliation sequence (hit -> retaliation -> possible predator death)
  - snapshot builder populates new person combat fields
- Add a headless simulation smoke test (no Graphics) that:
  - runs 1000 ticks at default map settings with combat primitives ON
  - asserts invariants: no negative stocks, no exceptions, and population does not collapse to zero for all colonies
  - logs counters (combat deaths, predator kills) for debugging

Acceptance:

- Tests run in CI/local and are deterministic (seeded RNG where needed).
- Smoke test catches "combat collapses the economy" regressions early.

#### Sprint 1 DoD

- Predator->human attacks can be enabled without one-way slaughter (retaliation exists).
- AI can fight or flee from predators.
- HP/combat state is visible in UI.

## 10. Phase 1 - Diplomacy/Territory + Basic Fortifications (Sprints 2-3)

Phase goal: create faction-to-faction motivation (stances/war) and add basic TD defenses (wood walls + watchtowers) with minimum pathfinding.

### Sprint 2 - Diplomacy, war state, and territory ownership (no walls yet)

#### Epic P1-A: Faction stance matrix + persistence

Tasks:

- Add `Stance` enum + relation matrix storage in `WorldSim.Runtime/Simulation/Diplomacy/`.
- Define defaults: all Neutral.
- Expose stance data via snapshot for UI.

Acceptance:

- Runtime has a single source of truth for faction stances.
- UI can render the stance matrix from snapshot.

#### Epic P1-B: Relation dynamics triggers (tension -> hostility -> war)

Tasks:

- Implement a `RelationManager` that updates stances based on:
  - sustained contested tiles
  - repeated border skirmish events
  - raids/siege outcomes (later)
- Add cooldowns to prevent rapid flipping.

Acceptance:

- Under sustained border pressure, stances degrade over time.
- Peace cooldown works (war does not instantly re-trigger).

#### Epic P1-C: Territory influence + contested tiles

Tasks:

- Implement periodic tile ownership computation (influence function in Section 5.3).
- Add `OwnerFaction` and `IsContested` to snapshot.
- Add debug counters: total contested tiles per faction pair.

Acceptance:

- Tiles have deterministic owners given colony positions and structures.
- Contested zones appear where influence overlaps.

#### Epic P1-D: Enemy sensing in AI (hostile/war targets)

Tasks:

- Expand `NpcAiContext` with hostile counts nearby.
- Implement an enemy sensor in runtime context-building.
- Introduce the role system (Section 4.5) in Runtime and expose role flags to AI context:
  - minimal Phase 1 requirement: `IsWarrior` (or `PersonRole.Warrior`) and `IsCivilian`
  - wartime mobilization: assign a small fraction of adults as warriors under Hostile/War stances
- Add AI behaviors:
  - civilians flee to colony center when hostile is near
  - warrior-role NPCs prioritize guarding/patrolling

Acceptance:

- NPCs react differently to predators vs hostile faction NPCs.
- Civilians reduce random wandering when danger is present.
- Under Hostile/War, each colony can mobilize at least 1 warrior-role NPC (if population permits) without changing `Profession`.

#### Epic P1-E: Diplomacy panel + territory overlay (Track A)

Tasks:

- Add a Diplomacy panel rendering a 4x4 stance matrix.
- Add a territory overlay that colors tiles by `OwnerFaction` and marks contested tiles.
- Suggested keybinds:
  - F2: Diplomacy panel
  - F5: Territory overlay

Acceptance:

- Stance changes are observable without debugging logs.
- Contested tiles are visually obvious.

#### Sprint 2 DoD

- Factions have stances and they change based on territory pressure.
- Territory ownership is computed periodically and rendered.

### Sprint 3 - Basic fortifications + pathfinding v1

#### Epic P1-F: Defense domain scaffold (walls + watchtower)

Tasks:

- Add `WorldSim.Runtime/Simulation/Defense/` domain types.
- Implement WoodWall segments as tile occupants with HP.
- Implement Watchtower as an autonomous tower with:
  - range
  - cooldown
  - target selection (nearest hostile)
- Add a `DefenseManager` update loop invoked from world tick.

Acceptance:

- Walls block movement for hostiles.
- Watchtowers auto-fire on hostiles and apply damage.

#### Epic P1-G: Navigation/pathfinding v1 (BFS when blocked)

Tasks:

- Introduce a navigation grid abstraction that can answer `IsBlocked(x,y, moverColonyId)`.
- Implement BFS pathfinding when a direct StepTowards move is blocked by a wall/gate.
- Add a short-horizon path cache per NPC so BFS is not executed per tick:
  - Cache stores: `target`, `pathTiles`, `nextIndex`, and `topologyVersion`.
  - Cache is used for up to N steps (suggested N=12) then replan.
- Specify cache invalidation strategy (mandatory):
  - Add `NavigationGrid.TopologyVersion` that increments when walls/gates are built, destroyed, or gate-open state changes.
  - If cached `topologyVersion != current`, discard cache immediately.
  - If the next step is blocked at runtime, discard cache and replan.
- Add a performance guard:
  - BFS has a max expansion budget (e.g., 4096 nodes). If exceeded, fall back to a local "unstick" step
    (random walk away from obstacle for K steps) and retry later.
- Add unit tests in `WorldSim.Runtime.Tests` for:
  - topology version invalidation (build wall mid-path invalidates path)
  - blocked-step invalidation
  - BFS finds an alternate route around a wall ring when a gap exists

Acceptance:

- NPCs do not get permanently stuck behind walls.
- Performance remains acceptable on 128x128 and 256x144 maps.
- After a topology change (wall added/removed), path caches invalidate within 1 tick.

#### Epic P1-H: AI defense building + raid skeleton

Tasks:

- Add AI goals to build walls/towers when stance is Hostile/War.
- Add a basic raid behavior:
  - attackers move toward contested border
  - if blocked by wall, AttackStructure
- Add runtime-side rules for raid success: small resource loot + stance impact.

Acceptance:

- In War, at least some NPCs perform raids and target defenses.
- Raids are visible in event feed and affect stance/war score.

#### Epic P1-I: Graphics for walls/towers/projectiles

Tasks:

- Add `DefensiveStructureRenderData` to snapshot and render it.
- Render walls and towers distinctly.
- Add a simple projectile/beam visualization for tower shots.
- Add wall/tower HP bars when damaged.

Acceptance:

- You can see where walls are, which tower is firing, and what is being hit.

#### Sprint 3 DoD

- Basic fortifications exist (walls + watchtowers) and matter in combat.
- Pathfinding prevents wall-induced deadlocks.

## 11. Phase 2 - Military Tech + Advanced Defenses (Sprint 4)

Phase goal: add a military tech branch and upgrade the fortification set (stone walls, gates, stronger towers, upkeep).

### Sprint 4 - Military tech and defense upgrades

#### Epic P2-A: Add military + fortification techs to technologies.json

Tasks:

- Extend `Tech/technologies.json` with new tech entries (Section 6).
- Implement new effect strings in `WorldSim.Runtime/Simulation/TechTree.cs`.
- Add unlock gates for fortifications based on unlocked tech ids.

Acceptance:

- Tech menu shows new locked techs.
- Unlocking tech changes combat/defense behavior deterministically.

#### Epic P2-B: Colony equipment levels (weapon/armor)

Tasks:

- Add colony-level `WeaponLevel` and `ArmorLevel` (0..3).
- Apply equipment levels to warrior-role damage/defense.
- Add snapshot fields for UI (optional).

Acceptance:

- Two colonies with different weapon levels produce different combat outcomes.

#### Epic P2-C: Advanced defenses (stone walls, gates, arrow/catapult towers)

Tasks:

- Implement StoneWall/ReinforcedWall upgrades.
- Implement Gate behavior: friendly passable, hostile blocked unless destroyed.
- Implement ArrowTower and CatapultTower (AoE).
- Implement upkeep rules (inactive if unpaid).

Acceptance:

- Defenses upgrade only when tech is unlocked.
- Catapult AoE is visible and impacts group fights.

#### Epic P2-D: AI becomes tech-aware (avoid unwinnable fights)

Tasks:

- Add AI evaluation for enemy weapon/armor advantage.
- Update fight/flee decisions accordingly.
- Add goal to unlock military tech when threat is high.

Acceptance:

- AI reduces suicidal attacks into stronger defenses.
- Under pressure, colonies prioritize military tech unlocks.

#### Epic P2-E: Graphics and HUD updates

Tasks:

- Render new wall materials and tower types.
- Add a fortification/military branch section in the Tech menu.

Acceptance:

- Players (dev) can visually distinguish defense upgrades and tech progression.

#### Sprint 4 DoD

- Military tech exists and affects combat.
- Advanced defenses upgrade correctly and are rendered.

## 12. Phase 3 - Tactical/Formation Combat + Siege (Sprints 5-6)

Phase goal: evolve from 1v1 skirmishes into group combat with formations and readable siege progression.

### Sprint 5 - Group combat, formations, morale

#### Epic P3-A: Formation system + group combat resolver

Tasks:

- Add `Formation` enum (Line, Wedge, DefensiveCircle, Skirmish).
- Implement a tick-based group combat resolver that:
  - aggregates squad strength
  - applies formation modifiers
  - applies tech/equipment modifiers
  - produces per-tick damage and casualties

Acceptance:

- Group fights take multiple ticks and produce readable outcomes.
- Formation choice changes outcome.

#### Epic P3-B: Combat morale + routing

Tasks:

- Add per-person `CombatMorale` (0..100).
- Morale decreases on ally death, low supply, being outnumbered; increases on enemy death.
- If morale reaches 0: rout -> Flee job.

Acceptance:

- Routed squads disengage and retreat.
- Morale is visible (snapshot + UI) when enabled.

#### Epic P3-C: Commander bonus (Intelligence-based)

Tasks:

- Select a commander per squad (highest Intelligence).
- Commander provides morale stability + better formation selection.

Acceptance:

- High-Intelligence commanders improve win rate in otherwise equal fights.

#### Epic P3-D: Graphics for battles

Tasks:

- Add battle zone overlays.
- Render formation markers (simple).
- Render morale bars if implemented.

Acceptance:

- You can see where battles happen and whether a squad is routing.

#### Sprint 5 DoD

- Group combat exists and is not instant.
- Morale/routing creates non-lethal resolutions.

### Sprint 6 - Siege mechanics (breach, structure damage, tower interplay)

#### Epic P3-E: Siege state + breach logic

Tasks:

- Introduce SiegeState tracking per active attacker/defender interaction.
- Enforce targeting priorities: walls/gates/towers before interior.
- Add breach effects: once a wall tile is destroyed, path opens.

Acceptance:

- Breaches occur and change movement access.
- Siege progression is visible via snapshot.

#### Epic P3-F: Structure damage integration

Tasks:

- Extend combat resolver to apply damage to defensive structures.
- Add siege damage scaling from tech (`siege_craft`).
- Add structure destruction events and counters.

Acceptance:

- Walls/towers can be destroyed through sustained attack.
- Siege tech changes time-to-breach.

#### Epic P3-G: AI siege tactics (attack vs retreat vs sortie)

Tasks:

- Add siege target selection logic: prioritize towers if tower DPS is high.
- Add defender sortie behavior: break siege if attackers are weak.
- Add retreat logic based on morale + supply.

Acceptance:

- Attackers do not tunnel a wall while being annihilated by towers.
- Defenders sometimes counterattack rather than always turtling.

#### Epic P3-H: Siege UI/overlays

Tasks:

- Add a siege overlay with:
  - breach markers
  - wall HP visualization
  - tower firing lines
- Add event feed entries: siege starts, breach achieved, siege repelled.

Acceptance:

- A siege is understandable from the UI without reading logs.

#### Sprint 6 DoD

- Siege is readable and produces structure damage/breaches.
- Towers matter at siege scale.

## 13. Phase 4 - Refinery / Season Director integration (Optional Sprint 7)

Phase goal: allow an optional director to nudge diplomacy/campaign arcs while keeping runtime deterministic and safe.

### Sprint 7 (optional) - Contracts v2 and director hooks

#### Epic P4-A: Contracts v2 for diplomacy/campaign ops

Tasks:

- Add `WorldSim.Contracts/v2` ops such as:
  - DeclareWarOp
  - ProposeTreatyOp
  - AddStoryBeatOp (war/siege flavored)
  - SetColonyDirectiveOp (mobilize, fortify, peace_talks)
- Add strict validation policies and version negotiation.

Acceptance:

- Invalid ops are rejected deterministically.
- v1 remains supported.

#### Epic P4-B: Adapter translation to runtime commands

Tasks:

- Extend `WorldSim.RefineryAdapter/Translation/PatchCommandTranslation.cs` to map v2 ops to runtime commands.
- Ensure unknown op handling is deterministic.

Acceptance:

- v2 ops translate into runtime command calls without Java-specific branching in Runtime.

#### Epic P4-C: Runtime command endpoints

Tasks:

- Add runtime commands:
  - DeclareWar(factionA, factionB)
  - ProposeTreaty(...)
  - ApplyMilitaryEvent(...)
  - SetColonyDirective(...)

Acceptance:

- Runtime can run with refinery OFF and no behavioral change.
- Runtime can run with refinery ON and apply validated commands.

#### Epic P4-D: Java service beats (mock + gated)

Tasks:

- Add mock director beats to `refinery-service-java` planners.
- Add gating: LLM suggestions must pass formal validation; fallback to deterministic output.

Acceptance:

- Same checkpoint input produces reproducible validated outputs.

#### Sprint 7 DoD

- Track D can influence war/campaign arcs without destabilizing runtime.

## 14. Phase 5 - Supply & Personal Inventory (Sprints 8-9)

Phase goal: implement personal inventory + storage interaction so campaigns and cross-map warfare become sustainable and interesting.

### Sprint 8 - Personal inventory and storage interaction

#### Epic P5-A: Person inventory data model

Tasks:

- Add `PersonInventory` with slot count (default 3) and item counts.
- Add `ItemType` enum for carried goods (at minimum Food).
- Add tech hook: `backpacks` increases slot count.

Acceptance:

- NPCs can carry food and the capacity is tech-modifiable.

#### Epic P5-B: Storehouse integration (withdraw/deposit)

Tasks:

- Define interaction rules with storehouse buildings.
- Add jobs/commands: RefillInventory.
- Ensure colony stock and inventory stay consistent (no duping).

Acceptance:

- NPC can refill food from a storehouse; colony stock decreases accordingly.

#### Epic P5-C: Consumption from inventory first

Tasks:

- Modify hunger resolution to consume food from inventory before colony stock.
- Add debug counters: inventory consumption events.

Acceptance:

- NPCs on the move can survive without instantly teleport-eating from colony stock.

#### Epic P5-D: Snapshot and UI indicators

Tasks:

- Add carry indicators to `PersonRenderData` (optional: just a boolean `HasFood`).
- Render a small backpack/food icon above carriers.

Acceptance:

- You can see which NPCs are carrying supply.

#### Epic P5-E: Supply-related tech entries

Tasks:

- Add `backpacks` and `rationing` to `Tech/technologies.json`.
- Implement effects in `TechTree.cs`.

Acceptance:

- Tech unlock changes carry capacity or supply efficiency.

#### Sprint 8 DoD

- Personal inventory exists and is used for consumption.
- Storehouse interaction refills inventory deterministically.

### Sprint 9 - Army supply model and non-local food acquisition

#### Epic P5-F: Army supply model (aggregate + consumption)

Tasks:

- Define army supply as aggregate carried food of members.
- Add marching consumption: `foodConsumedPerPersonPerSecond`.
- Add attrition rules when supply is empty: morale drop, stamina drain.

Acceptance:

- Armies running out of supply degrade and retreat/route more often.

#### Epic P5-G: Supply carrier role + AI behaviors

Tasks:

- Introduce a supply carrier role (not necessarily a Profession enum change; can be a runtime role flag).
- AI assigns carriers and schedules resupply.
- Add escort logic (optional in Phase 7).

Acceptance:

- Armies can be resupplied mid-march by dedicated carriers.

#### Epic P5-H: Foraging behavior

Tasks:

- Add a forage command: gather food from the map while marching.
- Bound foraging so it does not replace agriculture.

Acceptance:

- Armies can extend campaigns slightly via foraging, but not indefinitely.

#### Epic P5-I: Fallback supply budget (for early prototypes)

Tasks:

- Provide a temporary colony-level supply allocation fallback in `SupplyModel` for early campaign prototypes.
- Define the fallback as a *reservation pool* so it interacts with economy cleanly (no duplication):
  - On campaign launch: reserve rations by removing food from colony stock into `Army.RationPoolFood`.
  - Consumption during march/siege reduces `RationPoolFood`.
  - On return: remaining `RationPoolFood` is returned to colony stock.
- Define the initial budget formula (tuning knobs):
  - `minHomeReserveFood = colonyPopulation * 2` (keep 2 food per person at home)
  - `maxReserveFraction = 0.25` (never reserve more than 25% of current food stock)
  - `campaignDaysBudget = 3` and `foodPerWarriorPerDay = 1`
  - `reservedFood = min(colonyFood * maxReserveFraction, armySize * campaignDaysBudget * foodPerWarriorPerDay)`
    but also `colonyFood - reservedFood >= minHomeReserveFood`.
- Document how fallback transitions to real inventory:
  - When inventory is enabled, `RationPoolFood` is distributed at rally into warrior inventories (and the pool is removed).
- Use this fallback only when inventory/supply feature flag is disabled.

Acceptance:

- Campaign prototype can run even if inventory is feature-flagged OFF.
- Fallback supply reduces colony food immediately (affects morale/hunger), preventing "free" campaigns.

#### Sprint 9 DoD

- Supply is a first-class constraint and is visible.
- Army behavior changes under low supply.

## 15. Phase 6 - Campaigns (Hadjaratok) (Sprints 10-11)

Phase goal: implement full campaign loops: assemble -> march -> engage -> siege -> resolve -> return.

### Sprint 10 - Campaign skeleton (planning/assembly/march)

#### Epic P6-A: Campaign and army entities

Tasks:

- Add `Campaign` + `Army` domain types under `WorldSim.Runtime/Simulation/Military/`.
- Add `CampaignManager` to track active campaigns.
- Restrict initially: max 1 campaign per faction.

Acceptance:

- Campaign state progresses deterministically on ticks.

#### Epic P6-B: Assembly and rally points

Tasks:

- Add rally point selection near colony.
- Implement assembly: warrior-role units and carriers travel to rally before marching.

Acceptance:

- Armies visibly assemble rather than teleport.

#### Epic P6-C: March system + encounters

Tasks:

- Implement group movement from rally to target.
- Trigger engagements when encountering hostiles.
- Update supply consumption during march.

Acceptance:

- Marching produces encounters and consumes supply.

#### Epic P6-D: Snapshot + overlays

Tasks:

- Add campaign/army snapshot types.
- Render army blobs and marching paths.
- Add a Campaign HUD panel (list active campaigns, supply %, ETA).

Acceptance:

- You can track campaigns from UI without inspecting runtime state.

#### Sprint 10 DoD

- Campaign loop exists through Marching and Engaging.
- UI shows active campaigns.

### Sprint 11 - Campaign siege and resolution

#### Epic P6-E: Siege integration in campaign flow

Tasks:

- When army reaches target colony, enter Sieging and use Phase 3 siege mechanics.
- Integrate breach progress into campaign resolution.

Acceptance:

- Campaigns against fortified targets require siege time and supply.

#### Epic P6-F: Resolution (loot, war score, peace)

Tasks:

- Define loot rules: transfer a portion of target stock on victory.
- Define war score changes on victory/defeat.
- Define capitulation/forced peace thresholds.

Acceptance:

- Wars end; they do not run forever without resolution.

#### Epic P6-G: Strategic campaign AI

Tasks:

- Add faction-level strategist that decides:
  - when to launch a campaign
  - target selection
  - composition (warriors vs carriers)
  - abort rules

Acceptance:

- Campaigns are not random; they correlate with pressure and advantage.

#### Epic P6-H: Campaign UI polish

Tasks:

- Add event feed catalog entries (campaign launch, supply low, breach, victory, retreat).
- Add siege progress bars in overlay.

Acceptance:

- Campaign outcomes are understandable and auditable.

#### Sprint 11 DoD

- End-to-end campaign loop works and is observable.

## 16. Phase 7 - Cross-map expeditions + supply lines + advanced siege units (Sprints 12-13)

Phase goal: make long-range warfare and multi-step logistics possible and interesting.

### Sprint 12 - Supply lines, forward bases, scouting

#### Epic P7-A: Supply line (convoy) entities

Tasks:

- Implement `SupplyLine` that periodically spawns convoys from colony to army.
- Add vulnerability: convoys can be intercepted.

Acceptance:

- Long campaigns depend on protecting supply.

#### Epic P7-B: Forward bases / camps

Tasks:

- Implement a temporary forward base that provides rest and a local rally point.
- Add despawn rules.

Acceptance:

- Armies can operate far from home with intermediate staging.

#### Epic P7-C: Scout role + intel

Tasks:

- Add Scout role (speed bonus, higher detection radius).
- Feed scouting results into campaign planning.

Acceptance:

- Campaigns become more informed; fewer blind suicides.

#### Epic P7-D: UI for supply lines and forward bases

Tasks:

- Render convoys (icon or small blob) and supply routes (dashed lines).
- Render forward bases.

Acceptance:

- Logistics layer is visible.

#### Sprint 12 DoD

- Supply lines exist and can be disrupted.
- Forward bases and scouts affect planning.

### Sprint 13 - Dedicated siege units + multi-front constraints

#### Epic P7-E: Dedicated siege units (ram, siege tower, mobile catapult)

Tasks:

- Add a `SiegeUnit` entity type that:
  - moves with army
  - has HP
  - provides specialized effects (high wall damage, wall bypass, long-range structure damage)
- Unlock via `siege_craft` and/or `advanced_tactics`.

Acceptance:

- Sieges differ meaningfully when siege units are present.

#### Epic P7-F: Siege unit AI deployment

Tasks:

- Add AI logic for unit placement and protection.
- Add escort requirements for slow units.

Acceptance:

- Siege units are not thrown away instantly.

#### Epic P7-G: Multi-front war (bounded)

Tasks:

- Allow multiple campaigns per faction behind constraints:
  - max 2 campaigns
  - minimum home garrison
- Add war score balancing.

Acceptance:

- Multi-front is possible but does not empty colonies completely.

#### Epic P7-H: Graphics for siege units

Tasks:

- Render siege unit icons and simple animations.
- Add impact effects.

Acceptance:

- Siege units are visually distinct and readable.

#### Sprint 13 DoD

- Advanced siege and multi-front warfare exist and are observable.

## 17. Cross-Track Dependencies

This program is snapshot-driven. Track A work depends on Track B snapshot additions.

High-level dependency rules:

- Track B must land snapshot changes first (compile + runtime tests).
- Track A should not guess domain rules; it renders what snapshot exposes.
- Track C can be developed in parallel once Track B exposes required context fields.

Dependency matrix (minimum):

| Feature | Track B ships | Track C can start | Track A can start |
| --- | --- | --- | --- |
| Phase 0 HP bars | Person health in snapshot | Threat fields in context | After snapshot fields exist |
| Diplomacy panel | Stance matrix in snapshot | War strategist hooks | After stance snapshot exists |
| Territory overlay | OwnerFaction + contested | Patrol/guard/raid goals | After tile ownership snapshot exists |
| Walls and towers render | DefensiveStructureRenderData | Build/attack structure AI | After defensive snapshot exists |
| Campaign overlay | CampaignRenderData | Campaign planner | After campaign snapshot exists |

Track D dependency:

- Contracts v2 and adapter translation depend on runtime command endpoints.
- Runtime must remain stable with refinery OFF.

## 18. Recommended Implementation Order

Critical path to "tower defense + siege + campaigns":

1. Phase 0 (Sprint 1): combat primitives + snapshot
2. Phase 1 (Sprints 2-3): diplomacy/territory + walls/towers + pathfinding v1
3. Phase 2 (Sprint 4): military tech + advanced defenses
4. Phase 3 (Sprints 5-6): group combat + siege
5. Phase 5 (Sprints 8-9): supply + inventory
6. Phase 6 (Sprints 10-11): campaigns
7. Phase 7 (Sprints 12-13): cross-map logistics + siege units

Optional branch:

- Phase 4 (Sprint 7): Refinery/Season Director integration can start after Phase 1 if desired.

## 19. Risks and Mitigations

- Risk: Walls break movement and cause deadlocks.
  Mitigation: Pathfinding v1 in Sprint 3 is mandatory; add stuck-detection counters.

- Risk: Entity count and snapshot size explode (walls, towers, armies, convoys).
  Mitigation: Keep snapshots compact, use IDs and enums, add basic perf stats to RenderStats.

- Risk: Combat dominates economy and collapses populations.
  Mitigation: Keep starvation avoidance highest priority; enforce war cooldowns; cap raid frequency.

- Risk: AI becomes unpredictable and hard to debug.
  Mitigation: Event feed catalog + AI debug panel integration; deterministic random seeds per tick if needed.

- Risk: Siege creates unwinnable stalemates.
  Mitigation: Add timeouts and supply attrition; add war score-driven forced peace.

- Risk: Track D destabilizes runtime.
  Mitigation: Strict gating and formal validation; deterministic fallback; runtime remains functional with refinery OFF.

## 20. Open Decisions (to resolve before implementation)

- Wall placement: fully AI-driven builder jobs vs runtime auto-construction rules (like specialized buildings).
- Tile occupancy: can walls share tiles with houses/buildings? (Recommended: no; one occupant type per tile.)
- Combat determinism: should combat RNG be seeded per tick for reproducibility?
- Territory compute cadence: 10 ticks vs 20 ticks; perf tradeoffs.
- Campaign success conditions: loot-only vs colony capture vs morale collapse.
- Supply balance: how much foraging is allowed before agriculture becomes irrelevant?

## 21. Quality Gates

Per-PR:

- `WorldSim.ArchTests` boundary tests pass.
- Runtime unit tests pass, and new subsystems add tests (combat, pathfinding, diplomacy, supply, campaigns).
- Graphics can render the latest snapshot without exceptions.
- No major regression in baseline economy loop when new features are feature-flagged OFF.
- Headless balance smoke test passes for each sprint milestone:
  - run 1000 ticks with the new feature flags ON for that phase
  - assert at least one colony remains viable (population > 0) and no colony has negative stocks
  - record key counters for debugging (combat deaths, raids, breaches, campaign outcomes)

Per-sprint:

- Sprint DoD items met.
- Basic smoke run on map presets (128x128, 192x108, 256x144 if available).
- Event feed includes key state changes for the sprint.

## 22. Out of Scope (for this master plan)

- Player-controlled combat or building placement UI.
- Freeform wall drawing.
- Fully asymmetric diplomacy stances (A likes B but B hates A).
- Naval combat or non-tile movement.
- Multiplayer or network sync.

## 23. Event Feed Message Catalog (initial)

Messages must be short, distinct, and useful for debugging.

Phase 0:

- Predator hit: "Predator hit a {ColonyName} citizen"
- Predator killed: "{ColonyName} citizen killed a predator"
- Combat death: "{ColonyName} citizen died in combat"

Phase 1:

- Stance change: "{FactionA} is now {Stance} toward {FactionB}"
- War declared: "{FactionA} declared WAR on {FactionB}!"
- Raid launched: "{FactionA} raiders crossed the border"
- Wall built: "{ColonyName} built a wall segment"

Phase 3:

- Siege started: "Siege began near {ColonyName}"
- Breach: "Wall breached near {ColonyName}!"
- Siege repelled: "Siege repelled by {ColonyName}"

Phase 6:

- Campaign launch: "{FactionA} launched a campaign against {FactionB}"
- Supply low: "{FactionA} army is running low on supplies"
- Campaign victory: "{FactionA} won the campaign against {FactionB}"

## 24. Performance Notes

- Walls are numerous; keep wall render data compact (consider run-length encoding later if needed).
- Tower targeting runs every tick; keep target search bounded (grid buckets or radius scan).
- Territory recompute should be periodic, not per tick.
- Campaign and supply line routing can be expensive; cache routes and update on major topology changes.

## 25. Glossary

- Fortifications: walls, gates, towers (tile-based, HP, defenses).
- Siege: sustained attack against fortifications to create a breach.
- Campaign: multi-tick military operation from a colony to a target.
- Supply line: convoy-based resupply from colony to army.
- Contested tile: tile where top two influences are within a threshold.
