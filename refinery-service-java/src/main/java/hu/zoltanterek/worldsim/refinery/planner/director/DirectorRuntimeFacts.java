package hu.zoltanterek.worldsim.refinery.planner.director;

public record DirectorRuntimeFacts(
        long tick,
        int colonyCount,
        long beatCooldownTicks
) {
}
