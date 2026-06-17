# Wave 10 Step10B.5-F2-A - Runtime War Mobilization / Launchability

Status: accepted / closed
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F2-A is the first behavior-fix slice after accepted F1 diagnostics.

Implementation policy (Track B runtime): `SimulationRuntime.EvaluateOrganicCampaignLaunches(...)` now synchronizes persistent `PersonRole.Warrior` roles from existing runtime mobilization state before `BuildOrganicCampaignStrategyContext(...)`. This is a narrow campaign-launch minimum policy: under `ColonyWarState.Tense` or `ColonyWarState.War`, the runtime targets `max(World.GetColonyWarriorCount(...), MinimumHomeDefenseWarriors + 1)` warrior roles, capped by living home candidates. If `World.GetColonyWarriorCount(...)` is `0`, F2-A does not workaround it.

Candidate policy: prefer plain non-special actors first. Actors with `Scout`, `SupplyCarrier`, or `Commander` roles are only used as fallback after plain candidates are exhausted. Existing campaign actors, `blockedCampaignActorIds`, and transient assembly-blocked actors are excluded from the sync candidate/quota pool so role sync and `GetOrganicCampaignEligibleMembers(...)` agree on blocked/transient launchability. F2-A does not change scout/target knowledge policy, direct campaign creation, strategist scoring, route preflight, caps, or `MinimumHomeDefenseWarriors` defaults.

Residual: persistent warrior roles are intentionally not demobilized in F2-A. Demobilization/provenance is a future policy decision if it becomes necessary.

## Purpose

Make hostile/war pressure produce an organic runtime path to enough launchable warriors after home-defense reserve. The goal is gameplay correctness, not just a ScenarioRunner harness workaround.

F1 accepted evidence:
- `cf34de6 feat(wave10): add organic launch diagnostics`
- hostile lifecycle main-run smoke `.artifacts/smr/wave10-step10b5-f1-hostile-diagnostics-smoke-003/`
- dominant blocker: `no_available_warriors_after_home_defense`

## Scope

Allowed:
- runtime-owned mobilization, role assignment, eligible-member selection, or narrowly scoped home-defense policy,
- focused runtime tests,
- focused ScenarioRunner tests if artifact shape or lane behavior changes,
- small Track-owned mini-SMR after tests.

Not allowed:
- direct campaign creation,
- scout intel injection,
- target-knowledge / war-known policy changes,
- AI strategist changes without Meta reopening Track C,
- App/Graphics changes,
- broad campaign score tuning,
- weakening survival assertions.

## Implementation Questions To Answer

- Are there enough warriors in hostile lifecycle, but home-defense reserve consumes them?
- Are people not becoming warriors under war/hostile pressure?
- Are warriors excluded by campaign eligibility or active assignment filters?
- Does mobilization happen too late for the lifecycle proof window?
- Is the correct fix role/mobilization policy, reserve policy, or lifecycle precondition timing?

## Acceptance

F2-A is accepted when:
- a controlled runtime case under War/Hostile pressure has enough launchable warriors after reserve,
- neutral/non-war scenarios do not gain accidental mobilization,
- caps, route preflight, and home-defense guards still work in negative cases,
- blocked/transient candidates cannot satisfy the mobilization quota,
- hostile lifecycle diagnostics move past `ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNoAvailableWarriorsAfterHomeDefense` to a later explicit blocker or a launch attempt,
- a no-scout hostile focused proof reports `LastAvailableWarriors > 0` and `DominantNoLaunchReason != ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNoAvailableWarriorsAfterHomeDefense`,
- no direct campaign/scout/target-knowledge shortcut is introduced.

Focused runtime proof currently passes in `WorldSim.Runtime.Tests/Wave10OrganicCampaignLaunchTests`: war pressure produces an organic launch with actionable scout intel, no-scout war pressure moves to a later blocker, neutral stance does not mobilize, repeated cadence is idempotent, blocked/transient actors cannot satisfy the mobilization quota, special-role actors are avoided before fallback, and fallback only occurs after plain candidates are exhausted.

## Verification

Minimum:
- focused runtime tests for mobilization/availability,
- focused ScenarioRunner tests if lane behavior changes,
- full solution build,
- `git diff --check`,
- `pre_check_batch` on changed files.

After focused tests pass, proceed to F2-B mini-SMR/harness confidence.

## Handoff

The F2-A handoff must state:
- exact runtime policy changed,
- whether `no_available_warriors_after_home_defense` is cleared in focused proof,
- next dominant blocker if any,
- tests run,
- whether F2-B mini-SMR can run.
