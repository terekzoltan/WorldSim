package hu.zoltanterek.worldsim.refinery.planner;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;

import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorPipelineTelemetry;

class ComposedPatchPlannerSolverObservabilityTest {
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void planDirectorPipeline_SolverObservabilityGateIsAdditiveAndDoesNotChangePatch() throws Exception {
        PatchRequest request = request();
        PatchResponse gateOff = planner(false, new DirectorPipelineTelemetry()).plan(request);
        DirectorPipelineTelemetry telemetry = new DirectorPipelineTelemetry();
        PatchResponse gateOn = planner(true, telemetry).plan(request);

        assertEquals(gateOff.patch(), gateOn.patch());
        assertTrue(gateOn.explain().contains("directorStage:refinery-validated"));
        assertFalse(gateOff.explain().stream().anyMatch(item -> item.startsWith("directorSolver")));
        assertTrue(gateOn.explain().contains("directorSolverPath:validated_core"));
        assertTrue(gateOn.explain().contains("directorSolverStatus:success"));
        assertTrue(gateOn.explain().contains("directorSolverExtraction:success"));
        assertTrue(gateOn.explain().contains("directorSolverValidatedCoverage:story_core"));
        assertTrue(gateOn.explain().contains("directorSolverValidatedCoverage:directive_core"));
        assertTrue(gateOn.explain().contains("directorSolverUnsupported:none"));
        assertEquals(1, telemetry.snapshot().solverSuccessCount());
    }

    private ComposedPatchPlanner planner(boolean solverObservabilityEnabled, DirectorPipelineTelemetry telemetry) {
        return new ComposedPatchPlanner(
                new MockPlanner(objectMapper, "both", false),
                new LlmPlanner(false, null),
                null,
                new DirectorRefineryPlanner(true, 0, telemetry),
                telemetry,
                "pipeline",
                "both",
                5.0,
                solverObservabilityEnabled
        );
    }

    private PatchRequest request() throws Exception {
        String requestBody = Files.readString(
                Path.of("examples/requests/patch-season-director-v1.json"),
                StandardCharsets.UTF_8
        );
        return objectMapper.readValue(requestBody, PatchRequest.class);
    }
}
