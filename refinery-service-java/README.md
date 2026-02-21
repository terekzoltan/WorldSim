# refinery-service-java

Local Java 21 service for the WorldSim polyglot monorepo.

It receives a simulation snapshot and returns a deterministic patch/delta over HTTP.
Current default planner is deterministic mock; pipeline mode can enable a minimal Refinery-backed TECH_TREE slice.

## Prerequisites

- JDK 21

## Run

```bash
./gradlew bootRun
```

Default port is `8091`.
Override with environment variable `REFINERY_SERVICE_PORT`.

Planner mode defaults to deterministic mock:

- `PLANNER_MODE=mock` (default)
- `PLANNER_MODE=pipeline` (scaffolded LLM -> Refinery chain with deterministic fallback)
- `PLANNER_REFINERY_ENABLED=false` (default)
- `PLANNER_LLM_ENABLED=false` (default)

When `PLANNER_MODE=pipeline`, responses include an explicit explain marker:

- `refineryStage:enabled` if Refinery stage is enabled
- `refineryStage:disabled` if Refinery stage is off

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
- Supported operations now:
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

- `MockPlanner` exists now for deterministic scaffolding.
- TODO next:
  1. `LlmPlanner` proposes candidate patch.
  2. `RefineryPlanner` validates/repairs candidates against constraints.
  3. Compose as pipeline: `LLM -> Refinery validation/repair -> PatchResponse`.

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

### Current Refinery slice

- Enabled with `PLANNER_MODE=pipeline` and `PLANNER_REFINERY_ENABLED=true`.
- Scope is intentionally narrow: `TECH_TREE_PATCH` with `addTech` operations.
- Invariants currently enforced in this slice:
  - `addTech.techId` must be known.
  - `addTech.prereqTechIds` must reference known tech IDs.
  - Self-prereq is removed.
  - `cost.research` is repaired to `80` if missing or non-positive.
- Non-`addTech` operations under `TECH_TREE_PATCH` are rejected by this slice.

### Season Director next slice (planned)

- Planned Track D direction is a checkpoint-based Season Director goal that can emit:
  - `story beat`
  - `planner nudge`
- Planned output gating modes: `both`, `story_only`, `nudge_only`, `off`.
- LLM stage remains optional; formal Refinery gate remains mandatory for accepted output in director pipeline mode.
- Detailed implementation plan is documented in:
  - `WorldSim.RefineryAdapter/Docs/Plans/Track-D-Season-Director-Plan.md`

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
