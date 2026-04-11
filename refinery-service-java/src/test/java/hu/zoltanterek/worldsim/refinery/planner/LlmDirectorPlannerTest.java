package hu.zoltanterek.worldsim.refinery.planner;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;
import java.util.Optional;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.planner.llm.DirectorCandidateParser;
import hu.zoltanterek.worldsim.refinery.planner.llm.DirectorPromptFactory;

class LlmDirectorPlannerTest {
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void propose_WhenDisabled_ReturnsEmpty() {
        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                false,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> "{}"
        );

        LlmDirectorPlanner.ProposalResult result = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(result.patch().isEmpty());
        assertEquals(LlmDirectorPlanner.ProposalStatus.DISABLED, result.status());
    }

    @Test
    void propose_WhenMissingConfig_ReturnsMissingConfigStatus() {
        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> "{}"
        );

        LlmDirectorPlanner.ProposalResult result = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(result.patch().isEmpty());
        assertEquals(LlmDirectorPlanner.ProposalStatus.MISSING_CONFIG, result.status());
    }

    @Test
    void propose_WhenMalformedResponse_ReturnsEmptyGracefully() {
        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> "not-json"
        );

        LlmDirectorPlanner.ProposalResult result = planner.proposeDetailed(directorRequest(), List.of("INV-02 bad"));
        assertTrue(result.patch().isEmpty());
        assertEquals(1, result.completionCount());
        assertEquals(LlmDirectorPlanner.ProposalStatus.PARSE_FAILED, result.status());
    }

    @Test
    void propose_WhenValidResponse_MapsToDirectorOps() {
        String response = """
                {
                  "designatedOutput": {
                    "storyBeatSlot": {
                      "beatId": " BEAT_LLM_1 ",
                      "text": " LLM narrative text ",
                      "durationTicks": 200,
                      "severity": "major",
                      "effects": [
                        {"kind":"domain_modifier","domain":"food","modifier":0.9,"durationTicks":200},
                        {"kind":"domain_modifier","domain":"unknown","modifier":0.1,"durationTicks":10}
                      ]
                    },
                    "directiveSlot": {
                      "colonyId": 99,
                      "directive": " PrioritizeFood ",
                      "durationTicks": -5,
                      "biases": [
                        {"kind":"goal_bias","goalCategory":"farming","weight":0.9,"durationTicks":100}
                      ]
                    }
                  }
                }
                """;

        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> response
        );

        LlmDirectorPlanner.ProposalResult detailed = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(detailed.patch().isPresent());
        assertEquals(1, detailed.completionCount());
        assertTrue(detailed.sanitized());
        assertFalse(detailed.sanitizeTags().isEmpty());
        assertEquals(LlmDirectorPlanner.ProposalStatus.CANDIDATE, detailed.status());
        assertTrue(detailed.sanitizeTags().contains("story_duration_clamped"));
        assertTrue(detailed.sanitizeTags().contains("directive_duration_clamped"));

        List<PatchOp> ops = detailed.patch().get();
        assertEquals(2, ops.size());
        assertTrue(ops.get(0) instanceof PatchOp.AddStoryBeat);
        assertTrue(ops.get(1) instanceof PatchOp.SetColonyDirective);

        PatchOp.AddStoryBeat story = (PatchOp.AddStoryBeat) ops.get(0);
        assertEquals("BEAT_LLM_1", story.beatId());
        assertEquals("LLM narrative text", story.text());
        assertEquals(96, story.durationTicks());
        assertEquals(1, story.effects().size());
        assertEquals(96, story.effects().get(0).durationTicks());

        PatchOp.SetColonyDirective directive = (PatchOp.SetColonyDirective) ops.get(1);
        assertEquals(1, directive.colonyId());
        assertEquals("PrioritizeFood", directive.directive());
        assertEquals(1, directive.durationTicks());
        assertEquals(1, directive.biases().size());
    }

    @Test
    void propose_WhenCausalChainPresent_MapsNestedChainToStoryBeatOp() {
        String response = """
                {
                  "designatedOutput": {
                    "storyBeatSlot": {
                      "beatId": "BEAT_CHAIN_A",
                      "text": "Primary chain story",
                      "durationTicks": 18,
                      "severity": "major",
                      "effects": [
                        {"kind":"domain_modifier","domain":"food","modifier":-0.1,"durationTicks":18}
                      ],
                      "causalChain": {
                        "type": "causal_chain",
                        "condition": {"metric":"food_reserves_pct","operator":"lt","threshold":35},
                        "followUpBeat": {
                          "beatId": "BEAT_CHAIN_A_FOLLOW",
                          "text": "Follow-up chain story",
                          "durationTicks": 12,
                          "severity": "major",
                          "effects": [
                            {"kind":"domain_modifier","domain":"morale","modifier":-0.08,"durationTicks":12}
                          ]
                        },
                        "windowTicks": 20,
                        "maxTriggers": 1
                      }
                    },
                    "directiveSlot": null
                  }
                }
                """;

        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> response
        );

        LlmDirectorPlanner.ProposalResult detailed = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(detailed.patch().isPresent());

        PatchOp.AddStoryBeat story = (PatchOp.AddStoryBeat) detailed.patch().get().get(0);
        assertTrue(story.causalChain() != null);
        assertEquals("causal_chain", story.causalChain().type());
        assertEquals("food_reserves_pct", story.causalChain().condition().metric());
        assertEquals("BEAT_CHAIN_A_FOLLOW", story.causalChain().followUpBeat().beatId());
        assertEquals(1, story.causalChain().maxTriggers());
    }

    @Test
    void propose_WhenPopulationEqThresholdIsFractional_DropsCausalChain() {
        String response = """
                {
                  "designatedOutput": {
                    "storyBeatSlot": {
                      "beatId": "BEAT_CHAIN_B",
                      "text": "Population check",
                      "durationTicks": 14,
                      "severity": "major",
                      "effects": [
                        {"kind":"domain_modifier","domain":"food","modifier":-0.05,"durationTicks":14}
                      ],
                      "causalChain": {
                        "type": "causal_chain",
                        "condition": {"metric":"population","operator":"eq","threshold":24.5},
                        "followUpBeat": {
                          "beatId": "BEAT_CHAIN_B_FOLLOW",
                          "text": "Follow-up chain story",
                          "durationTicks": 10,
                          "severity": "major",
                          "effects": [
                            {"kind":"domain_modifier","domain":"morale","modifier":-0.08,"durationTicks":10}
                          ]
                        },
                        "windowTicks": 20,
                        "maxTriggers": 1
                      }
                    },
                    "directiveSlot": null
                  }
                }
                """;

        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> response
        );

        LlmDirectorPlanner.ProposalResult detailed = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(detailed.patch().isPresent());
        PatchOp.AddStoryBeat story = (PatchOp.AddStoryBeat) detailed.patch().get().get(0);
        assertTrue(story.causalChain() == null);
        assertTrue(detailed.sanitizeTags().contains("causal_chain_population_eq_non_integer"));
    }

    @Test
    void propose_WhenCampaignEnabled_MapsCampaignSlotToPatchOp() {
        String response = """
                {
                  "designatedOutput": {
                    "storyBeatSlot": null,
                    "directiveSlot": null,
                    "campaignSlot": {
                      "kind": "declare_war",
                      "attackerFactionId": 1,
                      "defenderFactionId": 2,
                      "reason": "border pressure"
                    }
                  }
                }
                """;

        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                true,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> response
        );

        LlmDirectorPlanner.ProposalResult detailed = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(detailed.patch().isPresent());
        assertEquals(1, detailed.patch().get().size());
        assertTrue(detailed.patch().get().get(0) instanceof PatchOp.DeclareWar);
    }

    @Test
    void propose_WhenCampaignDisabled_DropsCampaignSlot() {
        String response = """
                {
                  "designatedOutput": {
                    "storyBeatSlot": null,
                    "directiveSlot": null,
                    "campaignSlot": {
                      "kind": "propose_treaty",
                      "proposerFactionId": 1,
                      "receiverFactionId": 2,
                      "treatyKind": "ceasefire",
                      "note": "hold lines"
                    }
                  }
                }
                """;

        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> response
        );

        LlmDirectorPlanner.ProposalResult detailed = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(detailed.patch().isEmpty());
        assertTrue(detailed.sanitizeTags().contains("campaign_slot_dropped_gate_off"));
    }

    @Test
    void propose_WhenCampaignTreatyInvalid_DropsCampaignSlot() {
        String response = """
                {
                  "designatedOutput": {
                    "storyBeatSlot": null,
                    "directiveSlot": null,
                    "campaignSlot": {
                      "kind": "propose_treaty",
                      "proposerFactionId": 1,
                      "receiverFactionId": 1,
                      "treatyKind": "alliance"
                    }
                  }
                }
                """;

        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                true,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> response
        );

        LlmDirectorPlanner.ProposalResult detailed = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(detailed.patch().isEmpty());
        assertTrue(detailed.sanitizeTags().contains("campaign_self_target_dropped")
                || detailed.sanitizeTags().contains("campaign_treaty_dropped"));
    }

    @Test
    void propose_UsesWorldColonyCountInsteadOfDirectorPopulationForDirectiveClamp() {
        String response = """
                {
                  "designatedOutput": {
                    "storyBeatSlot": null,
                    "directiveSlot": {
                      "colonyId": 99,
                      "directive": "PrioritizeFood",
                      "durationTicks": 12,
                      "biases": []
                    }
                  }
                }
                """;

        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> response
        );

        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.putObject("world").put("colonyCount", 2);
        snapshot.putObject("director")
                .put("colonyPopulation", 47)
                .put("beatCooldownRemainingTicks", 0);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-llm-colony-count",
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );

        Optional<List<PatchOp>> maybeOps = planner.propose(request, List.of());
        assertTrue(maybeOps.isPresent());
        PatchOp.SetColonyDirective directive = (PatchOp.SetColonyDirective) maybeOps.get().get(0);
        assertEquals(1, directive.colonyId());
    }

    @Test
    void propose_InjectsRemainingInfluenceBudgetIntoPrompt() {
        AtomicReference<String> capturedUserPrompt = new AtomicReference<>();
        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> {
                    capturedUserPrompt.set(u);
                    return "{\"designatedOutput\":{\"storyBeatSlot\":null,\"directiveSlot\":null}}";
                }
        );

        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.putObject("world").put("colonyCount", 2);
        snapshot.putObject("director")
                .put("colonyPopulation", 47)
                .put("beatCooldownRemainingTicks", 0)
                .put("remainingInfluenceBudget", 3.75);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-llm-budget",
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );

        planner.propose(request, List.of());

        String prompt = capturedUserPrompt.get();
        assertTrue(prompt.contains("colonyCount=2"));
        assertTrue(prompt.contains("remainingInfluenceBudget=3.750"));
        assertTrue(prompt.contains("designatedOutput"));
        assertTrue(prompt.contains("effect.durationTicks exactly equal to storyBeat.durationTicks"));
    }

    @Test
    void propose_WhenLegacyStoryNudgeShapeProvided_ReturnsParseFailedStatus() {
        String response = """
                {
                  "storyBeat": { "enabled": false },
                  "nudge": { "enabled": false }
                }
                """;

        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> response
        );

        LlmDirectorPlanner.ProposalResult result = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(result.patch().isEmpty());
        assertEquals(1, result.completionCount());
        assertEquals(LlmDirectorPlanner.ProposalStatus.PARSE_FAILED, result.status());
    }

    @Test
    void propose_WhenCompletionGatewayFails_ReturnsRequestFailedStatus() {
        LlmDirectorPlanner planner = new LlmDirectorPlanner(
                true,
                "key",
                "model",
                0.4,
                500,
                "both",
                5.0,
                false,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> {
                    throw new IllegalStateException("gateway offline");
                }
        );

        LlmDirectorPlanner.ProposalResult result = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(result.patch().isEmpty());
        assertEquals(0, result.completionCount());
        assertEquals(LlmDirectorPlanner.ProposalStatus.REQUEST_FAILED, result.status());
    }

    private PatchRequest directorRequest() {
        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.putObject("world").put("colonyCount", 2);
        snapshot.putObject("director")
                .put("colonyPopulation", 47)
                .put("beatCooldownRemainingTicks", 0);

        return new PatchRequest(
                "v1",
                "req-llm-director",
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );
    }
}
