# refinery-service-java

Local Java 21 service for the WorldSim polyglot monorepo.

It receives a simulation snapshot and returns a deterministic patch/delta over HTTP.
The service supports both baseline deterministic mock output and the Season Director pipeline (`LLM -> formal validation/repair -> deterministic fallback`).

## Prerequisites

- JDK 21

## Run

```bash
./gradlew bootRun
```

Default port is `8091`.
Override with environment variable `REFINERY_SERVICE_PORT`.

Planner env profile:

- `PLANNER_MODE=mock` (default)
- `PLANNER_MODE=pipeline` (LLM -> formal validator -> bounded retries -> deterministic fallback)
- `PLANNER_REFINERY_ENABLED=false` (default)
- `PLANNER_LLM_ENABLED=false` (default)
- `PLANNER_LLM_API_KEY=` (required for live LLM calls)
- `PLANNER_LLM_BASE_URL=https://openrouter.ai/api/v1`
- `PLANNER_LLM_MODEL=openai/gpt-4o-mini`
- `PLANNER_LLM_TIMEOUT_MS=3000`
- `PLANNER_LLM_HTTP_REFERER=https://worldsim.local`
- `PLANNER_LLM_APP_TITLE=WorldSim`
- `PLANNER_LLM_TEMPERATURE=0.4`
- `PLANNER_LLM_MAX_TOKENS=500`
- `PLANNER_DIRECTOR_OUTPUT_MODE=both` (default Java-side director output mode)
- `PLANNER_DIRECTOR_MAX_RETRIES=2` (iterative correction retries)
- `PLANNER_DIRECTOR_BUDGET=5.0` (influence budget limit for director checkpoints)

When `PLANNER_MODE=pipeline`, responses include explicit explain markers:

- `directorStage:<...>` (Season Director pipeline stage)
- `refineryStage:enabled` / `refineryStage:disabled` (legacy non-director slice)
- `directorOutputMode:<both|story_only|nudge_only|off>`
- `llmStage:<disabled|missing_config|candidate|parse_failed|request_failed>`
- `llmCompletionCount:<n>` (actual OpenRouter completion calls inside this `/v1/patch`)
- `llmRetryRounds:<n>` (validator feedback/correction rounds)
- `llmRetries:<n>` (legacy alias, same value as `llmRetryRounds`)
- `llmCandidateSanitized:<true|false>`
- `llmCandidateSanitizeTags:<comma-separated-tags>`
- `budgetUsed:<decimal>`
- `causalChainOps:<n>` (number of `addStoryBeat` ops carrying optional nested `causalChain`)
- `causalChainMaxTriggers:1`
- `causalChainMetrics:food_reserves_pct,morale_avg,population,economy_output`
- `causalChainEqPolicy:population_exact;floating_tolerance=0.0001`

## Director Live Smoke Notes (Wave 6.1)

Recommended Java-side local profile:

```bash
export PLANNER_MODE=pipeline
export PLANNER_REFINERY_ENABLED=true
export PLANNER_LLM_ENABLED=true
export PLANNER_LLM_API_KEY=<your-openrouter-key>
./gradlew bootRun
```

Important behavior:

- One manual `F6` on the C# side may trigger `1..(PLANNER_DIRECTOR_MAX_RETRIES+1)` OpenRouter completions inside one `/v1/patch` request.
- This is expected: iterative correction runs inside a single director request lifecycle.
- `llmStage` should now distinguish disabled configuration from parse/request failures in the LLM path.
- `llmCompletionCount` tells how many completions actually happened for this request.
- `llmRetryRounds` tells how many validator retry rounds were used.
- Java telemetry differentiates:
- `retryAttemptsTotal` = extra LLM completion attempts beyond the first completion in a request
- `validationRetryRoundsTotal` = validator retry rounds used by the director refinement loop
- `llmCandidateSanitized` + `llmCandidateSanitizeTags` show whether Java-side planner normalization repaired the LLM candidate before validation.
- For quick local observability, query telemetry counters:
- Final Wave 6.1.1 manual regression matrix + pass/fail rules: `Docs/Wave3-Director-Smoke-Checklist.md`.

```bash
curl http://localhost:8091/v1/director/telemetry
```

## Smoke lanes (S7-B)

Use these lane names consistently in operator docs/checklists:

- `java_planner_smoke`: Java-only `/v1/patch` + explain marker verification.
- `full_stack_smoke`: manual app/runtime (`F6`) + adapter/runtime status verification.

Preset naming alignment (app/operator side):

- `fixture_smoke`
- `live_mock`
- `live_director`

Note:

- helper scripts are convenience tooling, not source-of-truth for contract semantics.
- `run-smoke.ps1` / `check-markers.ps1` validate the Java planner smoke lane only.
- full-stack smoke still requires running the C# app and verifying HUD/settings/apply state manually.

## Test

```bash
./gradlew test
```

## TR1-A tools.refinery spike

Wave 6.2 TR1-A adds the first minimal `tools.refinery` proof inside this service codebase.

- Gradle setup now uses `tools.refinery.settings` in `settings.gradle.kts` and `implementation(refinery.generator)` in `build.gradle.kts`.
- First problem artifact lives at `src/main/resources/refinery/director/tr1a-spike.problem`.
- Repeatable proof test: `src/test/java/hu/zoltanterek/worldsim/refinery/planner/ToolsRefinerySpikeTest.java`.
- The proof test loads the `.problem` artifact, runs local generation with `StandaloneRefinery`, and reads relation facts from `Directory::children`.

This is intentionally a minimal spike. It does not replace the production director pipeline yet.

## TR1-B artifact layout policy

Wave 6.2 TR1-B formalizes artifact layout/ownership without changing production flow.

- Canonical root: `src/main/resources/refinery/`
- Family split: `common/`, `director/`, `combat/`, `campaign/`
- Historical spike remains: `director/tr1a-spike.problem`
- Canonical TR1-C targets are reserved only: `director/design.problem`, `director/model.problem`, `director/runtime.problem`, `director/output.problem`
- Minimal catalog ownership in Java: `RefineryArtifactFamily` + `RefineryArtifactCatalog`

## TR1-C director family skeleton

Wave 6.2 TR1-C materializes the first canonical director artifact family files:

- `src/main/resources/refinery/director/design.problem`
- `src/main/resources/refinery/director/model.problem`
- `src/main/resources/refinery/director/runtime.problem`
- `src/main/resources/refinery/director/output.problem`

These files define layer boundaries and designated output-area ownership.
Production director planning flow remains unchanged in this step.

## TR1-D structured assertion-candidate ingest

- Canonical LLM candidate shape is assertion-oriented under `designatedOutput` (not patch-op oriented).
- Canonical slots are `storyBeatSlot` and `directiveSlot` with presence-driven semantics.
- Runtime-fact authority for candidate normalization is the `DirectorSnapshotMapper` -> `DirectorRuntimeFacts` path.
- Wire-level patch output (`v1` today) remains a bridge contract in this step.

## TR1-E bridge contract policy

- Java director proposal/assertion handling and bridge DTO mapping now have an explicit seam.
- Director bridge mapping to wire-level `PatchOp` is isolated in a dedicated mapper.
- `PatchResponse` stays the transport/output bridge contract for C# consumers, not the primary internal ontology.
- Transitional note: validator/fallback logic still using `PatchOp` is explicitly transitional in TR1-E.

## S7-A causal-chain contract lock

- `causalChain` is an optional nested field on `addStoryBeat` (no new op type).
- Wire/root envelope remains unchanged (`schemaVersion`, `requestId`, `seed`, `patch`, `explain`, `warnings`).
- Canonical causal condition metrics in S7-A:
  - `food_reserves_pct` (0..100)
  - `morale_avg` (0..100)
  - `population` (living population count)
  - `economy_output` (runtime multiplier)
- `military_strength` is intentionally excluded from S7-A allowlist.
- `maxTriggers` is frozen to `1` in S7-A.
- `eq` semantics lock:
  - `population` uses exact equality (integer threshold expected)
  - `food_reserves_pct`, `morale_avg`, `economy_output` use tolerance `Math.Abs(actual - threshold) <= 0.0001`
- Budget policy lock:
  - no deferred runtime debt in S7-A
  - causal-chain combined cost is validator/planner-side guarded (`INV-17`)
  - runtime checkpoint budget mirror semantics stay unchanged

## API

### `GET /health`

Returns:

```json
{"status":"ok","version":"0.1.0"}
```

Example:

```bash
curl http://localhost:8091/health
```

### `POST /v1/patch`

Request DTO: `PatchRequest`

- `schemaVersion` string, currently `"v1"`
- `requestId` string UUID from caller (echoed back)
- `seed` long for deterministic planning
- `tick` long simulation tick
- `goal` enum: `TECH_TREE_PATCH`, `WORLD_EVENT`, `NPC_POLICY`
- director goal: `SEASON_DIRECTOR_CHECKPOINT`
- `snapshot` object (`JsonNode`) required
- `constraints` object (`JsonNode`) optional

Example request:

```bash
curl -X POST http://localhost:8091/v1/patch \
  -H "Content-Type: application/json" \
  -d '{
    "schemaVersion": "v1",
    "requestId": "49e95c3f-8df6-45b5-8f47-7d2be30c23f3",
    "seed": 123,
    "tick": 42,
    "goal": "TECH_TREE_PATCH",
    "snapshot": {
      "world": "minimal"
    }
  }'
```

Example response:

```json
{
  "schemaVersion": "v1",
  "requestId": "49e95c3f-8df6-45b5-8f47-7d2be30c23f3",
  "seed": 123,
  "patch": [
    {
      "op": "addTech",
      "opId": "op_N9AA4H5WVBI2",
      "techId": "agriculture",
      "prereqTechIds": ["woodcutting"],
      "cost": {"research": 80},
      "effects": {"foodYieldDelta": 1, "waterEfficiencyDelta": 1}
    }
  ],
  "explain": [
    "MockPlanner produced a deterministic response for pipeline testing.",
    "Given the same goal, seed, and tick, this service returns the same patch."
  ],
  "warnings": []
}
```

## Patch semantics

- Patch operations are modeled as a discriminated union via `op`.
- Every patch operation includes deterministic `opId` for idempotent client-side dedupe.
- `opId` is derived from stable inputs only: `goal` string + `seed` + `tick` + op stable key.
- Supported operations:
  - `addTech`
  - `tweakTech`
  - `addWorldEvent`
  - `addStoryBeat`
  - `setColonyDirective`
- Deterministic behavior means same `goal + seed + tick` produces same output.
- `tweakTech` is delta-based, so clients must dedupe by `opId` and apply each op at most once.
- The current wire contract is versioned via `schemaVersion` (`v1`) for forward compatibility.

## Architecture

Package root: `hu.zoltanterek.worldsim.refinery`

- `controller/` HTTP endpoints and API error handling
- `service/` orchestration and request checks
- `planner/` planner interfaces and implementations
- `model/` DTOs and patch operation types
- `util/` deterministic helpers and schema validator

`PatchPlanner` is the extension seam:

- `MockPlanner` provides deterministic baseline output.
- Pipeline mode composes LLM proposals with formal validator/repair and fallback behavior.

## How this integrates with C# MonoGame

- MonoGame side serializes world snapshot and sends it to `POST /v1/patch`.
- Service returns deterministic patch operations + explanation strings.
- C# applies patch ops to its local simulation state.
- In next iteration, C# can also send optional constraints, and Java service will enforce them with Refinery.

### C# mapping notes

- Map `goal` to C# enum values: `TECH_TREE_PATCH`, `WORLD_EVENT`, `NPC_POLICY`.
- Read `patch[].op` discriminator and deserialize per concrete op payload.
- Persist or short-term cache applied `opId`s and skip duplicates.
- Keep `schemaVersion` check on the C# side to reject unknown contracts early.

### C# integration runtime

- Runtime setup, env switches, and debug playbook are documented in `WorldSim.Runtime/Integration/README.md`.
- Typical local flow:
  1. Start this Java service: `./gradlew bootRun`
  2. Run C# with `REFINERY_INTEGRATION_MODE=live`
  3. Trigger patch from game with `F6`
  4. Optional parity check: `REFINERY_PARITY_TEST=true` then `dotnet test WorldSim.RefineryClient.Tests/WorldSim.RefineryClient.Tests.csproj`

### Current slices

- `TECH_TREE_PATCH` slice:
  - Enabled with `PLANNER_MODE=pipeline` and `PLANNER_REFINERY_ENABLED=true`.
  - Scope: `TECH_TREE_PATCH` with `addTech` operations.
  - Invariants include known IDs, prereq checks, and deterministic repair for invalid research cost.
- `SEASON_DIRECTOR_CHECKPOINT` slice:
  - Supports story beats + colony directives with output mode gating.
  - Includes budget-aware validation (`budgetUsed` explain marker) and iterative correction loop.
  - Runtime-safe contract for Wave 6.1 keeps story effect duration aligned to parent beat duration.

For runtime/HUD-side smoke interpretation, see `WorldSim.Runtime/Integration/README.md`.

### Deprecated wording notice

Older references in this repo that describe the director as "planned" are historical planning artifacts.
The live director path is implemented; smoke and ops should follow the Wave 6.1 guidance.

### Director design and implementation docs

- `WorldSim.RefineryAdapter/Docs/Plans/Track-D-Season-Director-Plan.md`
- `Docs/Plans/Master/Director-Integration-Master-Plan.md`

## Fixtures for integration tests

- Fixture folder: `refinery-service-java/examples/`
- Valid request fixtures: `refinery-service-java/examples/requests/`
- Expected deterministic responses: `refinery-service-java/examples/responses/`
- Negative request fixtures: `refinery-service-java/examples/negative/requests/`
- JSON Schemas: `refinery-service-java/examples/schema/`

## Further steps roadmap

### Phase 1

- Keep current mock planner endpoint and complete C# HTTP wiring + DTO mapping.

### Phase 2

- Publish JSON Schema for request/response and add contract compatibility notes.

### Phase 3

- Add planner pipeline composition:
  - `LlmPlanner` stage proposes candidate patch.
  - `RefineryPlanner` stage validates and repairs.
  - Return only contract-valid patch operations.
- For LLM stage testing, avoid golden-output assertions. Test contract invariants + Refinery validity instead.

### Phase 4

- Add structured observability with required log fields: `requestId`, `goal`, `seed`, `tick`.
- MDC is configured so these fields appear automatically in log lines.

### Phase 5

- First idempotency strategy: deterministic `opId` + client-side dedupe.
- Add server-side `idempotencyKey` only if needed, because it introduces server statefulness.
