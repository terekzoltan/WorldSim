package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorOutputAssertions;

public final class DirectorOutputAssertionsProblemMapper {
    static final String CONTEXT_ID = "runtimeCheckpoint";
    static final String CHECKPOINT_ID = "checkpoint_000";
    static final String OUTPUT_AREA_ID = "outputArea_000";

    public OutputAreaMapping map(DirectorOutputAssertions assertions) {
        List<String> lines = new ArrayList<>();
        List<String> unsupportedFeatures = new ArrayList<>();

        lines.add("DirectorCheckpoint(" + CHECKPOINT_ID + ").");
        lines.add("DesignatedOutputArea(" + OUTPUT_AREA_ID + ").");
        lines.add("RuntimeCheckpointContext::checkpoint(" + CONTEXT_ID + ", " + CHECKPOINT_ID + ").");
        lines.add("DesignatedOutputArea::checkpoint(" + OUTPUT_AREA_ID + ", " + CHECKPOINT_ID + ").");

        if (assertions == null) {
            return new OutputAreaMapping(new DirectorProblemFragment(lines), unsupportedFeatures);
        }

        appendStory(lines, unsupportedFeatures, assertions.storyBeat());
        appendDirective(lines, assertions.directive());
        if (assertions.campaign() != null) {
            unsupportedFeatures.add("campaign");
        }

        return new OutputAreaMapping(new DirectorProblemFragment(lines), unsupportedFeatures);
    }

    private static void appendStory(
            List<String> lines,
            List<String> unsupportedFeatures,
            DirectorOutputAssertions.StoryBeatAssertion story
    ) {
        if (story == null) {
            return;
        }

        String storyId = "storyOutput_000";
        lines.add("StoryBeatOutput(" + storyId + ").");
        lines.add("DesignatedOutputArea::storyBeatSlot(" + OUTPUT_AREA_ID + ", " + storyId + ").");
        lines.add("StoryBeatOutput::beatId(" + storyId + "): \"" + escapeString(story.beatId()) + "\".");
        lines.add("StoryBeatOutput::text(" + storyId + "): \"" + escapeString(story.text()) + "\".");
        lines.add("StoryBeatOutput::durationTicks(" + storyId + "): " + Math.max(0L, story.durationTicks()) + ".");
        String severity = safeIdentifierPart(story.severity() == null ? "minor" : story.severity());
        lines.add("Severity(severity_" + severity + ").");
        lines.add("StoryBeatOutput::severity(" + storyId + ", severity_" + severity + ").");

        if (story.causalChain() != null) {
            unsupportedFeatures.add("causalChain");
        }
    }

    private static void appendDirective(List<String> lines, DirectorOutputAssertions.DirectiveAssertion directive) {
        if (directive == null) {
            return;
        }

        String directiveId = "directiveOutput_000";
        String directiveKind = safeIdentifierPart(directive.directive());
        lines.add("ColonyDirectiveOutput(" + directiveId + ").");
        lines.add("DesignatedOutputArea::directiveSlot(" + OUTPUT_AREA_ID + ", " + directiveId + ").");
        lines.add("ColonyDirectiveOutput::colonyId(" + directiveId + "): " + Math.max(0, directive.colonyId()) + ".");
        lines.add("ColonyDirectiveOutput::directiveKey(" + directiveId + "): \"" + escapeString(directive.directive()) + "\".");
        lines.add("ColonyDirectiveOutput::durationTicks(" + directiveId + "): " + Math.max(0L, directive.durationTicks()) + ".");
        lines.add("DirectiveKind(directive_" + directiveKind + ").");
        lines.add("ColonyDirectiveOutput::directive(" + directiveId + ", directive_" + directiveKind + ").");
    }

    private static String safeIdentifierPart(String value) {
        String normalized = value == null ? "unknown" : value
                .toLowerCase(Locale.ROOT)
                .replaceAll("[^a-z0-9]+", "_")
                .replaceAll("^_+|_+$", "");
        if (normalized.isBlank()) {
            return "unknown";
        }
        if (Character.isDigit(normalized.charAt(0))) {
            return "v_" + normalized;
        }
        return normalized;
    }

    private static String escapeString(String value) {
        StringBuilder builder = new StringBuilder();
        String text = value == null ? "" : value;
        for (int i = 0; i < text.length(); i++) {
            char ch = text.charAt(i);
            switch (ch) {
                case '\\' -> builder.append("\\\\");
                case '"' -> builder.append("\\\"");
                case '\n' -> builder.append("\\n");
                case '\r' -> builder.append("\\r");
                case '\t' -> builder.append("\\t");
                default -> builder.append(ch);
            }
        }
        return builder.toString();
    }

    public record OutputAreaMapping(DirectorProblemFragment fragment, List<String> unsupportedFeatures) {
        public OutputAreaMapping {
            unsupportedFeatures = List.copyOf(unsupportedFeatures == null ? List.of() : unsupportedFeatures);
        }
    }
}
