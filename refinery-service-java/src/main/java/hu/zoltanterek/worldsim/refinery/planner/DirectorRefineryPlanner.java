package hu.zoltanterek.worldsim.refinery.planner;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import java.util.function.Function;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorCampaignOpFactory;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorModelValidator;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorPipelineTelemetry;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorSnapshotMapper;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorValidationOutcome;
import hu.zoltanterek.worldsim.refinery.util.DeterministicIds;

@Component
public class DirectorRefineryPlanner {
    private static final Logger logger = LoggerFactory.getLogger(DirectorRefineryPlanner.class);

    private final boolean refineryEnabled;
    private final int maxRetries;
    private final double directorBudget;
    private final boolean campaignEnabled;
    private final DirectorPipelineTelemetry telemetry;
    private final DirectorSnapshotMapper snapshotMapper = new DirectorSnapshotMapper();
    private final DirectorModelValidator validator;

    @Autowired
    public DirectorRefineryPlanner(
            @Value("${planner.refinery.enabled:false}") boolean refineryEnabled,
            @Value("${planner.director.maxRetries:2}") int maxRetries,
            @Value("${planner.director.budget:5.0}") double directorBudget,
            @Value("${planner.director.campaignEnabled:false}") boolean campaignEnabled,
            DirectorPipelineTelemetry telemetry
    ) {
        this.refineryEnabled = refineryEnabled;
        this.maxRetries = Math.max(0, maxRetries);
        this.directorBudget = directorBudget > 0d ? directorBudget : DirectorDesign.DEFAULT_INFLUENCE_BUDGET;
        this.campaignEnabled = campaignEnabled;
        this.telemetry = telemetry;
        this.validator = new DirectorModelValidator(campaignEnabled);
    }

    DirectorRefineryPlanner(boolean refineryEnabled, int maxRetries) {
        this(refineryEnabled, maxRetries, DirectorDesign.DEFAULT_INFLUENCE_BUDGET, false, new DirectorPipelineTelemetry());
    }

    DirectorRefineryPlanner(boolean refineryEnabled, int maxRetries, DirectorPipelineTelemetry telemetry) {
        this(refineryEnabled, maxRetries, DirectorDesign.DEFAULT_INFLUENCE_BUDGET, false, telemetry);
    }

    DirectorRefineryPlanner(boolean refineryEnabled, int maxRetries, boolean campaignEnabled, DirectorPipelineTelemetry telemetry) {
        this(refineryEnabled, maxRetries, DirectorDesign.DEFAULT_INFLUENCE_BUDGET, campaignEnabled, telemetry);
    }

    public DirectorValidationResult validateAndRepair(PatchRequest request, List<PatchOp> candidatePatch) {
        return validateAndRepair(request, candidatePatch, feedback -> Optional.empty());
    }

    public DirectorValidationResult validateAndRepair(
            PatchRequest request,
            List<PatchOp> candidatePatch,
            Function<List<String>, Optional<List<PatchOp>>> retryCandidateProvider
    ) {
        if (!refineryEnabled) {
            logger.info("director refinery validation disabled; pass-through candidateOps={}", candidatePatch.size());
            return new DirectorValidationResult(candidatePatch, false, List.of(), List.of(), 0, false);
        }

        DirectorRuntimeFacts facts = snapshotMapper.map(request, directorBudget);
        List<PatchOp> attempt = candidatePatch;
        List<String> feedback = new ArrayList<>();
        List<String> warnings = new ArrayList<>();

        int duplicateOpIds = countDuplicateOpIds(candidatePatch);
        if (duplicateOpIds > 0) {
            logger.warn(
                    "director candidate contains duplicate opId entries duplicateCount={} candidateOps={}",
                    duplicateOpIds,
                    candidatePatch.size()
            );
        }

        logger.info(
                "director validation start candidateOps={} maxRetries={} colonyCount={} beatCooldownTicks={} remainingBudget={}",
                candidatePatch.size(),
                maxRetries,
                facts.colonyCount(),
                facts.beatCooldownTicks(),
                facts.remainingInfluenceBudget()
        );

        for (int retry = 0; retry <= maxRetries; retry++) {
            try {
                DirectorValidationOutcome outcome = validator.validateAndRepair(attempt, facts);
                int droppedOps = Math.max(0, attempt.size() - outcome.patch().size());
                if (droppedOps > 0) {
                    telemetry.recordRejectedCommands(droppedOps);
                    logger.warn(
                            "director validator dropped operations droppedOps={} retry={} inputOps={} outputOps={}",
                            droppedOps,
                            retry,
                            attempt.size(),
                            outcome.patch().size()
                    );
                }

                warnings.addAll(outcome.warnings());
                feedback.addAll(outcome.feedback());

                telemetry.recordValidatedOutput(retry);
                logger.info(
                        "director validation completed validated=true retriesUsed={} outputOps={} warnings={} feedback={}",
                        retry,
                        outcome.patch().size(),
                        warnings.size(),
                        feedback.size()
                );
                return new DirectorValidationResult(outcome.patch(), true, warnings, feedback, retry, false);
            } catch (IllegalArgumentException ex) {
                telemetry.recordRejectedCommands(attempt.size());
                feedback.add(ex.getMessage());
                logger.warn(
                        "director validation failed retry={} error={} attemptOps={}",
                        retry,
                        ex.getMessage(),
                        attempt.size()
                );
                if (retry == maxRetries) {
                    break;
                }

                Optional<List<PatchOp>> regenerated = retryCandidateProvider.apply(List.copyOf(feedback));
                if (regenerated.isPresent() && !regenerated.get().isEmpty()) {
                    attempt = regenerated.get();
                    logger.warn(
                            "director retry prepared from llm feedback retry={} nextAttemptOps={} feedbackCount={}",
                            retry + 1,
                            attempt.size(),
                            feedback.size()
                    );
                    continue;
                }

                int attemptBeforeRetry = attempt.size();
                attempt = validator.conservativeRetryPatch(attempt, facts);
                int retryDroppedOps = Math.max(0, attemptBeforeRetry - attempt.size());
                if (retryDroppedOps > 0) {
                    telemetry.recordRejectedCommands(retryDroppedOps);
                    logger.warn(
                            "director conservative retry dropped operations droppedOps={} nextAttemptOps={}",
                            retryDroppedOps,
                            attempt.size()
                    );
                }
                feedback.add(DirectorDesign.INV_14 + " conservative retry regenerated candidate patch.");
                logger.warn("director retry prepared retry={} nextAttemptOps={}", retry + 1, attempt.size());
            }
        }

        List<PatchOp> fallback = buildDeterministicFallback(request, facts, campaignEnabled);
        warnings.add("directorFallback deterministic fallback planner output was used.");
        telemetry.recordFallback(maxRetries);
        logger.error(
                "director validation exhausted retries; deterministic fallback applied fallbackOps={} retriesUsed={} feedback={}",
                fallback.size(),
                maxRetries,
                feedback.size()
        );
        return new DirectorValidationResult(fallback, false, warnings, feedback, maxRetries, true);
    }

    private static int countDuplicateOpIds(List<PatchOp> candidatePatch) {
        Set<String> unique = new HashSet<>();
        int duplicateCount = 0;
        for (PatchOp op : candidatePatch) {
            String opId = extractOpId(op);
            if (opId == null || opId.isBlank()) {
                continue;
            }
            if (!unique.add(opId)) {
                duplicateCount++;
            }
        }
        return duplicateCount;
    }

    private static String extractOpId(PatchOp op) {
        if (op instanceof PatchOp.AddStoryBeat storyBeat) {
            return storyBeat.opId();
        }
        if (op instanceof PatchOp.SetColonyDirective directive) {
            return directive.opId();
        }
        if (op instanceof PatchOp.DeclareWar declareWar) {
            return declareWar.opId();
        }
        if (op instanceof PatchOp.ProposeTreaty treaty) {
            return treaty.opId();
        }
        return null;
    }

    private static List<PatchOp> buildDeterministicFallback(PatchRequest request, DirectorRuntimeFacts facts, boolean campaignEnabled) {
        List<PatchOp> fallback = new ArrayList<>(2);

        if (facts.beatCooldownTicks() <= 0) {
            String beatId = "BEAT_FALLBACK_" + DeterministicIds.shortStableId(
                    request.seed(),
                    request.tick(),
                    Goal.SEASON_DIRECTOR_CHECKPOINT.name(),
                    "fallback_beat"
            );
            String storyOpId = DeterministicIds.opId(
                    request.seed(),
                    request.tick(),
                    Goal.SEASON_DIRECTOR_CHECKPOINT.name(),
                    "addStoryBeat",
                    beatId
            );

            fallback.add(new PatchOp.AddStoryBeat(
                    storyOpId,
                    beatId,
                    "Season pressure rises; hold lines and preserve reserves.",
                    DirectorDesign.MIN_STORY_DURATION + 11
            ));
        }

        if (facts.colonyCount() > 0) {
            String directive = "PrioritizeFood";
            String directiveOpId = DeterministicIds.opId(
                    request.seed(),
                    request.tick(),
                    Goal.SEASON_DIRECTOR_CHECKPOINT.name(),
                    "setColonyDirective",
                    "fallback:colony0:" + directive
            );
            fallback.add(new PatchOp.SetColonyDirective(
                    directiveOpId,
                    0,
                    directive,
                    DirectorDesign.MIN_DIRECTIVE_DURATION + 17
            ));
        }

        DirectorCampaignOpFactory.buildDeterministicCampaignOp(request, campaignEnabled).ifPresent(fallback::add);

        return fallback;
    }

    public record DirectorValidationResult(
            List<PatchOp> patch,
            boolean validated,
            List<String> warnings,
            List<String> feedback,
            int retriesUsed,
            boolean fallbackUsed
    ) {
    }
}
