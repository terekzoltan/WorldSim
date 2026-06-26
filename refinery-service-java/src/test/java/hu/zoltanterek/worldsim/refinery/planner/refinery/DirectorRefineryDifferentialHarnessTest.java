package hu.zoltanterek.worldsim.refinery.planner.refinery;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorBridgeContractMapper;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorCorePatchAssertionsMapper;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorModelValidator;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorOutputAssertions;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorValidationOutcome;

class DirectorRefineryDifferentialHarnessTest {
    private final DirectorModelValidator validator = new DirectorModelValidator(true);
    private final DirectorCorePatchAssertionsMapper coreMapper = new DirectorCorePatchAssertionsMapper();
    private final DirectorRefinerySolver solver = new DirectorRefinerySolver();
    private final DirectorBridgeContractMapper bridgeMapper = new DirectorBridgeContractMapper();
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void evaluate_CoveredFormalAndValidatorCasesAreClassifiedDeterministically() {
        List<DifferentialCase> cases = List.of(
                new DifferentialCase(
                        "active major rejects major story",
                        facts(0, 50.0, new DirectorRuntimeFacts.ActiveBeatFact("BEAT_ACTIVE_MAJOR", "major", 6)),
                        List.of(story("op_major", "BEAT_MAJOR", "Major pressure", 24, "major", majorEffects())),
                        DifferentialOutcome.BOTH_REJECT_COVERED_RULE,
                        List.of("effects")
                ),
                new DifferentialCase(
                        "active epic rejects epic story",
                        facts(0, 50.0, new DirectorRuntimeFacts.ActiveBeatFact("BEAT_ACTIVE_EPIC", "epic", 10)),
                        List.of(story("op_epic", "BEAT_EPIC", "Epic pressure", 24, "epic", epicEffects())),
                        DifferentialOutcome.BOTH_REJECT_COVERED_RULE,
                        List.of("effects")
                ),
                new DifferentialCase(
                        "cooldown rejects story",
                        facts(8, 50.0),
                        List.of(story("op_cooldown", "BEAT_COOLDOWN", "Cooldown pressure", 24, "minor", List.of())),
                        DifferentialOutcome.BOTH_REJECT_COVERED_RULE,
                        List.of()
                ),
                new DifferentialCase(
                        "minor story during active major remains valid",
                        facts(0, 50.0, new DirectorRuntimeFacts.ActiveBeatFact("BEAT_ACTIVE_MAJOR", "major", 6)),
                        List.of(story("op_minor", "BEAT_MINOR", "Minor pressure", 24, "minor", List.of())),
                        DifferentialOutcome.BOTH_ACCEPT_SAME_CORE,
                        List.of()
                )
        );

        for (DifferentialCase testCase : cases) {
            DifferentialReport report = evaluate(testCase);

            assertEquals(testCase.expectedOutcome(), report.outcome(), report.toString());
            assertEquals(testCase.expectedUnsupportedFeatures(), report.unsupportedByFormal(), report.toString());
            assertFalse(report.solverLoadFailure(), report.toString());
        }
    }

    @Test
    void evaluate_ValidatorRepairOnlyAndDivergenceCasesAreClassifiedDeterministically() {
        List<DifferentialCase> cases = List.of(
                new DifferentialCase(
                        "validator repairs explicit severity from effect count",
                        facts(0, 50.0),
                        List.of(story("op_repair", "BEAT_REPAIR", "Repair severity", 24, "epic", List.of())),
                        DifferentialOutcome.VALIDATOR_REPAIR_ONLY_TRANSITIONAL,
                        List.of()
                ),
                new DifferentialCase(
                        "formal sees explicit major while validator repairs to minor",
                        facts(0, 50.0, new DirectorRuntimeFacts.ActiveBeatFact("BEAT_ACTIVE_MAJOR", "major", 6)),
                        List.of(story("op_diverge", "BEAT_DIVERGE", "Explicit major without effects", 24, "major", List.of())),
                        DifferentialOutcome.FORMAL_REJECTS_VALIDATOR_ACCEPTS,
                        List.of()
                ),
                new DifferentialCase(
                        "formal accepts invalid directive while validator rejects vocabulary",
                        facts(0, 50.0),
                        List.of(new PatchOp.SetColonyDirective(
                                "op_invalid_directive",
                                0,
                                "InvalidDirectiveName",
                                18,
                                List.of()
                        )),
                        DifferentialOutcome.VALIDATOR_REJECTS_FORMAL_ACCEPTS,
                        List.of()
                )
        );

        for (DifferentialCase testCase : cases) {
            DifferentialReport report = evaluate(testCase);

            assertEquals(testCase.expectedOutcome(), report.outcome(), report.toString());
            assertEquals(testCase.expectedUnsupportedFeatures(), report.unsupportedByFormal(), report.toString());
            assertFalse(report.solverLoadFailure(), report.toString());
            if (testCase.expectedOutcome() == DifferentialOutcome.VALIDATOR_REJECTS_FORMAL_ACCEPTS) {
                assertValidatorRejectsFormalAcceptsPreconditions(report);
            }
        }
    }

    @Test
    void evaluate_UnsupportedFormalSurfacesAreExplicitlyReported() {
        List<DifferentialCase> cases = List.of(
                new DifferentialCase(
                        "nested effects and biases are omitted from formal core",
                        facts(0, 50.0),
                        List.of(
                                story("op_effects", "BEAT_EFFECTS", "Nested effects", 24, "major", majorEffects()),
                                new PatchOp.SetColonyDirective(
                                        "op_biases",
                                        0,
                                        "PrioritizeFood",
                                        18,
                                        List.of(new PatchOp.GoalBiasEntry("goal_bias", "farming", 0.25, 18L))
                                )
                        ),
                        DifferentialOutcome.UNSUPPORTED_BY_FORMAL,
                        List.of("effects", "biases")
                ),
                new DifferentialCase(
                        "campaign op is validator-owned and unsupported by formal core",
                        facts(0, 50.0),
                        List.of(new PatchOp.DeclareWar("op_campaign", 1, 2, "pressure")),
                        DifferentialOutcome.UNSUPPORTED_BY_FORMAL,
                        List.of("campaign")
                ),
                new DifferentialCase(
                        "causal chain is validator-owned and unsupported by formal core",
                        facts(0, 50.0),
                        List.of(story(
                                "op_causal",
                                "BEAT_CAUSAL",
                                "Causal pressure",
                                24,
                                "minor",
                                List.of(),
                                new PatchOp.CausalChainEntry(
                                        "causal_chain",
                                        new PatchOp.CausalCondition("population", "gt", 3),
                                        new PatchOp.CausalFollowUpBeat("BEAT_FOLLOW", "Follow up", 12, "minor", List.of()),
                                        20,
                                        1
                                )
                        )),
                        DifferentialOutcome.UNSUPPORTED_BY_FORMAL,
                        List.of("causalChain")
                )
        );

        for (DifferentialCase testCase : cases) {
            DifferentialReport report = evaluate(testCase);

            assertEquals(testCase.expectedOutcome(), report.outcome(), report.toString());
            assertEquals(testCase.expectedUnsupportedFeatures(), report.unsupportedByFormal(), report.toString());
            assertFalse(report.solverLoadFailure(), report.toString());
        }
    }

    private DifferentialReport evaluate(DifferentialCase testCase) {
        ValidatorPathResult validatorResult = runValidator(testCase);
        DirectorCorePatchAssertionsMapper.Result coreResult = coreMapper.map(testCase.candidatePatch());
        List<String> unsupportedByFormal = unsupportedByFormal(testCase.candidatePatch(), coreResult);

        DirectorRefinerySolveResult solveResult = null;
        List<PatchOp> bridgePatch = List.of();
        if (coreResult.available()) {
            solveResult = solver.solve(testCase.runtimeFacts(), coreResult.assertions(), unsupportedByFormal);
            if (solveResult.success()) {
                bridgePatch = bridgeMapper.toPatchOps(request(testCase.name()), solveResult.validatedOutput());
            }
        }

        DifferentialOutcome outcome = classify(validatorResult, coreResult, solveResult, unsupportedByFormal, bridgePatch);
        return new DifferentialReport(
                testCase.name(),
                outcome,
                validatorResult.accepted(),
                validatorResult.repaired(),
                solveResult == null ? null : solveResult.status(),
                solveResult != null && solveResult.status() == DirectorRefinerySolveStatus.LOAD_FAILURE,
                unsupportedByFormal,
                bridgePatch
        );
    }

    private ValidatorPathResult runValidator(DifferentialCase testCase) {
        try {
            DirectorValidationOutcome outcome = validator.validateAndRepair(testCase.candidatePatch(), testCase.runtimeFacts());
            return new ValidatorPathResult(true, outcome.repaired(), outcome.patch(), null);
        } catch (IllegalArgumentException ex) {
            return new ValidatorPathResult(false, false, List.of(), ex.getMessage());
        }
    }

    private static DifferentialOutcome classify(
            ValidatorPathResult validatorResult,
            DirectorCorePatchAssertionsMapper.Result coreResult,
            DirectorRefinerySolveResult solveResult,
            List<String> unsupportedByFormal,
            List<PatchOp> bridgePatch
    ) {
        if (!coreResult.available()) {
            return DifferentialOutcome.UNSUPPORTED_BY_FORMAL;
        }
        if (solveResult == null || solveResult.status() == DirectorRefinerySolveStatus.LOAD_FAILURE) {
            return DifferentialOutcome.UNSUPPORTED_BY_FORMAL;
        }
        if (!validatorResult.accepted() && solveResult.status() == DirectorRefinerySolveStatus.NON_SUCCESS) {
            return DifferentialOutcome.BOTH_REJECT_COVERED_RULE;
        }
        if (!unsupportedByFormal.isEmpty()) {
            return DifferentialOutcome.UNSUPPORTED_BY_FORMAL;
        }
        if (validatorResult.accepted() && solveResult.status() == DirectorRefinerySolveStatus.NON_SUCCESS) {
            return DifferentialOutcome.FORMAL_REJECTS_VALIDATOR_ACCEPTS;
        }
        if (!validatorResult.accepted() && solveResult.success()) {
            return DifferentialOutcome.VALIDATOR_REJECTS_FORMAL_ACCEPTS;
        }
        if (validatorResult.repaired() || !sameCore(validatorResult.patch(), bridgePatch)) {
            return DifferentialOutcome.VALIDATOR_REPAIR_ONLY_TRANSITIONAL;
        }
        return DifferentialOutcome.BOTH_ACCEPT_SAME_CORE;
    }

    private static boolean sameCore(List<PatchOp> validatorPatch, List<PatchOp> bridgePatch) {
        return storyCore(validatorPatch).equals(storyCore(bridgePatch))
                && directiveCore(validatorPatch).equals(directiveCore(bridgePatch));
    }

    private static void assertValidatorRejectsFormalAcceptsPreconditions(DifferentialReport report) {
        assertFalse(report.validatorAccepted(), report.toString());
        assertEquals(DirectorRefinerySolveStatus.SUCCESS, report.solverStatus(), report.toString());
        assertTrue(report.unsupportedByFormal().isEmpty(), report.toString());
        assertEquals(1, report.bridgePatch().size(), report.toString());
        assertTrue(report.bridgePatch().get(0) instanceof PatchOp.SetColonyDirective, report.toString());
        PatchOp.SetColonyDirective directive = (PatchOp.SetColonyDirective) report.bridgePatch().get(0);
        assertEquals(0, directive.colonyId());
        assertEquals("InvalidDirectiveName", directive.directive());
        assertEquals(18, directive.durationTicks());
    }

    private static StoryCore storyCore(List<PatchOp> patch) {
        for (PatchOp op : patch) {
            if (op instanceof PatchOp.AddStoryBeat story) {
                return new StoryCore(story.beatId(), story.text(), story.durationTicks(), story.severity());
            }
        }
        return StoryCore.empty();
    }

    private static DirectiveCore directiveCore(List<PatchOp> patch) {
        for (PatchOp op : patch) {
            if (op instanceof PatchOp.SetColonyDirective directive) {
                return new DirectiveCore(directive.colonyId(), directive.directive(), directive.durationTicks());
            }
        }
        return DirectiveCore.empty();
    }

    private static List<String> unsupportedByFormal(
            List<PatchOp> candidatePatch,
            DirectorCorePatchAssertionsMapper.Result coreResult
    ) {
        Set<String> unsupported = new LinkedHashSet<>();
        for (PatchOp op : candidatePatch) {
            if (op instanceof PatchOp.AddStoryBeat story) {
                if (story.effects() != null && !story.effects().isEmpty()) {
                    unsupported.add("effects");
                }
                if (story.causalChain() != null) {
                    unsupported.add("causalChain");
                }
            } else if (op instanceof PatchOp.SetColonyDirective directive) {
                if (directive.biases() != null && !directive.biases().isEmpty()) {
                    unsupported.add("biases");
                }
            }
        }
        if (coreResult.unsupportedFeatures() != null) {
            unsupported.addAll(coreResult.unsupportedFeatures());
        }
        if (!coreResult.available() && coreResult.unavailableReason() != null) {
            unsupported.add(coreResult.unavailableReason());
        }
        return List.copyOf(unsupported);
    }

    private PatchRequest request(String name) {
        return new PatchRequest(
                "v1",
                "req-" + name.toLowerCase().replaceAll("[^a-z0-9]+", "-"),
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                objectMapper.createObjectNode(),
                null
        );
    }

    private static DirectorRuntimeFacts facts(long cooldown, double budget, DirectorRuntimeFacts.ActiveBeatFact... activeBeats) {
        return new DirectorRuntimeFacts(100, 2, cooldown, budget, List.of(activeBeats), List.of());
    }

    private static PatchOp.AddStoryBeat story(
            String opId,
            String beatId,
            String text,
            long durationTicks,
            String severity,
            List<PatchOp.EffectEntry> effects
    ) {
        return story(opId, beatId, text, durationTicks, severity, effects, null);
    }

    private static PatchOp.AddStoryBeat story(
            String opId,
            String beatId,
            String text,
            long durationTicks,
            String severity,
            List<PatchOp.EffectEntry> effects,
            PatchOp.CausalChainEntry causalChain
    ) {
        return new PatchOp.AddStoryBeat(opId, beatId, text, durationTicks, severity, effects, causalChain);
    }

    private static List<PatchOp.EffectEntry> majorEffects() {
        return List.of(new PatchOp.EffectEntry("domain_modifier", "food", -0.05, 24));
    }

    private static List<PatchOp.EffectEntry> epicEffects() {
        return List.of(
                new PatchOp.EffectEntry("domain_modifier", "food", -0.05, 24),
                new PatchOp.EffectEntry("domain_modifier", "morale", -0.05, 24),
                new PatchOp.EffectEntry("domain_modifier", "economy", -0.05, 24)
        );
    }

    enum DifferentialOutcome {
        BOTH_ACCEPT_SAME_CORE,
        BOTH_REJECT_COVERED_RULE,
        FORMAL_REJECTS_VALIDATOR_ACCEPTS,
        VALIDATOR_REJECTS_FORMAL_ACCEPTS,
        VALIDATOR_REPAIR_ONLY_TRANSITIONAL,
        UNSUPPORTED_BY_FORMAL
    }

    record DifferentialCase(
            String name,
            DirectorRuntimeFacts runtimeFacts,
            List<PatchOp> candidatePatch,
            DifferentialOutcome expectedOutcome,
            List<String> expectedUnsupportedFeatures
    ) {
    }

    record DifferentialReport(
            String name,
            DifferentialOutcome outcome,
            boolean validatorAccepted,
            boolean validatorRepaired,
            DirectorRefinerySolveStatus solverStatus,
            boolean solverLoadFailure,
            List<String> unsupportedByFormal,
            List<PatchOp> bridgePatch
    ) {
        public DifferentialReport {
            unsupportedByFormal = List.copyOf(unsupportedByFormal == null ? List.of() : unsupportedByFormal);
            bridgePatch = List.copyOf(bridgePatch == null ? List.of() : bridgePatch);
        }
    }

    record ValidatorPathResult(boolean accepted, boolean repaired, List<PatchOp> patch, String failureMessage) {
        public ValidatorPathResult {
            patch = List.copyOf(patch == null ? new ArrayList<>() : patch);
        }
    }

    record StoryCore(String beatId, String text, long durationTicks, String severity) {
        static StoryCore empty() {
            return new StoryCore(null, null, -1, null);
        }
    }

    record DirectiveCore(int colonyId, String directive, long durationTicks) {
        static DirectiveCore empty() {
            return new DirectiveCore(-1, null, -1);
        }
    }
}
