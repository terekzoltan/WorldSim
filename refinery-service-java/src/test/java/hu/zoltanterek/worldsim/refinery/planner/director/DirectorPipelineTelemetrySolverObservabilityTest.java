package hu.zoltanterek.worldsim.refinery.planner.director;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.List;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.planner.refinery.DirectorSolverObservability;

class DirectorPipelineTelemetrySolverObservabilityTest {
    @Test
    void recordSolverObservability_AddsCountersAndKeepsExistingSnapshotFields() {
        DirectorPipelineTelemetry telemetry = new DirectorPipelineTelemetry();
        telemetry.recordDirectorRequest();
        telemetry.recordSolverObservability(new DirectorSolverObservability.Report(
                "validated_core",
                "success",
                "success",
                "success",
                List.of("story_core", "directive_core"),
                List.of("campaign"),
                List.of(),
                List.of()
        ));

        DirectorPipelineTelemetry.Snapshot snapshot = telemetry.snapshot();

        assertEquals(1, snapshot.directorRequestsCount());
        assertEquals(0, snapshot.validatedOutputsCount());
        assertEquals(1, snapshot.solverSuccessCount());
        assertEquals(0, snapshot.solverNonSuccessCount());
        assertEquals(0, snapshot.solverLoadFailureCount());
        assertEquals(1, snapshot.solverValidatedStoryCount());
        assertEquals(1, snapshot.solverValidatedDirectiveCount());
        assertEquals(1, snapshot.solverUnsupportedFeatureCount());
        assertEquals("validated_core", snapshot.latestSolverObservability().path());
        assertEquals("success", snapshot.latestSolverObservability().status());
    }
}
