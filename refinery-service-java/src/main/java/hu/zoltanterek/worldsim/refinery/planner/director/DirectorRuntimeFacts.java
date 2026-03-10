package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.List;

public record DirectorRuntimeFacts(
        long tick,
        int colonyCount,
        long beatCooldownTicks,
        List<ActiveBeatFact> activeBeats,
        List<ActiveDirectiveFact> activeDirectives
) {
    public record ActiveBeatFact(
            String beatId,
            String severity,
            long remainingTicks
    ) {
    }

    public record ActiveDirectiveFact(
            int colonyId,
            String directive
    ) {
    }
}
