package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.List;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;

public record DirectorValidationOutcome(
        List<PatchOp> patch,
        List<String> warnings
) {
}
