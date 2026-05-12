# Wave 10.5 Refinery TR3 Audit Gates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make TR3 convergence measurable by locking Java/C# output-mode parity, validator responsibility ownership, bridge roundtrip coverage, solver coverage honesty, snapshot mapper parity, and shared vocabulary.

**Architecture:** Java remains the proposal, validation, fallback, and Refinery-side artifact owner. C# remains bridge parser/applier/adapter/runtime-command owner. Shared vocabulary and evidence schema changes must reduce mirrored constants instead of adding another parallel list.

**Tech Stack:** Java refinery service, C# `WorldSim.Contracts`, `WorldSim.RefineryClient`, `WorldSim.RefineryAdapter`, `WorldSim.ScenarioRunner`, JUnit, xUnit.

---

## Current Scope

This plan expands the TR3 audit gates referenced from Combined Wave 10.5. It is not ready until Wave 10 closeout and Wave 8.5 sidecar foundation are complete.

Required pre-reads:
- `Docs/Plans/Master/Tools-Refinery-Migration-Plan.md`
- `Docs/Plans/Master/Tools-Refinery-Agent-Guide.md`
- `Docs/Plans/Master/Refinery-Live-SMR-Plan.md`
- `Docs/Plans/Master/Wave9-10-SMR-Closeout-Plan.md`

## File Ownership Map

- Modify: `refinery-service-java/src/main/java/com/worldsim/refinery/planner/ComposedPatchPlanner.java`
- Modify: `refinery-service-java/src/main/java/com/worldsim/refinery/planner/DirectorRefineryPlanner.java`
- Modify: `refinery-service-java/src/main/java/com/worldsim/refinery/planner/director/DirectorModelValidator.java`
- Modify: `refinery-service-java/src/main/java/com/worldsim/refinery/planner/director/DirectorDesign.java`
- Modify: `refinery-service-java/src/main/java/com/worldsim/refinery/planner/refinery/DirectorRefinerySolver.java`
- Modify: `WorldSim.Contracts/v1/PatchOp.cs`
- Modify: `WorldSim.Contracts/v2/DirectorOps.cs`
- Modify: `WorldSim.Contracts/v2/CampaignOps.cs`
- Modify: `WorldSim.RefineryClient/Serialization/PatchResponseParser.cs`
- Modify: `WorldSim.RefineryClient/Apply/PatchApplier.cs`
- Modify: `WorldSim.RefineryAdapter/Integration/RefineryPatchRuntime.cs`
- Modify: `WorldSim.RefineryAdapter/Translation/PatchCommandTranslation.cs`
- Modify: `WorldSim.ScenarioRunner/Refinery/RefineryScenarioRunner.cs`
- Test: `WorldSim.RefineryClient.Tests/*`
- Test: `WorldSim.RefineryAdapter.Tests/*`
- Test: `refinery-service-java/src/test/java/com/worldsim/refinery/**`

## Non-Negotiable Gates

- `off` output mode must suppress story, directive, and campaign/nudge ops consistently in Java and C# adapter paths.
- Every `INV-*` must be classified as exactly one of: formal Refinery artifact, bridge guard, runtime adapter guard, or retired.
- v2 director and campaign ops must roundtrip through Java fixtures and C# parsing/serialization without type drift.
- Evidence must distinguish `validated_core` from unsupported effect, bias, causal, and campaign fields.
- C# refinery snapshots must be accepted by the Java mapper without silent shape drift.
- Faction ids, treaty kinds, goal categories, domains, severities, and output-mode vocabulary must stop expanding as mirrored constants.
- Paid/live paths remain explicit opt-in and local; no default CI or generic evidence path may require paid completions.

## Task 1: Output-Mode Parity Matrix

**Files:**
- Modify: `refinery-service-java/src/test/java/com/worldsim/refinery/planner/*`
- Modify: `WorldSim.RefineryAdapter.Tests/*`
- Modify if needed: `WorldSim.RefineryAdapter/Integration/RefineryPatchRuntime.cs`

- [ ] **Step 1: Add matrix fixtures**

Build fixtures that include story, directive, and campaign ops in the same response. Exercise output modes:
- `both`,
- `story_only`,
- `nudge_only`,
- `off`.

- [ ] **Step 2: Assert Java behavior**

Java planner output must match the mode:
- `both`: story + directive + campaign/nudge allowed,
- `story_only`: story allowed, directive/campaign suppressed,
- `nudge_only`: directive/campaign allowed, story suppressed,
- `off`: no director/campaign ops.

- [ ] **Step 3: Assert C# adapter behavior**

C# adapter filtering must match Java. A `DeclareWar` or `ProposeTreaty` op surviving `off` is a failing test.

Run:

```powershell
dotnet test WorldSim.RefineryAdapter.Tests\WorldSim.RefineryAdapter.Tests.csproj --filter OutputMode --no-restore
Push-Location refinery-service-java
.\gradlew.bat test --tests "*OutputMode*"
Pop-Location
```

Expected: Java and C# parity matrix passes.

## Task 2: Validator Responsibility Matrix

**Files:**
- Create: `Docs/Plans/Master/Refinery-TR3-Validator-Responsibility-Matrix.md`
- Modify: `refinery-service-java/src/main/java/com/worldsim/refinery/planner/director/DirectorModelValidator.java`
- Test: `refinery-service-java/src/test/java/com/worldsim/refinery/planner/director/*`

- [ ] **Step 1: Inventory every invariant**

List each current `INV-*` code and every non-coded validator rule. For each row, record:
- owner category,
- current implementation file,
- desired TR3 endpoint,
- test file,
- retirement condition if any.

- [ ] **Step 2: Move only rules with a clear destination**

Do not delete an imperative validator rule until its replacement category is covered:
- formal artifact assertion,
- bridge guard,
- runtime adapter guard,
- explicit retirement with evidence.

- [ ] **Step 3: Add regression tests**

Run:

```powershell
Push-Location refinery-service-java
.\gradlew.bat test --tests "*DirectorModelValidator*"
Pop-Location
```

Expected: each moved or retained invariant has a test and a matrix row.

## Task 3: Bridge Contract Roundtrip and v2 Coverage

**Files:**
- Modify: `WorldSim.Contracts/v1/PatchOp.cs`
- Modify: `WorldSim.Contracts/v2/DirectorOps.cs`
- Modify: `WorldSim.Contracts/v2/CampaignOps.cs`
- Modify: `WorldSim.RefineryClient/Serialization/PatchResponseParser.cs`
- Test: `WorldSim.RefineryClient.Tests/*`

- [ ] **Step 1: Add fixture set**

Fixtures must include:
- v1 ops,
- `AddStoryBeat`,
- `SetColonyDirective`,
- `DeclareWar`,
- `ProposeTreaty`,
- unknown op,
- unknown tech/faction/treaty values.

- [ ] **Step 2: Assert parse/apply behavior**

Expected behavior:
- known v1/v2 ops parse deterministically,
- unknown op yields deterministic error or ignored state per policy,
- invalid faction/treaty values fail in the same layer documented by the matrix.

- [ ] **Step 3: Add serialization warning test**

If direct `System.Text.Json` polymorphic serialization remains unsupported for v2 ops, add a test or doc note that makes this explicit so future agents do not assume it works.

Run:

```powershell
dotnet test WorldSim.RefineryClient.Tests\WorldSim.RefineryClient.Tests.csproj --no-restore
```

Expected: all bridge fixtures pass with deterministic diagnostics.

## Task 4: Solver Coverage Honesty

**Files:**
- Modify: `refinery-service-java/src/main/java/com/worldsim/refinery/planner/refinery/DirectorRefinerySolver.java`
- Modify: `refinery-service-java/src/main/java/com/worldsim/refinery/planner/refinery/DirectorSolverObservability.java`
- Modify: `WorldSim.ScenarioRunner/Refinery/RefineryScenarioRunner.cs`
- Test: `refinery-service-java/src/test/java/com/worldsim/refinery/planner/refinery/*`

- [ ] **Step 1: Split coverage fields**

Evidence must report at least:
- `validated_core`,
- `unsupported_effects`,
- `unsupported_biases`,
- `unsupported_causal_fields`,
- `unsupported_campaign_fields`.

- [ ] **Step 2: Add scenario artifact assertions**

ScenarioRunner refinery evidence must preserve the split instead of flattening everything into a single success label.

- [ ] **Step 3: Run tests**

```powershell
Push-Location refinery-service-java
.\gradlew.bat test --tests "*Solver*"
Pop-Location
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --filter Refinery --no-restore
```

Expected: evidence makes unsupported coverage explicit.

## Task 5: Snapshot Mapper Parity

**Files:**
- Modify: `WorldSim.RefineryAdapter/Integration/RefineryPatchRuntime.cs`
- Modify: `refinery-service-java/src/main/java/com/worldsim/refinery/planner/director/DirectorSnapshotMapper.java`
- Test: `WorldSim.RefineryAdapter.Tests/*`
- Test: `refinery-service-java/src/test/java/com/worldsim/refinery/planner/director/*`

- [ ] **Step 1: Export C# snapshot fixtures**

Use representative C# refinery snapshots with active beats, directives, budget defaults, faction stance, campaign fields when available, and empty/minimal state.

- [ ] **Step 2: Consume fixtures in Java**

Java mapper tests must parse the C# fixture shape without silent defaulting of fields that should be required.

- [ ] **Step 3: Add drift detection**

When C# changes snapshot field names or shapes, Java tests should fail clearly.

Run:

```powershell
dotnet test WorldSim.RefineryAdapter.Tests\WorldSim.RefineryAdapter.Tests.csproj --filter Snapshot --no-restore
Push-Location refinery-service-java
.\gradlew.bat test --tests "*SnapshotMapper*"
Pop-Location
```

Expected: C# and Java snapshot mapper parity passes.

## Task 6: Shared Vocabulary

**Files:**
- Create or modify: `WorldSim.Contracts/v2/RefineryVocabulary.cs`
- Create or modify: `refinery-service-java/src/main/java/com/worldsim/refinery/contracts/RefineryVocabulary.java`
- Modify: `DirectorDesign.java`
- Modify: `PatchResponseParser.cs`
- Modify: `PatchApplier.cs`
- Modify: `PatchCommandTranslation.cs`

- [ ] **Step 1: Define one vocabulary surface**

Vocabulary must cover:
- faction ids,
- treaty kinds,
- goal categories,
- domains,
- severities,
- output modes.

- [ ] **Step 2: Replace mirrored constants**

Move validation callers toward the shared vocabulary. If generated code is not available yet, keep mirrored files with parity tests and a clear source-of-truth note.

- [ ] **Step 3: Add parity tests**

Run:

```powershell
dotnet test WorldSim.RefineryClient.Tests\WorldSim.RefineryClient.Tests.csproj --filter Vocabulary --no-restore
Push-Location refinery-service-java
.\gradlew.bat test --tests "*Vocabulary*"
Pop-Location
```

Expected: vocabulary parity is explicit and fails on drift.

## Verification Matrix

```powershell
dotnet test WorldSim.RefineryClient.Tests\WorldSim.RefineryClient.Tests.csproj --no-restore
dotnet test WorldSim.RefineryAdapter.Tests\WorldSim.RefineryAdapter.Tests.csproj --no-restore
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --filter Refinery --no-restore
Push-Location refinery-service-java
.\gradlew.bat test
Pop-Location
git diff --check
```

Closeout expected: TR3 cannot be marked done until parity, matrix, roundtrip, coverage, mapper, vocabulary, and paid/live guardrails all have passing evidence.
