# Wave 11 E11-H Step 5c1-A - Track B Agent Prompt

**Session type:** Track B implementation
**Status:** READY
**Target:** E11-H Step 5c1-A Runtime initial-state observability producer
**Prerequisite:** Step 5b4 diagnostic fallback accepted; failed candidates reverted/not retained

## Required Pre-Read

1. `ops/PROJECT_STATE.md`
2. `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` Step 5c1-A
3. `Docs/Plans/Master/Wave11-E11-H-Step5c-Habitat-Aware-Ecology-Seeding-And-SMR-Calibration-Plan.md`
4. `Docs/Plans/Master/Wave11-E11-H-Step5c1A-Track-B-Initial-State-Observability-Implementation-Plan.md`
5. `Docs/Evidence/Manual/Wave11-E11-H-Step5c-Manual-Observation-001.md`

## Instruction

Implement the Step 5c1-A plan exactly. This is observability-only.

Allowed files:

- `WorldSim.Runtime/Diagnostics/ScenarioEcologyTelemetry.cs`
- `WorldSim.Runtime/Simulation/World.cs`
- `WorldSim.Runtime.Tests/ScenarioEcologyTelemetryTests.cs`

Do not change initial spawn behavior, `Animal.Spawn(...)`, `RandomFreePos()`, RNG cadence, lifecycle constants, combat policy, `Person.cs`, AI, ScenarioRunner, App, or Graphics.

Capture immutable initial ecology truth after map/colonies/people/animals/EcologyState construction and before the first world tick. Explicitly measure water/invalid initial placement. Add first-event tick instrumentation only at existing authoritative report/mutation seams.

## Required Verification

```powershell
dotnet test "WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj" --filter "ScenarioEcologyTelemetryTests" --no-restore -m:1 -p:UseSharedCompilation=false
git diff --check
```

Do not run expanded/full E11-H matrices.

## Required Handoff

Return:

- exact changed files;
- exact DTO/builder names and fields;
- focused test result;
- confirmation of unchanged behavior and RNG cadence;
- nullable/empty semantics;
- targeted diff self-review;
- state continuity update;
- explicit signal that SMR Step 5c1-B remains blocked pending Meta acceptance.

Do not stage, commit, or push.
