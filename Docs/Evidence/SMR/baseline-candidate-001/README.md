# SMR Evidence - baseline-candidate-001

Date: 2026-03-10
Status: Accepted as a baseline candidate for the `small-default` smoke lane

## Run config

- Profile name: `baseline-candidate-001`
- Mode: `all`
- Output: `json`
- Seeds: `101,202,303`
- Planners: `simple,goap,htn`
- Ticks: `1200`
- Configs: `small-default` only (`64x40`, `initialPop=24`)
- Combat: `false`
- Diplomacy: `false`
- Artifact dir: `.artifacts/smr/baseline-candidate-001/`
- Baseline path during run: `none`

## Source artifacts

Primary source files for this evidence note:

- `.artifacts/smr/baseline-candidate-001/manifest.json`
- `.artifacts/smr/baseline-candidate-001/summary.json`
- `.artifacts/smr/baseline-candidate-001/assertions.json`
- `.artifacts/smr/baseline-candidate-001/anomalies.json`
- `.artifacts/smr/baseline-candidate-001/perf.json`
- `.artifacts/smr/baseline-candidate-001/drilldown/index.json`

## Healthy signals

- `9/9` runs completed successfully with exit `0:ok`.
- `0` assertion failures.
- `0` anomalies.
- `0` perf red statuses; only `1` perf yellow status across the full matrix.
- All runs ended with `4` living colonies.
- Starvation remained `0` in every run.
- Food stayed positive in every run.
- End-of-run food per person remained healthy (`35.67` to `48.25`).
- Drilldown top-3 runs all had score `0` with no flagged reasons.

## Minor caveat

- `small-default/Simple/101` was the least clean run, but still not a blocker:
  - `avgTickMs = 2.572`
  - `p99TickMs = 9.122` (yellow)
  - `maxTickMs = 59.259` (single outlier-style spike)
- This was not accompanied by anomalies, assertion failures, or broader instability.

## Decision

- Accept this run as a **baseline candidate for the `small-default` smoke profile**.
- Do **not** treat it as a broad project-wide canonical baseline yet.
- This evidence is suitable for future `compare-baseline` runs that intentionally target the same `small-default` matrix.

## What this baseline does not cover

- It does not represent `medium-default` or `standard-default` crowd/perf behavior.
- It does not cover combat-enabled runs.
- It does not establish a full-project regression gate by itself.

## Recommended next use

- Use `.artifacts/smr/baseline-candidate-001/summary.json` as the baseline path for the next `compare-baseline` run on the same `small-default` matrix.
- Keep separate evidence notes for wider matrix baselines (for example a future medium/standard or combat baseline) instead of overloading this one.
