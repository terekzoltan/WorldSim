package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.ArrayList;
import java.util.List;

import com.fasterxml.jackson.databind.JsonNode;

import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

public final class DirectorSnapshotMapper {
    public DirectorRuntimeFacts map(PatchRequest request) {
        return map(request, DirectorDesign.DEFAULT_INFLUENCE_BUDGET);
    }

    public DirectorRuntimeFacts map(PatchRequest request, double configuredBudget) {
        JsonNode director = request.snapshot().path("director");
        JsonNode legacyWorld = request.snapshot().path("world");
        JsonNode constraints = request.constraints();

        int colonyCount = Math.max(1, readColonyCount(director, legacyWorld));
        long cooldown = Math.max(
                0L,
                firstPresentLong(director, legacyWorld, "beatCooldownRemainingTicks", "storyBeatCooldownTicks", 0L)
        );

        double fallbackBudget = configuredBudget > 0d
                ? configuredBudget
                : DirectorDesign.DEFAULT_INFLUENCE_BUDGET;
        double remainingInfluenceBudget = Math.max(
                0d,
                firstPresentDouble(
                        constraints,
                        director,
                        "maxBudget",
                        "remainingInfluenceBudget",
                        fallbackBudget
                )
        );

        List<DirectorRuntimeFacts.ActiveBeatFact> activeBeats = mapActiveBeats(firstPresentNode(
                director,
                legacyWorld,
                "activeBeats",
                "activeBeats"
        ));
        List<DirectorRuntimeFacts.ActiveDirectiveFact> activeDirectives = mapActiveDirectives(firstPresentNode(
                director,
                legacyWorld,
                "activeDirectives",
                "activeDirectives"
        ));

        return new DirectorRuntimeFacts(
                request.tick(),
                colonyCount,
                cooldown,
                remainingInfluenceBudget,
                activeBeats,
                activeDirectives
        );
    }

    private static JsonNode firstPresentNode(
            JsonNode primary,
            JsonNode secondary,
            String primaryField,
            String secondaryField
    ) {
        JsonNode first = primary.path(primaryField);
        if (!first.isMissingNode() && !first.isNull()) {
            return first;
        }
        return secondary.path(secondaryField);
    }

    private static int firstPresentInt(
            JsonNode primary,
            JsonNode secondary,
            String primaryField,
            String secondaryField,
            int defaultValue
    ) {
        JsonNode first = primary.path(primaryField);
        if (!first.isMissingNode() && !first.isNull()) {
            return first.asInt(defaultValue);
        }
        return secondary.path(secondaryField).asInt(defaultValue);
    }

    private static int readColonyCount(JsonNode director, JsonNode legacyWorld) {
        JsonNode directorCount = director.path("colonyCount");
        if (!directorCount.isMissingNode() && !directorCount.isNull()) {
            return directorCount.asInt(1);
        }

        JsonNode worldCount = legacyWorld.path("colonyCount");
        if (!worldCount.isMissingNode() && !worldCount.isNull()) {
            return worldCount.asInt(1);
        }

        return 1;
    }

    private static long firstPresentLong(
            JsonNode primary,
            JsonNode secondary,
            String primaryField,
            String secondaryField,
            long defaultValue
    ) {
        JsonNode first = primary.path(primaryField);
        if (!first.isMissingNode() && !first.isNull()) {
            return first.asLong(defaultValue);
        }
        return secondary.path(secondaryField).asLong(defaultValue);
    }

    private static double firstPresentDouble(
            JsonNode primary,
            JsonNode secondary,
            String primaryField,
            String secondaryField,
            double defaultValue
    ) {
        if (primary != null && !primary.isNull()) {
            JsonNode first = primary.path(primaryField);
            if (!first.isMissingNode() && !first.isNull()) {
                return first.asDouble(defaultValue);
            }
        }

        JsonNode second = secondary.path(secondaryField);
        if (!second.isMissingNode() && !second.isNull()) {
            return second.asDouble(defaultValue);
        }

        return defaultValue;
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
