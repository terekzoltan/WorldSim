# Balance Surface - Candidate Parameter Map

This document is a curated candidate surface map for future balance operations.

Important:

- this is not an authoritative list of all tunable values in the codebase
- some items are true knobs today, some are only candidates
- only the subset that is explicitly wired into runtime code becomes active balance surface
- until then, `Config/balance-surface.json` is a draft skeleton, not live configuration

## Status Legend

- `safe` - low-coupling data / threshold candidate
- `guarded` - likely tunable, but requires holdout validation
- `blocked` - structural / code-coupled, not for balance patching
- `audit-pending` - candidate exists, but first-class wiring status is not clean enough yet

## Initial Candidate Scope

Target starting scope: roughly 30 core parameters.

Why this is intentionally narrow:

- balance values are still spread across multiple code paths
- some values are in dead options classes, others are in `const` fields, others are implicit in logic
- over-declaring the surface too early would create config drift and false authority

## Candidate Surface Table

| Category | Candidate | Current snapshot | Source | Status | Notes |
|---|---|---:|---|---|---|
| survival | HungerPerSecond | 1.65 | `WorldSim.Runtime/Simulation/RuntimeBalanceOptions.cs` | safe | good first wiring candidate; class exists but is unwired |
| survival | StarvationDamageSevere | 2.6 | `WorldSim.Runtime/Simulation/RuntimeBalanceOptions.cs` | safe | same |
| survival | StarvationDamageLight | 1.2 | `WorldSim.Runtime/Simulation/RuntimeBalanceOptions.cs` | safe | same |
| survival | CriticalEatPreemptThreshold | 78 | `WorldSim.Runtime/Simulation/RuntimeBalanceOptions.cs` | safe | same |
| survival | EmergencyInstantEatThreshold | 96 | `WorldSim.Runtime/Simulation/RuntimeBalanceOptions.cs` | safe | same |
| survival | EatThresholdNormal | 62 | `WorldSim.Runtime/Simulation/RuntimeBalanceOptions.cs` | safe | same |
| survival | EatThresholdEmergency | 54 | `WorldSim.Runtime/Simulation/RuntimeBalanceOptions.cs` | safe | same |
| survival | SeekFoodThresholdNormal | 50 | `WorldSim.Runtime/Simulation/RuntimeBalanceOptions.cs` | safe | same |
| survival | SeekFoodThresholdEmergency | 42 | `WorldSim.Runtime/Simulation/RuntimeBalanceOptions.cs` | safe | same |
| survival | AgingTickDivisor | 90 | `WorldSim.Runtime/Simulation/RuntimeBalanceOptions.cs` | safe | same |
| economy | WoodYield | 1 | `WorldSim.Runtime/Simulation/World.cs` | safe | property-based candidate |
| economy | StoneYield | 1 | `WorldSim.Runtime/Simulation/World.cs` | safe | property-based candidate |
| economy | IronYield | 1 | `WorldSim.Runtime/Simulation/World.cs` | safe | property-based candidate |
| economy | GoldYield | 1 | `WorldSim.Runtime/Simulation/World.cs` | safe | property-based candidate |
| economy | FoodYield | 2 | `WorldSim.Runtime/Simulation/World.cs` | safe | property-based candidate |
| survival | HealthBonus | 0 | `WorldSim.Runtime/Simulation/World.cs` | safe | tech also touches it |
| survival | MaxAge | 80 | `WorldSim.Runtime/Simulation/World.cs` | safe | tech also touches it |
| economy | HouseCapacity | 5 | `WorldSim.Runtime/Simulation/World.cs` | safe | tech also touches it |
| survival | BirthRateMultiplier | 1.0 | `WorldSim.Runtime/Simulation/World.cs` | safe | already exposed in ScenarioRunner configs too |
| combat | PredatorHumanDamage | 10.0 | `WorldSim.Runtime/Simulation/World.cs` | guarded | property-based, but combat-sensitive |
| combat | CombatDamageBonusMultiplier | 1.0 | `WorldSim.Runtime/Simulation/World.cs` | guarded | tech also touches it |
| combat | CombatDefenseBonusMultiplier | 1.0 | `WorldSim.Runtime/Simulation/World.cs` | guarded | tech also touches it |
| combat | SiegeDamageMultiplier | 1.0 | `WorldSim.Runtime/Simulation/World.cs` | guarded | tech also touches it |
| economy | HouseWoodCost | 50 | `WorldSim.Runtime/Simulation/Colony.cs` | safe | simple property candidate |
| economy | HouseStoneCost | 15 | `WorldSim.Runtime/Simulation/Colony.cs` | safe | simple property candidate |
| movement | MovementSpeedMultiplier | 1.0 | `WorldSim.Runtime/Simulation/Colony.cs` | guarded | affects navigation feel and balance |
| morale | MoraleStart | 55 | `WorldSim.Runtime/Simulation/Colony.cs` | guarded | currently implicit via default property init |
| combat | FortificationHpMultiplier | 1.0 | `WorldSim.Runtime/Simulation/Colony.cs` | guarded | defense / combat coupling |
| combat | BaseHumanDamage | 8.0 | `WorldSim.Runtime/Simulation/Combat/CombatConstants.cs` | guarded | currently `const`; needs refactor before wiring |
| combat | PredatorStrength | 10 | `WorldSim.Runtime/Simulation/Combat/CombatConstants.cs` | guarded | currently `const`; needs refactor before wiring |
| combat | PersonCombatCooldownSeconds | 0.75 | `WorldSim.Runtime/Simulation/Combat/CombatConstants.cs` | guarded | currently `const`; needs refactor before wiring |
| combat | PredatorCombatCooldownSeconds | 0.9 | `WorldSim.Runtime/Simulation/Combat/CombatConstants.cs` | guarded | currently `const`; needs refactor before wiring |
| combat | RandomFactorMin | 0.85 | `WorldSim.Runtime/Simulation/Combat/CombatConstants.cs` | guarded | currently `const`; needs refactor before wiring |
| combat | RandomFactorMax | 1.15 | `WorldSim.Runtime/Simulation/Combat/CombatConstants.cs` | guarded | currently `const`; needs refactor before wiring |
| diplomacy | BorderPressureWeight | 0.35 | `WorldSim.Runtime/Simulation/Diplomacy/RelationManager.cs` | guarded | embedded in scoring logic |
| diplomacy | SkirmishPressureWeight | 8.0 | `WorldSim.Runtime/Simulation/Diplomacy/RelationManager.cs` | guarded | embedded in scoring logic |
| diplomacy | PassivePressureDecay | 1.2 | `WorldSim.Runtime/Simulation/Diplomacy/RelationManager.cs` | guarded | embedded in scoring logic |
| diplomacy | NeutralCooldownTicks | 180 | `WorldSim.Runtime/Simulation/Diplomacy/RelationManager.cs` | guarded | hardcoded threshold/cooldown candidate |
| diplomacy | EscalatedCooldownTicks | 90 | `WorldSim.Runtime/Simulation/Diplomacy/RelationManager.cs` | guarded | hardcoded threshold/cooldown candidate |
| diplomacy | RaidImpactFloor | 55.0 | `WorldSim.Runtime/Simulation/Diplomacy/RelationManager.cs` | guarded | hardcoded escalation floor |
| diplomacy | RaidImpactBoost | 60.0 | `WorldSim.Runtime/Simulation/Diplomacy/RelationManager.cs` | guarded | hardcoded escalation boost |
| diplomacy | HostileThreshold | 55.0 | `WorldSim.Runtime/Simulation/Diplomacy/RelationManager.cs` | guarded | true threshold candidate |
| diplomacy | WarThreshold | 120.0 | `WorldSim.Runtime/Simulation/Diplomacy/RelationManager.cs` | guarded | true threshold candidate |
| tech-later | cheap_houses | 5 wood | `WorldSim.Runtime/Simulation/TechTree.cs` | audit-pending | later explicit surface; not in initial balance json |
| tech-later | extra_wood | 2 wood yield | `WorldSim.Runtime/Simulation/TechTree.cs` | audit-pending | tech-driven, later explicit gate |
| tech-later | health_boost | +20 health, max age 100 | `WorldSim.Runtime/Simulation/TechTree.cs` | audit-pending | tech-driven, later explicit gate |
| tech-later | damage_bonus | 1.15 multiplier | `WorldSim.Runtime/Simulation/TechTree.cs` | audit-pending | tech-driven, later explicit gate |
| tech-later | defense_bonus | 1.15 multiplier | `WorldSim.Runtime/Simulation/TechTree.cs` | audit-pending | tech-driven, later explicit gate |
| tech-later | fortification_hp_bonus | 1.2 multiplier | `WorldSim.Runtime/Simulation/TechTree.cs` | audit-pending | tech-driven, later explicit gate |
| ai-later | threat thresholds | audit pending | `WorldSim.Runtime/Simulation/AI/*` | audit-pending | useful future surface, but not cleanly mapped yet |
| ai-later | crowd penalties | audit pending | `WorldSim.Runtime/Simulation/AI/*` | audit-pending | useful future surface, but not cleanly mapped yet |
| ai-later | no-progress backoff tuning | audit pending | `WorldSim.Runtime/Simulation/*` | audit-pending | useful future surface, but not cleanly mapped yet |

## Candidate Surface Notes

### Best first wiring candidates

If this becomes active later, the lowest-risk first batch is likely:

- `RuntimeBalanceOptions` survival thresholds
- `World` resource yield properties
- `Colony` house cost properties

These are the clearest path to a real, wired subset without pretending the whole balance model is already config-driven.

### Guarded areas

The following should stay guarded until the workflow proves itself:

- `CombatConstants`
- diplomacy pressure logic in `RelationManager`
- AI behavior thresholds and penalties

### Explicitly not authoritative yet

This file is intentionally a map of candidates, not an ownership declaration over all gameplay tuning.
If a value is not wired, it is only an observation and a future candidate.
