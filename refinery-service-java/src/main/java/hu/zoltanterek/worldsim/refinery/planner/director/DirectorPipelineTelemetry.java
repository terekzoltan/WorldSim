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

    private volatile Instant lastUpdatedUtc = Instant.EPOCH;

    public void recordDirectorRequest() {
        directorRequestsCount.incrementAndGet();
        touch();
    }

    public void recordValidatedOutput(int retriesUsed) {
        validatedOutputsCount.incrementAndGet();
        recordRetries(retriesUsed);
    }

    public void recordFallback(int retriesUsed) {
        fallbackCount.incrementAndGet();
        recordRetries(retriesUsed);
    }

    public void recordRejectedCommands(long rejectedCount) {
        if (rejectedCount <= 0) {
            return;
        }
        rejectedCommandCount.addAndGet(rejectedCount);
        touch();
    }

    public Snapshot snapshot() {
        long requests = directorRequestsCount.get();
        long retries = retryAttemptsTotal.get();
        double averageRetryCount = requests == 0 ? 0.0 : ((double) retries) / requests;
        return new Snapshot(
                directorRequestsCount.get(),
                validatedOutputsCount.get(),
                fallbackCount.get(),
                rejectedCommandCount.get(),
                retries,
                averageRetryCount,
                lastUpdatedUtc,
                PIPELINE_VERSION
        );
    }

    private void recordRetries(int retriesUsed) {
        if (retriesUsed > 0) {
            retryAttemptsTotal.addAndGet(retriesUsed);
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
            Instant lastUpdatedUtc,
            String pipelineVersion
    ) {
    }
}
