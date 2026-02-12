package hu.zoltanterek.worldsim.refinery.model;

import java.util.List;

public record ErrorResponse(
        String message,
        List<String> details
) {
}
