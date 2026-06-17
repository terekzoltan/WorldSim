# Wave 10 Step10B.5-F2-A - Runtime War Mobilization / Launchability

Status: ready
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F2-A is the first behavior-fix slice after accepted F1 diagnostics.

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
- hostile lifecycle diagnostics move past `no_available_warriors_after_home_defense` to a later explicit blocker or a launch attempt,
- no direct campaign/scout/target-knowledge shortcut is introduced.

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
