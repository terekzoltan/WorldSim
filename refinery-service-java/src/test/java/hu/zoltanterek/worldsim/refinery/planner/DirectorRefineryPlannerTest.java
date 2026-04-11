package hu.zoltanterek.worldsim.refinery.planner;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorPipelineTelemetry;

class DirectorRefineryPlannerTest {
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void validateAndRepair_UsesDeterministicFallbackAfterRetries() {
        DirectorPipelineTelemetry telemetry = new DirectorPipelineTelemetry();
        DirectorRefineryPlanner planner = new DirectorRefineryPlanner(true, 0, telemetry);
        PatchRequest request = directorRequest(128L, 2, 0);

        List<PatchOp> invalidCandidate = List.of(
                new PatchOp.SetColonyDirective("op_bad", 99, "UnknownDirective", 5)
        );

        DirectorRefineryPlanner.DirectorValidationResult result = planner.validateAndRepair(request, invalidCandidate);

        assertFalse(result.validated());
        assertTrue(result.fallbackUsed());
        assertEquals(0, result.retriesUsed());
        assertTrue(result.warnings().stream().anyMatch(msg -> msg.contains("directorFallback")));
        assertTrue(result.patch().stream().allMatch(op ->
                op instanceof PatchOp.AddStoryBeat || op instanceof PatchOp.SetColonyDirective
        ));

        DirectorPipelineTelemetry.Snapshot snapshot = telemetry.snapshot();
        assertEquals(1, snapshot.fallbackCount());
        assertEquals(1, snapshot.rejectedCommandCount());
        assertEquals(0, snapshot.validatedOutputsCount());
    }

    @Test
    void validateAndRepair_ReturnsValidatedResultForValidCandidate() {
        DirectorPipelineTelemetry telemetry = new DirectorPipelineTelemetry();
        DirectorRefineryPlanner planner = new DirectorRefineryPlanner(true, 2, telemetry);
        PatchRequest request = directorRequest(64L, 1, 0);

        List<PatchOp> validCandidate = List.of(
                new PatchOp.SetColonyDirective("op_dir_1", 0, "PrioritizeFood", 16),
                new PatchOp.AddStoryBeat("op_story_1", "BEAT_OK", "Season remains stable.", 20)
        );

        DirectorRefineryPlanner.DirectorValidationResult result = planner.validateAndRepair(request, validCandidate);

        assertTrue(result.validated());
        assertFalse(result.fallbackUsed());
        assertEquals(0, result.retriesUsed());
        assertEquals(2, result.patch().size());
        assertTrue(result.patch().get(0) instanceof PatchOp.AddStoryBeat);

        DirectorPipelineTelemetry.Snapshot snapshot = telemetry.snapshot();
        assertEquals(1, snapshot.validatedOutputsCount());
        assertEquals(0, snapshot.fallbackCount());
    }

    @Test
    void validateAndRepair_FallbackCanEmitCampaignWhenEnabled() {
        DirectorPipelineTelemetry telemetry = new DirectorPipelineTelemetry();
        DirectorRefineryPlanner planner = new DirectorRefineryPlanner(true, 0, true, telemetry);
        PatchRequest request = directorRequest(128L, 2, 0);

        List<PatchOp> invalidCandidate = List.of(
                new PatchOp.SetColonyDirective("op_bad", 99, "UnknownDirective", 5)
        );

        DirectorRefineryPlanner.DirectorValidationResult result = planner.validateAndRepair(request, invalidCandidate);

        assertTrue(result.fallbackUsed());
        assertTrue(result.patch().stream().anyMatch(op -> op instanceof PatchOp.DeclareWar || op instanceof PatchOp.ProposeTreaty));
    }

    @Test
    void validateAndRepair_CountsRejectedDuplicatesWhenCandidateContainsDuplicateOpId() {
        DirectorPipelineTelemetry telemetry = new DirectorPipelineTelemetry();
        DirectorRefineryPlanner planner = new DirectorRefineryPlanner(true, 1, telemetry);
        PatchRequest request = directorRequest(64L, 1, 0);

        List<PatchOp> duplicateOpIdCandidate = List.of(
                new PatchOp.SetColonyDirective("op_dup", 0, "PrioritizeFood", 15),
                new PatchOp.AddStoryBeat("op_dup", "BEAT_DUP", "major pressure wave", 22)
        );

        planner.validateAndRepair(request, duplicateOpIdCandidate);

        DirectorPipelineTelemetry.Snapshot snapshot = telemetry.snapshot();
        assertTrue(snapshot.rejectedCommandCount() >= 1);
    }

    @Test
    void validateAndRepair_CountsRejectedDuplicatesForCampaignOpIdsToo() {
        DirectorPipelineTelemetry telemetry = new DirectorPipelineTelemetry();
        DirectorRefineryPlanner planner = new DirectorRefineryPlanner(true, 1, true, telemetry);
        PatchRequest request = directorRequest(64L, 1, 0);

        List<PatchOp> duplicateOpIdCandidate = List.of(
                new PatchOp.DeclareWar("op_campaign_dup", 1, 2, "pressure"),
                new PatchOp.ProposeTreaty("op_campaign_dup", 2, 1, "ceasefire", "cooldown")
        );

        planner.validateAndRepair(request, duplicateOpIdCandidate);

        DirectorPipelineTelemetry.Snapshot snapshot = telemetry.snapshot();
        assertTrue(snapshot.rejectedCommandCount() >= 1);
    }

    @Test
    void validateAndRepair_GateOffCampaignCandidateFallsBackSafely() {
        DirectorPipelineTelemetry telemetry = new DirectorPipelineTelemetry();
        DirectorRefineryPlanner planner = new DirectorRefineryPlanner(true, 0, false, telemetry);
        PatchRequest request = directorRequest(64L, 1, 0);

        List<PatchOp> candidate = List.of(
                new PatchOp.DeclareWar("op_campaign_blocked", 1, 2, "pressure")
        );

        DirectorRefineryPlanner.DirectorValidationResult result = planner.validateAndRepair(request, candidate);

        assertTrue(result.fallbackUsed());
        assertTrue(result.patch().stream().noneMatch(op -> op instanceof PatchOp.DeclareWar || op instanceof PatchOp.ProposeTreaty));
    }

    private PatchRequest directorRequest(long tick, int colonyCount, long cooldownTicks) {
        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.putObject("world")
                .put("colonyCount", colonyCount)
                .put("storyBeatCooldownTicks", cooldownTicks);

        return new PatchRequest(
                "v1",
                "req-director",
                321L,
                tick,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );
    }
}
