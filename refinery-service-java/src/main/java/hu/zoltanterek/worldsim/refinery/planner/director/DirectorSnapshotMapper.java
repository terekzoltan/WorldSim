package hu.zoltanterek.worldsim.refinery.planner.director;

import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

public final class DirectorSnapshotMapper {
    public DirectorRuntimeFacts map(PatchRequest request) {
        int colonyCount = Math.max(1, request.snapshot().path("world").path("colonyCount").asInt(1));
        long cooldown = Math.max(0L, request.snapshot().path("world").path("storyBeatCooldownTicks").asLong(0L));
        return new DirectorRuntimeFacts(request.tick(), colonyCount, cooldown);
    }
}
