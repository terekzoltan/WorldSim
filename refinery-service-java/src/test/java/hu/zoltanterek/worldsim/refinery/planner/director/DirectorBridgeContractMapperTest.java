package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.List;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class DirectorBridgeContractMapperTest {
    private final ObjectMapper objectMapper = new ObjectMapper();
    private final DirectorBridgeContractMapper mapper = new DirectorBridgeContractMapper();

    @Test
    void mapsAssertionOutputToBridgePatchOpsDeterministically() {
        DirectorOutputAssertions assertions = new DirectorOutputAssertions(
                new DirectorOutputAssertions.StoryBeatAssertion(
                        "BEAT_A",
                        "Story text",
                        24,
                        "major",
                        List.of(new DirectorOutputAssertions.EffectAssertion("food", -0.1, 24)),
                        new DirectorOutputAssertions.CausalChainAssertion(
                                new DirectorOutputAssertions.ConditionAssertion("food_reserves_pct", "lt", 35d),
                                new DirectorOutputAssertions.FollowUpBeatAssertion(
                                        "BEAT_A_FOLLOW",
                                        "Follow-up story text",
                                        12,
                                        "major",
                                        List.of(new DirectorOutputAssertions.EffectAssertion("morale", -0.08, 12))
                                ),
                                20,
                                1
                        )
                ),
                new DirectorOutputAssertions.DirectiveAssertion(
                        0,
                        "PrioritizeFood",
                        18,
                        List.of(new DirectorOutputAssertions.BiasAssertion("farming", 0.25, 18L))
                )
        );

        PatchRequest request = new PatchRequest(
                "v1",
                "req-bridge-map",
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                objectMapper.createObjectNode(),
                null
        );

        List<PatchOp> patch = mapper.toPatchOps(request, assertions);
        assertEquals(2, patch.size());
        assertTrue(patch.get(0) instanceof PatchOp.AddStoryBeat);
        assertTrue(patch.get(1) instanceof PatchOp.SetColonyDirective);

        PatchOp.AddStoryBeat story = (PatchOp.AddStoryBeat) patch.get(0);
        assertEquals("BEAT_A", story.beatId());
        assertEquals("Story text", story.text());
        assertEquals(24, story.durationTicks());
        assertEquals("major", story.severity());
        assertEquals(1, story.effects().size());
        assertEquals("food", story.effects().get(0).domain());
        assertEquals("causal_chain", story.causalChain().type());
        assertEquals("food_reserves_pct", story.causalChain().condition().metric());
        assertEquals("BEAT_A_FOLLOW", story.causalChain().followUpBeat().beatId());
        assertEquals(1, story.causalChain().maxTriggers());

        PatchOp.SetColonyDirective directive = (PatchOp.SetColonyDirective) patch.get(1);
        assertEquals(0, directive.colonyId());
        assertEquals("PrioritizeFood", directive.directive());
        assertEquals(18, directive.durationTicks());
        assertEquals(1, directive.biases().size());
        assertEquals("farming", directive.biases().get(0).goalCategory());
    }
}
