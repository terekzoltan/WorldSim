# Tools.Refinery Agent Guide

Purpose:
- Give every WorldSim track/agent/session a common reference for what `tools.refinery` actually is.
- Record the intended target direction for the refinery/director subsystem based on the official Refinery docs, the local OnlabRefinery documentation, and the local OnlabRefinery gitingest snapshot.
- Prevent further drift between "OnlabRefinery-inspired imperative Java validation" and "actual tools.refinery-based formal modeling" in future planning and implementation.

Status:
- This document is reference and alignment material only.
- It does not by itself change any current implementation plan or code.
- Existing WorldSim director code is still mostly imperative Java validation plus C# runtime application, not true `tools.refinery` integration yet.

Mandatory usage rule:
- Any track/agent/session that plans, reviews, implements, or refactors any refinery-related artifact must read this whole document first.
- This includes, at minimum: Refinery model files, Java refinery service files, director/refinery plans, adapter/refinery integration code, and any new formal-model or designated-output-area design work.
- The reader must also open and read the linked official Refinery pages referenced in the `Official References` section, not only this local summary.
- When a plan or implementation step says that refinery files/models/specifications must be created or changed, this document is a required pre-read.

## Official References

Primary official sources:
- Refinery homepage: <https://refinery.tools/>
- Introduction: <https://refinery.tools/learn/>
- Language reference index: <https://refinery.tools/learn/language/>
- Classes and references: <https://refinery.tools/learn/language/classes/>
- Partial modeling and four-valued logic: <https://refinery.tools/learn/language/logic/>
- Graph predicates: <https://refinery.tools/learn/language/predicates/>
- Java programming guide: <https://refinery.tools/develop/java/>
- Online solver/editor: <https://refinery.services/>

Local reference sources in this repo:
- `OnlabRefineryDocumentation.txt`
- `OnlabRefineryDocumentation.pdf`
- `gitingest_OnlabRefinery.txt`
- `Docs/Plans/Master/Tools-Refinery-Migration-Plan.md`
- `Docs/Plans/Master/Director-Integration-Master-Plan.md`

## What tools.refinery Actually Is

Refinery is not just a validator helper. It is:
- a graph solver,
- a partial modeling language,
- a model generation framework,
- and a Java library / service / web UI stack around these capabilities.

The key official ideas relevant to WorldSim are:
- metamodeling with classes, references, containment, multiplicity, inheritance,
- partial models using four-valued logic (`true`, `false`, `unknown`, `error`),
- assertions over partial models,
- scopes for model generation,
- graph predicates for querying and validation,
- error predicates for explicitly marking forbidden states,
- and generator execution that refines partial models into consistent concrete models.

This matters because actual `tools.refinery` is not "Java code that checks invariants after parsing JSON". It is a formal modeling environment where:
- the model is expressed in the Refinery language,
- uncertainty is explicit,
- constraints are part of the formal model,
- and solving / refinement happens in the Refinery engine.

## Core Language Concepts

### 1. Classes And References

Refinery supports an EMF/Xcore-like structural layer:
- `class`, `abstract class`, `extends`
- references with multiplicity
- `contains` / `container`
- `opposite`
- attributes like `string`, `boolean`, `int`, `real`

This gives the stable graph vocabulary.

WorldSim implication:
- the future director formal model should define its world-intervention vocabulary here, not only in Java enums/constants.

### 2. Partial Modeling

Refinery works with partial models using four-valued logic:
- `true`: known fact
- `false`: known absence
- `unknown`: unresolved design space
- `error`: contradiction / invalid state

Important consequence:
- the formal model does not need to be fully concrete before solving.
- uncertainty is a first-class thing, not an ad hoc "missing JSON field" situation.

WorldSim implication:
- director checkpoint generation should move from "LLM emits final patch ops directly" toward "LLM contributes assertions into a partial model, then Refinery resolves/validates the designated output area".

### 3. Assertions

Assertions add or constrain facts in the model.
Examples from Refinery docs:
- positive assertions,
- negative assertions with `!`,
- explicit unknowns with `?`,
- default assertions with lower priority,
- attribute assertions,
- node/object existence and equality controls.

WorldSim implication:
- the LLM output area should ideally be a bounded assertion set, not free-form Java DTO composition.

### 4. Predicates And Error Predicates

Refinery predicates are formal logic queries/constraints over the graph.
They can be used for:
- querying,
- derived features,
- validation,
- and explicit error marking.

`error` predicates are especially important:
- they express states that must have no matches in a valid model.

WorldSim implication:
- many current Java-side validator invariants should ultimately become named formal predicates / error predicates in the Refinery model, instead of only imperative checks.

### 5. Scopes And Generation

Refinery uses `scope` declarations to constrain model size and search space.
This is a real model generation feature, not just a validator convenience.

WorldSim implication:
- future director modeling can use explicit scope limits for interventions, candidate entities, output slots, or relation cardinalities instead of only Java-side max-count constants.

## What The OnlabRefinery Pattern Actually Means

From `OnlabRefineryDocumentation.txt` and `gitingest_OnlabRefinery.txt`, the important pattern is:

### Separation of responsibilities
- LLM: proposal generator only.
- Refinery: formal quality gate and solver.
- Runtime/app: consumer of already validated output.

### Layered model architecture
- `design`: stable framing, vocabulary, skeleton, read-only for the LLM.
- `model`: constraints, schema, legal combinations.
- `runtime`: current problem facts / current world state / active context.

### Designated output area
- The LLM must not rewrite the whole model.
- It gets a bounded editable region.
- Only that region is allowed to vary between attempts.

### Iterative correction loop
- LLM proposes.
- Refinery validates.
- If invalid, targeted feedback goes back.
- LLM retries only on the designated area.
- After bounded retries, deterministic fallback is allowed.

This is the most important OnlabRefinery lesson for WorldSim:
- The point is not merely "LLM + some validation".
- The point is "LLM modifies only a formal, bounded, solver-controlled fragment of a layered model".

## Where WorldSim Currently Differs

Current WorldSim reality:
- Java parses LLM JSON into candidate patch ops.
- Java validator repairs/checks these patch ops imperatively.
- C# adapter/runtime applies translated commands.

This is useful and pragmatic, but it is not actual `tools.refinery` integration.

Specifically, the current system is still missing these tools.refinery characteristics:
- the formal model is not authored in the Refinery language,
- constraints are not primarily expressed as Refinery predicates/error predicates,
- the LLM is not writing into a designated assertion area inside a Refinery model,
- solving/refinement is not done by the Refinery engine,
- partial-model semantics are not first-class,
- runtime/design/model layers are conceptually present but not yet materially represented as actual Refinery artifacts.

## Target Direction For WorldSim

If WorldSim moves to true `tools.refinery` usage, the target should be this:

### 1. Formal artifacts become first-class project assets

We should expect dedicated Refinery model files, not only Java classes.
Conceptually these should cover:
- design layer,
- model layer,
- runtime facts layer,
- designated output area.

These files should become part of the repo and planning discipline just like runtime contracts or schemas.

### 2. The LLM should emit assertions, not final patch commands

Preferred long-term shape:
- the LLM returns a bounded assertion fragment for the designated output area,
- not direct world patch DTOs as the authoritative artifact.

Then:
- Refinery checks satisfiability and invariants,
- derives validated output facts,
- and only then the adapter maps validated output facts to C# runtime commands.

### 3. Java becomes orchestration around Refinery, not the primary formal layer

Java should still exist, but its role should shift toward:
- assembling layered inputs,
- invoking Refinery,
- collecting diagnostics,
- handling LLM calls and retry policy,
- mapping validated model facts to wire/runtime outputs.

The primary source of formal truth should not live only in Java validator code.

### 4. Runtime should consume validated facts, not director-specific improvisation

The C# runtime should stay generic:
- consume validated facts,
- apply translated commands,
- remain independent from LLM-specific behavior.

This matches the original WorldSim and OnlabRefinery design intent.

## Guidance For All Tracks / Agents

### Track D

Track D is the main owner of the future `tools.refinery` migration.

Default assumption going forward:
- do not treat the current imperative validator as the end-state formal model.
- treat it as a temporary bridge until the actual Refinery model exists.

Track D responsibilities in a true tools.refinery architecture:
- author and version the Refinery model artifacts,
- define designated output area semantics,
- define predicate/error-predicate based invariants,
- orchestrate LLM -> Refinery -> validated output -> adapter mapping.

### Track B

Track B should treat the director system as a consumer/producer of validated facts and runtime commands.

Track B should not:
- invent director constraint logic independently in C#,
- duplicate formal semantics that belong in the Refinery model.

Track B should provide:
- snapshot/runtime facts needed by the runtime layer,
- deterministic runtime command endpoints for validated output application,
- read-model visibility for verification.

### Track A

Track A should assume that future director/debug UI will need to visualize:
- response stage,
- solver/validation outcome,
- designated output results,
- and possibly model-level diagnostics.

Track A should avoid baking in assumptions that the director is "just a patch API".

### Track C

Track C should assume that future AI-facing director nudges may come from validated symbolic/model facts, not only current patch DTOs.

AI integration should remain interface-driven and avoid coupling directly to temporary Java-side validator internals.

### All agents / sessions

When discussing refinery/director architecture, use this vocabulary precisely:
- `LLM proposal`
- `design layer`
- `model layer`
- `runtime layer`
- `designated output area`
- `formal validation`
- `error predicates`
- `partial model`
- `solver/refinement`

Avoid collapsing these into vague phrases like:
- "the validator checks it"
- "the LLM generates the patch"
- "Refinery-like"

If the implementation still does those things today, say so explicitly as current-state pragmatism, not as the target architecture.

## Practical Migration Heuristic

Until the plans are rewritten, agents should use this heuristic:

1. Ask: is this logic truly formal-model logic, or only temporary Java validation?
2. If it is formal-model logic, prefer documenting it as future Refinery-layer responsibility.
3. If it is temporary Java validation, call it transitional and avoid expanding it carelessly.
4. Keep runtime/application layers generic and deterministic.
5. Keep LLM freedom bounded to a designated output area concept even before the actual tools.refinery migration lands.

## Recommended Immediate Planning Assumptions

Before future plan rewrites, the project should assume:
- the current director master plan section that says "imperative Java validation (not the actual tools.refinery solver SDK)" is transitional, not aspirational,
- Wave/track planning should move toward real Refinery artifacts,
- OnlabRefinery parity means actual layered solver-backed modeling, not only similar terminology,
- future director contracts should be evaluated in terms of how cleanly they map into a Refinery model and designated output area.

## Short Takeaway

For WorldSim, `tools.refinery` adoption should mean:
- real Refinery-language model artifacts,
- real solver-backed formalization,
- LLM output restricted to a bounded assertion area,
- Java as orchestration and mapping glue,
- C# runtime as deterministic consumer,
- and track plans that treat the formal model as a first-class project boundary.

If a proposal does not move the project in that direction, it may still be useful pragmatically, but it should not be described as true OnlabRefinery / `tools.refinery` alignment.
