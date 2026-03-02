package hu.zoltanterek.worldsim.refinery.planner;

import java.util.ArrayList;
import java.util.List;
import java.util.Optional;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Primary;
import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;

@Component
@Primary
public class ComposedPatchPlanner implements PatchPlanner {
    private final MockPlanner mockPlanner;
    private final LlmPlanner llmPlanner;
    private final RefineryPlanner refineryPlanner;
    private final DirectorRefineryPlanner directorRefineryPlanner;
    private final String plannerMode;
    private final String directorOutputMode;

    public ComposedPatchPlanner(
            MockPlanner mockPlanner,
            LlmPlanner llmPlanner,
            RefineryPlanner refineryPlanner,
            DirectorRefineryPlanner directorRefineryPlanner,
            @Value("${planner.mode:mock}") String plannerMode,
            @Value("${planner.director.outputMode:both}") String directorOutputMode
    ) {
        this.mockPlanner = mockPlanner;
        this.llmPlanner = llmPlanner;
        this.refineryPlanner = refineryPlanner;
        this.directorRefineryPlanner = directorRefineryPlanner;
        this.plannerMode = plannerMode;
        this.directorOutputMode = normalizeOutputMode(directorOutputMode);
    }

    @Override
    public PatchResponse plan(PatchRequest request) {
        PatchResponse mockResponse = mockPlanner.plan(request);
        if (!"pipeline".equalsIgnoreCase(plannerMode)) {
            return mockResponse;
        }

        if (request.goal() == Goal.SEASON_DIRECTOR_CHECKPOINT) {
            return planDirectorPipeline(request, mockResponse);
        }

        return planLegacyPipeline(request, mockResponse);
    }

    private PatchResponse planLegacyPipeline(PatchRequest request, PatchResponse mockResponse) {
        Optional<List<PatchOp>> llmProposal = llmPlanner.propose(request);
        List<PatchOp> candidatePatch = llmProposal.orElseGet(mockResponse::patch);
        List<PatchOp> validatedPatch;
        boolean refineryValidated = false;
        boolean refineryFailed = false;
        try {
            validatedPatch = refineryPlanner.validateAndRepair(request, candidatePatch);
            refineryValidated = refineryPlanner.isRefineryEnabled() && request.goal() == Goal.TECH_TREE_PATCH;
        } catch (IllegalArgumentException ex) {
            validatedPatch = mockResponse.patch();
            refineryFailed = true;
        }

        List<String> explain = new ArrayList<>();
        explain.add(refineryPlanner.isRefineryEnabled() ? "refineryStage:enabled" : "refineryStage:disabled");
        if (llmProposal.isPresent()) {
            explain.add("LLM planner proposed candidate patch.");
        } else {
            explain.add("LLM planner unavailable, used deterministic mock candidate patch.");
        }
        if (refineryValidated) {
            explain.add("Refinery planner validated TECH_TREE_PATCH addTech slice.");
        } else {
            explain.add("Refinery planner stage skipped or pass-through.");
        }
        explain.addAll(mockResponse.explain());

        List<String> warnings = new ArrayList<>(mockResponse.warnings());
        if (llmProposal.isEmpty()) {
            warnings.add("LLM disabled or not implemented; using mock planner output.");
        }
        if (refineryFailed) {
            warnings.add("Refinery validation failed, falling back to deterministic mock patch.");
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

    private PatchResponse planDirectorPipeline(PatchRequest request, PatchResponse mockResponse) {
        Optional<List<PatchOp>> llmProposal = llmPlanner.propose(request);
        List<PatchOp> candidatePatch = applyDirectorOutputMode(
                llmProposal.orElseGet(mockResponse::patch),
                directorOutputMode
        );

        List<PatchOp> validatedPatch = candidatePatch;
        boolean validationFailed = false;
        DirectorRefineryPlanner.DirectorValidationResult validationResult;
        try {
            validationResult = directorRefineryPlanner.validateAndRepair(request, candidatePatch);
            validatedPatch = applyDirectorOutputMode(validationResult.patch(), directorOutputMode);
        } catch (IllegalArgumentException ex) {
            validationResult = new DirectorRefineryPlanner.DirectorValidationResult(
                    applyDirectorOutputMode(mockResponse.patch(), directorOutputMode),
                    false,
                    List.of(),
                    List.of(ex.getMessage())
            );
            validatedPatch = validationResult.patch();
            validationFailed = true;
        }

        String stage = validationResult.validated()
                ? "directorStage:refinery-validated"
                : validationFailed ? "directorStage:fallback-mock" : "directorStage:mock";

        List<String> explain = new ArrayList<>();
        explain.add(stage);
        explain.add("directorOutputMode:" + directorOutputMode);
        explain.add(llmProposal.isPresent() ? "llmStage:candidate" : "llmStage:disabled");
        if (llmProposal.isPresent()) {
            explain.add("LLM planner proposed director candidate patch.");
        } else {
            explain.add("LLM planner unavailable for director; deterministic mock candidate used.");
        }
        if (validationResult.validated()) {
            explain.add("Director candidate passed formal validation.");
        } else if (validationFailed) {
            explain.add("Director validation failed; deterministic mock fallback applied.");
        } else {
            explain.add("Director formal validation gate disabled; pass-through mode.");
        }

        List<String> warnings = new ArrayList<>();
        warnings.addAll(validationResult.warnings());
        if (llmProposal.isEmpty()) {
            warnings.add("LLM disabled or missing credentials; using mock planner output.");
        }
        if (!validationResult.feedback().isEmpty()) {
            warnings.addAll(validationResult.feedback().stream().map(msg -> "directorFeedback:" + msg).toList());
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

    private static String normalizeOutputMode(String rawMode) {
        String mode = rawMode == null ? "both" : rawMode.trim().toLowerCase();
        return switch (mode) {
            case "both", "story_only", "nudge_only", "off" -> mode;
            default -> "both";
        };
    }

    private static List<PatchOp> applyDirectorOutputMode(List<PatchOp> patch, String outputMode) {
        return switch (outputMode) {
            case "story_only" -> patch.stream().filter(op -> op instanceof PatchOp.AddStoryBeat).toList();
            case "nudge_only" -> patch.stream().filter(op -> op instanceof PatchOp.SetColonyDirective).toList();
            case "off" -> List.of();
            default -> patch;
        };
    }
}
