# SMR Minimum Ops Checklist

Purpose:
- Give sessions a lightweight, repeatable operating pattern for SMR without opening a new Wave.
- Standardize how evidence runs are named, where artifacts go, and how results are summarized.
- Keep this intentionally small: operational discipline first, larger reporting/workflow upgrades later.

Status:
- Active guidance
- Non-wave operational layer

---

## 1. Standard run profiles

Use these names by default unless there is a reason not to:

- `all-around-smoke` -- general health check across multiple seeds/planners/configs
- `clustering-deep` -- medium/standard configs, longer ticks, clustering/backoff focus
- `planner-compare` -- planner comparison on the same seed/config matrix
- `baseline-candidate` -- clean run intended to become or refresh a canonical baseline
- `compare-baseline` -- same matrix as baseline, explicit compare focus
- `combat-smoke` -- combat-enabled sanity run, separate from peaceful evidence
- `perf-long` -- longer perf-oriented run, usually with `WORLDSIM_SCENARIO_PERF=true`

Suggested naming pattern:

- `<profile>-001`
- `<profile>-002`
- `<profile>-wave5-a`

Artifact directory pattern:

- `.artifacts/smr/<run-name>/`

---

## 2. Minimum operator checklist

Before running:

1. Choose the run profile name.
2. Decide if the goal is:
   - smoke,
   - clustering investigation,
   - planner comparison,
   - baseline creation,
   - compare/regression,
   - perf.
3. Set a repo-local artifact directory under `.artifacts/smr/`.
4. Decide whether baseline compare is actually available. If not, say so explicitly.

After running:

1. Confirm artifact location.
2. Read first:
   - `manifest.json`
   - `summary.json`
   - `anomalies.json`
3. If enabled, also read:
   - `assertions.json`
   - `compare.json`
   - `perf.json`
   - `drilldown/index.json`
4. Rank the worst runs instead of only listing aggregate averages.
5. Say clearly:
   - what looks healthy,
   - what looks suspicious,
   - what remains unknown,
   - what run should happen next.

---

## 3. Minimum report format

Every SMR session report should use this structure:

1. `Run config`
   - profile name
   - seeds
   - planners
   - ticks
   - configs
   - mode
   - artifact dir
   - baseline path or "none"
2. `Healthy signals`
   - what passed / stayed stable
3. `Suspicious signals`
   - concrete issues, ordered by severity
4. `Worst runs ranked`
   - top 3-5 with reasons
5. `Unknowns`
   - what this run cannot answer
6. `Suggested next run`
   - one recommended next profile, not five competing ideas by default

---

## 4. Baseline operating rule

- Do not treat a run as a regression run unless there is an explicit baseline path.
- Do not create a canonical baseline from a suspicious or partially reviewed run.
- Preferred canonical baseline minimum:
  - seeds: `101,202,303`
  - planners: `simple,goap,htn`
  - clean assertions
  - reviewed anomalies

---

## 5. Deferred larger improvements

These are intentionally deferred and should be handled after the current wave closeouts, not folded into routine SMR operation ad hoc:

- richer summary/report synthesis layer
- severity scoring / ranked composite health signals
- baseline lifecycle tooling beyond manual protocol
- standard analyst-friendly comparison dashboards
- replay/drilldown consumers beyond raw JSON
- eventual visual `SMR Lab`

These belong to the later SMR operational/productization backlog, not the minimum checklist.
