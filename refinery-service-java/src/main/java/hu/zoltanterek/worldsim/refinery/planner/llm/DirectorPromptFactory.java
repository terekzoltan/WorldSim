package hu.zoltanterek.worldsim.refinery.planner.llm;

import java.util.List;
import java.util.Locale;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;

public final class DirectorPromptFactory {
    public String systemPrompt() {
        return "You are the WorldSim Season Director assistant. Output strict JSON only. " +
                "Do not include markdown, code fences, explanations, or extra keys.";
    }

    public String userPrompt(
            DirectorRuntimeFacts facts,
            String outputMode,
            boolean campaignEnabled,
            List<String> feedbackHints
    ) {
        int colonyCount = Math.max(1, facts.colonyCount());
        long cooldown = Math.max(0L, facts.beatCooldownTicks());
        double remainingInfluenceBudget = Math.max(0d, facts.remainingInfluenceBudget());

        StringBuilder sb = new StringBuilder();
        sb.append("Fill only the designated output area and output strict JSON with this shape exactly: ");
        sb.append("{\"explanation\":string,\"designatedOutput\":{");
        sb.append("\"storyBeatSlot\":{\"beatId\":string,\"text\":string,\"durationTicks\":int,");
        sb.append("\"severity\":\"minor|major|epic\",\"effects\":[{\"kind\":\"domain_modifier\",\"domain\":string,");
        sb.append("\"modifier\":number,\"durationTicks\":int}],");
        sb.append("\"causalChain\":{\"type\":\"causal_chain\",\"condition\":{\"metric\":string,\"operator\":\"lt|gt|eq\",\"threshold\":number},");
        sb.append("\"followUpBeat\":{\"beatId\":string,\"text\":string,\"durationTicks\":int,\"severity\":\"minor|major|epic\",\"effects\":[{\"kind\":\"domain_modifier\",\"domain\":string,\"modifier\":number,\"durationTicks\":int}]},");
        sb.append("\"windowTicks\":int,\"maxTriggers\":1}|null}|null,");
        sb.append("\"directiveSlot\":{\"colonyId\":int,\"directive\":string,\"durationTicks\":int,");
        sb.append("\"biases\":[{\"kind\":\"goal_bias\",\"goalCategory\":string,\"weight\":number,\"durationTicks\":int}] }|null");
        if (campaignEnabled) {
            sb.append(",\"campaignSlot\":");
            sb.append("{\"kind\":\"declare_war\",\"attackerFactionId\":int,\"defenderFactionId\":int,\"reason\":string}|{");
            sb.append("\"kind\":\"propose_treaty\",\"proposerFactionId\":int,\"receiverFactionId\":int,");
            sb.append("\"treatyKind\":\"ceasefire|peace_talks\",\"note\":string}|null");
        }
        sb.append(" } }.");
        sb.append(" Do not return patch ops, op types, or opIds.");
        sb.append(" Allowed directives: ").append(String.join(",", DirectorDesign.ALLOWED_DIRECTIVES)).append('.');
        sb.append(" Allowed domains: ").append(String.join(",", DirectorDesign.VALID_DOMAINS)).append('.');
        sb.append(" Allowed goal categories: ").append(String.join(",", DirectorDesign.VALID_GOAL_CATEGORIES)).append('.');
        sb.append(" outputMode=").append(outputMode).append('.');
        sb.append(" colonyCount=").append(colonyCount).append('.');
        sb.append(" storyBeatCooldownTicks=").append(cooldown).append('.');
        sb.append(" remainingInfluenceBudget=").append(String.format(java.util.Locale.ROOT, "%.3f", remainingInfluenceBudget)).append('.');
        sb.append(" Influence budget is gameplay cost, not token budget.");
        sb.append(" Cost model: each story effect costs abs(modifier)*durationTicks*")
                .append(String.format(Locale.ROOT, "%.1f", DirectorDesign.DOMAIN_MODIFIER_COST_FACTOR)).append(".");
        sb.append(" Each directive bias costs weight*durationTicks*")
                .append(String.format(Locale.ROOT, "%.1f", DirectorDesign.GOAL_BIAS_COST_FACTOR)).append(".");
        sb.append(" Each causalChain adds base cost ")
                .append(String.format(Locale.ROOT, "%.1f", DirectorDesign.CAUSAL_CHAIN_BASE_COST))
                .append(" plus follow-up effect cost.");
        sb.append(" For causalChain.condition.metric use only: food_reserves_pct,morale_avg,population,economy_output.");
        sb.append(" Metric units: food_reserves_pct=0..100, morale_avg=0..100, population=living count, economy_output=current multiplier.");
        sb.append(" For population with operator eq, use exact integer threshold only.");
        sb.append(" For food_reserves_pct/morale_avg/economy_output with operator eq, runtime compares with tolerance <= 0.0001.");
        sb.append(" causalChain.maxTriggers must be exactly 1 and causalChain.windowTicks must be in [10,100].");
        sb.append(" For every story beat effect, set effect.durationTicks exactly equal to storyBeat.durationTicks.");
        sb.append(" For every causal follow-up effect, set effect.durationTicks exactly equal to followUpBeat.durationTicks.");
        sb.append(" Keep text under 160 chars and durations positive.");
        sb.append(" Do not emit contradictory same-domain modifiers with mixed signs in one checkpoint.");
        sb.append(" Stay within influence budget, otherwise INV-15 or INV-17 will reject the candidate.");
        sb.append(" If budget is tight, prefer no causalChain and fewer/lighter effects over richer output.");
        if (campaignEnabled) {
            sb.append(" campaignSlot is optional and budget-neutral in this phase.");
            sb.append(" campaign kind allowlist: declare_war,propose_treaty.");
            sb.append(" faction IDs must be in [0,3] and self-target is forbidden.");
            sb.append(" treatyKind allowlist: ceasefire,peace_talks.");
        }

        if (containsBudgetFeedback(feedbackHints)) {
            sb.append(" Budget repair hint: previous candidate exceeded influence budget.");
            sb.append(" Remove causalChain first.");
            sb.append(" Then reduce story effects to at most one light effect with abs(modifier)<=0.05 and short duration.");
            sb.append(" Keep directive biases empty or tiny unless budget clearly allows them.");
            sb.append(" If still over budget, omit optional slots instead of exceeding budget.");
        }

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

    private static boolean containsBudgetFeedback(List<String> feedbackHints) {
        for (String hint : feedbackHints) {
            if (hint == null) {
                continue;
            }

            String normalized = hint.toLowerCase(Locale.ROOT);
            if (normalized.contains("inv-15")
                    || normalized.contains("inv-17")
                    || normalized.contains("budget")
                    || normalized.contains("exceeds limit")) {
                return true;
            }
        }

        return false;
    }
}
