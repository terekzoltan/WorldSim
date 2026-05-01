package hu.zoltanterek.worldsim.refinery.planner.refinery;

public record DirectorValidatedCoreOutput(
        StoryBeatCore storyBeat,
        DirectiveCore directive
) {
    public boolean isEmpty() {
        return storyBeat == null && directive == null;
    }

    public record StoryBeatCore(
            String beatId,
            String text,
            long durationTicks,
            String severity
    ) {
    }

    public record DirectiveCore(
            int colonyId,
            String directive,
            long durationTicks
    ) {
    }
}
