package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.List;

public record DirectorOutputAssertions(
        StoryBeatAssertion storyBeat,
        DirectiveAssertion directive,
        CampaignAssertion campaign
) {
    public record StoryBeatAssertion(
            String beatId,
            String text,
            long durationTicks,
            String severity,
            List<EffectAssertion> effects,
            CausalChainAssertion causalChain
    ) {
    }

    public record DirectiveAssertion(
            int colonyId,
            String directive,
            long durationTicks,
            List<BiasAssertion> biases
    ) {
    }

    public record EffectAssertion(
            String domain,
            double modifier,
            long durationTicks
    ) {
    }

    public record BiasAssertion(
            String goalCategory,
            double weight,
            Long durationTicks
    ) {
    }

    public record CampaignAssertion(
            String kind,
            Integer attackerFactionId,
            Integer defenderFactionId,
            String reason,
            Integer proposerFactionId,
            Integer receiverFactionId,
            String treatyKind,
            String note
    ) {
    }

    public record CausalChainAssertion(
            ConditionAssertion condition,
            FollowUpBeatAssertion followUpBeat,
            long windowTicks,
            int maxTriggers
    ) {
    }

    public record ConditionAssertion(
            String metric,
            String operator,
            double threshold
    ) {
    }

    public record FollowUpBeatAssertion(
            String beatId,
            String text,
            long durationTicks,
            String severity,
            List<EffectAssertion> effects
    ) {
    }
}
