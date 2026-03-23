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

- `refineryStage:enabled` / `refineryStage:disabled`
- `directorStage:<...>`
- `directorOutputMode:<both|story_only|nudge_only|off>`
- `llmRetries:<n>`
- `budgetUsed:<decimal>`

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
- For quick local observability, query telemetry counters:

```bash
curl http://localhost:8091/v1/director/telemetry
```

## Test

```bash
./gradlew test
```

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
- Deterministic behavior means same `goal + seed + tick` produces same output.
- `tweakTech` is delta-based, so clients must dedupe by `opId` and apply each op at most once.
- The contract is versioned via `schemaVersion` (`v1`) for forward compatibility.

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
