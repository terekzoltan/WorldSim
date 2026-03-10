package hu.zoltanterek.worldsim.refinery.planner;

import java.util.ArrayList;
import java.util.List;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorModelValidator;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorSnapshotMapper;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorValidationOutcome;
import hu.zoltanterek.worldsim.refinery.util.DeterministicIds;

@Component
public class DirectorRefineryPlanner {
    private final boolean refineryEnabled;
    private final int maxRetries;
    private final DirectorSnapshotMapper snapshotMapper = new DirectorSnapshotMapper();
    private final DirectorModelValidator validator = new DirectorModelValidator();

    public DirectorRefineryPlanner(
            @Value("${planner.refinery.enabled:false}") boolean refineryEnabled,
            @Value("${planner.director.maxRetries:2}") int maxRetries
    ) {
        this.refineryEnabled = refineryEnabled;
        this.maxRetries = Math.max(0, maxRetries);
    }

    public DirectorValidationResult validateAndRepair(PatchRequest request, List<PatchOp> candidatePatch) {
        if (!refineryEnabled) {
            return new DirectorValidationResult(candidatePatch, false, List.of(), List.of(), 0, false);
        }

        DirectorRuntimeFacts facts = snapshotMapper.map(request);
        List<PatchOp> attempt = candidatePatch;
        List<String> feedback = new ArrayList<>();
        List<String> warnings = new ArrayList<>();

        for (int retry = 0; retry <= maxRetries; retry++) {
            try {
                DirectorValidationOutcome outcome = validator.validateAndRepair(attempt, facts);
                warnings.addAll(outcome.warnings());
                feedback.addAll(outcome.feedback());
                return new DirectorValidationResult(outcome.patch(), true, warnings, feedback, retry, false);
            } catch (IllegalArgumentException ex) {
                feedback.add(ex.getMessage());
                if (retry == maxRetries) {
                    break;
                }
                attempt = validator.conservativeRetryPatch(attempt, facts);
                feedback.add(DirectorDesign.INV_14 + " conservative retry regenerated candidate patch.");
            }
        }

        List<PatchOp> fallback = buildDeterministicFallback(request, facts);
        warnings.add("directorFallback deterministic fallback planner output was used.");
        return new DirectorValidationResult(fallback, false, warnings, feedback, maxRetries, true);
    }

    private static List<PatchOp> buildDeterministicFallback(PatchRequest request, DirectorRuntimeFacts facts) {
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
