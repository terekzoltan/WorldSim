# Wave 7 S7-A Track D Contract + Monitoring Lock

Date: 2026-04-05
Owner: Track D
Consumers: Track B (runtime condition evaluation + runtime/read-model monitoring), Track A (consume-side debug UX in S7-B)

Purpose:
- Provide the minimal lock package for S7-A so Track B can implement runtime condition evaluation without contract drift.

## 1) Payload shape lock

- `causalChain` is an **optional nested field** on `addStoryBeat`.
- S7-A introduces **no new op type** for causal chains.
- Missing `causalChain` means current behavior remains unchanged.

Canonical wire shape (additive):

```json
{
  "op": "addStoryBeat",
  "opId": "...",
  "beatId": "...",
  "text": "...",
  "durationTicks": 20,
  "severity": "major",
  "effects": [ ... ],
  "causalChain": {
    "type": "causal_chain",
    "condition": {
      "metric": "food_reserves_pct|morale_avg|population|economy_output",
      "operator": "lt|gt|eq",
      "threshold": 35
    },
    "followUpBeat": {
      "beatId": "...",
      "text": "...",
      "durationTicks": 12,
      "severity": "major",
      "effects": [ ... ]
    },
    "windowTicks": 20,
    "maxTriggers": 1
  }
}
```

## 2) Metric IDs + units lock

S7-A canonical allowlist:
- `food_reserves_pct` -> range/interpretation: `0..100`
- `morale_avg` -> range/interpretation: `0..100`
- `population` -> living population count
- `economy_output` -> current runtime multiplier value

Excluded from S7-A:
- `military_strength`

## 3) Equality rule lock (`operator = eq`)

- `population`: exact equality.
- `food_reserves_pct`, `morale_avg`, `economy_output`: floating equality with tolerance
  `Math.Abs(actual - threshold) <= 0.0001`.

## 4) Trigger/window lock

- `maxTriggers` is frozen to `1` in S7-A.
- `windowTicks` bounds: `[10, 100]`.

## 5) Budget rule lock

- S7-A does **not** introduce deferred runtime budget debt.
- Combined chain budget is validator/planner-side guarded (`INV-17`).
- Runtime checkpoint budget mirror semantics remain unchanged in S7-A.

## 6) Monitoring vocabulary lock

Wire/root envelope remains unchanged:
- `schemaVersion`, `requestId`, `seed`, `patch`, `explain`, `warnings`

No new top-level response monitoring block in S7-A.

Request-scoped observability remains explain-marker based, with additive causal-chain markers:
- `causalChainOps:<n>`
- `causalChainMaxTriggers:1`
- `causalChainMetrics:food_reserves_pct,morale_avg,population,economy_output`
- `causalChainEqPolicy:population_exact;floating_tolerance=0.0001`

Aggregate Java telemetry endpoint remains:
- `GET /v1/director/telemetry`

Persistent causal-chain monitoring state in runtime/read-model is Track B scope.
