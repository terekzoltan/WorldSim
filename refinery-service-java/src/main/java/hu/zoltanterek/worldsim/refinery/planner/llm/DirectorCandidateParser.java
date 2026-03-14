package hu.zoltanterek.worldsim.refinery.planner.llm;

import java.util.ArrayList;
import java.util.List;
import java.util.Optional;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

public final class DirectorCandidateParser {
    private final ObjectMapper objectMapper;

    public DirectorCandidateParser(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    public Optional<DirectorCandidate> parse(String content) {
        String json = extractJson(content);
        if (json == null || json.isBlank()) {
            return Optional.empty();
        }

        try {
            JsonNode root = objectMapper.readTree(json);
            JsonNode storyNode = root.path("storyBeat");
            JsonNode nudgeNode = root.path("nudge");

            StoryBeatCandidate story = parseStory(storyNode);
            NudgeCandidate nudge = parseNudge(nudgeNode);
            return Optional.of(new DirectorCandidate(story, nudge));
        } catch (Exception ex) {
            return Optional.empty();
        }
    }

    private static StoryBeatCandidate parseStory(JsonNode node) {
        List<StoryEffectCandidate> effects = new ArrayList<>();
        JsonNode effectsNode = node.path("effects");
        if (effectsNode.isArray()) {
            for (JsonNode effectNode : effectsNode) {
                effects.add(new StoryEffectCandidate(
                        effectNode.path("type").asText(""),
                        effectNode.path("domain").asText(""),
                        effectNode.path("modifier").asDouble(0.0),
                        effectNode.path("durationTicks").asLong(0)
                ));
            }
        }

        return new StoryBeatCandidate(
                node.path("enabled").asBoolean(false),
                node.path("beatId").asText(""),
                node.path("text").asText(""),
                node.path("durationTicks").asLong(0),
                node.path("severity").asText(""),
                List.copyOf(effects)
        );
    }

    private static NudgeCandidate parseNudge(JsonNode node) {
        List<GoalBiasCandidate> biases = new ArrayList<>();
        JsonNode biasesNode = node.path("biases");
        if (biasesNode.isArray()) {
            for (JsonNode biasNode : biasesNode) {
                biases.add(new GoalBiasCandidate(
                        biasNode.path("type").asText(""),
                        biasNode.path("goalCategory").asText(""),
                        biasNode.path("weight").asDouble(0.0),
                        biasNode.path("durationTicks").isMissingNode() ? null : biasNode.path("durationTicks").asLong()
                ));
            }
        }

        return new NudgeCandidate(
                node.path("enabled").asBoolean(false),
                node.path("colonyId").asInt(-1),
                node.path("directive").asText(""),
                node.path("durationTicks").asLong(0),
                List.copyOf(biases)
        );
    }

    private static String extractJson(String content) {
        if (content == null) {
            return null;
        }

        String trimmed = content.trim();
        if (trimmed.startsWith("```")) {
            int firstBrace = trimmed.indexOf('{');
            int lastBrace = trimmed.lastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace) {
                return trimmed.substring(firstBrace, lastBrace + 1);
            }
        }

        int firstBrace = trimmed.indexOf('{');
        int lastBrace = trimmed.lastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace) {
            return trimmed.substring(firstBrace, lastBrace + 1);
        }

        return trimmed;
    }

    public record DirectorCandidate(StoryBeatCandidate storyBeat, NudgeCandidate nudge) {
    }

    public record StoryBeatCandidate(
            boolean enabled,
            String beatId,
            String text,
            long durationTicks,
            String severity,
            List<StoryEffectCandidate> effects
    ) {
    }

    public record NudgeCandidate(
            boolean enabled,
            int colonyId,
            String directive,
            long durationTicks,
            List<GoalBiasCandidate> biases
    ) {
    }

    public record StoryEffectCandidate(
            String type,
            String domain,
            double modifier,
            long durationTicks
    ) {
    }

    public record GoalBiasCandidate(
            String type,
            String goalCategory,
            double weight,
            Long durationTicks
    ) {
    }
}
