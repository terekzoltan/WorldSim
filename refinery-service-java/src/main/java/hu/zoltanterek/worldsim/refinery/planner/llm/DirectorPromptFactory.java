package hu.zoltanterek.worldsim.refinery.planner.llm;

import java.util.List;

import com.fasterxml.jackson.databind.JsonNode;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;

public final class DirectorPromptFactory {
    public String systemPrompt() {
        return "You are the WorldSim Season Director assistant. Output strict JSON only. " +
                "Do not include markdown, code fences, explanations, or extra keys.";
    }

    public String userPrompt(JsonNode snapshot, String outputMode, double remainingInfluenceBudget, List<String> feedbackHints) {
        JsonNode director = snapshot.path("director");
        JsonNode world = snapshot.path("world");
        int colonyCount = Math.max(
                1,
                readIntWithFallback(director, "colonyCount", world, "colonyCount", 1)
        );
        long cooldown = Math.max(
                0L,
                readLongWithFallback(director, "beatCooldownRemainingTicks", world, "storyBeatCooldownTicks", 0L)
        );

        StringBuilder sb = new StringBuilder();
        sb.append("Create a candidate director output with this JSON shape exactly: ");
        sb.append("{\"storyBeat\":{\"enabled\":bool,\"beatId\":string,\"text\":string,\"durationTicks\":int,");
        sb.append("\"severity\":\"minor|major|epic\",\"effects\":[{\"type\":\"domain_modifier\",\"domain\":string,");
        sb.append("\"modifier\":number,\"durationTicks\":int}]},");
        sb.append("\"nudge\":{\"enabled\":bool,\"colonyId\":int,\"directive\":string,\"durationTicks\":int,");
        sb.append("\"biases\":[{\"type\":\"goal_bias\",\"goalCategory\":string,\"weight\":number,\"durationTicks\":int}]}}.");
        sb.append(" Allowed directives: ").append(String.join(",", DirectorDesign.ALLOWED_DIRECTIVES)).append('.');
        sb.append(" Allowed domains: ").append(String.join(",", DirectorDesign.VALID_DOMAINS)).append('.');
        sb.append(" Allowed goal categories: ").append(String.join(",", DirectorDesign.VALID_GOAL_CATEGORIES)).append('.');
        sb.append(" outputMode=").append(outputMode).append('.');
        sb.append(" colonyCount=").append(colonyCount).append('.');
        sb.append(" storyBeatCooldownTicks=").append(cooldown).append('.');
        sb.append(" remainingInfluenceBudget=").append(String.format(java.util.Locale.ROOT, "%.3f", remainingInfluenceBudget)).append('.');
        sb.append(" Keep text under 160 chars and durations positive.");
        sb.append(" Do not emit contradictory same-domain modifiers with mixed signs in one checkpoint.");
        sb.append(" Stay within influence budget, otherwise INV-15 will reject the candidate.");

        if (!feedbackHints.isEmpty()) {
            sb.append(" Previous formal validation feedback: ");
            for (int i = 0; i < feedbackHints.size(); i++) {
                if (i > 0) {
                    sb.append(" | ");
                }
                sb.append(feedbackHints.get(i));
            }
            sb.append('.');
        }

        return sb.toString();
    }

    private static int readIntWithFallback(
            JsonNode primary,
            String primaryField,
            JsonNode fallback,
            String fallbackField,
            int defaultValue
    ) {
        JsonNode value = primary.path(primaryField);
        if (!value.isMissingNode() && !value.isNull()) {
            return value.asInt(defaultValue);
        }
        return fallback.path(fallbackField).asInt(defaultValue);
    }

    private static long readLongWithFallback(
            JsonNode primary,
            String primaryField,
            JsonNode fallback,
            String fallbackField,
            long defaultValue
    ) {
        JsonNode value = primary.path(primaryField);
        if (!value.isMissingNode() && !value.isNull()) {
            return value.asLong(defaultValue);
        }
        return fallback.path(fallbackField).asLong(defaultValue);
    }
}
