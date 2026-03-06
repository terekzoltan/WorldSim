# Wave 1 Manual QA Checklist

**Scope:** Visual and runtime verification of all Wave 1 deliverables.
**Build:** `dotnet build WorldSim.sln` must be green before starting.
**Tests:** full `dotnet test WorldSim.sln` gate must be green before manual QA.

---

## How to run the game

```
dotnet run --project WorldSim.App
```

---

## P0-A/B — Damage model + predator combat

| # | Action | Expected |
|---|--------|----------|
| 1 | Let the simulation run for ~30 seconds (observe colony near predator territory) | Predators move across the map; people nearby should occasionally engage |
| 2 | Watch the console / event feed for predator attack events | No crash; counters increment (visible in HUD if enabled) |
| 3 | Run 1000-tick headless smoke test: `dotnet test --filter HeadlessSmoke_1000Ticks` | Green; `TotalPredatorHumanHits > 0` confirmed |

---

## P0-C — AI Fight/Flee threat response

| # | Action | Expected |
|---|--------|----------|
| 4 | Observe people near an active predator | Person should visibly switch to Fight (moves toward predator) or Flee (moves away) job state |
| 5 | Check AI debug panel (if GOAP trace overlay is on) | `SelectedGoal` shows `Fight` or `Flee` for threatened NPCs |
| 6 | Run: `dotnet test --filter ThreatResponse_AdjacentEnemies` | Green (new test, validates 1-tick Fight/Flee trigger) |

---

## P0-D — Health bars + combat markers

| # | Action | Expected |
|---|--------|----------|
| 7 | Observe a person being attacked by a predator | A health bar should render above the person; bar shrinks as health drops |
| 8 | Person takes damage | An "in-combat" marker (icon/highlight) appears on that person |
| 9 | Person health reaches 0 | Person disappears from map; no NaN/Infinity in health values |
| 10 | Zoom in and out (`scroll wheel` or `+/-`) | Health bars and markers remain correctly positioned relative to person sprites |

---

## S1-A/C/D — Director contract wire (adapter round-trip)

| # | Action | Expected |
|---|--------|----------|
| 11 | Run: `dotnet test --filter DirectorEndToEndTests` | Green (2 new tests: full pipeline + dedup idempotency) |
| 12 | Run: `dotnet test --filter DirectorOps_AreAppliedAndDedupedByOpId` | Green (existing RefineryClient dedup test) |

---

## S1-B — Java mock director (if Java service is running)

> Skip if Java service is not available locally.

| # | Action | Expected |
|---|--------|----------|
| 13 | Start Java service: `cd refinery-service-java && mvn spring-boot:run` | Service starts on configured port |
| 14 | Trigger a director checkpoint (if runtime has a manual trigger path) | Story beat appears in event feed; colony directive is applied |
| 15 | Trigger same checkpoint again | Duplicate opId is ignored (idempotent); no duplicate beat in feed |

---

## F1/F6 regression — UI flows must not break

| # | Action | Expected |
|---|--------|----------|
| 16 | Press `F1` | Tech tree menu opens; no crash |
| 17 | Navigate tech tree, unlock a tech | Tech is applied; no error overlay |
| 18 | Press `F6` (or whatever overlay key is mapped) | Overlay toggles without crash |
| 19 | Press `F1` again | Menu closes cleanly |

---

## Camera / zoom regression

| # | Action | Expected |
|---|--------|----------|
| 20 | Scroll wheel to zoom in close | Map renders correctly; no sprite misalignment |
| 21 | Scroll wheel to zoom out fully | Full map visible; no texture corruption |
| 22 | Pan with middle mouse / arrow keys | Camera tracks correctly; HUD stays anchored to screen |

---

## AI debug panel (Track C)

| # | Action | Expected |
|---|--------|----------|
| 23 | Press `PgUp` / `PgDn` | Tracked NPC switches; debug panel shows new NPC's trace |
| 24 | Press `Home` | Tracked NPC resets to default |
| 25 | Observe GOAP trace overlay for a threatened NPC | Fields visible: `plan cost`, `replan reason`, `method name` |

---

## Pass criteria

- All numbered items above produce expected behavior with no crashes, no exceptions, no visual corruption.
- `dotnet test WorldSim.sln` returns fully green after any code changes made during QA.

---

## Known limitations (not bugs)

- Runtime feature activation may depend on env toggles (`WORLDSIM_ENABLE_DIPLOMACY`, `WORLDSIM_ENABLE_COMBAT_PRIMITIVES`, `WORLDSIM_ENABLE_PREDATOR_ATTACKS`).
- Person-vs-person damage loop does not exist yet (`CombatResolver.CalculateDamage` is defined but not called in production). Wave 2 territory.
