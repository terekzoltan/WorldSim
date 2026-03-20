package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.List;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;

public final class DirectorInfluenceBudget {
    private DirectorInfluenceBudget() {
    }

    public static double calculateBudgetUsed(List<PatchOp> patch) {
        double total = 0.0d;
        for (PatchOp op : patch) {
            if (op instanceof PatchOp.AddStoryBeat storyBeat) {
                total += calculateStoryBeatCost(storyBeat);
                continue;
            }
            if (op instanceof PatchOp.SetColonyDirective directive) {
                total += calculateDirectiveCost(directive);
            }
        }
        return total;
    }

    private static double calculateStoryBeatCost(PatchOp.AddStoryBeat storyBeat) {
        if (storyBeat.effects() == null || storyBeat.effects().isEmpty()) {
            return 0.0d;
        }

        double total = 0.0d;
        for (PatchOp.EffectEntry effect : storyBeat.effects()) {
            if (effect == null) {
                continue;
            }
            if (!"domain_modifier".equalsIgnoreCase(effect.type())) {
                continue;
            }
            double duration = Math.max(0L, effect.durationTicks());
            total += Math.abs(effect.modifier()) * duration * DirectorDesign.DOMAIN_MODIFIER_COST_FACTOR;
        }
        return total;
    }

    private static double calculateDirectiveCost(PatchOp.SetColonyDirective directive) {
        if (directive.biases() == null || directive.biases().isEmpty()) {
            return 0.0d;
        }

        double total = 0.0d;
        for (PatchOp.GoalBiasEntry bias : directive.biases()) {
            if (bias == null) {
                continue;
            }
            if (!"goal_bias".equalsIgnoreCase(bias.type())) {
                continue;
            }
            long duration = bias.durationTicks() == null
                    ? directive.durationTicks()
                    : bias.durationTicks();
            total += Math.max(0d, bias.weight()) * Math.max(0L, duration) * DirectorDesign.GOAL_BIAS_COST_FACTOR;
        }
        return total;
    }
}
