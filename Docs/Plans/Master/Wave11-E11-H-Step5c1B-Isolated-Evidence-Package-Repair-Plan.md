# Wave 11 E11-H Step 5c1-B - Isolated Evidence Package Repair Plan

Status: REVIEW-FIX CYCLE IMPLEMENTED - one step-review pending; no staging or commit
Owner sequence: Meta review-fix -> Meta step-review -> closeout-commit only after final GREEN
Last updated: 2026-07-13

## Purpose

Make the functionally sound Step 5c1-B implementation independently attributable and
review-ready without packaging unrelated dirty-worktree changes. Repository durability is
established only by `/closeout-commit` after a GREEN final step-review. This repair does not
change Runtime behavior, ecology balance, seeding, lifecycle, AI, App, Graphics, refinery, or
`ECO-*` semantics.

The current functional evidence remains valid but is not formally accepted. Its README and
matching source/test contract are now prepared for one step-review, but the README must not be
called checked-in or repository-durable before closeout. Step 5c2 and all downstream work
remain blocked.

The P0 boundary, accepted Track B prerequisite classification, and base reference remain
evidence. The P0-P4 and three-preplanned-commit model is superseded as an operator workflow.

## Canonical Operator Workflow

```text
Track fix plan complete
-> one /review-javit-by-meta implementation cycle with no staging or commit
-> one /step-review
-> /closeout-commit only after FINAL STEP REVIEW SYNTHESIS GREEN
```

This review-fix cycle does not authorize a worktree, branch, stage, commit, merge,
cherry-pick, reset, stash, or push. The dirty primary worktree must not be cleaned or rewritten.
The review-ready diff remains uncommitted for the next single step-review.

## P0 Boundary Evidence

| Lock | Value |
|---|---|
| Base commit | `fee461152349d4ceefc1d44c89ecdc45173cb2c8` |
| Historical proposed repair branch | `repair/e11h-step5c1b-package` (not created; not active workflow authority) |
| Historical proposed worktree path | `C:\Users\ASUS\AppData\Local\Temp\opencode\WorldSim-e11h-step5c1b-package` (not created; not active workflow authority) |
| Historical proposed commits | 3 (superseded as operator workflow) |
| Canonical continuation | Final step-review GREEN followed by explicit `/closeout-commit` |
| Dirty `master` | Must remain untouched by repair Git operations |
| Push | Forbidden |
| Merge/cherry-pick/reset | Forbidden without a separate future decision |
| Expanded/full matrix | Out of scope |
| F3 timeline hardening | `DEFER_STEP5C5` |
| Natural-caller hardening | Separate Track B Step 5c5 gate |

The retained values classify the accepted package boundary only. They grant no Git permission
and must not be executed as a P0-P4 runbook.

## Superseded P0-P4 Boundary Model (Non-Operative)

The following sequence and commit tables are retained only to preserve the owner-safe
inclusion/exclusion research. They are not the active execution route.

| Sequence | Owner | Input | Output | Unlock |
|---|---|---|---|---|
| P0 | SMR Analyst | Meta review and current diff | Exact manifest, authority locks, durable plan | Track B confirmation |
| P1 | Track B | Meta-confirmed manifest and user authorization | Commit 1 accepted prerequisite closure | P2 |
| P2 | SMR Analyst | Reviewed Commit 1 SHA | Commit 2 Step 5c1-B package and pre-review governance | P3 |
| P3 | Meta Coordinator | Commit 1/2 stack and evidence | GREEN/YELLOW/RED re-review | P4 only on GREEN |
| P4 | Meta governance | Meta GREEN | Commit 3 governance closeout and local state update | Step 5c2 decision |

P1 and P2 are strictly serial. The SMR Analyst must not reconstruct or commit Track B-owned
Runtime prerequisites under SMR authority.

## Superseded Three-Commit Boundary Model (Non-Operative)

| Commit | Owner | Scope | Required authority |
|---|---|---|---|
| Commit 1 | Track B | Accepted E11-G and Step 5c1-A prerequisite closure | Track B confirmation plus explicit user commit authorization |
| Commit 2 | SMR Analyst | Step 5c1-B source/test/docs/evidence and pre-review F1/F2/F3 routing | Reviewed Commit 1 plus explicit user commit authorization |
| Commit 3 | Meta governance | Final verdict, SHAs, status, Step 5c2 continuation lock | P3 Meta GREEN plus explicit user commit authorization |

Commit 2 wording must remain pre-review:

```text
Step 5c1-B package reconstructed and committed; Meta re-review pending; Step 5c2 remains blocked.
```

Commit 3 may use the following only after P3 GREEN:

```text
Step 5c1-B accepted GREEN; Step 5c2 READY on the approved repair branch continuation base.
```

Commit 3 cannot record its own SHA inside its contents. Its commit object/ref is the durable
authority; its SHA/tree identity is recorded in the local operational state and final handoff.

## P0 Preflight Evidence

Read-only checks on 2026-07-11 confirmed:

- current `HEAD` equals the locked base commit;
- `repair/e11h-step5c1b-package` does not exist;
- `C:\Users\ASUS\AppData\Local\Temp\opencode` exists;
- the target worktree path does not exist;
- existing worktrees are present at the primary workspace, `WorldSim-W6I-DocsTrial`, and a
  detached Codex worktree.

Before any worktree mutation, repeat:

```powershell
git rev-parse HEAD
git worktree list --porcelain
git branch --list "repair/e11h-step5c1b-package"
Test-Path -LiteralPath "C:\Users\ASUS\AppData\Local\Temp\opencode"
Test-Path -LiteralPath "C:\Users\ASUS\AppData\Local\Temp\opencode\WorldSim-e11h-step5c1b-package"
```

Expected: locked base, branch absent, parent present, target absent.

## Manifest Schema

Every manifest row must retain these fields:

- path;
- owner;
- source step;
- exact symbol or anchor-level hunk;
- classification;
- target commit;
- verification.

Allowed classifications:

```text
PREREQUISITE_ACCEPTED
STEP5C1B_OWNED
EVIDENCE_REQUIRED
GOVERNANCE_REQUIRED
DEFER_STEP5C5
EXCLUDE_FAILED
EXCLUDE_UNRELATED
LOCAL_ONLY
```

## Commit 1 Candidate Prerequisite Manifest

All candidate rows require explicit Track B confirmation before staging. Whole-file copying
from the dirty worktree is forbidden.

| Path | Source | Exact candidate hunk | Classification | Verification |
|---|---|---|---|---|
| `WorldSim.Runtime/Simulation/Ecology/PlantBiomassModel.cs` | E11-G | `EcologySupplyCounters` and `Empty` | `PREREQUISITE_ACCEPTED` candidate | Runtime compile and supply tests |
| `WorldSim.Runtime/Simulation/Ecology/EcologyState.cs` | E11-G | supply state, four positive-amount reporters with saturating add, plus separate `ReportSupplyBridgeSkippedByNoBiomass()` | `PREREQUISITE_ACCEPTED` | Supply tests |
| `WorldSim.Runtime/Simulation/Military/ArmyForagingModel.cs` | E11-G | no-biomass classification and successful plant production reporting | `PREREQUISITE_ACCEPTED` candidate | E11-G focused tests |
| `WorldSim.Runtime/Simulation/Person.cs` | E11-G | three food-production hunks only | `PREREQUISITE_ACCEPTED` candidate | E11-G focused tests |
| `WorldSim.Runtime/Simulation/Person.cs` | Route A | predator pursuit/local threat hunk | `EXCLUDE_FAILED` | Forbidden diff audit |
| `WorldSim.Runtime/Simulation/Animal.cs` | E11-G | same-tile `TryConsumePlantFoodByAnimal(Pos, 1)` substitution plus successful predator-capture `ReportMeatFromHunt(1)` | `PREREQUISITE_ACCEPTED` | E11-G focused tests |
| `WorldSim.Runtime/Simulation/Animal.cs` | Route A | threat-time grazing, adjacent-food traversal including the traversal-owned adjacent consume call, lifecycle/predator tuning | `EXCLUDE_FAILED` | Exact diff audit |
| `WorldSim.Runtime/Diagnostics/ScenarioEcologyTelemetry.cs` | E11-G | five supply fields, empty values, timeline mapping | `PREREQUISITE_ACCEPTED` candidate | Runtime focused gate |
| `WorldSim.Runtime/Diagnostics/ScenarioEcologyTelemetry.cs` | Step 5c1-A | distance/region/initial DTOs and seven first-event fields | `PREREQUISITE_ACCEPTED` | Runtime focused gate |
| `WorldSim.Runtime/Simulation/World.cs` | E11-G | accepted supply builder/reporters/harvest/consume seams only | `PREREQUISITE_ACCEPTED` candidate | E11-G focused tests |
| `WorldSim.Runtime/Simulation/World.cs` | Step 5c1-A | cached snapshot, builder/capture helpers, constants, telemetry and event instrumentation | `PREREQUISITE_ACCEPTED` | Runtime focused gate |
| `WorldSim.Runtime/Simulation/World.cs` | Mixed/unused | unreferenced Route A helpers such as `IsActiveFoodAt(...)` | `EXCLUDE_FAILED` or `EXCLUDE_UNRELATED` | Symbol/reference audit |
| `WorldSim.Runtime.Tests/ScenarioEcologyTelemetryTests.cs` | Step 5c1-A/E11-G compatibility | exact focused test and helper set needed for 17 cases | `PREREQUISITE_ACCEPTED` | Exactly 17/17 |
| `WorldSim.Runtime.Tests/Wave11EcologySupplyBridgeTests.cs` | E11-G | named accepted tests and only reachable helpers | `PREREQUISITE_ACCEPTED` candidate | Nonzero matched, all pass |

Default Commit 1 exclusions:

- `WorldSim.AI/**`;
- `WorldSim.AI.Tests/**`;
- `WorldSim.Runtime.Tests/CombatPrimitivesTests.cs`;
- `WorldSim.Runtime.Tests/RuntimeNpcBrainTests.cs`;
- broad `Wave11AnimalLifecycleTests.cs` hunks;
- `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs`;
- failed Route A behavior;
- `WorldSim.ScenarioRunner.Tests/ArtifactBundleTests.cs`;
- `WorldSim.ScenarioRunner.Tests/Wave11EcologyEvidenceTests.cs`;
- E11-H assertion/drilldown scaffold unless a proven unresolved-symbol dependency exists;
- raw evidence matrices and `.artifacts/**`.

Animal boundary amendment accepted by Meta after the first closeout preflight: the adjacent
`TryConsumePlantFoodByAnimal(...)` call has no independent anchor in locked base `fee4611` and
is excluded with its traversal loop. The same-tile substitution and predator-capture report are
the only authorized `Animal.cs` hunks. This correction preserves the E11-G consumption counter
without importing traversal or threat-time behavior.

## Commit 1 Durable Documents

| Path | Classification |
|---|---|
| `Docs/Plans/Master/Wave11-E11-H-Step5c-Habitat-Aware-Ecology-Seeding-And-SMR-Calibration-Plan.md` | `GOVERNANCE_REQUIRED` |
| `Docs/Plans/Master/Wave11-E11-H-Step5c1A-Track-B-Initial-State-Observability-Implementation-Plan.md` | `PREREQUISITE_ACCEPTED` |
| `Docs/Plans/Master/Wave11-E11-H-Step5c1A-Track-B-Agent-Prompt.md` | `PREREQUISITE_ACCEPTED` |
| `Docs/Evidence/Manual/Wave11-E11-H-Step5c-Manual-Observation-001.md` | `EVIDENCE_REQUIRED` |
| `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` | Exact Step 5c and accepted Step 5c1-A hunks only |

## Historical P1 Boundary Detail (Do Not Execute)

Prerequisites:

- this manifest receives Track B confirmation;
- Meta confirms material equivalence;
- the user explicitly authorizes worktree/branch creation, explicit-path staging, and local
  commits;
- the clean worktree starts at the locked base.

Execution:

1. Re-run the worktree/branch/path preflight.
2. Create the repair branch/worktree only under explicit user authorization.
3. Materialize this plan in the clean worktree without material deviation.
4. Reconstruct only Track B-confirmed Commit 1 hunks.
5. Split mixed `Animal.cs`, `Person.cs`, and `World.cs` hunks exactly.
6. Add only confirmed E11-G tests and reachable helpers.
7. Run focused prerequisite verification.
8. Stage explicit Commit 1 paths only.
9. Review the full-index staged diff.
10. Commit only under explicit user authorization.
11. Re-run authoritative focused gates on committed `HEAD`.
12. Record Commit 1 SHA, tree SHA, and base-to-Commit-1 diff.
13. Return the reviewed SHA to the SMR Analyst.

Suggested message:

```text
feat(ecology): restore accepted step 5c1-a prerequisites
```

Commit 1 verification:

```powershell
dotnet test "WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj" --filter "FullyQualifiedName~ScenarioEcologyTelemetryTests" --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test "WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj" --filter "FullyQualifiedName~Wave11EcologySupplyBridgeTests" --no-restore -m:1 -p:UseSharedCompilation=false
dotnet build "WorldSim.ScenarioRunner\WorldSim.ScenarioRunner.csproj" --no-restore -m:1 -p:UseSharedCompilation=false
git diff --cached --check
```

Acceptance:

- Scenario ecology test count exactly 17/17;
- E11-G focused class has nonzero matched and all pass;
- ScenarioRunner build has zero errors;
- no forbidden path or failed Route A hunk;
- clean full-index boundary proof.

## Commit 2 SMR Manifest

| Path | Source | Exact hunk | Classification | Verification |
|---|---|---|---|---|
| `WorldSim.ScenarioRunner/Program.cs` | Step 5c1-B | cached builder read before fixtures | `STEP5C1B_OWNED` | Focused class |
| `WorldSim.ScenarioRunner/Program.cs` | Step 5c1-B | `BuildRunResult` argument | `STEP5C1B_OWNED` | Compile |
| `WorldSim.ScenarioRunner/Program.cs` | Step 5c1-B | `BuildRunResult` parameter | `STEP5C1B_OWNED` | Compile |
| `WorldSim.ScenarioRunner/Program.cs` | Step 5c1-B | direct result assignment | `STEP5C1B_OWNED` | Run/summary equality |
| `WorldSim.ScenarioRunner/Program.cs` | Step 5c1-B | nullable defaulted record field | `STEP5C1B_OWNED` | Legacy parse |
| `WorldSim.ScenarioRunner/Program.cs` | E11-H scaffold | assertion/drilldown/lane helper hunks | `EXCLUDE_UNRELATED` | Commit-1-to-2 diff |
| `WorldSim.ScenarioRunner.Tests/EcologyTelemetryArtifactTests.cs` | Accepted E11-G | supply field assertions, exit-0 tightening, deterministic supply test | `PREREQUISITE_ACCEPTED` candidate | Included in 19-case ledger |
| `WorldSim.ScenarioRunner.Tests/EcologyTelemetryArtifactTests.cs` | Step 5c1-B | artifact shape, determinism, compares, exact-null, companion, supply, env, timeline, cleanup | `STEP5C1B_OWNED` | Exactly 19/19 |
| `Docs/Plans/Master/Wave11-E11-H-Step5c1B-SMR-Initial-Ecology-Artifact-Plan.md` | Step 5c1-B | synchronized plan | `STEP5C1B_OWNED` | Durable path audit |
| `Docs/Plans/Master/Wave11-E11-H-Step5c1B-SMR-Analyst-Agent-Prompt.md` | Step 5c1-B | synchronized prompt | `STEP5C1B_OWNED` | Durable path audit |
| `Docs/Plans/Master/Wave11-E11-H-Step5c1B-Isolated-Evidence-Package-Repair-Plan.md` | Repair | this materially equivalent plan | `GOVERNANCE_REQUIRED` | Plan review |
| `Docs/Evidence/SMR/wave11-e11-h-step5c1-initial-observability/README.md` | Step 5c1-B | review-ready evidence, exact attribution anchors, and non-claims | `EVIDENCE_REQUIRED` | Step-review inclusion/wording audit; durability only in `/closeout-commit` |
| `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` | Repair | pre-review status and F3 route only | `GOVERNANCE_REQUIRED` | Wording audit |
| `Docs/Review-Findings-Registry.md` | Repair | F1/F2 package status and F3 defer route | `GOVERNANCE_REQUIRED` | Entry audit |

The 19-case ledger is locked as:

| Origin | Cases |
|---|---:|
| Base class cases preserved from `fee4611` | 8 |
| Accepted E11-G deterministic supply case | 1 |
| Step 5c1-B split compare cases | 3 |
| Step 5c1-B exact-null lifecycle theory rows | 3 |
| Step 5c1-B companion-main theory rows | 2 |
| Step 5c1-B supply-fixture invariance | 1 |
| Step 5c1-B inherited-environment isolation | 1 |
| Total | 19 |

If reconstruction produces a different count, stop and request Meta confirmation. Do not
silently rewrite the expected count.

## Historical P2 Boundary Detail (Do Not Execute)

Prerequisite: reviewed Commit 1 SHA.

Execution:

1. Verify repair worktree `HEAD` equals Commit 1.
2. Apply only the five `Program.cs` hunks.
3. Reconstruct the accepted E11-G plus Step 5c1-B focused test stack.
4. Reconstruct the durable plan/prompt/evidence chain.
5. Add pre-review Combined and registry wording; Step 5c2 remains blocked.
6. Persist F3 as `DEFER_STEP5C5` owned by SMR Analyst.
7. Keep natural-caller hardening as a separate Track B Step 5c5 gate.
8. Run the exact 19-case focused gate.
9. Generate the short local artifact from the candidate Commit 2 tree.
10. Compare deterministic values with the current draft README.
11. Stop for explicit evidence review if values differ.
12. Stage explicit Commit 2 paths only.
13. Review full-index staged diff and forbidden paths.
14. Commit only under explicit user authorization.
15. Re-run focused verification on committed `HEAD`.
16. Regenerate and compare the local artifact from exact Commit 2.
17. Update only the repair-worktree local ignored `ops/PROJECT_STATE.md`.
18. Record Commit 2 SHA/tree and Commit-1-to-2 diff for P3.

Required F3 wording in Commit 2:

```text
F3 uniquely-valued timeline mapping hardening is DEFER_STEP5C5. The SMR Analyst owns it at the Step 5c5/package closeout gate. It must be fixed, explicitly waived, or reclassified before E11-H package closeout.
```

Suggested message:

```text
test(smr): package step 5c1-b ecology evidence
```

## Commit 2 Verification

Focused test:

```powershell
dotnet test "WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj" --filter "FullyQualifiedName~EcologyTelemetryArtifactTests" --no-restore -m:1 -p:UseSharedCompilation=false
```

Acceptance: exactly 19 matched, 19 passed, 0 failed, 0 skipped. Zero matched is failure.

Local artifact profile:

```text
lane=core
mode=standard
seed=101
planner=simple
ticks=8
dt=0.25
visualLane=Headless
emergencyRescue=disabled
```

Artifact acceptance:

- process exit `0`;
- manifest `exitCode=0`;
- `assertionFailures=0`;
- `anomalyCount=0`;
- one config/planner/seed/run;
- run and summary initial ecology equal;
- exact constructor-initial provenance;
- raw artifact local-only;
- no automatic README rewrite on value drift.

Durability and attribution:

```powershell
git ls-files --error-unmatch "Docs/Evidence/SMR/wave11-e11-h-step5c1-initial-observability/README.md"
git show "<commit2>:Docs/Evidence/SMR/wave11-e11-h-step5c1-initial-observability/README.md"
git show --stat --oneline <commit2>
git rev-parse <commit2>^{tree}
git diff --full-index <commit1>..<commit2>
git ls-files -- ".artifacts/**"
```

Expected: README tracked/readable, raw artifact query empty, exact five production hunks, and
no Runtime/AI/App/Graphics change in Commit 2.

## Canonical Next Gate - One Step Review

The next and only review stage is one `/step-review`. Input:

- locked base reference and exact owner-safe anchor ledger;
- current review-ready uncommitted files;
- Runtime and E11-G focused results;
- ScenarioRunner build;
- current 19/19 result;
- local artifact manifest;
- README explicit inclusion and truthful pending-durability wording;
- durable reference audit;
- raw artifact absence;
- F3 route;
- local operational-state update;
- no-push statement.

Verdict handling:

| Verdict | Result |
|---|---|
| GREEN | `/closeout-commit` may be invoked; Step 5c2 remains blocked until that closeout completes |
| YELLOW | No commit; targeted repair/route required; Step 5c2 blocked |
| RED | No commit; repair/fix planning required; Step 5c2 blocked |

## Future Closeout Gate

Prerequisite: final step-review synthesis GREEN. The `/closeout-commit` invocation is the only
commit authorization. No commit is authorized by this plan or the review-fix cycle.

Future `/closeout-commit` exact scope:

| Path | Final hunk |
|---|---|
| Accepted Track B prerequisite files | Only the owner-confirmed E11-G and Step 5c1-A hunks required by the locked 17/17 and supply gates |
| `WorldSim.ScenarioRunner/Program.cs` | Exactly the five Step 5c1-B source anchors |
| `WorldSim.ScenarioRunner.Tests/EcologyTelemetryArtifactTests.cs` | The reviewed locked 19-case contract only |
| Step 5c/5c1-A/5c1-B plan, prompt, and manual-evidence paths | Only the reviewed owner-safe package documents |
| `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` | Step 5c1-B GREEN and Step 5c2 READY after successful closeout only |
| `Docs/Review-Findings-Registry.md` | final F1/F2 disposition; F3 and natural-caller Step 5c5 gates remain active |
| `Docs/Evidence/SMR/wave11-e11-h-step5c1-initial-observability/README.md` | Final closeout identity and repository-durable status |
| this repair plan | Meta GREEN verdict, closeout identity, continuation lock |
| `AGENTS.md` | one final cross-track handoff entry appended at file end |

Suggested message:

```text
docs(ecology): accept step 5c1-b package
```

After the closeout commit:

- record the closeout SHA/tree in local `ops/PROJECT_STATE.md` and the final handoff;
- unlock Step 5c2 only after closeout verification;
- do not push automatically.

## Historical Durable Document Set (Boundary Evidence Only)

| Path | Classification | Commit |
|---|---|---:|
| Step 5c parent plan | `GOVERNANCE_REQUIRED` | 1 |
| Step 5c1-A implementation plan | `PREREQUISITE_ACCEPTED` | 1 |
| Step 5c1-A agent prompt | `PREREQUISITE_ACCEPTED` | 1 |
| Step 5c manual evidence | `EVIDENCE_REQUIRED` | 1 |
| Step 5c1-B implementation plan | `STEP5C1B_OWNED` | 2 |
| Step 5c1-B agent prompt | `STEP5C1B_OWNED` | 2 |
| this repair plan | `GOVERNANCE_REQUIRED` | 2, closeout hunk in 3 |
| Step 5c1-B evidence README | `EVIDENCE_REQUIRED` | 2, SHA metadata in 3 |
| Combined plan | exact governance hunks | 1, 2, 3 |
| Review Findings Registry | exact governance hunks | 2, 3 |
| `AGENTS.md` | final handoff only | 3 |

Explicit committed-tree exceptions:

- `ops/PROJECT_STATE.md`: ignored local operational state only;
- `.artifacts/**`: local-only;
- `.fal/**`: absent and not created;
- `.swarm/runbooks/**`: absent and not created;
- parent `.swarm`: tooling debt outside package;
- `Docs/Evidence/SMR/e11-h-ecology-matrix/**`: outside this exact repair package.

## Future Closeout Attribution Proof

After final step-review GREEN, `/closeout-commit` records:

```text
locked base/reference -> reviewed closeout commit
```

Capture the closeout commit SHA, tree SHA, full-index diff, stat, check result, and allowed-path
audit. The commit/tree identity becomes the durable attribution authority; the review-fix
anchor ledger is the pre-commit review authority.

## Stop Conditions

Stop immediately if:

- the accepted Track B hunk boundary or Meta material-equivalence evidence changes;
- a whole dirty file would need copying;
- Route A or unrelated Track hunks appear;
- the prerequisite test count is not 17;
- the focused artifact count is not 19;
- deterministic artifact values drift without review;
- a required review-ready document is missing;
- raw artifacts enter the review-ready package;
- final step-review verdict is not GREEN when closeout is proposed;
- any push, merge, cherry-pick, reset, `git add .`, `git add -A`, or `git commit -a` is proposed.

## Software Ecology Resolution

### Liability

The repair adds no production behavior. The exact owner-safe manifest and source/test anchor
ledger replace an unreviewable interleaved package. F3 and natural-caller hardening stay out of
current source scope.

### Coupling

Track B retains Runtime/E11-G ownership. SMR remains an artifact consumer. The review-fix does
not claim ownership of unrelated dirty hunks.

### Review Load

The owner-safe prerequisite boundary, five ScenarioRunner source anchors, locked 19-case test
surface, and governance/evidence docs are independently reviewable. Material deviation or count
drift reopens planning.

### Test And Build

The review-fix uses Runtime 17-case, E11-G focused, ScenarioRunner build, and 19-case focused
gates. Expanded/full matrices remain out of scope.

### Architecture Drift

Runtime remains ecology truth owner; ScenarioRunner only serializes. No forwarding contract,
seeding, lifecycle, balance, AI, App, Graphics, or assertion policy is added.

### Prototype Isolation

Failed Route A behavior, broad lifecycle tests, assertion/drilldown scaffold, and raw matrices
are excluded. No repair branch or worktree is created by this cycle.

### Evidence

Required review proof includes the locked base reference, exact owner-safe anchor/case ledger,
focused tests/build, local artifact manifest, truthful README inclusion, raw artifact exclusion,
F3 route, local state update, and no-Git-mutation statement. Git durability proof belongs only
to the future authorized closeout.

## Review-Fix Done Criteria

The one review-fix cycle is complete when:

- the README is explicitly included and truthfully states that durability is pending closeout;
- F2 uses an independently reproducible exact anchor/case ledger instead of narrative history;
- F3 is active as `DEFER_STEP5C5` in Combined, the parent Step 5c plan, and the findings registry;
- the canonical command route is consistent in plan and operational state;
- the locked Runtime 17/17, supply-bridge, ScenarioRunner build, and ScenarioRunner 19/19 gates pass;
- raw artifacts remain local-only;
- no Git mutation occurs;
- Step 5c2 and all downstream work remain blocked pending one step-review and closeout.

## Current Readiness

| Gate | Status |
|---|---|
| Blocking review changes incorporated in this plan | GREEN |
| Locked base/reference evidence | GREEN |
| P0-P4 sequence | SUPERSEDED as operator workflow; retained as boundary evidence |
| Three-commit model | SUPERSEDED as operator workflow |
| Exact owner-safe manifest | GREEN / accepted evidence |
| Track B prerequisite confirmation | GREEN |
| Meta material-equivalence confirmation | GREEN |
| Review-fix cycle | IMPLEMENTED; accepted GREEN |
| Runtime `ScenarioEcologyTelemetryTests` | 17/17 PASS |
| Runtime `Wave11EcologySupplyBridgeTests` | 24/24 PASS; nonzero matched |
| ScenarioRunner build | PASS; 0 warnings, 0 errors |
| ScenarioRunner `EcologyTelemetryArtifactTests` | 19/19 PASS |
| Closeout ID | `wave11-e11-h-step5c1b-closeout-20260713` |
| Next gate | Verified `/closeout-commit`, then Step 5c2 Track B kickoff |
| Commit authorization | GRANTED only through the bounded closeout invocation and exact hunk manifest |
| Push authorization | FORBIDDEN |
| Step 5c2 | READY only after commit and committed-tree verification are recorded in `ops/PROJECT_STATE.md` |

The package becomes durable only through closeout ID
`wave11-e11-h-step5c1b-closeout-20260713`. Commit/tree/full-index identity is recorded in the
ignored operational state after committed-tree verification. No push is authorized.
