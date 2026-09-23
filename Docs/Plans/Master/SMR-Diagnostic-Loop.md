# Bounded SMR diagnostic loop

Status: Owner-approved guidance for future reviewed assignments, 2026-09-16.
Existing frozen plans, run limits and active work scopes are not retroactively
expanded. This is a WorldSim operating convention within the existing AWC
lifecycle; it needs no new agent, command or router policy.

## One question, one assignment, several permitted measurements

Owner/Meta defines the decision-relevant question and scope. SMR plans a bounded
investigation. A reviewed implementation stage may contain several analysis and
measurement iterations; each measurement does not need a new lifecycle.
Use existing artifacts/source first. A deterministic replay tests reproducibility;
it is not an additional independent statistical sample.

Put this short block in the ordinary diagnostic plan:

1. Question and the project decision it will inform.
2. Source/build, effective config and comparison/control cases.
3. Existing evidence, missing signals, permitted measurement surfaces.
4. Competing explanations and observations that distinguish them.
5. Maximum rounds, explicitly enumerated cases/replays, tick and elapsed-time
   budget, and any preauthorized conditional follow-up.
6. Sufficient-answer and stop conditions; artifact and next responsible role.

Recommended starting limit: two measurement/analysis rounds, with the run budget
chosen for the question. This is not a blanket permission for two arbitrary
matrices. Replays count toward cost. A narrow one-round plan remains valid.

## Within the reviewed scope

SMR can read/reanalyze evidence, run the approved cases and conditional follow-up,
and adjust approved test-owned collection/reporting. If collector semantics
change, distinguish those versions/results. Neither a result nor an exhausted
budget authorizes Runtime changes or turns a hypothesis into proven causality.

No new Owner question for each planned measurement. Return a concrete proposal
when scope/budget must expand, product behavior must change, or acceptance and
baseline policy are at issue. Stop early when the question is answered; stop
with a useful limitation when available evidence cannot answer it.

## Lifecycle and ownership

SMR plan -> Meta plan review -> SMR plan revision -> SMR collection/analysis ->
Meta evidence review -> SMR response -> ordinary diagnostic-package closeout.

| Classification | Follow-up |
|---|---|
| Specific product defect supported | Owning Track repair plan, then SMR remeasurement |
| Collection/report defect | SMR tooling fix within scope; production instrumentation remains Track-owned |
| Balance tradeoff | Separately approved tuning hypothesis and holdout |
| Acceptable / irrelevant observation | Documented closure |
| Insufficient evidence | Bounded new question or explicit limitation |

Track B owns Runtime and existing ScenarioRunner behavior; Track C owns planner
changes. No general Runtime repair permission is added to SMR Diagnostic Delivery.
Meta reviews measurement trustworthiness and inference, not whether every animal
survived. A GREEN diagnostic can truthfully report a RED simulation outcome.

Loss channel is not automatically root cause. Preserve proxy/unclassified labels.
Changing seed and planner together cannot establish a planner effect. A repaired
case gets relevant same-config remeasurement; tuning also gets a preselected
holdout. Existing focused -> expanded -> full acceptance ordering stays in force.

## Historical E11-H planning context (2026-09-16)

At the time of this planning note, the four-case package was GREEN/ACK_ONLY,
pending its own closeout. This is historical context, not current branch status. This guidance does not authorize rerunning it. A possible next question
is why OFF herbivore births do not compensate for capture losses; WorldSim's
next reviewed plan must choose its source, signals and concrete run budget.
The old rejected candidate remains unavailable/forbidden to reconstruct.
Investigate residual mortality only if it changes a decision, not as a mandatory
purity gate for every otherwise valid result.

## Chief of Staff feedback

Recurring manual log work, missing decision-relevant signals and comparison
problems feed the SMR improvement backlog. Chief of Staff observes on request;
the project retains its current plan, source of truth and execution owner.
The [Lab](SMR-Lab-v01-Plan.md) supports this workflow but is not required to use it.
