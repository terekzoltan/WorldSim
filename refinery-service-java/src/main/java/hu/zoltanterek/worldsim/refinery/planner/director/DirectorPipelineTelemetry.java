package hu.zoltanterek.worldsim.refinery.planner.director;

import java.time.Instant;
import java.util.concurrent.atomic.AtomicLong;

import org.springframework.stereotype.Component;

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

    private volatile Instant lastUpdatedUtc = Instant.EPOCH;

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
            Instant lastUpdatedUtc,
            String pipelineVersion
    ) {
    }
}
