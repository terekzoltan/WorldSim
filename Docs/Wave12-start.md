Current Understanding
Wave 12 célja nem feature wave, hanem kontrollált architektúra-javító wave a Wave 8-11 után. A fő fókusz: nagy god-file-ok bontása, boundary-k mélyítése, ScenarioRunner/SMR modularizálása, Runtime/AI/App/Refinery seam-ek tisztítása, viselkedésváltozás nélkül.
Confirmed Facts
- Legnagyobb C# hotspotok:
- WorldSim.Runtime/Simulation/World.cs: 3013 sor
- WorldSim.Runtime/Simulation/Person.cs: 2794 sor
- WorldSim.ScenarioRunner/Program.cs: 1924 sor
- WorldSim.Runtime/SimulationRuntime.cs: 1145 sor
- WorldSim.App/GameHost.cs: 917 sor
- A project dependency graph alapvetően jó, arch tests védik.
- Graphics nem használ WorldSim.Simulation namespace-t.
- ScenarioRunner jelenleg túl sok mindent egy Program.cs-ben tart.
- Runtime-ban World és Person túl sok domain felelősséget visz.
- App-ban GameHost túl sok host/input/render/operator orchestration felelősséget visz.
- WorldSim.Contracts/CONTRACT_OWNERSHIP.md stale: azt állítja, hogy aktív DTO-k még WorldSim.RefineryClient/Contracts alatt vannak.
- A Combined planben most dirty egy unrelated Wave 12: Improve codebase architecture sor. Ezt Step 7B review/commit során külön kell kezelni, ne keveredjen SMR evidence closeouttal.
Main Uncertainties
- Wave 12 legyen egy nagy cross-track architecture wave, vagy több kisebb sub-wave?
- Első prioritás Runtime god-object bontás legyen, vagy ScenarioRunner, mert az SMR workflow most sokat nőtt?
- Megengedett-e file move / namespace split Wave 12-ben, vagy első körben csak extraction-with-same-public-behavior?
- Wave 12 timing: Wave 11 után induljon, vagy Wave 8-10 mellett sidecar cleanupként?
Blocking Questions
- Wave 12 elsődleges célja melyik legyen: Runtime maintainability, ScenarioRunner maintainability, App/Graphics cleanup, vagy boundary hardening?
- Elfogadod-e, hogy Wave 12 első sprintje behavior-preserving refactor legyen, új gameplay nélkül?
- Lehet-e új internal folder/module struktúrát létrehozni és fájlokat mozgatni, ha arch tests + full tests zöldek?
- A Wave 12 záró gate legyen csak build/tests, vagy kell SMR evidence is?
Suggested Default Assumptions
- Wave 12 legyen behavior-preserving architecture wave.
- Ne legyen gameplay, UI polish, balance, ecology redesign, director feature.
- Elsőként ScenarioRunner és App/Graphics kisebb szeletek, utána Runtime nagyobb bontás.
- World.cs / Person.cs bontása csak előkészítő facade/test-harness után induljon.
Wave 12 Draft Plan
Wave 12 — Improve Codebase Architecture
Purpose:
- Reduce god-file pressure and scattered responsibilities without changing gameplay behavior.
- Turn shallow modules into deeper modules with stable interfaces.
- Improve testability and future-agent navigation before post-Wave11 expansion.
Non-goals:
- No new gameplay mechanics.
- No balance tuning.
- No renderer visual redesign.
- No Java/tools.refinery feature expansion.
- No public contract break unless explicitly approved.
- No large file moves without passing arch/test gates.
Sprint A — Architecture Baseline And Guardrails
- W12-A1: Write architecture baseline note from current audit.
- W12-A2: Update stale WorldSim.Contracts/CONTRACT_OWNERSHIP.md.
- W12-A3: Expand WorldSim.ArchTests with source-level guardrails.
- W12-A4: Add “no new god-file growth” guidance for World.cs, Person.cs, ScenarioRunner/Program.cs, GameHost.cs.
Acceptance:
- Docs updated.
- Arch tests still green.
- No behavior changes.
Sprint B — ScenarioRunner Modularization
Target:
- WorldSim.ScenarioRunner/Program.cs
Proposed slices:
- W12-B1: Extract ScenarioRunner contract records/enums from Program.cs.
- W12-B2: Extract env/config parsing into a configuration module.
- W12-B3: Extract artifact writing and drilldown writing.
- W12-B4: Extract assertion/anomaly/perf/exit-code evaluation.
- W12-B5: Extract scenario setup surface, with supply scenario as first setup plugin.
- W12-B6: Consolidate ScenarioRunner test helpers.
Why first:
- High value, lower gameplay risk than World.cs.
- Heavy SMR usage makes this immediately useful.
- Existing tests are artifact-focused and should protect behavior.
Acceptance:
- WorldSim.ScenarioRunner.Tests green.
- Artifact JSON shape unchanged except explicitly documented.
- Existing Step 7A/7B evidence workflows still reproducible.
Sprint C — App And Graphics Host Cleanup
Target:
- WorldSim.App/GameHost.cs
- WorldSim.Graphics/UI/HudRenderer.cs
- WorldSim.Graphics/UI/SettingsPanelRenderer.cs
- WorldSim.Graphics/Rendering/ActorRenderPass.cs
- WorldSim.Graphics/Rendering/StructureRenderPass.cs
Proposed slices:
- W12-C1: Extract input/action mapping from GameHost.
- W12-C2: Extract draw/HUD orchestration from GameHost.
- W12-C3: Add SettingsPanelViewModel to replace long parameter list.
- W12-C4: Add director/operator HUD view models.
- W12-C5: Split actor marker rendering helpers.
- W12-C6: Split structure projectile/effect visualization from structure glyph rendering.
Acceptance:
- Graphics still consumes snapshots only.
- F1/F6/F8 and visual lane controls still work.
- Arch tests green.
- No visual behavior changes except accepted equivalence.
Sprint D — Runtime Facade And Telemetry Extraction
Target:
- WorldSim.Runtime/Simulation/World.cs
Proposed slices:
- W12-D1: Add internal world collection/read facade for people/colonies/animals/structures.
- W12-D2: Move scenario telemetry aggregation into a ScenarioTelemetryState or collector layer.
- W12-D3: Extract ecology counters/regrowth/replenishment state behind an EcologySystem seam.
- W12-D4: Extract occupancy/deconfliction system.
- W12-D5: Extract territory influence system only after tests prove no drift.
Acceptance:
- Runtime tests green.
- ScenarioRunner tests green.
- SMR compatibility tests green.
- Snapshot outputs unchanged for same seed/config.
Sprint E — Person Behavior Decomposition
Target:
- WorldSim.Runtime/Simulation/Person.cs
Proposed slices:
- W12-E1: Extract supply/refill/consume helper.
- W12-E2: Extract build-intent/build-site helper.
- W12-E3: Extract movement/no-progress/backoff helper.
- W12-E4: Extract combat/flee/raid action helper.
- W12-E5: Add focused tests around extracted behavior before larger movement.
Acceptance:
- No command/job behavior drift.
- Existing Wave 8 supply tests green.
- Combat/siege/AI tests green.
Sprint F — AI Boundary Deepening
Target:
- WorldSim.AI/Abstractions.cs
- WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs
Proposed slices:
- W12-F1: Segment NpcAiContext into grouped subcontexts.
- W12-F2: Extract RuntimeNpcContextBuilder.
- W12-F3: Extract command-to-job mapper.
- W12-F4: Extract AI decision recorder/telemetry boundary.
Acceptance:
- WorldSim.AI still has no Runtime/App/Graphics dependency.
- AI tests green.
- RuntimeNpcBrain tests green.
Sprint G — Refinery Boundary Cleanup
Target:
- WorldSim.RefineryAdapter
- WorldSim.RefineryClient
- WorldSim.Contracts
Proposed slices:
- W12-G1: Split RefineryPatchRuntime orchestration into policy/request/apply/status components.
- W12-G2: Introduce explicit IRefineryTechIdMapper identity mapper.
- W12-G3: Consolidate patch validation constants/policy.
- W12-G4: Add parity guardrails for op names, treaty kinds, faction ID policy.
- W12-G5: Clean stale contract ownership docs.
Acceptance:
- Adapter/client tests green.
- Java parity tests or fixture parity tests still green where applicable.
- No wire contract break.
Suggested Step Order
- Step 1: W12-A baseline/guardrails.
- Step 2: W12-B ScenarioRunner modularization.
- Step 3: W12-C App/Graphics host cleanup.
- Step 4: W12-D Runtime facade/telemetry extraction.
- Step 5: W12-E Person behavior extraction.
- Step 6: W12-F AI boundary cleanup.
- Step 7: W12-G Refinery boundary cleanup.
- Step 8: Full evidence gate and architecture closeout.
Verification Plan
- Always run relevant focused test suite per sprint.
- For ScenarioRunner slices: WorldSim.ScenarioRunner.Tests.
- For Runtime slices: WorldSim.Runtime.Tests plus WorldSim.ScenarioRunner.Tests.
- For App/Graphics slices: dotnet build WorldSim.sln, WorldSim.ArchTests, manual smoke checklist.
- For Refinery slices: adapter/client tests plus Java tests if Java boundary touched.
- For closeout: full solution build, full .NET tests, arch tests, and one SMR smoke if runtime/scenario behavior was touched.
Risks
- World.cs and Person.cs extraction can cause subtle deterministic drift.
- ScenarioRunner modularization can accidentally change JSON shape or exit-code policy.
- App/Graphics cleanup can break manual hotkeys without compile failure.
- AI context segmentation can cause constructor/test churn.
- Refinery boundary cleanup can accidentally affect live/manual director smoke semantics.
Current Readiness
ALMOST READY.
A draft Wave 12 plan is now concrete enough to put into Combined after you answer the priority questions. I would not write it into the plan yet until we lock the scope and first sprint ordering.

Future Resume Context
This file is intentionally a parked planning snapshot, not an approved Combined-plan wave.

Why it is parked:
- The audit was captured around Wave 8 Step 7B / Wave 12 placeholder discussion.
- Waves 9, 10, 10.5, and 11 are expected to change the codebase materially before Wave 12 starts.
- Therefore the concrete hotspot line counts, file ownership pressure, and sprint ordering below must be revalidated before this becomes a real Wave 12 plan.
- Do not copy this whole draft into `Combined-Execution-Sequencing-Plan.md` without a fresh post-Wave11 audit.

Original audit context:
- Date context: 2026-04-29 session timeframe.
- Role context: Meta Coordinator planning, not implementation.
- Skills used: `improve-codebase-architecture` and `grill-me`.
- Mode at time of audit: plan/read-only for architecture analysis.
- The audit included C# projects, runtime/app/graphics/scenario-runner/refinery boundaries, existing architecture tests, master plans, and key evidence docs.

Known repo state at parking time:
- `WorldSim.Runtime/Simulation/World.cs` and `Person.cs` were the largest runtime hotspots.
- `WorldSim.ScenarioRunner/Program.cs` had just grown with Wave 8 SMR supply telemetry and was a strong candidate for early modularization.
- `WorldSim.App/GameHost.cs` remained the main App host/input/render/operator orchestration hotspot.
- `WorldSim.Contracts/CONTRACT_OWNERSHIP.md` looked stale and should be checked before any contract cleanup slice.
- `WorldSim.ArchTests/BoundaryRulesTests.cs` enforced only coarse project/reference and Graphics namespace guardrails; source-level guardrails were limited.
- A temporary `Wave 12: Improve codebase architecture` line existed in the Combined plan during the discussion; it should not be treated as an accepted plan entry by itself.

Re-entry protocol before using this plan:
1. Finish or explicitly pause the active wave in `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`.
2. Re-read `AGENTS.md`, especially dependency graph, track ownership, wave turn-gate protocol, and cross-track notes added after this file was created.
3. Re-read current master plans that may affect architecture order:
   - `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
   - `Docs/Plans/Master/Wave11-Closed-Loop-Ecology-Redesign-Plan.md`
   - `Docs/Plans/Master/Tools-Refinery-Migration-Plan.md`
   - `Docs/Plans/Master/Combat-Defense-Campaign-Master-Plan.md`
   - `Docs/Plans/Master/Director-Integration-Master-Plan.md`
   - `Docs/Plans/Master/SMR-Minimum-Ops-Checklist.md`
4. Re-run a fresh architecture audit instead of trusting the line counts in this document.
5. Compare the fresh audit against this parked draft and only keep slices that still match the current code.
6. Ask the blocking questions again; do not assume the old defaults are still correct.
7. Only then write the real Wave 12 section into the Combined plan.

Suggested fresh audit commands / checks:
- `git status --short --branch`
- `git log --oneline -20`
- `git ls-files "*.cs" | xargs wc -l`
- Inspect all `.csproj` project references.
- Inspect `WorldSim.ArchTests/BoundaryRulesTests.cs`.
- Search for direct mutable-state access: `_people`, `_colonies`, `_animals`, `SpecializedBuildings`, `InternalsVisibleTo`.
- Search for architecture debt markers: `TODO`, `legacy`, `transitional`, `temporary`, `HACK`, `FIXME`.
- Check current `Docs/Evidence/SMR/` packages to understand the latest operational workflows.

Likely files to re-check first:
- `WorldSim.Runtime/Simulation/World.cs`
- `WorldSim.Runtime/Simulation/Person.cs`
- `WorldSim.Runtime/SimulationRuntime.cs`
- `WorldSim.ScenarioRunner/Program.cs`
- `WorldSim.App/GameHost.cs`
- `WorldSim.Graphics/UI/HudRenderer.cs`
- `WorldSim.Graphics/UI/SettingsPanelRenderer.cs`
- `WorldSim.RefineryAdapter/Integration/RefineryPatchRuntime.cs`
- `WorldSim.RefineryAdapter/Translation/PatchCommandTranslation.cs`
- `WorldSim.AI/Abstractions.cs`
- `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs`
- `WorldSim.Contracts/CONTRACT_OWNERSHIP.md`
- `WorldSim.ArchTests/BoundaryRulesTests.cs`

Important dependencies on future waves:
- Wave 9 / Wave 10 supply, campaign, and cross-map work may change Runtime, AI, ScenarioRunner, and Graphics priorities.
- Wave 10.5 / Tools.Refinery TR3 may change RefineryAdapter, Contracts, Java parity, and director boundary cleanup priorities.
- Wave 11 ecology redesign may make Runtime ecology extraction much more urgent, or may already perform part of it.
- If Wave 11 creates new ecology systems, do not plan W12-D3 from this snapshot blindly; audit the new model first.

Planning rules when this is resumed:
- Treat Wave 12 as behavior-preserving unless Meta explicitly changes that.
- Prefer one bounded architecture slice per implementation session.
- Do not mix architecture cleanup with gameplay tuning or balance changes.
- Preserve public contracts by default.
- Add/strengthen tests before moving behavior out of `World.cs`, `Person.cs`, `Program.cs`, or `GameHost.cs`.
- Keep raw `.artifacts/` outputs uncommitted; evidence summaries/docs can be committed if they are part of a gate.
- Never mark a large architecture slice complete from build-only verification if runtime/ScenarioRunner behavior can drift.

Suggested Wave 12 readiness gate before implementation:
- Fresh audit note exists and references current HEAD commit.
- Combined plan has an explicit Wave 12 section with prerequisites and step order.
- All prerequisite feature waves are either closed or explicitly declared non-blocking.
- `WorldSim.ArchTests` has at least the current dependency-boundary coverage green.
- A targeted verification matrix is defined for the first slice.
- Worktree is clean or unrelated changes are explicitly documented and isolated.

Possible first slice after re-audit:
- If ScenarioRunner is still a large single-file bottleneck, start with ScenarioRunner contracts/config/artifact extraction because it is well-covered by artifact tests and lower gameplay-risk than Runtime extraction.
- If Wave 11 makes ecology the largest immediate risk, start with Runtime ecology boundaries instead, but only after pinning deterministic SMR baselines.
- If App/Graphics manual workflow becomes the main bottleneck, start with `GameHost` input/draw orchestration extraction and settings/HUD view-models.

Do not forget:
- The draft sprint names below are placeholders. Renumber or reorder them after the fresh audit.
- If this file conflicts with the current Combined plan, the Combined plan wins.
- If new AGENTS.md rules were added after this note, those rules win.
- If a future review finds one of these slices obsolete, delete or rewrite it instead of carrying stale architecture debt forward.
