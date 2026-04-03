package hu.zoltanterek.worldsim.refinery.planner.llm;

import java.util.List;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;

public final class DirectorPromptFactory {
    public String systemPrompt() {
        return "You are the WorldSim Season Director assistant. Output strict JSON only. " +
                "Do not include markdown, code fences, explanations, or extra keys.";
    }

    public String userPrompt(DirectorRuntimeFacts facts, String outputMode, List<String> feedbackHints) {
        int colonyCount = Math.max(1, facts.colonyCount());
        long cooldown = Math.max(0L, facts.beatCooldownTicks());
        double remainingInfluenceBudget = Math.max(0d, facts.remainingInfluenceBudget());

        StringBuilder sb = new StringBuilder();
        sb.append("Fill only the designated output area and output strict JSON with this shape exactly: ");
        sb.append("{\"explanation\":string,\"designatedOutput\":{");
        sb.append("\"storyBeatSlot\":{\"beatId\":string,\"text\":string,\"durationTicks\":int,");
        sb.append("\"severity\":\"minor|major|epic\",\"effects\":[{\"kind\":\"domain_modifier\",\"domain\":string,");
        sb.append("\"modifier\":number,\"durationTicks\":int}]}|null,");
        sb.append("\"directiveSlot\":{\"colonyId\":int,\"directive\":string,\"durationTicks\":int,");
        sb.append("\"biases\":[{\"kind\":\"goal_bias\",\"goalCategory\":string,\"weight\":number,\"durationTicks\":int}] }|null } }.");
        sb.append(" Do not return patch ops, op types, or opIds.");
        sb.append(" Allowed directives: ").append(String.join(",", DirectorDesign.ALLOWED_DIRECTIVES)).append('.');
        sb.append(" Allowed domains: ").append(String.join(",", DirectorDesign.VALID_DOMAINS)).append('.');
        sb.append(" Allowed goal categories: ").append(String.join(",", DirectorDesign.VALID_GOAL_CATEGORIES)).append('.');
        sb.append(" outputMode=").append(outputMode).append('.');
        sb.append(" colonyCount=").append(colonyCount).append('.');
        sb.append(" storyBeatCooldownTicks=").append(cooldown).append('.');
        sb.append(" remainingInfluenceBudget=").append(String.format(java.util.Locale.ROOT, "%.3f", remainingInfluenceBudget)).append('.');
        sb.append(" For every story beat effect, set effect.durationTicks exactly equal to storyBeat.durationTicks.");
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
}
