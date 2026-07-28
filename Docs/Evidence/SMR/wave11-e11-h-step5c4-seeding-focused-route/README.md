# Wave 11 E11-H Step 5c4 Seeding And Focused Route Evidence

Date: 2026-07-25
Governance reconciled: 2026-07-28

## Scope

This packet records the Step 5c4 initialization repair and the strict focused lifecycle
route decision. The seeding-only initialization package is accepted GREEN and verified at
commit `5b377583a81b6c9bcda84e884da97bacb46f6b98`, tree
`1dca80444e255947269fad319274b1f638ae58a8`. This packet does not claim E11-H GREEN,
expanded 9-run coverage, full 45-run coverage, or approval of a new lifecycle tuning
candidate.

Reviewed base:

- commit `ea94e7a8b8f3fe43cd44d5eb7b4c9fbf7351eae9`;
- tree `8939f1800ff865234ff252af5b37f1dc76ce5b5f`.

Verified seeding-only closeout:

- commit `5b377583a81b6c9bcda84e884da97bacb46f6b98`;
- tree `1dca80444e255947269fad319274b1f638ae58a8`;
- committed-tree `InitialAnimalSeedingTests`: `24/24 PASS`.

Raw ScenarioRunner artifacts remain local-only under
`.artifacts/smr/wave11-e11-h-step5c4-seeding-calibration-001/`.

## Exact Package Manifest

The verified seeding-only package is limited to:

- `WorldSim.Runtime/Simulation/Ecology/InitialAnimalSeeding.cs`: aggregate predator cap,
  selected scalar handoff, and bounded final predator materialization;
- `WorldSim.Runtime/Simulation/World.cs`: only the `GetPredatorCapacityLimit(int)` overload
  and the existing region overload delegation;
- `WorldSim.Runtime.Tests/InitialAnimalSeedingTests.cs`: fragmented aggregate/regional cap,
  low-population compatibility, and locked early-contact regressions.

Explicitly excluded from this package:

- `WorldSim.Runtime/Simulation/Person.cs`;
- `WorldSim.Runtime.Tests/ContactFollowThroughTests.cs`;
- `WorldSim.Runtime.Tests/Wave11AnimalLifecycleTests.cs`;
- all raw `.artifacts/**` content;
- the pre-existing Step 5c1-A and Step 5c1-B Meta plan diffs.

## Initialization Repair

The defect was the independent minimum-one predator floor in every prey-bearing region.
On the locked fragmented profile this compounded into a global `5H/5P` start. The bounded
repair preserves regional capacity but caps total initial predators from total selected
herbivores through the existing Runtime capacity formula. Final materialization receives
the selected scalar maximum so plan and output cannot diverge.

Focused command:

```powershell
dotnet test "WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj" --no-restore --filter "FullyQualifiedName~InitialAnimalSeedingTests" -m:1 -p:UseSharedCompilation=false --verbosity minimal
```

Result: `24/24 PASS`.

Key controlled outcomes:

- fragmented-region fixture: deterministic `8H/2P`;
- low-population compatibility: `1H/1P`;
- locked seeds `101,202,303`: herbivores remain positive through the prior tick-2/3 collapse window;
- emergency rescue, herbivore replenishment, and predator replenishment remain zero.

The local 300-tick calibration contains six runs (`3` seeds x `2` policies), exits `0`,
has zero assertion failures and zero anomalies, and reports habitat-aware final populations:

| Seed | Initial | Final herbivores | Final predators |
|------|---------|------------------|-----------------|
| 101 | 8H/2P | 50 | 5 |
| 202 | 8H/2P | 27 | 5 |
| 303 | 8H/2P | 40 | 7 |

All three habitat-aware rows have `TicksWithZeroHerbivores=0`,
`FirstZeroHerbivoreTick=null`, and zero rescue/replenishment.

This calibration is a Simple-planner, combat-disabled initialization/early-stability lane.
It is not planner-matrix, combat, predator-human lifecycle, expanded, or full-matrix proof.

## Recent-Hostile Correctness Finding

`Person.HasRecentCombatIntent(...)` subtracted an `int.MinValue` sentinel from the current
tick. Unchecked overflow made a never-observed hostile appear recent. A focused test first
failed with `Actual: True`; after the initialized-timestamp guard, the complete
`ContactFollowThroughTests` class passes `7/7`.

This is an orthogonal combat-intent correctness fix. Independent review classified the
implementation as correct but outside the authorized seeding-only package. It requires a
separate reviewed handoff and must not be used to broaden the initialization acceptance.

## Canonical Focused Lifecycle Gate

The durable sentinel uses the production `SimulationRuntime.AdvanceTick(...)` path:

- world `64x40`, initial population `24`;
- `1200` ticks at `0.25` seconds;
- exact cases `101/Simple`, `101/Goap`, `202/Simple`, `202/Goap`, `202/Htn`;
- global planner policy and habitat-aware Runtime default;
- combat primitives and predator-human interaction enabled;
- emergency rescue disabled;
- no rescue or replenishment may be observed.

Command:

```powershell
dotnet test "WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj" --no-restore --filter "FullyQualifiedName~HabitatAwarePredatorHuman_FocusedLifecycleGatePreservesBothSpecies" -m:1 -p:UseSharedCompilation=false --verbosity minimal
```

Result: `4/5 PASS`, therefore RED.

The only failing row is `101/Goap`:

```text
herbivores=128
predators=0
zeroHerbivoreTicks=0
zeroPredatorTicks=210
firstZeroPredator=991
herbivoreBirths=297
predatorBirths=11
herbivoreStarvations=9
predatorStarvations=0
activeFood=6
plantConsumed=2196
meatFromHunt=167
predatorHumanHits=22
predatorDeaths=13
predatorKillsByHumans=13
emergencyRescues=0
herbivoreReplenishment=0
predatorReplenishment=0
```

The initialization repair is therefore not the remaining failure. The row generates
predator births and food/prey activity, but every predator death is a human kill.

## Predator-Human OFF Control

The same five cases are retained as a separate executable hard-control theory with only
predator-human interaction disabled:

```powershell
dotnet test "WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj" --no-restore --filter "FullyQualifiedName~HabitatAwarePredatorHumanOff_FocusedLifecycleControlPreservesBothSpecies" -m:1 -p:UseSharedCompilation=false --verbosity minimal
```

Both ON and OFF theories call the same `AssertFocusedLifecycleContinuity(...)` production
Runtime fixture. The helper asserts the requested toggle, habitat-aware/runtime-default
initial identity, full species continuity, disabled rescue, and zero replenishment. The OFF
branch additionally asserts zero predator-human hits and zero human predator kills.

Result: `1/5 PASS`, therefore RED.

Failed controls:

| Case | Final H/P | First zero H | First zero P | H births | P births | P starvations |
|------|-----------|--------------|--------------|----------|----------|---------------|
| 101/Goap | 0/2 | 780 | null | 116 | 14 | 14 |
| 202/Simple | 0/2 | 888 | null | 116 | 14 | 14 |
| 202/Goap | 0/2 | 888 | null | 116 | 14 | 14 |
| 202/Htn | 0/0 | 526 | 992 | 77 | 13 | 15 |

Every failed control had zero predator-human hits, zero human predator kills, zero rescue,
and zero replenishment. Human pressure changes the failure mode but is not the sole defect.

## Decision

- Initialization verification and seeding-only package closeout: GREEN and verified at commit `5b377583a81b6c9bcda84e884da97bacb46f6b98`, tree `1dca80444e255947269fad319274b1f638ae58a8`.
- Recent-hostile overflow fix: correct local TDD fix, but separate combat-intent package.
- Focused lifecycle: RED; expanded 9-run and full 45-run remain blocked.
- Next route: one diagnostics-first predator-prey recruitment/mortality analysis covering
  births, captures/meat, starvation, and first-zero timing. Do not stack constant changes or
  enable rescue/replenishment for acceptance.

## Remaining Closeout Blockers

- historical `DEFER_STEP5C5` seven-field timeline proof or explicit evidence-backed waiver;
- natural production-caller timestamp regressions for contact, hunt, grazing, and predator
  death, or explicit evidence-backed waiver;
- focused 5/5 GREEN before expanded 9-run, then expanded GREEN before full 45-run;
- final explicit Meta E11-H GREEN.

## Verification Summary

- committed-tree `InitialAnimalSeedingTests`: `24/24 PASS`.
- `ContactFollowThroughTests`: expected RED before fix, then `7/7 PASS`.
- canonical focused lifecycle sentinel: `4/5 PASS`, gate RED.
- executable predator-human OFF hard control: `1/5 PASS`, gate RED.
- `WorldSim.Runtime.Tests` build: `0 warnings`, `0 errors`.
- `git diff --check`: no whitespace errors; line-ending conversion warnings only.

The seeding-only package is committed as `5b377583a81b6c9bcda84e884da97bacb46f6b98`
with tree `1dca80444e255947269fad319274b1f638ae58a8`. No expanded matrix, full matrix,
or lifecycle-repair package is claimed by this packet; raw `.artifacts/**` remains local-only.
