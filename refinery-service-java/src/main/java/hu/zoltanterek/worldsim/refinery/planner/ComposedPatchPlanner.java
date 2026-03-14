package hu.zoltanterek.worldsim.refinery.planner;

import java.util.ArrayList;
import java.util.List;
import java.util.Optional;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
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
    private static final Logger logger = LoggerFactory.getLogger(ComposedPatchPlanner.class);

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
        logger.info("legacy pipeline start goal={} plannerMode={}", request.goal(), plannerMode);
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
            logger.error("legacy pipeline refinery stage failed; falling back to mock output");
        }

        logger.info(
                "legacy pipeline completed goal={} candidateOps={} outputOps={} llmPresent={} refineryValidated={} refineryFailed={}",
                request.goal(),
                candidatePatch.size(),
                validatedPatch.size(),
                llmProposal.isPresent(),
                refineryValidated,
                refineryFailed
        );

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
        logger.info("director pipeline start outputMode={} plannerMode={}", directorOutputMode, plannerMode);
        Optional<List<PatchOp>> llmProposal = llmPlanner.propose(request);
        List<PatchOp> candidatePatch = applyDirectorOutputMode(
                llmProposal.orElseGet(mockResponse::patch),
                directorOutputMode
        );

        DirectorRefineryPlanner.DirectorValidationResult validationResult =
                directorRefineryPlanner.validateAndRepair(request, candidatePatch);
        List<PatchOp> validatedPatch = applyDirectorOutputMode(validationResult.patch(), directorOutputMode);

        String stage = validationResult.fallbackUsed()
                ? "directorStage:fallback-deterministic"
                : validationResult.validated()
                ? "directorStage:refinery-validated"
                : "directorStage:mock";

        List<String> explain = new ArrayList<>();
        explain.add(stage);
        explain.add("directorOutputMode:" + directorOutputMode);
        explain.add(llmProposal.isPresent() ? "llmStage:candidate" : "llmStage:disabled");
        explain.add("llmRetries:" + validationResult.retriesUsed());
        if (llmProposal.isPresent()) {
            explain.add("LLM planner proposed director candidate patch.");
        } else {
            explain.add("LLM planner unavailable for director; deterministic mock candidate used.");
        }
        if (validationResult.fallbackUsed()) {
            explain.add("Director validation exhausted retries; deterministic fallback candidate applied.");
        } else if (validationResult.validated()) {
            explain.add("Director candidate passed formal validation.");
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

        logger.info(
                "director pipeline completed stage={} candidateOps={} outputOps={} retries={} fallback={} llmPresent={} warningCount={}",
                stage,
                candidatePatch.size(),
                validatedPatch.size(),
                validationResult.retriesUsed(),
                validationResult.fallbackUsed(),
                llmProposal.isPresent(),
                warnings.size()
        );
        if (validationResult.fallbackUsed()) {
            logger.error(
                    "director pipeline fallback was required after retries retries={} feedbackCount={}",
                    validationResult.retriesUsed(),
                    validationResult.feedback().size()
            );
        } else if (!validationResult.feedback().isEmpty()) {
            logger.warn("director pipeline validation feedback emitted feedbackCount={}", validationResult.feedback().size());
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
