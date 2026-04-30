package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.util.List;

public record DirectorRefinerySolveResult(
        DirectorRefinerySolveStatus status,
        String generatorResult,
        List<String> diagnostics,
        List<String> unsupportedFeaturesIgnored
) {
    public DirectorRefinerySolveResult {
        diagnostics = List.copyOf(diagnostics == null ? List.of() : diagnostics);
        unsupportedFeaturesIgnored = List.copyOf(unsupportedFeaturesIgnored == null ? List.of() : unsupportedFeaturesIgnored);
    }

    public boolean success() {
        return status == DirectorRefinerySolveStatus.SUCCESS;
    }
}
