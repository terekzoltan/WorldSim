package hu.zoltanterek.worldsim.refinery.planner.refinery;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorOutputAssertions;

class DirectorOutputAssertionsProblemMapperTest {
    private final DirectorOutputAssertionsProblemMapper mapper = new DirectorOutputAssertionsProblemMapper();

    @Test
    void map_DirectiveOnlyEmitsSharedCheckpointBinding() {
        DirectorOutputAssertions assertions = new DirectorOutputAssertions(
                null,
                new DirectorOutputAssertions.DirectiveAssertion(0, "PrioritizeFood", 12, List.of()),
                null
        );

        DirectorOutputAssertionsProblemMapper.OutputAreaMapping result = mapper.map(assertions);
        String fragment = result.fragment().problemFragment();

        assertTrue(fragment.contains("DirectorCheckpoint(checkpoint_000)."));
        assertTrue(fragment.contains("DesignatedOutputArea(outputArea_000)."));
        assertTrue(fragment.contains("RuntimeCheckpointContext::checkpoint(runtimeCheckpoint, checkpoint_000)."));
        assertTrue(fragment.contains("DesignatedOutputArea::checkpoint(outputArea_000, checkpoint_000)."));
        assertTrue(fragment.contains("ColonyDirectiveOutput(directiveOutput_000)."));
        assertTrue(fragment.contains("DesignatedOutputArea::directiveSlot(outputArea_000, directiveOutput_000)."));
        assertTrue(fragment.contains("ColonyDirectiveOutput::directiveKey(directiveOutput_000): \"PrioritizeFood\"."));
    }

    @Test
    void map_StoryOnlyEmitsStableStoryOutput() {
        DirectorOutputAssertions assertions = new DirectorOutputAssertions(
                new DirectorOutputAssertions.StoryBeatAssertion("BEAT_A", "Story", 24, "major", List.of(), null),
                null,
                null
        );

        String fragment = mapper.map(assertions).fragment().problemFragment();

        assertTrue(fragment.contains("StoryBeatOutput(storyOutput_000)."));
        assertTrue(fragment.contains("DesignatedOutputArea::storyBeatSlot(outputArea_000, storyOutput_000)."));
        assertTrue(fragment.contains("StoryBeatOutput::beatId(storyOutput_000): \"BEAT_A\"."));
        assertTrue(fragment.contains("StoryBeatOutput::text(storyOutput_000): \"Story\"."));
        assertTrue(fragment.contains("StoryBeatOutput::durationTicks(storyOutput_000): 24."));
        assertTrue(fragment.contains("StoryBeatOutput::severity(storyOutput_000, severity_major)."));
    }

    @Test
    void map_StoryAndDirectiveKeepDeterministicOrdering() {
        DirectorOutputAssertions assertions = new DirectorOutputAssertions(
                new DirectorOutputAssertions.StoryBeatAssertion("BEAT_A", "Story", 24, "minor", List.of(), null),
                new DirectorOutputAssertions.DirectiveAssertion(1, "StabilizeMorale", 18, List.of()),
                null
        );

        String fragment = mapper.map(assertions).fragment().problemFragment();

        assertTrue(fragment.indexOf("DirectorCheckpoint(checkpoint_000).") < fragment.indexOf("StoryBeatOutput(storyOutput_000)."));
        assertTrue(fragment.indexOf("StoryBeatOutput(storyOutput_000).") < fragment.indexOf("ColonyDirectiveOutput(directiveOutput_000)."));
    }

    @Test
    void map_ReportsCampaignAndCausalChainAsUnsupportedFeatures() {
        DirectorOutputAssertions assertions = new DirectorOutputAssertions(
                new DirectorOutputAssertions.StoryBeatAssertion(
                        "BEAT_A",
                        "Story",
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
        );

        DirectorOutputAssertionsProblemMapper.OutputAreaMapping result = mapper.map(assertions);

        assertEquals(List.of("causalChain", "campaign"), result.unsupportedFeatures());
    }

    @Test
    void map_EscapesStrings() {
        DirectorOutputAssertions assertions = new DirectorOutputAssertions(
                new DirectorOutputAssertions.StoryBeatAssertion("BEAT \"Q\"", "Line\\path\nnext", 24, "minor", List.of(), null),
                null,
                null
        );

        String fragment = mapper.map(assertions).fragment().problemFragment();

        assertTrue(fragment.contains("StoryBeatOutput::beatId(storyOutput_000): \"BEAT \\\"Q\\\"\"."));
        assertTrue(fragment.contains("StoryBeatOutput::text(storyOutput_000): \"Line\\\\path\\nnext\"."));
    }
}
