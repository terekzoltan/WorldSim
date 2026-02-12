package hu.zoltanterek.worldsim.refinery.model;

import java.util.List;

public record PatchResponse(
        String schemaVersion,
        String requestId,
        long seed,
        List<PatchOp> patch,
        List<String> explain,
        List<String> warnings
) {
}
