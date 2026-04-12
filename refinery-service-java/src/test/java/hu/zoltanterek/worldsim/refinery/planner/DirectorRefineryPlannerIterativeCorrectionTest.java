package hu.zoltanterek.worldsim.refinery.planner;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;
import java.util.concurrent.atomic.AtomicInteger;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorPipelineTelemetry;

class DirectorRefineryPlannerIterativeCorrectionTest {
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void validateAndRepair_UsesFeedbackProviderToRecoverAfterInvalidCandidate() {
        DirectorPipelineTelemetry telemetry = new DirectorPipelineTelemetry();
        DirectorRefineryPlanner planner = new DirectorRefineryPlanner(true, 1, telemetry);

        PatchRequest request = baseDirectorRequest();
        List<PatchOp> invalidCandidate = List.of(new PatchOp.SetColonyDirective("op_bad", 0, "UnknownDirective", 10));
        List<PatchOp> recovered = List.of(new PatchOp.SetColonyDirective("op_ok", 0, "PrioritizeFood", 10));

        AtomicInteger feedbackCalls = new AtomicInteger();
        DirectorRefineryPlanner.DirectorValidationResult result = planner.validateAndRepair(
                request,
                invalidCandidate,
                feedback -> {
                    feedbackCalls.incrementAndGet();
                    return feedbackCalls.get() == 1 ? java.util.Optional.of(recovered) : java.util.Optional.empty();
                }
        );

        assertTrue(result.validated());
        assertFalse(result.fallbackUsed());
        assertEquals(1, result.retriesUsed());
        assertEquals(1, feedbackCalls.get());
        assertEquals(1, result.patch().size());
        assertTrue(result.patch().get(0) instanceof PatchOp.SetColonyDirective);
    }

    @Test
    void validateAndRepair_FallsBackWhenFeedbackProviderCannotConverge() {
        DirectorPipelineTelemetry telemetry = new DirectorPipelineTelemetry();
        DirectorRefineryPlanner planner = new DirectorRefineryPlanner(true, 1, telemetry);

        PatchRequest request = directorRequestWithActiveMajorBeat();
        List<PatchOp> invalidCandidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story",
                        "BEAT_MAJOR_2",
                        "Major pressure wave arrives.",
                        20,
                        "major",
                        List.of(new PatchOp.EffectEntry("domain_modifier", "food", -0.05, 20))
                )
        );

        DirectorRefineryPlanner.DirectorValidationResult result = planner.validateAndRepair(
                request,
                invalidCandidate,
                feedback -> java.util.Optional.empty()
        );

        assertFalse(result.validated());
        assertTrue(result.fallbackUsed());
        assertEquals(1, result.retriesUsed());
        assertTrue(result.warnings().stream().anyMatch(item -> item.contains("directorFallback")));
    }

    private PatchRequest baseDirectorRequest() {
        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.putObject("director")
                .put("colonyPopulation", 1)
                .put("beatCooldownRemainingTicks", 0);

        return new PatchRequest(
                "v1",
                "req-director-base",
                123L,
                456L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );
    }

    private PatchRequest directorRequestWithActiveMajorBeat() {
        ObjectNode director = objectMapper.createObjectNode();
        director.put("colonyPopulation", 1);
        director.put("beatCooldownRemainingTicks", 0);
        ArrayNode activeBeats = director.putArray("activeBeats");
        activeBeats.addObject()
                .put("beatId", "BEAT_MAJOR_1")
                .put("severity", "major")
                .put("remainingTicks", 8);

        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.set("director", director);

        return new PatchRequest(
                "v1",
                "req-director-major",
                123L,
                789L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );
    }
}
