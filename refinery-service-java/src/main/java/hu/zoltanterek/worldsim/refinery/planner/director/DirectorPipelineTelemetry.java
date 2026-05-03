package hu.zoltanterek.worldsim.refinery.planner.director;

import java.time.Instant;
import java.util.List;
import java.util.concurrent.atomic.AtomicLong;

import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.planner.refinery.DirectorSolverObservability;

@Component
public class DirectorPipelineTelemetry {
    private static final String PIPELINE_VERSION = "director-pipeline-v1";

    private final AtomicLong directorRequestsCount = new AtomicLong();
    private final AtomicLong validatedOutputsCount = new AtomicLong();
    private final AtomicLong fallbackCount = new AtomicLong();
    private final AtomicLong rejectedCommandCount = new AtomicLong();
    private final AtomicLong retryAttemptsTotal = new AtomicLong();
    private final AtomicLong validationRetryRoundsTotal = new AtomicLong();
    private final AtomicLong llmCompletionCountTotal = new AtomicLong();
    private final AtomicLong sanitizedProposalCount = new AtomicLong();
    private final AtomicLong causalChainOpCountTotal = new AtomicLong();
    private final AtomicLong solverSuccessCount = new AtomicLong();
    private final AtomicLong solverNonSuccessCount = new AtomicLong();
    private final AtomicLong solverLoadFailureCount = new AtomicLong();
    private final AtomicLong solverExtractionFailureCount = new AtomicLong();
    private final AtomicLong solverValidatedStoryCount = new AtomicLong();
    private final AtomicLong solverValidatedDirectiveCount = new AtomicLong();
    private final AtomicLong solverUnsupportedFeatureCount = new AtomicLong();

    private volatile Instant lastUpdatedUtc = Instant.EPOCH;
    private volatile SolverObservabilitySnapshot latestSolverObservability = SolverObservabilitySnapshot.empty();

    public void recordDirectorRequest() {
        directorRequestsCount.incrementAndGet();
        touch();
    }

    public void recordValidatedOutput(int retriesUsed) {
        validatedOutputsCount.incrementAndGet();
        recordValidationRetryRounds(retriesUsed);
    }

    public void recordFallback(int retriesUsed) {
        fallbackCount.incrementAndGet();
        recordValidationRetryRounds(retriesUsed);
    }

    public void recordRejectedCommands(long rejectedCount) {
        if (rejectedCount <= 0) {
            return;
        }
        rejectedCommandCount.addAndGet(rejectedCount);
        touch();
    }

    public void recordLlmProposalObservability(int completionCount, boolean sanitized) {
        if (completionCount > 0) {
            llmCompletionCountTotal.addAndGet(completionCount);
            retryAttemptsTotal.addAndGet(Math.max(0, completionCount - 1));
        }
        if (sanitized) {
            sanitizedProposalCount.incrementAndGet();
        }
        touch();
    }

    public void recordCausalChainOps(int causalChainOps) {
        if (causalChainOps > 0) {
            causalChainOpCountTotal.addAndGet(causalChainOps);
        }
        touch();
    }

    public void recordSolverObservability(DirectorSolverObservability.Report report) {
        if (report == null) {
            return;
        }
        if ("success".equals(report.status())) {
            solverSuccessCount.incrementAndGet();
        } else if ("non_success".equals(report.status())) {
            solverNonSuccessCount.incrementAndGet();
        } else if ("load_failure".equals(report.status())) {
            solverLoadFailureCount.incrementAndGet();
        }
        if ("failed".equals(report.extraction())) {
            solverExtractionFailureCount.incrementAndGet();
        }
        if (report.coverage().contains("story_core")) {
            solverValidatedStoryCount.incrementAndGet();
        }
        if (report.coverage().contains("directive_core")) {
            solverValidatedDirectiveCount.incrementAndGet();
        }
        solverUnsupportedFeatureCount.addAndGet(report.unsupported().stream().filter(item -> !"none".equals(item)).count());
        latestSolverObservability = SolverObservabilitySnapshot.from(report);
        touch();
    }

    public Snapshot snapshot() {
        long requests = directorRequestsCount.get();
        long llmRetryAttempts = retryAttemptsTotal.get();
        long validationRetryRounds = validationRetryRoundsTotal.get();
        long completionCount = llmCompletionCountTotal.get();
        double averageRetryCount = requests == 0 ? 0.0 : ((double) llmRetryAttempts) / requests;
        double averageValidationRetryRounds = requests == 0 ? 0.0 : ((double) validationRetryRounds) / requests;
        double averageCompletionCount = requests == 0 ? 0.0 : ((double) completionCount) / requests;
        return new Snapshot(
                directorRequestsCount.get(),
                validatedOutputsCount.get(),
                fallbackCount.get(),
                rejectedCommandCount.get(),
                llmRetryAttempts,
                averageRetryCount,
                validationRetryRounds,
                averageValidationRetryRounds,
                completionCount,
                averageCompletionCount,
                sanitizedProposalCount.get(),
                causalChainOpCountTotal.get(),
                solverSuccessCount.get(),
                solverNonSuccessCount.get(),
                solverLoadFailureCount.get(),
                solverExtractionFailureCount.get(),
                solverValidatedStoryCount.get(),
                solverValidatedDirectiveCount.get(),
                solverUnsupportedFeatureCount.get(),
                latestSolverObservability,
                lastUpdatedUtc,
                PIPELINE_VERSION
        );
    }

    private void recordValidationRetryRounds(int retriesUsed) {
        if (retriesUsed > 0) {
            validationRetryRoundsTotal.addAndGet(retriesUsed);
        }
        touch();
    }

    private void touch() {
        lastUpdatedUtc = Instant.now();
    }

    public record Snapshot(
            long directorRequestsCount,
            long validatedOutputsCount,
            long fallbackCount,
            long rejectedCommandCount,
            long retryAttemptsTotal,
            double averageRetryCount,
            long validationRetryRoundsTotal,
            double averageValidationRetryRounds,
            long llmCompletionCountTotal,
            double averageLlmCompletionCount,
            long sanitizedProposalCount,
            long causalChainOpCountTotal,
            long solverSuccessCount,
            long solverNonSuccessCount,
            long solverLoadFailureCount,
            long solverExtractionFailureCount,
            long solverValidatedStoryCount,
            long solverValidatedDirectiveCount,
            long solverUnsupportedFeatureCount,
            SolverObservabilitySnapshot latestSolverObservability,
            Instant lastUpdatedUtc,
            String pipelineVersion
    ) {
    }

    public record SolverObservabilitySnapshot(
            String path,
            String status,
            String generatorResult,
            String extraction,
            List<String> coverage,
            List<String> unsupported,
            List<String> diagnostics
    ) {
        public SolverObservabilitySnapshot {
            coverage = List.copyOf(coverage == null ? List.of() : coverage);
            unsupported = List.copyOf(unsupported == null ? List.of() : unsupported);
            diagnostics = List.copyOf(diagnostics == null ? List.of() : diagnostics);
        }

        static SolverObservabilitySnapshot empty() {
            return new SolverObservabilitySnapshot(
                    "unwired",
                    "not_run",
                    "none",
                    "not_run",
                    List.of("none"),
                    List.of("none"),
                    List.of()
            );
        }

        static SolverObservabilitySnapshot from(DirectorSolverObservability.Report report) {
            return new SolverObservabilitySnapshot(
                    report.path(),
                    report.status(),
                    report.generatorResult(),
                    report.extraction(),
                    report.coverage(),
                    report.unsupported(),
                    report.diagnostics()
            );
        }
    }
}
