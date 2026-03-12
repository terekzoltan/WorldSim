# WorldSim

MonoGame-based 2D colony simulation with multi-faction diplomacy, combat, tech progression, and an LLM-backed story director. The project is organized as a modular C# solution under active Track A–D migration.

## Project structure

### C# solution (`WorldSim.sln`)

| Project | Role |
|---------|------|
| `WorldSim.App/` | MonoGame host — input routing, wiring of Runtime + Graphics + RefineryAdapter |
| `WorldSim.Graphics/` | Camera, render passes, HUD, tech menu — consumes read-only snapshots only |
| `WorldSim.Runtime/` | World simulation: tick update, ecology/economy/tech/combat/diplomacy/territory |
| `WorldSim.AI/` | NPC brain interfaces; GOAP, HTN, Utility planners |
| `WorldSim.Contracts/` | Shared C#/Java DTO contract types (v1 + v2 namespaces) |
| `WorldSim.RefineryAdapter/` | Anti-corruption layer: Java patch ops → Runtime commands |
| `WorldSim.RefineryClient/` | HTTP client + patch parser/applier for the Java refinery service |
| `WorldSim.ScenarioRunner/` | Headless simulation runner for balance, regression, and perf evidence |

### Test projects

| Project | Coverage area |
|---------|---------------|
| `WorldSim.Runtime.Tests/` | Simulation invariants, combat, navigation, territory, tech, building |
| `WorldSim.AI.Tests/` | Planner behavior and goal selection |
| `WorldSim.RefineryAdapter.Tests/` | Director op mapping and contract validation |
| `WorldSim.RefineryClient.Tests/` | HTTP client and patch parsing |
| `WorldSim.ScenarioRunner.Tests/` | Artifact bundle, assertion engine, baseline comparison, perf mode |
| `WorldSim.ArchTests/` | Dependency boundary enforcement (no Runtime → Graphics, etc.) |

### Other

| Path | Contents |
|------|----------|
| `refinery-service-java/` | Java Spring Boot service — LLM planner pipeline + director goal/op/output-mode API |
| `Tech/technologies.json` | Technology tree data (economy, ecology, military, fortification branches) |
| `Config/ai-policy.json` | AI policy table (faction-level planner config) |
| `Docs/Plans/Master/` | Combined execution sequencing plan and agent prompt archive |
| `Docs/Baselines/` | SMR balance baseline files (`balance-baseline.json`) |
| `AGENTS.md` | Cross-track coordination log and track scope definitions |
| `.github/workflows/smr-headless.yml` | CI: headless SMR runs in assert + perf modes on push/PR to `main` |

### Legacy

`WorldSim/` — original monolith (build artifacts only, being retired).

---

## Build and test

```powershell
# Full solution build
dotnet build WorldSim.sln

# All tests
dotnet test WorldSim.sln

# Architecture boundary checks only
dotnet test WorldSim.ArchTests/WorldSim.ArchTests.csproj

# Runtime simulation tests only
dotnet test WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj
```

---

## Headless scenario runner (SMR)

`WorldSim.ScenarioRunner` runs the simulation without MonoGame and produces structured artifact bundles for regression and balance validation.

```powershell
# Standard matrix run (3 seeds × 3 planners, 1200 ticks)
dotnet run --project WorldSim.ScenarioRunner

# Assert mode — checks survival/economy/combat invariants; exits non-zero on failure
$env:WORLDSIM_SCENARIO_ASSERT = "true"
$env:WORLDSIM_SCENARIO_ARTIFACT_DIR = ".\smr-out"
dotnet run --project WorldSim.ScenarioRunner

# Unified mode surface (standard | assert | compare | perf | all)
$env:WORLDSIM_SCENARIO_MODE = "all"
$env:WORLDSIM_SCENARIO_BASELINE_PATH = "Docs\Baselines\balance-baseline.json"
$env:WORLDSIM_SCENARIO_ARTIFACT_DIR = ".\smr-out"
dotnet run --project WorldSim.ScenarioRunner
```

### Key env vars

| Variable | Purpose | Default |
|----------|---------|---------|
| `WORLDSIM_SCENARIO_MODE` | `standard\|assert\|compare\|perf\|all` | `standard` |
| `WORLDSIM_SCENARIO_SEEDS` | Comma-separated RNG seeds | `101,202,303` |
| `WORLDSIM_SCENARIO_PLANNERS` | `simple`, `goap`, `htn` (comma-separated) | all three |
| `WORLDSIM_SCENARIO_TICKS` | Ticks per run | `1200` |
| `WORLDSIM_SCENARIO_ARTIFACT_DIR` | Output directory for artifact bundle | not set |
| `WORLDSIM_SCENARIO_BASELINE_PATH` | Path to `summary.json` baseline for compare mode | not set |
| `WORLDSIM_SCENARIO_ASSERT` | Enable assertion/invariant checks | `false` |
| `WORLDSIM_SCENARIO_PERF` | Enable per-tick timing and perf budget checks | `false` |
| `WORLDSIM_SCENARIO_COMPARE` | Enable baseline delta comparison | `false` |
| `WORLDSIM_SCENARIO_PERF_FAIL` | Exit 4 on red-zone perf anomaly | `false` |
| `WORLDSIM_SCENARIO_ANOMALY_FAIL` | Exit 4 on any anomaly | `false` |
| `WORLDSIM_SCENARIO_DELTA_FAIL` | Exit 4 on delta threshold breach | `false` |

### Artifact bundle layout

```
<artifact_dir>/
  manifest.json      # schema version, run counts, exit code, perf/assert summary
  summary.json       # full ScenarioRunEnvelope (all runs)
  assertions.json    # per-invariant assertion results (SURV/ECON/COMB/SCALE)
  anomalies.json     # detected anomalies
  compare.json       # baseline delta report (compare mode only)
  perf.json          # per-run perf metrics with budget status (perf mode only)
  run.log            # plain-text stdout capture
  runs/              # one JSON file per (config, planner, seed)
```

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | OK |
| `2` | Assertion failure |
| `3` | Config/baseline error |
| `4` | Anomaly gate fail |

---

## Dependency rules

Enforced by `WorldSim.ArchTests`:

- `Runtime` must not reference `App`, `Graphics`, or `RefineryClient`.
- `Graphics` consumes only snapshot/read-model types from `Runtime` — no mutable domain state.
- `App` is the only wiring point between `Runtime`, `Graphics`, and `RefineryAdapter`.
- `AI` must not reference `Graphics`.

---

## Java refinery service

The `refinery-service-java/` directory is a standalone Spring Boot service that provides the LLM-backed director pipeline.

```bash
cd refinery-service-java
./gradlew bootRun
```

The C# side communicates with it exclusively through `WorldSim.Contracts` types via `WorldSim.RefineryClient`. The adapter layer (`WorldSim.RefineryAdapter`) translates Java patch operations into `WorldSim.Runtime` commands.

---

## CI

`.github/workflows/smr-headless.yml` runs the scenario runner in `assert` and `perf` modes on every push/PR to `main`. Artifact bundles are uploaded with 14-day retention (extended to 30 days on failures).
