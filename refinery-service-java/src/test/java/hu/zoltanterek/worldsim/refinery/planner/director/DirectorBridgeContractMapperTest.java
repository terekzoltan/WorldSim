package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.List;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
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
                ),
                new DirectorOutputAssertions.CampaignAssertion(
                        "declare_war",
                        1,
                        2,
                        "border pressure",
                        null,
                        null,
                        null,
                        null
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
        assertEquals(3, patch.size());
        assertTrue(patch.get(0) instanceof PatchOp.AddStoryBeat);
        assertTrue(patch.get(1) instanceof PatchOp.SetColonyDirective);
        assertTrue(patch.get(2) instanceof PatchOp.DeclareWar);

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

        PatchOp.DeclareWar campaign = (PatchOp.DeclareWar) patch.get(2);
        assertEquals(1, campaign.attackerFactionId());
        assertEquals(2, campaign.defenderFactionId());
        assertEquals("border pressure", campaign.reason());
    }

    @Test
    void mapsProposeTreatyAssertionToBridgePatchOp() {
        DirectorOutputAssertions assertions = new DirectorOutputAssertions(
                null,
                null,
                new DirectorOutputAssertions.CampaignAssertion(
                        "propose_treaty",
                        null,
                        null,
                        null,
                        2,
                        1,
                        "peace_talks",
                        "de-escalate"
                )
        );

        PatchRequest request = new PatchRequest(
                "v1",
                "req-bridge-treaty",
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                objectMapper.createObjectNode(),
                null
        );

        List<PatchOp> patch = mapper.toPatchOps(request, assertions);
        assertEquals(1, patch.size());
        assertTrue(patch.get(0) instanceof PatchOp.ProposeTreaty);

        PatchOp.ProposeTreaty campaign = (PatchOp.ProposeTreaty) patch.get(0);
        assertEquals(2, campaign.proposerFactionId());
        assertEquals(1, campaign.receiverFactionId());
        assertEquals("peace_talks", campaign.treatyKind());
        assertEquals("de-escalate", campaign.note());
    }

    @Test
    void rejectsUnsupportedCampaignAssertionKind() {
        DirectorOutputAssertions assertions = new DirectorOutputAssertions(
                null,
                null,
                new DirectorOutputAssertions.CampaignAssertion(
                        "invalid_kind",
                        1,
                        2,
                        null,
                        null,
                        null,
                        null,
                        null
                )
        );

        PatchRequest request = new PatchRequest(
                "v1",
                "req-bridge-invalid",
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                objectMapper.createObjectNode(),
                null
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> mapper.toPatchOps(request, assertions)
        );

        assertTrue(ex.getMessage().contains("Unsupported campaign assertion kind"));
    }

    @Test
    void rejectsDeclareWarAssertionWhenRequiredFieldsMissing() {
        DirectorOutputAssertions assertions = new DirectorOutputAssertions(
                null,
                null,
                new DirectorOutputAssertions.CampaignAssertion(
                        "declare_war",
                        null,
                        2,
                        null,
                        null,
                        null,
                        null,
                        null
                )
        );

        PatchRequest request = new PatchRequest(
                "v1",
                "req-bridge-missing-war",
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                objectMapper.createObjectNode(),
                null
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> mapper.toPatchOps(request, assertions)
        );

        assertTrue(ex.getMessage().contains("campaign.attackerFactionId"));
    }

    @Test
    void rejectsProposeTreatyAssertionWhenRequiredFieldsMissing() {
        DirectorOutputAssertions assertions = new DirectorOutputAssertions(
                null,
                null,
                new DirectorOutputAssertions.CampaignAssertion(
                        "propose_treaty",
                        null,
                        null,
                        null,
                        2,
                        1,
                        null,
                        null
                )
        );

        PatchRequest request = new PatchRequest(
                "v1",
                "req-bridge-missing-treaty",
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                objectMapper.createObjectNode(),
                null
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> mapper.toPatchOps(request, assertions)
        );

        assertTrue(ex.getMessage().contains("campaign.treatyKind"));
    }
}
