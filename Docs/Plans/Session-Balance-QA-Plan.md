# Session: Balance & QA Agent

> Operational plan for a dedicated session that extends the ScenarioRunner into a balance
> regression gate -- asserting game invariants headlessly, comparing multi-config runs, and
> (eventually) running as a GitHub Actions CI workflow.
>
> **This session does not create a new project.** It extends `WorldSim.ScenarioRunner` (49 lines today)
> with assertion mode, combat counters, exit codes, and structured output.

Status: Planned (trigger: Combat Phase 0 end, or earlier if balance regressions appear)
Last updated: 2026-02-26

---

## CI REMINDER

> **A GitHub Actions workflow does not yet exist.**
> Until `.github/workflows/balance-smoke.yml` is created, all balance tests are manual
> (run ScenarioRunner by hand within a session).
>
> Creating the CI workflow is a deliverable of this session's Phase B.
> Proposed file: `.github/workflows/balance-smoke.yml`
> Trigger: push to `main`, PR to `main`, manual dispatch.

---

## 1. When to open this session

| Trigger | Priority |
|---------|----------|
| Combat Phase 0 ends (first combat deaths in the sim) | PRIMARY |
| Any balance regression suspected (e.g. mass starvation, population collapse) | EARLY TRIGGER |
| Every combat sprint gate (called by Combat Coordinator) | RECURRING |
| Before a milestone merge to main | PRE-MERGE |

---

## 2. Current ScenarioRunner state (baseline)

File: `WorldSim.ScenarioRunner/Program.cs` (49 lines)

| Capability | Status |
|---|---|
| Headless multi-seed run | EXISTS |
| Configurable seeds/ticks/dt via env vars | EXISTS |
| Reports: livingColonies, people, food, avgFpp, death counters | EXISTS |
| Combat death counters (`TotalCombatDeaths`, `TotalCombatKills`) | MISSING |
| Assertion mode (exit code 1 on invariant violation) | MISSING |
| Structured JSON output for machine parsing | MISSING |
| Multi-config matrix (vary map size, pop, feature flags) | MISSING |
| Perf timing (`--perf` mode) | MISSING (see Session-Perf-Profiling-Plan.md) |
| CI integration (GitHub Actions) | MISSING |

---

## 3. Implementation phases

### Phase A -- Assert mode (core deliverable)

**A1. Assertion framework in ScenarioRunner**

Add to `Program.cs`:
- Env var `WORLDSIM_SCENARIO_ASSERT=true` enables assertion mode
- After each seed run, evaluate invariants (see section 4)
- If any invariant fails: print `FAIL: [invariant name] [details]`, set exit code = 1
- If all pass: print `PASS`, exit code = 0
- Exit code is critical for CI integration

**A2. Combat counters**

Requires Track B to expose these on `World`:
- `TotalCombatDeaths` (people killed in combat)
- `TotalCombatKills` (animals/enemies killed by colonists)
- `TotalCombatEngagements` (number of fight events)

Until Track B exposes them, the assertion framework should gracefully skip combat invariants
(check via reflection or a version flag).

**A3. Structured JSON output**

When `WORLDSIM_SCENARIO_JSON=true`, output one JSON line per seed:
```json
{
  "seed": 101,
  "ticks": 1200,
  "livingColonies": 3,
  "people": 18,
  "food": 245.5,
  "avgFpp": 13.6,
  "deaths": {"age": 2, "starvation": 1, "predator": 3, "combat": 0, "other": 0},
  "combatEngagements": 0,
  "assertions": {"total": 8, "passed": 8, "failed": 0, "skipped": 0},
  "result": "PASS"
}
```
Final line: summary JSON with overall pass/fail and aggregated stats.

### Phase B -- CI integration

**B1. GitHub Actions workflow**

File: `.github/workflows/balance-smoke.yml`

```yaml
name: Balance Smoke
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:

jobs:
  balance-smoke:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet build WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj -c Release
      - name: Run balance smoke
        env:
          WORLDSIM_SCENARIO_SEEDS: "101,202,303,404,505"
          WORLDSIM_SCENARIO_TICKS: "1200"
          WORLDSIM_SCENARIO_ASSERT: "true"
          WORLDSIM_SCENARIO_JSON: "true"
        run: dotnet run --project WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj -c Release
```

**B2. Badge in README (optional)**

Add balance smoke status badge to project README once CI is green.

### Phase C -- Multi-config matrix

**C1. Config matrix runner**

Extend ScenarioRunner to accept multiple configurations in a single run:
- Env var `WORLDSIM_SCENARIO_CONFIGS` pointing to a JSON file with an array of configs
- Each config specifies: map size, initial pop, feature flags, seeds, ticks
- Produces a comparison table across configs

Example config file (`balance-configs.json`):
```json
[
  {"name": "small-default", "width": 64, "height": 40, "pop": 24, "seeds": [101,202,303], "ticks": 1200},
  {"name": "medium-default", "width": 128, "height": 72, "pop": 48, "seeds": [101,202,303], "ticks": 1200},
  {"name": "standard-default", "width": 192, "height": 108, "pop": 72, "seeds": [101,202,303], "ticks": 1200},
  {"name": "large-stress", "width": 384, "height": 216, "pop": 120, "seeds": [101,202], "ticks": 600}
]
```

**C2. Baseline comparison**

- Store a baseline results file (`balance-baseline.json`) in the repo
- On each run, compare current results to baseline
- Report deltas: "population +12% vs baseline", "starvation deaths -40%"
- Flag significant regressions (configurable threshold, default: any invariant that was PASS becoming FAIL)

---

## 4. Balance invariants (assertion catalog)

These invariants are evaluated per-seed after the run completes:

### Core survival invariants

| ID | Invariant | Threshold | Phase |
|---|---|---|---|
| `SURV-01` | At least 1 colony survives | livingColonies >= 1 | Phase A |
| `SURV-02` | Population does not collapse to zero | people > 0 | Phase A |
| `SURV-03` | No mass starvation (>50% of deaths from starvation) | starvDeaths / totalDeaths < 0.5 | Phase A |
| `SURV-04` | Average food per person above subsistence | avgFpp >= 1.0 | Phase A |
| `SURV-05` | Starvation-with-food anomaly is rare | starvWithFood <= 2 | Phase A |

### Combat invariants (Phase 0+)

| ID | Invariant | Threshold | Phase |
|---|---|---|---|
| `COMB-01` | Combat deaths exist (combat is happening) | combatDeaths > 0 | Phase 0 |
| `COMB-02` | Combat is not annihilating population | combatDeaths / totalDeaths < 0.7 | Phase 0 |
| `COMB-03` | Combat engagements proportional to population | engagements > 0 | Phase 0 |

### Economy invariants

| ID | Invariant | Threshold | Phase |
|---|---|---|---|
| `ECON-01` | Total food is positive at end of run | totalFood > 0 | Phase A |
| `ECON-02` | No colony has zero food and zero people | (food==0 && people==0) is expected, not a bug | Phase A |

### Scaling invariants (Phase C)

| ID | Invariant | Threshold | Phase |
|---|---|---|---|
| `SCALE-01` | Larger maps produce more colonies | large.colonies >= small.colonies | Phase C |
| `SCALE-02` | Population scales roughly with initial pop | people >= initialPop * 0.3 | Phase C |

---

## 5. Per-session workflow

```
1. PRE-CHECK
   a. Verify ScenarioRunner builds: dotnet build WorldSim.ScenarioRunner
   b. Check which assertion phases are available (Phase A core always, combat if counters exist)
   c. Read current baseline (if exists)

2. RUN ASSERTIONS
   a. Run ScenarioRunner with ASSERT=true, JSON=true
   b. Parse JSON output
   c. Report: total assertions, passed, failed, skipped
   d. If any FAIL: investigate the specific invariant

3. COMPARE TO BASELINE (if baseline exists)
   a. Load baseline JSON
   b. Compare per-seed metrics
   c. Flag significant regressions
   d. Report delta table

4. INVESTIGATE FAILURES
   a. For each FAIL, identify the root cause:
      - Is it a balance parameter change? (expected after tuning)
      - Is it a bug? (unexpected regression)
      - Is it a threshold that needs updating? (game has evolved)
   b. Recommend action: fix bug / adjust threshold / update baseline

5. UPDATE BASELINE (if run is clean or intentionally changed)
   a. Overwrite balance-baseline.json with current results
   b. Document reason for baseline update in commit message

6. REPORT
   a. Write uzenofal entry if regressions found
   b. Report to Combat Coordinator (if combat sprint gate)
   c. Report to Meta Coordinator (if phase boundary)
```

---

## 6. Relationship to other sessions

| Session | Relationship |
|---------|-------------|
| **Combat Coordinator** | Calls this session at every sprint gate. Combat invariants added incrementally. |
| **Performance Profiling** | Shares ScenarioRunner infrastructure. `--perf` mode is separate but complementary. |
| **Meta Coordinator** | Receives balance reports at phase boundaries. Updates risk registry on balance regressions. |
| **Track B sessions** | Track B exposes the counters and world state that assertions check. |
| **Track C sessions** | AI balance (NPC decision quality) is testable via assertion outcomes. |

---

## 7. Invariant evolution policy

- New invariants are added as combat phases introduce new mechanics
- Thresholds are tuned based on baseline data, not guesswork
- An invariant can be SKIPPED if the required counters don't exist yet (graceful degradation)
- Invariants are never silently removed -- they are explicitly marked RETIRED with a reason
- Each combat phase should add at least 1-2 new invariants specific to its mechanics

---

## 8. File inventory

| File | Purpose | Status |
|---|---|---|
| `WorldSim.ScenarioRunner/Program.cs` | Main runner, assertion host | EXISTS (49 lines, needs extension) |
| `balance-configs.json` | Multi-config matrix definition | PLANNED (Phase C) |
| `balance-baseline.json` | Stored baseline for comparison | PLANNED (Phase C) |
| `.github/workflows/balance-smoke.yml` | CI workflow | PLANNED (Phase B) |
| `Docs/Plans/Session-Balance-QA-Plan.md` | This plan | EXISTS |
