package hu.zoltanterek.worldsim.refinery.planner;

import java.util.ArrayList;
import java.util.List;
import java.util.Optional;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Primary;
import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;

@Component
@Primary
public class ComposedPatchPlanner implements PatchPlanner {
    private final MockPlanner mockPlanner;
    private final LlmPlanner llmPlanner;
    private final RefineryPlanner refineryPlanner;
    private final String plannerMode;

    public ComposedPatchPlanner(
            MockPlanner mockPlanner,
            LlmPlanner llmPlanner,
            RefineryPlanner refineryPlanner,
            @Value("${planner.mode:mock}") String plannerMode
    ) {
        this.mockPlanner = mockPlanner;
        this.llmPlanner = llmPlanner;
        this.refineryPlanner = refineryPlanner;
        this.plannerMode = plannerMode;
    }

    @Override
    public PatchResponse plan(PatchRequest request) {
        PatchResponse mockResponse = mockPlanner.plan(request);
        if (!"pipeline".equalsIgnoreCase(plannerMode)) {
            return mockResponse;
        }

        Optional<List<PatchOp>> llmProposal = llmPlanner.propose(request);
        List<PatchOp> candidatePatch = llmProposal.orElseGet(mockResponse::patch);
        List<PatchOp> validatedPatch = refineryPlanner.validateAndRepair(request, candidatePatch);

        List<String> explain = new ArrayList<>();
        if (llmProposal.isPresent()) {
            explain.add("LLM planner proposed candidate patch.");
        } else {
            explain.add("LLM planner unavailable, used deterministic mock candidate patch.");
        }
        explain.add("Refinery planner validated candidate patch (scaffold pass-through).");
        explain.addAll(mockResponse.explain());

        List<String> warnings = new ArrayList<>(mockResponse.warnings());
        if (llmProposal.isEmpty()) {
            warnings.add("LLM disabled or not implemented; using mock planner output.");
        }

        return new PatchResponse(
                mockResponse.schemaVersion(),
                mockResponse.requestId(),
                mockResponse.seed(),
                validatedPatch,
                explain,
                warnings
        );
    }
}
