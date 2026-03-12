# Session: Balance & QA Agent

> Operational plan for balance/profile QA using the Scenario Matrix Runner / SMR evidence path:
> asserting invariants headlessly, comparing multi-config runs, and evaluating regressions across
> `Showcase`, `DevLite`, and `Headless` policy lanes.
>
> **Wave 4.5 owns the core SMR implementation track.** This session now acts as the QA policy layer:
> it consumes the shared artifact/anomaly/baseline infrastructure instead of planning a separate runner rewrite.
>
> **Naming note:** `Showcase` / `DevLite` / `Headless` are the project-wide profile labels, but full runtime/profile
> plumbing is sequenced later (Wave 7.5). In this doc they are primarily QA/evidence buckets, not a claim that every
> profile toggle already exists in the live app.

Status: Planned (trigger: Combat Phase 0 end, or earlier if balance regressions appear)
Last updated: 2026-03-10

---

## CI REMINDER

> **The GitHub Actions workflow now exists in the workspace** at `.github/workflows/smr-headless.yml`.
> Until it is adopted and validated in the mainline branch, manual session runs remain the fallback
> (run ScenarioRunner by hand within a session).
>
> Wave 4.5 Phase B still owns the rollout/verification of that workflow.

---

## 1. When to open this session

| Trigger | Priority |
|---------|----------|
| Combat Phase 0 ends (first combat deaths in the sim) | PRIMARY |
| Any balance regression suspected (e.g. mass starvation, population collapse) | EARLY TRIGGER |
| Every combat sprint gate (called by Combat Coordinator) | RECURRING |
| Before a milestone merge to main | PRE-MERGE |

---

## 2. Current SMR / ScenarioRunner state (baseline)

Primary file: `WorldSim.ScenarioRunner/Program.cs`

| Capability | Status |
|---|---|
| Headless multi-seed run | EXISTS |
| Configurable seeds/ticks/dt via env vars | EXISTS |
| Reports: livingColonies, people, food, avgFpp, death counters | EXISTS |
| Structured JSON output for machine parsing | EXISTS |
| Multi-config matrix (vary map size, pop, feature flags) | EXISTS |
| Planner matrix (`Simple` / `Goap` / `Htn`) | EXISTS |
| Artifact bundle output | EXISTS (Wave 4.5 SMR-B1) |
| Assertion + anomaly engine | EXISTS (Wave 4.5 SMR-B2) |
| Baseline comparison | EXISTS / expanding (Wave 4.5 SMR-B3) |
| Perf timing (`--perf` mode) | EXISTS / expanding via Wave 4.5 (see Session-Perf-Profiling-Plan.md) |
| CI integration (GitHub Actions) | EXISTS in workspace; rollout/verification owned by Wave 4.5 SMR-B6 |

---

## 3. Operational focus areas

### Phase A -- Invariant policy and result interpretation

**A1. Assertion policy**

Use the SMR assertion/anomaly outputs as the primary balance evidence source.

- Keep invariant IDs and thresholds in sync with the shared SMR catalog.
- Distinguish hard balance failures from non-blocking anomaly warnings.
- Report failures using artifact outputs first; terminal summaries are secondary.

**A2. Combat counter policy**

Requires Track B to expose these on `World`:
- `TotalCombatDeaths` (people killed in combat)
- `TotalCombatKills` (animals/enemies killed by colonists)
- `TotalCombatEngagements` (number of fight events)

Until Track B exposes them, the assertion framework should gracefully skip combat invariants
(check via reflection or a version flag).

**A3. Artifact consumption**

Prefer SMR artifact bundles / structured output over ad hoc terminal parsing.

- `summary.json` / `assertions.json` / `anomalies.json` should be the default review inputs when available.
- Same-seed comparisons matter more than anecdotal one-off manual impressions.

### Phase B -- CI integration

**B1. GitHub Actions workflow**

File: `.github/workflows/smr-headless.yml`

```yaml
name: SMR Headless
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:

jobs:
  smr-headless:
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        mode: [assert, perf]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet build WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj -c Release
      - name: Run SMR headless gate
        env:
          WORLDSIM_SCENARIO_MODE: ${{ matrix.mode }}
          WORLDSIM_SCENARIO_SEEDS: "101,202,303,404,505"
          WORLDSIM_SCENARIO_TICKS: "1200"
          WORLDSIM_SCENARIO_PLANNERS: "simple,goap,htn"
          WORLDSIM_SCENARIO_OUTPUT: "json"
          WORLDSIM_SCENARIO_ARTIFACT_DIR: ${{ runner.temp }}/smr-${{ matrix.mode }}-${{ github.run_id }}
        run: dotnet run --project WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj -c Release
```

**B2. Badge in README (optional)**

Add balance smoke status badge to project README once CI is green.

### Phase C -- Profile-aware QA matrix

**C1. Profile-aware matrix**

Use the shared SMR matrix to separate three regression classes:

- **Sim/balance regression** -- colony survival, food, deaths, combat invariants.
- **Perf regression** -- tick cost / throughput / density drift.
- **Profile regression** -- `DevLite` or `Headless` expectations broken by changes that only make sense in `Showcase`.

Recommended QA lanes:

- `Showcase` -- visual/manual smoke lane.
- `DevLite` -- default developer regression lane.
- `Headless` -- evidence/balance/perf lane via SMR artifacts.

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
    d. Decide which lane is under review: `Showcase`, `DevLite`, or `Headless`

2. RUN ASSERTIONS
    a. Run SMR in assertion/anomaly mode for the target matrix
    b. Parse artifact output / structured JSON
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
       - Is it actually a profile regression (e.g. `DevLite` assumptions broken by visual cost)?
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
| **Performance Profiling** | Shares SMR infrastructure. This session consumes the same evidence for QA decisions. |
| **Meta Coordinator** | Receives balance reports at phase boundaries. Updates risk registry on balance regressions. |
| **Track B sessions** | Track B exposes the counters and world state that assertions check. |
| **Track C sessions** | AI balance (NPC decision quality) is testable via assertion outcomes and planner matrix evidence. |

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
| `WorldSim.ScenarioRunner/Program.cs` | Main runner / SMR entrypoint | EXISTS (expanded beyond the old baseline) |
| `balance-configs.json` | Multi-config matrix definition | OPTIONAL helper input |
| `balance-baseline.json` | Stored baseline for comparison | OPTIONAL local/canonical file once a clean baseline is established |
| `.github/workflows/smr-headless.yml` | CI workflow | EXISTS in workspace |
| `Docs/Plans/Session-Balance-QA-Plan.md` | This plan | EXISTS |
