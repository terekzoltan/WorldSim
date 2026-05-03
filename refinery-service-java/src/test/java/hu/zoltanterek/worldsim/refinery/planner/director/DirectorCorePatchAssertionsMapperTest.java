package hu.zoltanterek.worldsim.refinery.planner.director;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;

class DirectorCorePatchAssertionsMapperTest {
    private final DirectorCorePatchAssertionsMapper mapper = new DirectorCorePatchAssertionsMapper();

    @Test
    void map_CoreStoryAndDirectiveAreLosslessForCoreFieldsOnly() {
        DirectorCorePatchAssertionsMapper.Result result = mapper.map(List.of(
                new PatchOp.AddStoryBeat(
                        "story-op",
                        "BEAT_A",
                        "Story",
                        24,
                        "minor",
                        List.of(new PatchOp.EffectEntry("domain_modifier", "food", 0.1, 24)),
                        null
                ),
                new PatchOp.SetColonyDirective(
                        "directive-op",
                        0,
                        "PrioritizeFood",
                        18,
                        List.of(new PatchOp.GoalBiasEntry("goal_bias", "farming", 0.2, 18L))
                )
        ));

        assertTrue(result.available());
        assertEquals("BEAT_A", result.assertions().storyBeat().beatId());
        assertEquals("minor", result.assertions().storyBeat().severity());
        assertEquals("PrioritizeFood", result.assertions().directive().directive());
        assertTrue(result.assertions().storyBeat().effects().isEmpty());
        assertTrue(result.assertions().directive().biases().isEmpty());
        assertTrue(result.unsupportedFeatures().isEmpty());
    }

    @Test
    void map_CampaignAndCausalChainAreUnsupportedButNotValidatedCoverage() {
        DirectorCorePatchAssertionsMapper.Result result = mapper.map(List.of(
                new PatchOp.AddStoryBeat(
                        "story-op",
                        "BEAT_A",
                        "Story",
                        24,
                        "minor",
                        List.of(),
                        new PatchOp.CausalChainEntry(
                                "causal_chain",
                                new PatchOp.CausalCondition("population", "gt", 3),
                                new PatchOp.CausalFollowUpBeat("BEAT_B", "Follow", 12, "minor", List.of()),
                                20,
                                1
                        )
                ),
                new PatchOp.DeclareWar("campaign-op", 1, 2, "pressure")
        ));

        assertTrue(result.available());
        assertEquals(List.of("causalChain", "campaign"), result.unsupportedFeatures());
        assertEquals("BEAT_A", result.assertions().storyBeat().beatId());
        assertEquals(null, result.assertions().storyBeat().causalChain());
        assertEquals(null, result.assertions().campaign());
    }

    @Test
    void map_MissingStorySeverityReturnsUnavailable() {
        DirectorCorePatchAssertionsMapper.Result result = mapper.map(List.of(
                new PatchOp.AddStoryBeat("story-op", "BEAT_A", "Story", 24)
        ));

        assertFalse(result.available());
        assertEquals("story_core_unavailable", result.unavailableReason());
    }
}
