# Post-Wave7 Behavior Diagnosis Decision Note

Status: updated after threat-arbitration and contact-follow-through reruns
Owner: Meta Coordinator
Last updated: 2026-04-12

## 1. Context

Wave 7 campaign/director integration is technically green enough for manual and SMR closeout:

- fixture smoke healthy,
- `live_mock` healthy,
- `live_director` now reaches `directorStage:refinery-validated` and `apply=applied`,
- C# runtime/apply path and Java pipeline/gating path both have green focused tests.

The remaining problem is no longer the wire/apply boundary, but behavior quality in the simulation itself.

## 2. Manual Findings

Manual real-sim observation repeatedly showed:

- locally defensive / inert faction behavior,
- weak outward force projection,
- many `DefendSelf -> Fight/Flee` decisions in both GOAP and HTN,
- and a general sense that the simulation is not yet campaign-like even when the director path is functioning.

Important interpretation:

- this is not evidence that Wave 7 integration failed,
- it is evidence that shared runtime/AI behavior still needs diagnosis.

## 3. SMR Findings So Far

The post-Wave7 SMR work established several important facts:

1. The bad behavior is real and reproducible.
2. It is not planner-specific.
3. It is not primarily driven by the siege toggle.
4. The strongest bad lane is `standard-default / seed 101` across `simple`, `goap`, and `htn`.
5. The dominant suspicious signal is extreme backoff with weak or zero realized combat contact.

This shifted the main diagnosis question from:

- "is the Wave 7 director/apply wire correct?"

to:

- "why does the sim stay locally defensive and fail to realize contact?"

## 4. Important Technical Discovery

The first contact-realization A/B decision run used `MovementSpeedMultiplier=1.35` as the perturbation.
That run turned out to be technically invalid as a discriminator, because movement speed was not actually being applied in the intended way:

- `ScenarioRunner` set `World.MovementSpeedMultiplier`,
- actor movement mostly consumed `Colony.MovementSpeedMultiplier`,
- and key movement paths cast the multiplier to `int`, so `1.35 -> 1`.

As a result, the `default` vs `fastmove` comparison produced bit-identical or effectively identical runs.

Conclusion:

- the prior fast-move decision run was still useful as a diagnosis note,
- but it did **not** yet answer whether the root cause is movement/contact realization or threat/goal weighting.

## 5. Approved Immediate Next Step

The correct next step is:

1. fix movement multiplier application in runtime,
2. rerun the same decision package,
3. then decide between the two deeper fix directions.

Do **not** jump directly to threat/goal weighting changes before rerunning the decision package with a valid perturbation.

## 6. Decision Tree After The Reruns

This decision tree is now resolved in two stages.

### 6.1 Threat/arbitration stage

The first valid rerun showed that shared threat/arbitration was a real root cause.

Chosen fix direction:

- shared threat weighting,
- `DefendSelf` dominance,
- `Fight` vs `Flee` arbitration,
- and outward pressure suppression.

That fix landed via:

- `Docs/Plans/Master/Post-Wave7-Threat-Arbitration-Fix-Plan.md`

### 6.2 Contact follow-through stage

After the threat fix, the remaining issue narrowed to residual contact follow-through, especially on the larger standard lane.

Chosen fix direction:

- recent hostile pursue stickiness in `Fight`,
- raid-to-fight actor conversion,
- and wider combat-group pairing for recent combat intent.

That fix landed via:

- `Docs/Plans/Master/Post-Wave7-Contact-FollowThrough-Fix-Plan.md`

## 7. Current Resolution

Current conclusion after the latest reruns:

- the Wave 7 wire remains green,
- the broad threat/arbitration root cause is fixed,
- the residual contact follow-through slice also improved the bad lane,
- the previous `standard-default / htn / 101` deathless residual repro is no longer deathless,
- and both final rerun bundles finished with `exitCode=0`:
  - `planner-compare-wave7-contact-realization-medium-005`
  - `planner-compare-wave7-contact-realization-standard-005`

Residual observation:

- the larger standard lane is still more retreat-heavy than the medium lane,
- and `standard-fastmove / htn / 101` does not improve as strongly as `simple/goap`,
- but the hard zero-contact/deathless failure mode has been removed.
