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
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> "{}"
        );

        Optional<List<PatchOp>> result = planner.propose(directorRequest(), List.of());
        assertTrue(result.isEmpty());
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
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> "not-json"
        );

        Optional<List<PatchOp>> result = planner.propose(directorRequest(), List.of("INV-02 bad"));
        assertTrue(result.isEmpty());
    }

    @Test
    void propose_WhenValidResponse_MapsToDirectorOps() {
        String response = """
                {
                  "storyBeat": {
                    "enabled": true,
                    "beatId": " BEAT_LLM_1 ",
                    "text": " LLM narrative text ",
                    "durationTicks": 200,
                    "severity": "major",
                    "effects": [
                      {"type":"domain_modifier","domain":"food","modifier":0.9,"durationTicks":200},
                      {"type":"domain_modifier","domain":"unknown","modifier":0.1,"durationTicks":10}
                    ]
                  },
                  "nudge": {
                    "enabled": true,
                    "colonyId": 99,
                    "directive": " PrioritizeFood ",
                    "durationTicks": -5,
                    "biases": [
                      {"type":"goal_bias","goalCategory":"farming","weight":0.9,"durationTicks":100}
                    ]
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
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> response
        );

        LlmDirectorPlanner.ProposalResult detailed = planner.proposeDetailed(directorRequest(), List.of());
        assertTrue(detailed.patch().isPresent());
        assertEquals(1, detailed.completionCount());
        assertTrue(detailed.sanitized());
        assertFalse(detailed.sanitizeTags().isEmpty());
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
    void propose_UsesWorldColonyCountInsteadOfDirectorPopulationForDirectiveClamp() {
        String response = """
                {
                  "storyBeat": { "enabled": false },
                  "nudge": {
                    "enabled": true,
                    "colonyId": 99,
                    "directive": "PrioritizeFood",
                    "durationTicks": 12,
                    "biases": []
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
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                (m, t, tok, s, u) -> {
                    capturedUserPrompt.set(u);
                    return "{\"storyBeat\":{\"enabled\":false},\"nudge\":{\"enabled\":false}}";
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
        assertTrue(prompt.contains("effect.durationTicks exactly equal to storyBeat.durationTicks"));
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
