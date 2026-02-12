package hu.zoltanterek.worldsim.refinery.planner;

import java.util.List;

import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

@Component
public class RefineryPlanner {
    public List<PatchOp> validateAndRepair(PatchRequest request, List<PatchOp> candidatePatch) {
        // TODO: Integrate Refinery to validate and repair candidatePatch against constraints.
        return candidatePatch;
    }
}
