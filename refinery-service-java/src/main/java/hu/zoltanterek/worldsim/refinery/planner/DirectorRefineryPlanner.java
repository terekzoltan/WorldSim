package hu.zoltanterek.worldsim.refinery.planner;

import java.util.ArrayList;
import java.util.List;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorModelValidator;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorSnapshotMapper;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorValidationOutcome;

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
            return new DirectorValidationResult(candidatePatch, false, List.of(), List.of());
        }

        DirectorRuntimeFacts facts = snapshotMapper.map(request);
        List<PatchOp> attempt = candidatePatch;
        List<String> feedback = new ArrayList<>();

        for (int retry = 0; retry <= maxRetries; retry++) {
            try {
                DirectorValidationOutcome outcome = validator.validateAndRepair(attempt, facts);
                return new DirectorValidationResult(outcome.patch(), true, outcome.warnings(), feedback);
            } catch (IllegalArgumentException ex) {
                feedback.add(ex.getMessage());
                if (retry == maxRetries) {
                    break;
                }
                attempt = validator.conservativeRetryPatch(attempt, facts);
            }
        }

        throw new IllegalArgumentException("Director validation failed after retries: " + String.join(" | ", feedback));
    }

    public record DirectorValidationResult(
            List<PatchOp> patch,
            boolean validated,
            List<String> warnings,
            List<String> feedback
    ) {
    }
}
