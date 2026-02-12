package hu.zoltanterek.worldsim.refinery.model;

import com.fasterxml.jackson.databind.JsonNode;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

public record PatchRequest(
        @NotBlank String schemaVersion,
        @NotBlank String requestId,
        long seed,
        long tick,
        @NotNull Goal goal,
        @NotNull JsonNode snapshot,
        JsonNode constraints
) {
}
