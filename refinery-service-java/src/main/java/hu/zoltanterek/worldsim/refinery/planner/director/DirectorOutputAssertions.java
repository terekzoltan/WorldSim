package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.List;

public record DirectorOutputAssertions(
        StoryBeatAssertion storyBeat,
        DirectiveAssertion directive
) {
    public record StoryBeatAssertion(
            String beatId,
            String text,
            long durationTicks,
            String severity,
            List<EffectAssertion> effects
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
}
