package hu.zoltanterek.worldsim.refinery.planner;

import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Optional;
import java.util.Set;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Primary;
import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorCampaignOpFactory;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorCorePatchAssertionsMapper;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorInfluenceBudget;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorPipelineTelemetry;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorSnapshotMapper;
import hu.zoltanterek.worldsim.refinery.planner.refinery.DirectorRefinerySolver;
import hu.zoltanterek.worldsim.refinery.planner.refinery.DirectorSolverObservability;

@Component
@Primary
public class ComposedPatchPlanner implements PatchPlanner {
    private static final Logger logger = LoggerFactory.getLogger(ComposedPatchPlanner.class);

    private final MockPlanner mockPlanner;
    private final LlmPlanner llmPlanner;
    private final RefineryPlanner refineryPlanner;
    private final DirectorRefineryPlanner directorRefineryPlanner;
    private final DirectorPipelineTelemetry directorTelemetry;
    private final String plannerMode;
    private final String directorOutputMode;
    private final double directorBudget;
    private final boolean solverObservabilityEnabled;
    private final DirectorSnapshotMapper directorSnapshotMapper = new DirectorSnapshotMapper();
    private final DirectorCorePatchAssertionsMapper corePatchAssertionsMapper = new DirectorCorePatchAssertionsMapper();
    private final DirectorRefinerySolver directorSolver = new DirectorRefinerySolver();

    public ComposedPatchPlanner(
            MockPlanner mockPlanner,
            LlmPlanner llmPlanner,
            RefineryPlanner refineryPlanner,
            DirectorRefineryPlanner directorRefineryPlanner,
            DirectorPipelineTelemetry directorTelemetry,
            @Value("${planner.mode:mock}") String plannerMode,
            @Value("${planner.director.outputMode:both}") String directorOutputMode,
            @Value("${planner.director.budget:5.0}") double directorBudget,
            @Value("${planner.director.solverObservabilityEnabled:false}") boolean solverObservabilityEnabled
    ) {
        this.mockPlanner = mockPlanner;
        this.llmPlanner = llmPlanner;
        this.refineryPlanner = refineryPlanner;
        this.directorRefineryPlanner = directorRefineryPlanner;
        this.directorTelemetry = directorTelemetry;
        this.plannerMode = plannerMode;
        this.directorOutputMode = normalizeOutputMode(directorOutputMode);
        this.directorBudget = directorBudget > 0d ? directorBudget : DirectorDesign.DEFAULT_INFLUENCE_BUDGET;
        this.solverObservabilityEnabled = solverObservabilityEnabled;
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
        LlmDirectorPlanner.ProposalResult initialProposal = llmPlanner.proposeDirectorWithFeedback(request, List.of());
        Optional<List<PatchOp>> llmProposal = initialProposal.patch();
        List<PatchOp> candidatePatch = applyDirectorOutputMode(
                llmProposal.orElseGet(mockResponse::patch),
                directorOutputMode
        );
        int[] llmCompletionCount = new int[] { initialProposal.completionCount() };
        Set<String> sanitizeTags = new LinkedHashSet<>(initialProposal.sanitizeTags());
        boolean[] sanitized = new boolean[] { initialProposal.sanitized() };

        DirectorRefineryPlanner.DirectorValidationResult validationResult =
                directorRefineryPlanner.validateAndRepair(
                        request,
                        candidatePatch,
                        feedbackHints -> {
                            LlmDirectorPlanner.ProposalResult retryProposal = llmPlanner.proposeDirectorWithFeedback(request, feedbackHints);
                            llmCompletionCount[0] += retryProposal.completionCount();
                            if (retryProposal.sanitized()) {
                                sanitized[0] = true;
                                sanitizeTags.addAll(retryProposal.sanitizeTags());
                            }
                            return retryProposal.patch().map(patch -> applyDirectorOutputMode(patch, directorOutputMode));
                        }
                );
        List<PatchOp> validatedPatch = applyDirectorOutputMode(validationResult.patch(), directorOutputMode);
        directorTelemetry.recordLlmProposalObservability(llmCompletionCount[0], sanitized[0]);
        int causalChainOpCount = countCausalChainOps(validatedPatch);
        directorTelemetry.recordCausalChainOps(causalChainOpCount);

        String stage = validationResult.fallbackUsed()
                ? "directorStage:fallback-deterministic"
                : validationResult.validated()
                ? "directorStage:refinery-validated"
                : "directorStage:mock";

        List<String> explain = new ArrayList<>();
        explain.add(stage);
        explain.add("directorOutputMode:" + directorOutputMode);
        explain.add("llmStage:" + toLlmStageLabel(initialProposal.status()));
        explain.add("llmCompletionCount:" + llmCompletionCount[0]);
        explain.add("llmRetryRounds:" + validationResult.retriesUsed());
        explain.add("llmRetries:" + validationResult.retriesUsed());
        explain.add("llmCandidateSanitized:" + (sanitized[0] ? "true" : "false"));
        if (sanitized[0]) {
            explain.add("llmCandidateSanitizeTags:" + String.join(",", sanitizeTags));
        }
        explain.add("budgetUsed:" + formatBudgetUsed(DirectorInfluenceBudget.calculateBudgetUsed(validatedPatch)));
        explain.add("causalChainOps:" + causalChainOpCount);
        explain.add("causalChainMaxTriggers:" + DirectorDesign.CAUSAL_MAX_TRIGGERS);
        explain.add("causalChainMetrics:" + String.join(",", DirectorDesign.CAUSAL_ALLOWED_METRICS));
        explain.add("causalChainEqPolicy:population_exact;floating_tolerance=" + DirectorDesign.CAUSAL_EQ_TOLERANCE);
        if (solverObservabilityEnabled) {
            DirectorSolverObservability.Report solverReport = buildDirectorSolverObservability(request, validatedPatch);
            explain.addAll(solverReport.markers());
            directorTelemetry.recordSolverObservability(solverReport);
        }
        explain.add(describeLlmProposal(initialProposal.status()));
        if (validationResult.fallbackUsed()) {
            explain.add("Director validation exhausted retries; deterministic fallback candidate applied.");
        } else if (validationResult.validated()) {
            explain.add("Director candidate passed formal validation.");
        } else {
            explain.add("Director formal validation gate disabled; pass-through mode.");
        }

        List<String> warnings = new ArrayList<>();
        warnings.addAll(validationResult.warnings());
        addLlmProposalWarning(warnings, initialProposal.status());
        if (!validationResult.feedback().isEmpty()) {
            warnings.addAll(validationResult.feedback().stream().map(msg -> "directorFeedback:" + msg).toList());
        }

        logger.info(
                "director pipeline completed stage={} candidateOps={} outputOps={} retries={} llmCompletions={} llmSanitized={} fallback={} llmPresent={} warningCount={}",
                stage,
                candidatePatch.size(),
                validatedPatch.size(),
                validationResult.retriesUsed(),
                llmCompletionCount[0],
                sanitized[0],
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

    private DirectorSolverObservability.Report buildDirectorSolverObservability(PatchRequest request, List<PatchOp> validatedPatch) {
        try {
            DirectorCorePatchAssertionsMapper.Result mapping = corePatchAssertionsMapper.map(validatedPatch);
            if (!mapping.available()) {
                return DirectorSolverObservability.unavailable(mapping.unavailableReason());
            }

            DirectorRuntimeFacts facts = directorSnapshotMapper.map(request, directorBudget);
            return DirectorSolverObservability.fromSolveResult(directorSolver.solve(
                    facts,
                    mapping.assertions(),
                    mapping.unsupportedFeatures()
            ));
        } catch (Exception ex) {
            logger.warn("director solver observability sidecar unavailable: {}", ex.toString());
            return DirectorSolverObservability.unavailable("unexpected_exception");
        }
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
            case "nudge_only" -> patch.stream().filter(ComposedPatchPlanner::isNudgeSideDirectorOp).toList();
            case "off" -> List.of();
            default -> patch;
        };
    }

    private static boolean isNudgeSideDirectorOp(PatchOp op) {
        return op instanceof PatchOp.SetColonyDirective || DirectorCampaignOpFactory.isCampaignOp(op);
    }

    private static String formatBudgetUsed(double budgetUsed) {
        return String.format(Locale.ROOT, "%.3f", budgetUsed);
    }

    private static int countCausalChainOps(List<PatchOp> patch) {
        int count = 0;
        for (PatchOp op : patch) {
            if (op instanceof PatchOp.AddStoryBeat storyBeat && storyBeat.causalChain() != null) {
                count++;
            }
        }
        return count;
    }

    private static String toLlmStageLabel(LlmDirectorPlanner.ProposalStatus status) {
        return switch (status) {
            case DISABLED -> "disabled";
            case MISSING_CONFIG -> "missing_config";
            case CANDIDATE -> "candidate";
            case PARSE_FAILED -> "parse_failed";
            case REQUEST_FAILED -> "request_failed";
        };
    }

    private static String describeLlmProposal(LlmDirectorPlanner.ProposalStatus status) {
        return switch (status) {
            case CANDIDATE -> "LLM planner proposed director candidate patch.";
            case DISABLED -> "LLM planner disabled for director; deterministic mock candidate used.";
            case MISSING_CONFIG -> "LLM planner missing credentials/config; deterministic mock candidate used.";
            case PARSE_FAILED -> "LLM planner response could not be parsed; deterministic mock candidate used.";
            case REQUEST_FAILED -> "LLM planner request failed; deterministic mock candidate used.";
        };
    }

    private static void addLlmProposalWarning(List<String> warnings, LlmDirectorPlanner.ProposalStatus status) {
        switch (status) {
            case DISABLED -> warnings.add("LLM disabled; using mock planner output.");
            case MISSING_CONFIG -> warnings.add("LLM missing credentials or model; using mock planner output.");
            case PARSE_FAILED -> warnings.add("LLM candidate parse failed; using mock planner output.");
            case REQUEST_FAILED -> warnings.add("LLM request failed; using mock planner output.");
            case CANDIDATE -> {
            }
        }
    }
}
