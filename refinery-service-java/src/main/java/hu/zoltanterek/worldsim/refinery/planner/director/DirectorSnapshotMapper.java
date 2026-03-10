package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.ArrayList;
import java.util.List;

import com.fasterxml.jackson.databind.JsonNode;

import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

public final class DirectorSnapshotMapper {
    public DirectorRuntimeFacts map(PatchRequest request) {
        JsonNode world = request.snapshot().path("world");
        int colonyCount = Math.max(1, world.path("colonyCount").asInt(1));
        long cooldown = Math.max(0L, world.path("storyBeatCooldownTicks").asLong(0L));
        List<DirectorRuntimeFacts.ActiveBeatFact> activeBeats = mapActiveBeats(world.path("activeBeats"));
        List<DirectorRuntimeFacts.ActiveDirectiveFact> activeDirectives = mapActiveDirectives(world.path("activeDirectives"));

        return new DirectorRuntimeFacts(request.tick(), colonyCount, cooldown, activeBeats, activeDirectives);
    }

    private static List<DirectorRuntimeFacts.ActiveBeatFact> mapActiveBeats(JsonNode activeBeatsNode) {
        List<DirectorRuntimeFacts.ActiveBeatFact> activeBeats = new ArrayList<>();
        if (!activeBeatsNode.isArray()) {
            return List.of();
        }

        for (JsonNode node : activeBeatsNode) {
            String beatId = node.path("beatId").asText("");
            String severity = normalizeSeverity(node.path("severity").asText("minor"));
            long remainingTicks = Math.max(0L, node.path("remainingTicks").asLong(0L));
            activeBeats.add(new DirectorRuntimeFacts.ActiveBeatFact(beatId, severity, remainingTicks));
        }

        return List.copyOf(activeBeats);
    }

    private static List<DirectorRuntimeFacts.ActiveDirectiveFact> mapActiveDirectives(JsonNode activeDirectivesNode) {
        List<DirectorRuntimeFacts.ActiveDirectiveFact> activeDirectives = new ArrayList<>();
        if (!activeDirectivesNode.isArray()) {
            return List.of();
        }

        for (JsonNode node : activeDirectivesNode) {
            int colonyId = node.path("colonyId").asInt(-1);
            String directive = node.path("directive").asText("");
            if (colonyId < 0 || directive.isBlank()) {
                continue;
            }
            activeDirectives.add(new DirectorRuntimeFacts.ActiveDirectiveFact(colonyId, directive));
        }

        return List.copyOf(activeDirectives);
    }

    private static String normalizeSeverity(String rawSeverity) {
        String severity = rawSeverity == null ? "minor" : rawSeverity.trim().toLowerCase();
        return switch (severity) {
            case "minor", "major", "epic" -> severity;
            default -> "minor";
        };
    }
}
