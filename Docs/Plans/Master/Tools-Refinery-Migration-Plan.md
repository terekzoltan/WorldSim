# Tools-Refinery Migration Plan

Status: planning approved, documentation baseline
Owner: Track D primary, with Track B/A/C integration touchpoints

## 1. Purpose

Move the WorldSim refinery/director subsystem from the current transition-state pipeline:

- `LLM -> JSON candidate -> Java imperative validation/repair -> PatchResponse -> C# adapter -> runtime commands`

to a true `tools.refinery`-aligned architecture:

- `LLM -> structured assertion candidate -> layered Refinery model -> solver/refinement -> validated symbolic facts -> bridge contract mapping -> C# adapter -> runtime commands`

This plan exists because the project now has:
- a working Java service,
- a working C# runtime boundary,
- live/manual smoke infrastructure,
- and a clear understanding that the current imperative validator is useful but not the intended end-state formal layer.

## 2. Mandatory Pre-Read

Before any task that creates or modifies refinery-related artifacts, the implementing/reviewing agent must first read:

1. `Docs/Plans/Master/Tools-Refinery-Agent-Guide.md`
2. every external official link listed in that guide's `Official References` section

This rule applies to:
- Refinery model files
- Java refinery-service implementation files
- director/refinery planning docs
- adapter/runtime integration work that depends on formal model semantics
- any designated-output-area or assertion-candidate design task

## 3. Agreed Architectural Decisions

These decisions are the current project baseline for migration planning.

### 3.1 LLM output shape

Decision:
- First migration step uses a **structured assertion-candidate** format.

Reason:
- It is more robust than asking the LLM to emit raw Refinery-language fragments immediately.
- It still keeps the LLM close to the formal model by making the candidate assertion-oriented instead of patch-oriented.

Constraint:
- This candidate format is transitional ingest only.
- It must not become a new long-term patch/domain model.

### 3.2 Java service boundary

Decision:
- The Spring Boot HTTP service remains the external boundary.

Inside the Java service, the long-term split becomes:
- HTTP/controller layer
- orchestration layer
- Refinery model assembly + solve/refinement core
- validated fact extraction + mapping layer
- telemetry/diagnostics layer

Meaning:
- The Java service host remains.
- The formal truth should stop living primarily in validator classes.

### 3.3 Wire contract policy

Decision:
- The current patch/v2-oriented wire contract may remain temporarily as a **bridge contract**.

Internal target:
- symbolic validated facts first
- bridge mapping second

Meaning:
- `PatchResponse` is temporarily the transport/output boundary,
- but it is no longer the desired internal modeling language.

### 3.4 Model family strategy

Decision:
- Use **shared/common vocabulary** plus **separate problem families**.

Preferred structure:
- `common/`
- `director/`
- `combat/`
- `campaign/`

Meaning:
- Avoid one giant monolithic solve space.
- Avoid duplicated vocabulary definitions.

### 3.5 Fallback policy

Decision:
- Keep deterministic Java-side fallback for the near term.

Policy:
- It is an operational safety net, not a second hidden "smart director".
- A solver-backed conservative fallback may be added later, but not in the first migration slice.

## 4. Current State vs Target State

### 4.1 Current state

What exists now:
- Java service parses candidate JSON
- imperative Java validation/repair loop
- telemetry/retry/fallback infrastructure
- C# adapter/runtime boundary
- patch-based bridge contract
- manual smoke and HUD diagnostics

What is missing for true `tools.refinery` alignment:
- versioned Refinery-language artifacts as the primary formal source of truth
- designated output area expressed in the formal model
- actual solver/refinement in the core director path
- formal predicates/error predicates replacing most imperative validation logic
- explicit partial-model semantics in the core pipeline

### 4.2 Target state

Desired end-state responsibilities:
- LLM: proposes bounded assertion-oriented candidate only
- tools.refinery model: owns formal vocabulary, constraints, uncertainty, and solve/refinement
- Java orchestration: loads layers, invokes Refinery, extracts validated facts, maps to bridge contract
- C# runtime: deterministic consumer of validated commands/facts

## 5. Repository / Artifact Target Layout

Recommended initial layout inside the Java service:

```text
refinery-service-java/
  src/main/resources/refinery/
    common/
      design.problem
      shared-predicates.problem

    director/
      design.problem
      model.problem
      runtime.problem
      output.problem

    combat/
      design.problem
      model.problem
      runtime.problem
      output.problem

    campaign/
      design.problem
      model.problem
      runtime.problem
      output.problem
```

Recommended Java code boundary shape:

```text
refinery-service-java/src/main/java/.../
  controller/
  orchestration/
  llm/
  refinerycore/
    model/
    loader/
    mapper/
    solver/
    extract/
  bridge/
  telemetry/
```

This is conceptual structure guidance, not yet a required exact package layout.

## 6. Migration Principles

### 6.1 Model first

Every new formal rule should be evaluated with this question:
- should this live in Java code temporarily,
- or should it be introduced directly as a Refinery artifact/predicate?

Default bias going forward:
- if it is truly formal-model semantics, it belongs in the Refinery model.

### 6.2 Bridge contract, not end-state contract

The patch/v2 contract may stay temporarily,
but planning language should describe it as:
- bridge contract,
- transport boundary,
- mapping target,

not as the primary semantic form.

### 6.3 Runtime stays dumb

The runtime should not absorb formal-model responsibility.

Runtime should:
- expose checkpoint facts,
- execute validated commands,
- export read models for debugging/verification,
- stay deterministic and generic.

### 6.4 Designated output area discipline

The LLM must never get unfenced authority over the whole model.

Migration invariant:
- only a bounded output region is editable by the generative stage,
- all stable design/model/runtime facts remain read-only.

## 7. Transition Architecture

The intended transition chain is:

```text
World snapshot
  -> Java runtime-fact mapper
  -> layered Refinery model assembly
  -> structured assertion-candidate ingest
  -> designated output area assertions
  -> tools.refinery solve/refinement
  -> validated symbolic facts
  -> bridge mapping to current patch/v2 contract
  -> C# adapter translation
  -> runtime commands
```

The key migration event is:
- formal semantics move from Java validator classes into versioned Refinery artifacts.

## 8. Work Packages

## Phase TR1 - Foundation and artifact strategy

### TR1-A: Java/tools.refinery spike

Goal:
- prove that the Java service can load Refinery problem artifacts and invoke the solver as a library.

Tasks:
- add `tools.refinery` Gradle/plugin/dependency setup
- create a minimal executable spike using official Java integration guidance
- load a small `.problem` artifact
- run generation/solve and inspect at least one relation/predicate result

Acceptance:
- a repeatable Java test or mini-runner proves that WorldSim can invoke Refinery locally from the service codebase.

### TR1-B: Artifact strategy and common vocabulary

Goal:
- establish the repo-level artifact layout and shared/common vocabulary strategy.

Tasks:
- define `common/`, `director/`, `combat/`, `campaign/` split
- define naming/versioning rules for `.problem` artifacts
- define which concepts belong to shared vocabulary vs family-local vocabulary

Acceptance:
- file layout and vocabulary ownership are explicit enough that later tracks do not invent ad hoc incompatible model fragments.

### TR1-C: Director problem family skeleton

Goal:
- create the first actual director family artifact set.

Tasks:
- create `director/design.problem`
- create `director/model.problem`
- create `director/runtime.problem`
- create `director/output.problem`
- document designated output area boundaries

Acceptance:
- director formal artifacts exist and their responsibilities are clearly separated.

### TR1-D: Structured assertion-candidate ingest design

Goal:
- replace the current free-form patch-oriented candidate mental model with an assertion-oriented intermediate form.

Tasks:
- define the structured candidate schema
- make it assertion-oriented, not patch-oriented
- define deterministic Java mapping from candidate fields to output-area assertions

Acceptance:
- candidate format is close enough to the formal model that it does not become a second hidden domain language.

### TR1-E: Bridge contract policy

Goal:
- formally declare the current patch/v2 output as bridge contract only.

Tasks:
- define where symbolic validated facts end and bridge mapping begins
- define what may stay stable in the C# boundary during migration
- define what is explicitly transitional in Java today

Acceptance:
- future work no longer treats `PatchResponse` as the primary internal ontology.

## Phase TR2 - First solver-backed director slice

### TR2-A: Runtime snapshot -> runtime assertions mapper

Goal:
- convert current checkpoint snapshot data into the director runtime layer.

Tasks:
- map season/tick/cooldown/budget/active-beat/directive facts
- define runtime facts ownership between C# snapshot and Java model assembly

### TR2-B: Solve/refinement path for minimal director output

Goal:
- solve a minimal season-director checkpoint through actual Refinery artifacts.

Tasks:
- assemble design+model+runtime+output layers
- inject structured candidate into designated output area
- run Refinery solve/refinement
- detect invalid/error cases through formal model outcome

### TR2-C: Validated facts -> bridge mapping

Goal:
- convert solved symbolic facts to the current patch/v2 wire contract.

Tasks:
- extract validated story beat / directive facts
- map them to current bridge contract shape
- keep the C# boundary stable during the first migration step

### TR2-D: Solver-path observability

Goal:
- make solver-backed live smoke as diagnosable as the current imperative path.

Tasks:
- log/telemetry for solve success, unsat/error predicate hits, fallback path, completion counts
- preserve operator-facing diagnostics in README/checklists/HUD-facing bridge markers

## Phase TR3 - Convergence and cleanup

### TR3-A: Imperative validator deprecation plan

Goal:
- migrate remaining formal semantics out of Java imperative validation.

Tasks:
- audit current validator responsibilities
- classify: move to predicates, keep as orchestration, or remove
- shrink Java validator into transitional guards only

### TR3-B: Fallback boundary cleanup

Goal:
- keep fallback conservative and operationally clear.

Tasks:
- document solver path vs deterministic fallback responsibilities
- prevent fallback from becoming an ever-growing hidden second planner

### TR3-C: Shared vocabulary + family expansion prep

Goal:
- prepare combat/campaign families without merging everything into a monolith.

Tasks:
- factor out shared vocabulary where justified
- bootstrap `combat/` and `campaign/` families as separate problem families

## 9. Acceptance Gates

Migration cannot be considered materially underway until:
- at least one real `.problem` artifact family exists in the repo,
- at least one Java test/proof uses actual `tools.refinery` solving,
- the director designated output area is documented and versioned,
- symbolic validated facts are clearly separated from bridge-contract mapping,
- and planning docs stop describing imperative Java validation as the intended final formal layer.

## 10. Impact On Existing Plans

This migration plan must be reflected in:
- `Docs/Plans/Master/Director-Integration-Master-Plan.md`
- `WorldSim.RefineryAdapter/Docs/Plans/Track-D-Season-Director-Plan.md`
- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`

Policy:
- these plans should use transition-state language after this document lands,
- and later refinery-related implementation steps should explicitly reference this plan and `Tools-Refinery-Agent-Guide.md`.

## 11. Short Summary

WorldSim should not jump directly from the current imperative validator to raw LLM-written Refinery text.

The right transition is:
- structured assertion-candidate first,
- real versioned Refinery artifacts as formal truth,
- Spring Boot boundary retained,
- current patch/v2 contract treated as temporary bridge output,
- runtime kept deterministic and generic,
- and future director/combat/campaign modeling built as shared vocabulary plus separate problem families.
