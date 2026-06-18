# Wave 10 Step10B.5-F2-C - Target Knowledge Policy Fix

Status: accepted / closed
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F2-C is a conditional behavior-fix slice. It is only valid after F2-A/F2-B clear warrior availability/home-defense as the first main-run blocker and identify target knowledge / scout gate as the next no-launch blocker, or Meta explicitly confirms the default policy after warrior availability is healthy.

Implementation summary:
- `Stance.War` targets are baseline-known for organic campaign launch.
- `Stance.Hostile` targets remain scout-gated.
- Neutral/Tense targets remain non-launchable.
- Scout metadata remains actual scout metadata; War baseline knowledge does not fabricate scout intel.
- `CountCampaignTargetsWithScoutIntel(...)` remains a fresh actionable scout-intel metric, not a known-target metric.
- No `TargetKnowledgeSource` field was added.

Local mini-SMR evidence: `.artifacts/smr/wave10-step10b5-f2c-target-knowledge-mini-001/`.
- one seed x one planner x one hostile lifecycle config, `1200` ticks
- `manifest.json`: `exitCode=0`, `totalRuns=1`, `anomalyCount=0`
- `summary.json`: `runtimeSource=main_world_run`, `campaignLaunches=5`, `dominantNoLaunchReason=launch_applied`, `campaignTargetsWithScoutIntel=0`
- `wave10-probes.json`: absent
- artifact is local/ignored and must not be committed

## Purpose

Make organic campaign target knowledge consistent with gameplay intent after launchable warrior availability is solved. A declared war should be enough for a faction to know an enemy colony as a basic campaign target. Scout intel should improve quality and choice, not be the only possible first-launch gate under war conditions.

## Default Policy

Implement this unless Meta overrides it:

- `Stance.War` target colonies are baseline-known for minimal organic campaign launch.
- `Stance.Hostile` targets remain scout-gated unless F1 proves hostile baseline knowledge is needed too.
- Fresh scout intel remains a quality/confidence signal.
- Neutral and Tense targets remain non-launchable.

## Suggested Implementation Shape

Add a runtime-owned target knowledge resolver, for example:

- input: owner faction, target colony, stance, scout intel lookup result,
- output: known boolean, knowledge source, scout metadata.

Possible knowledge sources:

- `fresh_scout_intel`,
- `war_relation`,
- `hostile_relation`,
- `unknown`.

Use the resolver when building `CampaignTargetOption`.

Keep existing scout metadata fields intact:

- `HasScoutIntel`,
- `ScoutIntelTicksSinceRefresh`,
- `ScoutIntelConfidence`.

If a `TargetKnowledgeSource` field is exported, make it additive/default-safe.

## Guardrails

- Do not bypass owner cap.
- Do not bypass unordered pair cap.
- Do not bypass home-defense reserve.
- Do not use this slice to fix warrior availability; that is F2-A scope.
- Do not bypass route preflight.
- Do not allow same-faction launches.
- Do not make Neutral/Tense targets launchable.
- Do not change campaign score thresholds unless F1 proves score is the blocker.
- Do not directly create campaigns in lifecycle configs.

## Tests

Add focused tests for:

- War target without scout intel becomes known and can be selected if warriors/route/caps are valid.
- Hostile target behavior matches the chosen policy.
- Neutral/Tense target without scout intel remains not viable.
- Fresh scout intel still sets scout fields and remains visible in telemetry.
- No available warriors still blocks launch.
- Owner cap and pair cap still block launch.
- Route preflight failure still blocks launch.
- ScenarioRunner lifecycle pilot remains `main_world_run` and does not use side-probe evidence.

## Pilot Before Wider Work

After focused tests, run a tiny hostile lifecycle pilot before touching downstream systems:

- one seed,
- one planner,
- medium hostile config,
- 1000-2000 ticks,
- diagnostics enabled,
- no full package yet.

## Acceptance

F2 is accepted when:

- known target count is non-zero under War lifecycle preconditions,
- at least one controlled scenario can organically launch or reaches a new explicit blocker,
- old deterministic Step10B probe lanes remain separated,
- tests and build pass.

## Handoff To F3

The F2 handoff must include:

- exact policy implemented,
- whether Hostile was kept scout-gated or made baseline-known,
- launch incidence from the tiny pilot,
- any remaining blocker reason,
- tests run,
- whether F3 staged confirm can start.
