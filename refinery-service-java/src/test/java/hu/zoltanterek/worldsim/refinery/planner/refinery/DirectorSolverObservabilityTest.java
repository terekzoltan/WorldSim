package hu.zoltanterek.worldsim.refinery.planner.refinery;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

class DirectorSolverObservabilityTest {
    @Test
    void fromSolveResult_SuccessUsesNormalizedMultiMarkers() {
        DirectorRefinerySolveResult result = new DirectorRefinerySolveResult(
                DirectorRefinerySolveStatus.SUCCESS,
                "SUCCESS",
                new DirectorValidatedCoreOutput(
                        new DirectorValidatedCoreOutput.StoryBeatCore("BEAT_A", "Story", 24, "minor"),
                        new DirectorValidatedCoreOutput.DirectiveCore(0, "PrioritizeFood", 18)
                ),
                List.of("solverResult:success", "validatedCoverage:story_core", "validatedCoverage:directive_core"),
                List.of("campaign", "causalChain")
        );

        DirectorSolverObservability.Report report = DirectorSolverObservability.fromSolveResult(result);

        assertTrue(report.markers().contains("directorSolverPath:validated_core"));
        assertTrue(report.markers().contains("directorSolverStatus:success"));
        assertTrue(report.markers().contains("directorSolverGeneratorResult:success"));
        assertTrue(report.markers().contains("directorSolverExtraction:success"));
        assertTrue(report.markers().contains("directorSolverValidatedCoverage:story_core"));
        assertTrue(report.markers().contains("directorSolverValidatedCoverage:directive_core"));
        assertTrue(report.markers().contains("directorSolverUnsupported:campaign"));
        assertTrue(report.markers().contains("directorSolverUnsupported:causalChain"));
    }

    @Test
    void fromSolveResult_ExtractionFailureUsesStableDiagnosticCode() {
        DirectorRefinerySolveResult result = new DirectorRefinerySolveResult(
                DirectorRefinerySolveStatus.NON_SUCCESS,
                "SUCCESS",
                null,
                List.of("solverResult:success", "extractFailure:multiple_true_story_slots"),
                List.of()
        );

        DirectorSolverObservability.Report report = DirectorSolverObservability.fromSolveResult(result);

        assertTrue(report.markers().contains("directorSolverPath:sidecar"));
        assertTrue(report.markers().contains("directorSolverStatus:non_success"));
        assertTrue(report.markers().contains("directorSolverExtraction:failed"));
        assertTrue(report.markers().contains("directorSolverValidatedCoverage:none"));
        assertTrue(report.markers().contains("directorSolverUnsupported:none"));
        assertTrue(report.markers().contains("directorSolverDiagnostic:multiple_true_story_slots"));
        assertTrue(report.markers().contains("directorSolverDiagnostic:non_success"));
    }

    @Test
    void unavailable_ReportsNotRunWithoutPretendingValidation() {
        DirectorSolverObservability.Report report = DirectorSolverObservability.unavailable("story_core_unavailable");

        assertTrue(report.markers().contains("directorSolverPath:unavailable"));
        assertTrue(report.markers().contains("directorSolverStatus:not_run"));
        assertTrue(report.markers().contains("directorSolverGeneratorResult:none"));
        assertTrue(report.markers().contains("directorSolverExtraction:not_run"));
        assertTrue(report.markers().contains("directorSolverValidatedCoverage:none"));
        assertTrue(report.markers().contains("directorSolverUnsupported:none"));
        assertTrue(report.markers().contains("directorSolverDiagnostic:story_core_unavailable"));
    }
}
