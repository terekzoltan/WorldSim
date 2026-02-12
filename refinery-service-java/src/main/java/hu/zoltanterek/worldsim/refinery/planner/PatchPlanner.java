package hu.zoltanterek.worldsim.refinery.planner;

import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;

public interface PatchPlanner {
    PatchResponse plan(PatchRequest request);
}
