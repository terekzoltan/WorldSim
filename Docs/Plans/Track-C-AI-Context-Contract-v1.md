# Track C AI Context Contract v1 (Runtime Confirmation)

Status: confirmed baseline (P0/P1 prep)
Owner: Track B
Date: 2026-02-26

| field | cadence | owner note |
| --- | --- | --- |
| `Health` | per tick | Runtime computes from `Person.Health`, clamped+rounded for stable AI input. |
| `Strength` | per tick | Runtime supplies integer from `Person.Strength` (bounded). |
| `Defense` | per tick | Runtime supplies from `Person.Defense`; currently baseline value, combat layer will tune usage. |
| `NearbyPredators` | per tick | Runtime radius scan (manhattan radius=4). |
| `NearbyHostilePeople` | per tick | Runtime radius scan against other colonies. |
| `WarState` | periodic (10 tick target) | Temporary fallback currently derived from feature flag (`Peace/Tense`); full diplomacy state machine pending. |
| `TileContestedNearby` | periodic (10 tick target) | Temporary fallback `false` until territory ownership/contested system lands. |
| `IsWarrior` | per tick | Temporary fallback: role flag OR hunter profession; true role assignment flow pending. |
| `ColonyWarriorCount` | periodic (5 tick target) | Temporary fallback count from current warrior heuristic; real mobilization policy pending. |

Determinism policy:

- Seed strategy: world-level seed (`World(..., randomSeed)`) is source seed; entity RNG streams are derived from world RNG and propagated at spawn.
- Stable iteration: runtime scans use stable list iteration order; avoid unordered aggregation for AI-critical values.
- Numeric policy: AI context values are clamped and rounded before handoff (`RuntimeNpcBrain`) to reduce drift and keep regression reproducible.
