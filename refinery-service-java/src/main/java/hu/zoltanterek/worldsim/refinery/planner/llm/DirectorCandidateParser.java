package hu.zoltanterek.worldsim.refinery.planner.llm;

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
        return new StoryBeatCandidate(
                node.path("enabled").asBoolean(false),
                node.path("beatId").asText(""),
                node.path("text").asText(""),
                node.path("durationTicks").asLong(0)
        );
    }

    private static NudgeCandidate parseNudge(JsonNode node) {
        return new NudgeCandidate(
                node.path("enabled").asBoolean(false),
                node.path("colonyId").asInt(-1),
                node.path("directive").asText(""),
                node.path("durationTicks").asLong(0)
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

    public record StoryBeatCandidate(boolean enabled, String beatId, String text, long durationTicks) {
    }

    public record NudgeCandidate(boolean enabled, int colonyId, String directive, long durationTicks) {
    }
}
