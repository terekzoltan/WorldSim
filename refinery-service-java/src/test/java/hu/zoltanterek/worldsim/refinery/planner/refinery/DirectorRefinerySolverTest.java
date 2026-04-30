package hu.zoltanterek.worldsim.refinery.planner.refinery;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorOutputAssertions;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;

class DirectorRefinerySolverTest {
    private final DirectorRefinerySolver solver = new DirectorRefinerySolver();

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

        assertEquals(DirectorRefinerySolveStatus.SUCCESS, result.status());
        assertTrue(result.success());
        assertTrue(result.unsupportedFeaturesIgnored().isEmpty());
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

        assertEquals(DirectorRefinerySolveStatus.SUCCESS, result.status());
        assertEquals(List.of("causalChain", "campaign"), result.unsupportedFeaturesIgnored());
    }

    @Test
    void solveProblemText_InvalidProblemReturnsLoadFailureResult() {
        DirectorRefinerySolveResult result = solver.solveProblemText("not a refinery problem", List.of("campaign"));

        assertEquals(DirectorRefinerySolveStatus.LOAD_FAILURE, result.status());
        assertFalse(result.success());
        assertEquals(List.of("campaign"), result.unsupportedFeaturesIgnored());
        assertTrue(result.diagnostics().stream().anyMatch(item -> item.contains("load_failure")));
    }

    private static DirectorRuntimeFacts facts(long cooldownTicks) {
        return new DirectorRuntimeFacts(100, 2, cooldownTicks, 5.0, List.of(), List.of());
    }
}
