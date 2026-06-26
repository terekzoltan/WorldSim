# Refinery RFM-D2 Runtime-Fact Authority And Fixtures

Status: RFM-D2 accepted implementation artifact
Owner: Track D primary, Track B consult
Scope: Director runtime-fact authority for `DirectorSnapshotMapper -> DirectorRuntimeFacts`

## Purpose

RFM-D2 locks the current runtime-fact authority contract used by the Java director Refinery runtime layer. It ties the Java mapper to C#-originated snapshot shape, separates canonical authority from transitional fallbacks, and records drift cases that must fail clearly during future changes.

This artifact does not promote any `.problem` predicate, retire Java validator guards, change C# runtime semantics, or prove behavior. It is authority and fixture evidence for the runtime facts consumed by the current formal runtime layer.

## Track B Consult Note

Track B consult source: RFM-D2 Track B Consult Handoff for implementation.

Track B confirmed that the authoritative C# source is `SimulationRuntime.BuildRefinerySnapshot()` with the director block assembled by private `BuildDirectorSnapshotJson()` in `WorldSim.Runtime/SimulationRuntime.cs`.

No C# runtime change is required for RFM-D2. Current C# surfaces are sufficient for Track D docs, Java fixtures, and Java mapper parity tests unless implementation discovers a concrete mismatch.

## Canonical C# Authority Fields

The canonical runtime facts for `DirectorSnapshotMapper -> DirectorRuntimeFacts` are:

| Java runtime fact | C# authority source | Notes |
|---|---|---|
| `tick` | request-level `PatchRequest.tick` | Request tick is authoritative, not a nested snapshot field. |
| `colonyCount` | `snapshot.world.colonyCount` | Must not be derived from `snapshot.director.colonyPopulation`. |
| `beatCooldownTicks` | `snapshot.director.beatCooldownRemainingTicks` | Canonical cooldown field. |
| `remainingInfluenceBudget` | request constraint override or `snapshot.director.remainingInfluenceBudget` | See budget precedence below. |
| `activeBeats[].beatId` | `snapshot.director.activeBeats[].beatId` | Canonical active beat identity. |
| `activeBeats[].severity` | `snapshot.director.activeBeats[].severity` | Java normalizes unknown severity only as transitional tolerance. |
| `activeBeats[].remainingTicks` | `snapshot.director.activeBeats[].remainingTicks` | Must be preserved for active major/epic exclusivity inputs. |
| `activeDirectives[].colonyId` | `snapshot.director.activeDirectives[].colonyId` | Valid values are `>= 0`. |
| `activeDirectives[].directive` | `snapshot.director.activeDirectives[].directive` | Nonblank directive is canonical. |

`snapshot.director.activeDirectives[].remainingTicks` exists in the C# snapshot, but current Java `DirectorRuntimeFacts.ActiveDirectiveFact` consumes only `colonyId` and `directive`.

## Request Constraint Overrides

Current C# request constraints are built by `RefineryPatchRuntime.BuildRequestConstraints(...)`.

Canonical request constraint fields:

- `constraints.maxBudget`: emitted from `RefineryRuntimeOptions.DirectorMaxBudget`.
- `constraints.outputMode`: emitted only when requested mode is not `auto`; not consumed by `DirectorRuntimeFacts`.

Budget precedence for the Java mapper is intentional current behavior:

1. `request.constraints.maxBudget` overrides snapshot budget for the current director request budget cap.
2. `request.constraints.director.maxBudget` is transitional Java fallback only; C# does not currently emit it.
3. `snapshot.director.remainingInfluenceBudget` is authoritative snapshot state only when no request budget override exists.
4. Java configured/default budget is the final fallback only; it is not C# authority.

The RFM-D2 fixture tests assert this precedence exactly. The doc terminology treats `snapshot.director.remainingInfluenceBudget` as the runtime remaining budget mirror and `constraints.maxBudget` as the request max budget constraint.

## Transitional Fallbacks

The following paths remain supported by Java compatibility tests but are not canonical C# authority:

- `snapshot.world.storyBeatCooldownTicks` legacy cooldown fallback.
- `snapshot.world.activeBeats` and `snapshot.world.activeDirectives` legacy arrays.
- `snapshot.director.colonyCount` Java fallback when `snapshot.world.colonyCount` is missing.
- Missing `colonyCount` defaulting to `1`.
- Missing active arrays mapping to empty lists.
- Missing budget defaulting to configured/default influence budget.
- `constraints.director.maxBudget` nested fallback.
- Unknown active beat severity normalizing to `minor`.

Fallback/default tests must be labeled compatibility tests and must not be used as authority proof.

## Campaign Enablement Boundary

`campaignEnabled` is not a C# request, snapshot, or runtime fact in the current RFM-D2 authority surface.

Campaign enablement authority is Java planner config: `planner.director.campaignEnabled`. RFM-D2 fixtures therefore do not include `campaignEnabled` as C# fixture metadata. If a test needs campaign-enabled behavior, it must pass that state as an external Java test/config parameter, not as a C# runtime fact.

## Fixture Corpus

Checked-in Java test resources under `refinery-service-java/src/test/resources/fixtures/director-runtime-facts/` are full `PatchRequest` JSON payloads, not isolated snapshot snippets.

Canonical current-shape fixtures:

- `canonical-current-shape-minimal.json`
- `canonical-active-cooldown.json`
- `canonical-active-major-beat.json`
- `canonical-active-epic-beat.json`
- `canonical-active-directive.json`
- `canonical-budget-request-override.json`
- `canonical-multiple-colony-world.json`
- `canonical-causal-context-present-not-consumed.json`

Canonical mapper precedence fixture:

- `canonical-budget-snapshot-only.json` covers the snapshot-authority/no-request-override mapper case. It is not the full current season-director request-shape proof because current C# request construction emits root `constraints.maxBudget` for season-director requests.

Legacy compatibility fixtures:

- `legacy-world-cooldown-fallback.json`
- `legacy-nested-budget-fallback.json`

The canonical fixtures are based on the field shape confirmed by Track B for `SimulationRuntime.BuildRefinerySnapshot()` / `BuildDirectorSnapshotJson()` and the current director request constraints.

## Must-Fail Drift Cases

Future changes must fail clearly if they introduce any of the following:

- `snapshot.world.colonyCount` and mapped `DirectorRuntimeFacts.colonyCount` diverge.
- Mapper derives colony count from `snapshot.director.colonyPopulation` instead of `snapshot.world.colonyCount`.
- Mapper ignores root `constraints.maxBudget` when both root constraint and snapshot budget are present.
- Mapper prefers nested `constraints.director.maxBudget` over root `constraints.maxBudget`.
- Mapper treats Java `planner.director.campaignEnabled` as if it came from C# snapshot/request.
- Mapper loses active beat `severity` or `remainingTicks`.
- Mapper drops valid active directives with `colonyId >= 0` and nonblank `directive`.
- Missing canonical budget, cooldown, or colony fields silently passes as authoritative proof instead of being classified as fallback/default coverage.

RFM-D2 uses a test-only canonical fixture validator to distinguish canonical authority fixtures from fallback compatibility fixtures. This keeps production fallback behavior stable while making fixture drift visible.

## Verification Evidence

Required RFM-D2 verification:

- Focused Java tests from `refinery-service-java`:
  - `./gradlew.bat test --tests "*DirectorSnapshotMapperTest*" --tests "*DirectorRuntimeFactsFixtureTest*"`
- Full Java tests from `refinery-service-java`:
  - `./gradlew.bat test`
- Diff hygiene:
  - `git diff --check`
- Scope checks:
  - no `.problem` diff,
  - no C# runtime semantic diff unless Track B explicitly approves,
  - no ScenarioRunner diff,
  - no paid/live environment usage.

## RFM-D3 Handoff

RFM-D2 only locks runtime-fact authority. It does not decide which predicate family is promoted next.

After RFM-D2 review acceptance, RFM-D3 may choose a predicate-promotion family with less risk because budget, colony count, cooldown, active beat, and active directive inputs now have documented authority and fixture-backed parity.
