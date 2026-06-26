package hu.zoltanterek.worldsim.refinery.planner.refinery;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorModelValidator;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorOutputAssertions;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;

class DirectorRefineryPredicatePromotionTest {
    private final DirectorRefinerySolver solver = new DirectorRefinerySolver();
    private final DirectorModelValidator validator = new DirectorModelValidator();

    @Test
    void solve_ActiveMajorRejectsMajorStoryAsFormalNonSuccess() {
        DirectorRefinerySolveResult result = solveWithActiveBeat("major", 6L, "major");

        assertFormalNonSuccess(result);
        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(List.of(storyPatch("major")), factsWithActiveBeat("major", 6L))
        );
        assertTrue(ex.getMessage().contains(DirectorDesign.INV_08));
    }

    @Test
    void solve_ActiveEpicRejectsEpicStoryAsFormalNonSuccess() {
        DirectorRefinerySolveResult result = solveWithActiveBeat("epic", 10L, "epic");

        assertFormalNonSuccess(result);
        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(List.of(storyPatch("epic")), factsWithActiveBeat("epic", 10L))
        );
        assertTrue(ex.getMessage().contains(DirectorDesign.INV_09));
    }

    @Test
    void solve_ActiveMajorAllowsMinorStory() {
        assertFormalSuccess(solveWithActiveBeat("major", 6L, "minor"), "minor");
    }

    @Test
    void solve_ActiveMajorAllowsEpicStory() {
        assertFormalSuccess(solveWithActiveBeat("major", 6L, "epic"), "epic");
    }

    @Test
    void solve_ActiveEpicAllowsMajorStory() {
        assertFormalSuccess(solveWithActiveBeat("epic", 10L, "major"), "major");
    }

    @Test
    void solve_ExpiredActiveMajorAllowsMajorStory() {
        assertFormalSuccess(solveWithActiveBeat("major", 0L, "major"), "major");
        assertDoesNotThrow(() -> validator.validateAndRepair(List.of(storyPatch("major")), factsWithActiveBeat("major", 0L)));
    }

    private DirectorRefinerySolveResult solveWithActiveBeat(String activeSeverity, long remainingTicks, String storySeverity) {
        return solver.solve(
                factsWithActiveBeat(activeSeverity, remainingTicks),
                new DirectorOutputAssertions(
                        new DirectorOutputAssertions.StoryBeatAssertion(
                                "BEAT_" + storySeverity.toUpperCase(),
                                "Explicit " + storySeverity + " story",
                                24,
                                storySeverity,
                                List.of(),
                                null
                        ),
                        null,
                        null
                )
        );
    }

    private static DirectorRuntimeFacts factsWithActiveBeat(String severity, long remainingTicks) {
        return new DirectorRuntimeFacts(
                100,
                2,
                0,
                5.0,
                List.of(new DirectorRuntimeFacts.ActiveBeatFact("BEAT_ACTIVE_" + severity.toUpperCase(), severity, remainingTicks)),
                List.of()
        );
    }

    private static PatchOp.AddStoryBeat storyPatch(String severity) {
        return new PatchOp.AddStoryBeat(
                "op_story_" + severity,
                "BEAT_" + severity.toUpperCase(),
                "Explicit " + severity + " story",
                24,
                severity,
                effectsForSeverity(severity)
        );
    }

    private static List<PatchOp.EffectEntry> effectsForSeverity(String severity) {
        return switch (severity) {
            case "major" -> List.of(new PatchOp.EffectEntry("domain_modifier", "food", -0.05, 24));
            case "epic" -> List.of(
                    new PatchOp.EffectEntry("domain_modifier", "food", -0.05, 24),
                    new PatchOp.EffectEntry("domain_modifier", "morale", -0.05, 24),
                    new PatchOp.EffectEntry("domain_modifier", "economy", -0.05, 24)
            );
            default -> List.of();
        };
    }

    private static void assertFormalNonSuccess(DirectorRefinerySolveResult result) {
        assertEquals(DirectorRefinerySolveStatus.NON_SUCCESS, result.status(), result.diagnostics().toString());
        assertFalse(result.success());
        assertNull(result.validatedOutput());
        assertFalse(result.diagnostics().contains("solverResult:load_failure"), result.diagnostics().toString());
    }

    private static void assertFormalSuccess(DirectorRefinerySolveResult result, String expectedSeverity) {
        assertEquals(DirectorRefinerySolveStatus.SUCCESS, result.status(), result.diagnostics().toString());
        assertTrue(result.success());
        assertEquals(expectedSeverity, result.validatedOutput().storyBeat().severity());
        assertTrue(result.diagnostics().contains("solverResult:success"));
        assertTrue(result.diagnostics().contains("validatedCoverage:story_core"));
    }
}
