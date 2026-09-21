# Wave 11 E11-H Step 5c4 Recruitment/Mortality Diagnostic

Status: Review candidate - Meta classification pending

This package is the bounded evidence-only result for plan
`worldsim-e11h-step5c4-smr-diagnostic-plan-v1@7ef7eda`. It does not authorize a
Runtime repair, reconstruct the rejected predator hunger-gate candidate, run an
acceptance matrix, or close E11-H.

## Authority And Provenance

| Item | Identity |
|---|---|
| Routed source HEAD | `7ef7edab1be3b96777b916601b93f99b32bce69e` |
| Accepted Runtime base | `db8d18a799f12fcf39dd593e7a42f6811663c21e` |
| Accepted Runtime tree | `fc62cdd7885f6f6c3997486ba27213aee5771c03` |
| Diagnostic candidate | `worldsim-e11h-step5c4-smr-diagnostic-evidence-v1` |
| Test source blob | `dcc163272d77c950cca860b21c20b8022055caec` |
| Retained failed-candidate receipt | `.opencode-router/artifacts/e11-h-step5c4-predator-hunger-gated-capture-track-b-implementation-blocked-20260804.md` |
| Retained receipt SHA-256 | `56e7f0081d6119ce642da3fb235a0f21a4482338cabbcd1044ffa241dc3471df` |
| Canonical diagnostic payload SHA-256 | `b22addad3653eafa521162be77b74fa71cef3fdc938a747b19889f5dcb05a34d` |

The routed revision differs from the accepted Runtime base only on workflow and
governance surfaces. Preflight found no diff in production Runtime, AI,
ScenarioRunner, or the committed `Wave11LifecycleDiagnosticsTests.cs`. The
primary worktree's modified `CombatPrimitivesTests.cs` and
`Wave11AnimalLifecycleTests.cs`, and its untracked combat-intent plan, were not
copied into or read as candidate source in the isolated worktree.

The successful-capture proxy remains bounded to accepted source behavior. The
source audit found one production event emitter,
`Animal.cs -> ReportMeatFromHunt(1)`, on successful live-prey capture. The other
two production declarations only forward the amount through `World` and
`EcologyState`. `successfulPredatorCapturesProxy` is test-owned evidence, not a
new Runtime counter contract.

## Locked Execution

All runs used the same configuration except for the listed planner, seed, and
predator-human mode:

- map: `64x40`
- initial human population: `24`
- duration: `1200` ticks at `0.25` seconds
- policy: `GlobalPlanner`
- combat primitives: ON
- diplomacy: OFF
- siege: ON
- stone buildings: OFF
- birth-rate multiplier: `1`
- movement-speed multiplier: `1`
- emergency rescue policy: `disabled`
- replenishment and regrowth overrides: absent

The four canonical runs were exactly:

1. `101/Goap/OFF`
2. `101/Goap/ON`
3. `202/Simple/OFF`
4. `202/Simple/ON`

Only `202/Simple/OFF` and `202/Simple/ON` were replayed. Their complete semantic
rows and retained samples matched their canonical executions. No fifth case,
expanded 9-run matrix, full 45-run matrix, broad ScenarioRunner matrix, or
post-outcome rerun was executed.

## Signal Semantics

`firstReportedPredatorHumanHitTick` is the Runtime
`FirstPredatorHumanContactTick` value. Runtime sets it through
`ReportPredatorHumanHit`, so it means the first reported predator-human
hit/contact signal. It is not evidence of first spatial proximity, first visual
proximity, or the first possible encounter.

`firstHumanPredatorKillTick` is separate test-owned timing derived from the first
positive transition of `PredatorKillsByHumans`. Same-tick events have only tick
ordering; this package makes no intra-tick ordering claim.

Herbivore mortality uses checked accounting:

```text
initial herbivores + births + replenishment spawns - current herbivores
```

It is split into herbivore starvation, successful-capture proxy, and an
unclassified residual. Predator mortality uses:

```text
initial predators + births + replenishment spawns - current predators
```

It equals Runtime predator deaths and is split into predator starvation, human
kills, and other deaths. These equations and derived fields are diagnostic-only.

## Sampling Bound

The harness observed every tick but retained a sample only for tick zero, tick
one, a 100-tick checkpoint, the final tick, a first-zero transition, or a tick
with a relevant counter transition. Reasons on the same tick were deduplicated.

| Scope | Observed | Deterministic maximum |
|---|---:|---:|
| `101/Goap/OFF` | 519 | 1201 |
| `101/Goap/ON` | 1006 | 1201 |
| `202/Simple/OFF` | 562 | 1201 |
| `202/Simple/ON` | 995 | 1201 |
| Four-run canonical payload | 3082 | 4804 |
| Two in-memory target replays | 1557 | 2402 |

Replay samples were excluded from the canonical serialized payload. Every run
had unique, strictly increasing sample ticks and stayed within the reviewed
bound.

## Accepted-Source Results

All four accepted-source runs started with `8` herbivores and `2` predators.
Rescue, herbivore replenishment, and predator replenishment remained zero in
every run.

| Case | Final H/P | Zero H ticks / first | Zero P ticks / first | H births/starvations | P births/starvations | Captures | P deaths / human kills / other | Unclassified H mortality |
|---|---|---|---|---|---|---:|---|---:|
| `101/Goap/OFF` | `0/2` | `421 / 780` | `0 / null` | `116 / 3` | `14 / 14` | 120 | `14 / 0 / 0` | 1 |
| `101/Goap/ON` | `128/0` | `0 / null` | `210 / 991` | `297 / 9` | `11 / 0` | 167 | `13 / 13 / 0` | 1 |
| `202/Simple/OFF` | `0/2` | `313 / 888` | `0 / null` | `116 / 2` | `14 / 14` | 121 | `14 / 0 / 0` | 1 |
| `202/Simple/ON` | `31/4` | `0 / null` | `0 / null` | `271 / 11` | `18 / 1` | 236 | `16 / 15 / 0` | 1 |

The unclassified herbivore residual is consistently `1`; no negative residual
or predator accounting mismatch occurred.

### Exact First Events

| Case | Reported hit/contact | Human kill | Hunt | Grazing | H starvation | P starvation | H birth | P birth | H death | P death | First H zero | First P zero |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `101/Goap/OFF` | null | null | 1 | 1 | 410 | 583 | 1 | 81 | 1 | 583 | 780 | null |
| `101/Goap/ON` | 198 | 219 | 1 | 1 | 577 | null | 1 | 81 | 1 | 219 | null | 991 |
| `202/Simple/OFF` | null | null | 1 | 1 | 400 | 604 | 1 | 81 | 1 | 604 | 888 | null |
| `202/Simple/ON` | 96 | 100 | 1 | 1 | 840 | 1054 | 1 | 81 | 1 | 100 | null | null |

### State At First Zero

| Case | Target zero | Population H/P | Births H/P | Starvations H/P | Captures | Predator deaths / human kills | Derived H mortality / residual |
|---|---|---|---|---|---:|---|---|
| `101/Goap/OFF` at tick 780 | Herbivore | `0/13` | `116/14` | `3/3` | 120 | `3 / 0` | `124 / 1` |
| `101/Goap/ON` at tick 991 | Predator | `56/0` | `218/11` | `2/0` | 167 | `13 / 13` | `170 / 1` |
| `202/Simple/OFF` at tick 888 | Herbivore | `0/10` | `116/14` | `2/6` | 121 | `6 / 0` | `124 / 1` |
| `202/Simple/ON` | No zero species | n/a | n/a | n/a | n/a | n/a | n/a |

### ON Minus OFF Deltas

| Pair | Final H | Final P | H births | P births | H starvation | P starvation | Captures | P deaths | Human kills | Zero-H ticks | Zero-P ticks |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `101/Goap` | +128 | -2 | +181 | -3 | +6 | -14 | +47 | -1 | +13 | -421 | +210 |
| `202/Simple` | +31 | +2 | +155 | +4 | +9 | -13 | +115 | +2 | +15 | -313 | 0 |

## Retained Failed-Candidate Observations

The following values are copied only from the hash-bound failed implementation
receipt. The rejected source is absent and was not reconstructed. The receipt
does not provide births, complete death-cause accounting, or detailed event
timing, so those candidate-side values remain unavailable.

| Case | Reported final H/P | Reported first zero | Reported hits / human kills |
|---|---|---|---|
| `101/Goap/OFF` | `0/1` | H tick 784 | `0 / 0` by OFF invariant |
| `101/Goap/ON` | `0/21` | H tick 1101 | `181 / 20` |
| `202/Simple/OFF` | `0/0` | H tick 715; P tick 1168 | `0 / 0` by OFF invariant |
| `202/Simple/ON` | `141/0` | P tick 375 | `9 / 3` |

Accepted-base timing and cause data above must not be read as timing or cause data
for these retained candidate rows.

## Hypothesis Evidence For Meta

### H1 - Shared Recruitment/Mortality Bottleneck

The accepted source does not reproduce both retained target failures. It does
reproduce OFF herbivore extinction for both selected identities, despite 116
herbivore births, with 120-121 successful-capture proxy events plus 2-3
starvations and a residual of 1 accounting for 124 losses. However,
`202/Simple/ON` remains viable at `31H/4P`, with 18 predator births against 16
predator deaths. That result does not supply same-source evidence that the OFF
herbivore and retained ON seed-202 predator zero events share one current
production bottleneck.

### H2 - Independent Or Rejected-Candidate-Specific Channels

The accepted source shows distinct mode-sensitive channels:

- OFF herbivore extinction is capture-dominated in both selected identities.
- `101/Goap/ON` predator extinction occurs at tick 991 with all `13/13` predator
  deaths attributed to human kills, no predator starvation, and herbivores still
  present.
- `202/Simple/ON` receives its first reported hit/contact at tick 96 and first
  human kill at tick 100, but does not reach predator zero; it ends with 18 births,
  15 human kills, one starvation, and four predators.
- Predator extinction for `202/Simple/ON` appears only in the retained rejected
  candidate result. The bounded accepted-source evidence therefore points toward
  an independent or rejected-candidate-specific transition rather than proving a
  current shared repair seam.

This is evidence direction only. Meta owns the classification and any decision to
stop or plan a later lifecycle.

### H3 - Limitations

- The rejected candidate cannot be rerun and has no detailed birth, starvation,
  capture, or death-cause timeline.
- The selected evidence cannot prove the internal causal mechanism of the
  rejected hunger-gate change.
- `MeatFromHunt` remains a test-owned successful-capture proxy.
- The residual herbivore mortality value of 1 is nonnegative and consistent but
  remains unclassified by existing public telemetry.
- Four diagnostic runs are not an acceptance matrix and do not establish E11-H
  lifecycle health.

## Verification

The exact focused command was:

```powershell
dotnet test 'WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj' --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~Wave11SmrRecruitmentMortalityEvidenceTests' --logger 'console;verbosity=detailed'
```

Result:

- matched tests: `1`
- passed: `1/1`
- failed: `0`
- skipped: `0`
- elapsed test-run time: `44.9610` seconds
- `E11H_SMR_RECRUITMENT_MORTALITY_V1=` payload marker count: `1`
- canonical sample count: `3082`, maximum `4804`
- replay sample count: `1557`, maximum `2402`
- target replays: semantic equality PASS
- Runtime test-project warning-as-error build: PASS, `0` warnings and `0` errors

The first no-restore build in the fresh worktree stopped before compilation
because `obj/project.assets.json` did not exist. A project-scoped `dotnet restore`
created ignored build assets, after which the warning-as-error build passed. This
was worktree setup, not a candidate or simulation failure.

## Explicit Non-Claims And Stop

- No production Runtime, ScenarioRunner, existing test, AI, App, Graphics,
  governance, threshold, baseline, seeding, capacity, rescue/replenishment, or
  predator-human policy changed.
- No Runtime repair is selected or authorized.
- No rejected-candidate source was copied, reconstructed, or rerun.
- No expanded 9-run or full 45-run matrix was executed.
- No E11-H closeout, E11-I, E11-J, baseline promotion, or successor lifecycle is
  opened.
- Raw payload and test logs remain local-only.

The deterministic terminal condition is met for evidence delivery: four
canonical rows, two equal target replays, bounded samples, valid accounting,
exact provenance, one matched and passing focused test, and exactly one payload
marker. Work stops here for independent Meta review and evidence classification.
