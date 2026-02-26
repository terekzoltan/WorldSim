package hu.zoltanterek.worldsim.refinery.planner.llm;

import java.util.List;

import com.fasterxml.jackson.databind.JsonNode;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;

public final class DirectorPromptFactory {
    public String systemPrompt() {
        return "You are the WorldSim Season Director assistant. Output strict JSON only. " +
                "Do not include markdown, code fences, explanations, or extra keys.";
    }

    public String userPrompt(JsonNode snapshot, String outputMode, List<String> feedbackHints) {
        int colonyCount = Math.max(1, snapshot.path("world").path("colonyCount").asInt(1));
        long cooldown = Math.max(0L, snapshot.path("world").path("storyBeatCooldownTicks").asLong(0L));

        StringBuilder sb = new StringBuilder();
        sb.append("Create a candidate director output with this JSON shape exactly: ");
        sb.append("{\"storyBeat\":{\"enabled\":bool,\"beatId\":string,\"text\":string,\"durationTicks\":int},");
        sb.append("\"nudge\":{\"enabled\":bool,\"colonyId\":int,\"directive\":string,\"durationTicks\":int}}.");
        sb.append(" Allowed directives: ").append(String.join(",", DirectorDesign.ALLOWED_DIRECTIVES)).append('.');
        sb.append(" outputMode=").append(outputMode).append('.');
        sb.append(" colonyCount=").append(colonyCount).append('.');
        sb.append(" storyBeatCooldownTicks=").append(cooldown).append('.');
        sb.append(" Keep text under 160 chars and durations positive.");

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
}
