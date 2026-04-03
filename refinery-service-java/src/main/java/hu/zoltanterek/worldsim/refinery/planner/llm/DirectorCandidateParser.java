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
            JsonNode designatedOutputNode = root.path("designatedOutput");
            if (designatedOutputNode.isMissingNode() || designatedOutputNode.isNull() || !designatedOutputNode.isObject()) {
                return Optional.empty();
            }

            StoryBeatSlotCandidate storyBeatSlot = parseStoryBeatSlot(designatedOutputNode.path("storyBeatSlot"));
            DirectiveSlotCandidate directiveSlot = parseDirectiveSlot(designatedOutputNode.path("directiveSlot"));

            return Optional.of(new DirectorCandidate(
                    parseText(root.path("explanation")),
                    new DesignatedOutputCandidate(storyBeatSlot, directiveSlot)
            ));
        } catch (Exception ex) {
            return Optional.empty();
        }
    }

    private static StoryBeatSlotCandidate parseStoryBeatSlot(JsonNode node) {
        if (node.isMissingNode() || node.isNull() || !node.isObject()) {
            return null;
        }

        List<StoryEffectCandidate> effects = new ArrayList<>();
        JsonNode effectsNode = node.path("effects");
        if (effectsNode.isArray()) {
            for (JsonNode effectNode : effectsNode) {
                effects.add(new StoryEffectCandidate(
                        effectNode.path("kind").asText(""),
                        effectNode.path("domain").asText(""),
                        effectNode.path("modifier").asDouble(0.0),
                        effectNode.path("durationTicks").asLong(0)
                ));
            }
        }

        return new StoryBeatSlotCandidate(
                node.path("beatId").asText(""),
                node.path("text").asText(""),
                node.path("durationTicks").asLong(0),
                node.path("severity").asText(""),
                List.copyOf(effects)
        );
    }

    private static DirectiveSlotCandidate parseDirectiveSlot(JsonNode node) {
        if (node.isMissingNode() || node.isNull() || !node.isObject()) {
            return null;
        }

        List<GoalBiasCandidate> biases = new ArrayList<>();
        JsonNode biasesNode = node.path("biases");
        if (biasesNode.isArray()) {
            for (JsonNode biasNode : biasesNode) {
                biases.add(new GoalBiasCandidate(
                        biasNode.path("kind").asText(""),
                        biasNode.path("goalCategory").asText(""),
                        biasNode.path("weight").asDouble(0.0),
                        biasNode.path("durationTicks").isMissingNode() ? null : biasNode.path("durationTicks").asLong()
                ));
            }
        }

        return new DirectiveSlotCandidate(
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

    private static String parseText(JsonNode node) {
        if (node.isMissingNode() || node.isNull()) {
            return null;
        }
        String value = node.asText(null);
        return value == null || value.isBlank() ? null : value.trim();
    }

    public record DirectorCandidate(String explanation, DesignatedOutputCandidate designatedOutput) {
    }

    public record DesignatedOutputCandidate(
            StoryBeatSlotCandidate storyBeatSlot,
            DirectiveSlotCandidate directiveSlot
    ) {
    }

    public record StoryBeatSlotCandidate(
            String beatId,
            String text,
            long durationTicks,
            String severity,
            List<StoryEffectCandidate> effects
    ) {
    }

    public record DirectiveSlotCandidate(
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
