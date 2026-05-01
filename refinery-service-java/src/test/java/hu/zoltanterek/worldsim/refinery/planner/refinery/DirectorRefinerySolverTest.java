package hu.zoltanterek.worldsim.refinery.planner.refinery;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorBridgeContractMapper;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorOutputAssertions;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeAssertions;

import com.fasterxml.jackson.databind.ObjectMapper;

class DirectorRefinerySolverTest {
    private final DirectorRefinerySolver solver = new DirectorRefinerySolver();
    private final DirectorBridgeContractMapper bridgeMapper = new DirectorBridgeContractMapper();
    private final DirectorProblemAssembler assembler = new DirectorProblemAssembler();
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void solve_ValidStoryAndDirectiveCandidateGeneratesSuccessfully() {
        DirectorRefinerySolveResult result = solver.solve(
                facts(0),
                new DirectorOutputAssertions(
                        new DirectorOutputAssertions.StoryBeatAssertion("BEAT_A", "Stable season", 24, "minor", List.of(), null),
                        new DirectorOutputAssertions.DirectiveAssertion(0, "PrioritizeFood", 12, List.of()),
                        null
                )
        );

        assertEquals(DirectorRefinerySolveStatus.SUCCESS, result.status(), result.diagnostics().toString());
        assertTrue(result.success());
        assertTrue(result.unsupportedFeaturesIgnored().isEmpty());
        assertNotNull(result.validatedOutput());
        assertTrue(result.diagnostics().contains("solverResult:success"));
        assertEquals("BEAT_A", result.validatedOutput().storyBeat().beatId());
        assertEquals("PrioritizeFood", result.validatedOutput().directive().directive());
        assertTrue(result.diagnostics().contains("validatedCoverage:story_core"));
        assertTrue(result.diagnostics().contains("validatedCoverage:directive_core"));
    }

    @Test
    void solve_StoryCandidateDuringCooldownReturnsFormalNonSuccess() {
        DirectorRefinerySolveResult result = solver.solve(
                facts(8),
                new DirectorOutputAssertions(
                        new DirectorOutputAssertions.StoryBeatAssertion("BEAT_A", "Cooldown violation", 24, "minor", List.of(), null),
                        null,
                        null
                )
        );

        assertEquals(DirectorRefinerySolveStatus.NON_SUCCESS, result.status());
        assertFalse(result.success());
        assertNull(result.validatedOutput());
    }

    @Test
    void solve_ReportsUnsupportedCampaignAndCausalChainWithoutFailingValidCoreCandidate() {
        DirectorRefinerySolveResult result = solver.solve(
                facts(0),
                new DirectorOutputAssertions(
                        new DirectorOutputAssertions.StoryBeatAssertion(
                                "BEAT_A",
                                "Stable season",
                                24,
                                "minor",
                                List.of(),
                                new DirectorOutputAssertions.CausalChainAssertion(
                                        new DirectorOutputAssertions.ConditionAssertion("population", "gt", 3),
                                        new DirectorOutputAssertions.FollowUpBeatAssertion("BEAT_B", "Follow", 12, "minor", List.of()),
                                        20,
                                        1
                                )
                        ),
                        null,
                        new DirectorOutputAssertions.CampaignAssertion("declare_war", 1, 2, "pressure", null, null, null, null)
                )
        );

        assertEquals(DirectorRefinerySolveStatus.SUCCESS, result.status(), result.diagnostics().toString());
        assertEquals(List.of("causalChain", "campaign"), result.unsupportedFeaturesIgnored());
        assertTrue(result.diagnostics().contains("solverResult:success"));
        assertTrue(result.diagnostics().contains("unsupportedFeaturesIgnored:causalChain"));
        assertTrue(result.diagnostics().contains("unsupportedFeaturesIgnored:campaign"));
    }

    @Test
    void solve_EmptyOutputCanStillSucceedAndBridgeToEmptyPatch() {
        DirectorRefinerySolveResult result = solver.solve(facts(0), null);

        assertEquals(DirectorRefinerySolveStatus.SUCCESS, result.status());
        assertNotNull(result.validatedOutput());
        assertTrue(result.validatedOutput().isEmpty());
        assertTrue(result.diagnostics().contains("solverResult:success"));
        assertTrue(result.diagnostics().contains("validatedOutput:empty"));
        assertTrue(bridgeMapper.toPatchOps(request(), result.validatedOutput()).isEmpty());
    }

    @Test
    void solve_ValidatedCoreBridgeMappingUsesExtractedCoreOnlyFields() {
        DirectorRefinerySolveResult result = solver.solve(
                facts(0),
                new DirectorOutputAssertions(
                        new DirectorOutputAssertions.StoryBeatAssertion(
                                "BEAT_A",
                                "Stable season",
                                24,
                                "minor",
                                List.of(new DirectorOutputAssertions.EffectAssertion("food", -0.1, 24)),
                                new DirectorOutputAssertions.CausalChainAssertion(
                                        new DirectorOutputAssertions.ConditionAssertion("population", "gt", 3),
                                        new DirectorOutputAssertions.FollowUpBeatAssertion("BEAT_B", "Follow", 12, "minor", List.of()),
                                        20,
                                        1
                                )
                        ),
                        new DirectorOutputAssertions.DirectiveAssertion(
                                0,
                                "PrioritizeFood",
                                18,
                                List.of(new DirectorOutputAssertions.BiasAssertion("farming", 0.25, 18L))
                        ),
                        new DirectorOutputAssertions.CampaignAssertion("declare_war", 1, 2, "pressure", null, null, null, null)
                )
        );

        var patch = bridgeMapper.toPatchOps(request(), result.validatedOutput());

        assertEquals(2, patch.size());
        var story = (hu.zoltanterek.worldsim.refinery.model.PatchOp.AddStoryBeat) patch.get(0);
        assertTrue(story.effects().isEmpty());
        assertNull(story.causalChain());
        var directive = (hu.zoltanterek.worldsim.refinery.model.PatchOp.SetColonyDirective) patch.get(1);
        assertTrue(directive.biases().isEmpty());
        assertTrue(result.diagnostics().contains("unvalidatedNestedFieldsOmitted:effects"));
        assertTrue(result.diagnostics().contains("unvalidatedNestedFieldsOmitted:biases"));
    }

    @Test
    void solveProblemText_DuplicateStorySlotsReturnExplicitExtractionFailure() {
        DirectorRuntimeAssertions runtimeAssertions = new DirectorRuntimeAssertions(List.of(
                "RuntimeCheckpointContext(runtimeCheckpoint).",
                "tick(runtimeCheckpoint): 100.",
                "colonyCount(runtimeCheckpoint): 2.",
                "beatCooldownRemainingTicks(runtimeCheckpoint): 0.",
                "remainingInfluenceBudget(runtimeCheckpoint): 5.0."
        ));
        DirectorProblemFragment duplicateStoryFragment = new DirectorProblemFragment(List.of(
                "DirectorCheckpoint(checkpoint_000).",
                "DesignatedOutputArea(outputArea_000).",
                "RuntimeCheckpointContext::checkpoint(runtimeCheckpoint, checkpoint_000).",
                "DesignatedOutputArea::checkpoint(outputArea_000, checkpoint_000).",
                "StoryBeatOutput(storyOutput_000).",
                "DesignatedOutputArea::storyBeatSlot(outputArea_000, storyOutput_000).",
                "StoryBeatOutput::storyBeatId(storyOutput_000): \"BEAT_A\".",
                "StoryBeatOutput::text(storyOutput_000): \"One\".",
                "StoryBeatOutput::storyDurationTicks(storyOutput_000): 24.",
                "Severity(severity_minor).",
                "StoryBeatOutput::severity(storyOutput_000, severity_minor).",
                "StoryBeatOutput(storyOutput_001).",
                "DesignatedOutputArea::storyBeatSlot(outputArea_000, storyOutput_001).",
                "StoryBeatOutput::storyBeatId(storyOutput_001): \"BEAT_B\".",
                "StoryBeatOutput::text(storyOutput_001): \"Two\".",
                "StoryBeatOutput::storyDurationTicks(storyOutput_001): 24.",
                "StoryBeatOutput::severity(storyOutput_001, severity_minor)."
        ));
        String problemText = assembler.assemble(List.of(
                new DirectorProblemFragment(runtimeAssertions.lines()),
                duplicateStoryFragment
        ));

        DirectorRefinerySolveResult result = solver.solveProblemText(problemText, List.of());

        assertEquals(DirectorRefinerySolveStatus.NON_SUCCESS, result.status());
        assertNull(result.validatedOutput());
        assertTrue(result.diagnostics().contains("extractFailure:multiple_true_story_slots"));
    }

    @Test
    void solveProblemText_DuplicateDirectiveSlotsReturnExplicitExtractionFailure() {
        DirectorRuntimeAssertions runtimeAssertions = new DirectorRuntimeAssertions(List.of(
                "RuntimeCheckpointContext(runtimeCheckpoint).",
                "tick(runtimeCheckpoint): 100.",
                "colonyCount(runtimeCheckpoint): 2.",
                "beatCooldownRemainingTicks(runtimeCheckpoint): 0.",
                "remainingInfluenceBudget(runtimeCheckpoint): 5.0."
        ));
        DirectorProblemFragment duplicateDirectiveFragment = new DirectorProblemFragment(List.of(
                "DirectorCheckpoint(checkpoint_000).",
                "DesignatedOutputArea(outputArea_000).",
                "RuntimeCheckpointContext::checkpoint(runtimeCheckpoint, checkpoint_000).",
                "DesignatedOutputArea::checkpoint(outputArea_000, checkpoint_000).",
                "ColonyDirectiveOutput(directiveOutput_000).",
                "DesignatedOutputArea::directiveSlot(outputArea_000, directiveOutput_000).",
                "ColonyDirectiveOutput::directiveColonyId(directiveOutput_000): 0.",
                "ColonyDirectiveOutput::directiveName(directiveOutput_000): \"PrioritizeFood\".",
                "ColonyDirectiveOutput::directiveDurationTicks(directiveOutput_000): 18.",
                "DirectiveKind(directive_prioritizefood).",
                "ColonyDirectiveOutput::directive(directiveOutput_000, directive_prioritizefood).",
                "ColonyDirectiveOutput(directiveOutput_001).",
                "DesignatedOutputArea::directiveSlot(outputArea_000, directiveOutput_001).",
                "ColonyDirectiveOutput::directiveColonyId(directiveOutput_001): 1.",
                "ColonyDirectiveOutput::directiveName(directiveOutput_001): \"StabilizeMorale\".",
                "ColonyDirectiveOutput::directiveDurationTicks(directiveOutput_001): 20.",
                "DirectiveKind(directive_stabilizemorale).",
                "ColonyDirectiveOutput::directive(directiveOutput_001, directive_stabilizemorale)."
        ));
        String problemText = assembler.assemble(List.of(
                new DirectorProblemFragment(runtimeAssertions.lines()),
                duplicateDirectiveFragment
        ));

        DirectorRefinerySolveResult result = solver.solveProblemText(problemText, List.of());

        assertEquals(DirectorRefinerySolveStatus.NON_SUCCESS, result.status());
        assertNull(result.validatedOutput());
        assertTrue(result.diagnostics().contains("solverResult:success"));
        assertTrue(result.diagnostics().contains("extractFailure:multiple_true_directive_slots"));
    }

    @Test
    void solveProblemText_InvalidProblemReturnsLoadFailureResult() {
        DirectorRefinerySolveResult result = solver.solveProblemText("not a refinery problem", List.of("campaign"));

        assertEquals(DirectorRefinerySolveStatus.LOAD_FAILURE, result.status());
        assertFalse(result.success());
        assertEquals(List.of("campaign"), result.unsupportedFeaturesIgnored());
        assertTrue(result.diagnostics().stream().anyMatch(item -> item.contains("load_failure")));
    }

    private PatchRequest request() {
        return new PatchRequest(
                "v1",
                "req-valid-core",
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                objectMapper.createObjectNode(),
                null
        );
    }

    private static DirectorRuntimeFacts facts(long cooldownTicks) {
        return new DirectorRuntimeFacts(100, 2, cooldownTicks, 5.0, List.of(), List.of());
    }
}
