package hu.zoltanterek.worldsim.refinery.planner;

import java.util.List;
import java.util.Optional;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.Goal;

@Component
public class LlmPlanner {
    private static final Logger logger = LoggerFactory.getLogger(LlmPlanner.class);

    private final boolean enabled;
    private final LlmDirectorPlanner llmDirectorPlanner;

    public LlmPlanner(
            @Value("${planner.llm.enabled:false}") boolean enabled,
            LlmDirectorPlanner llmDirectorPlanner
    ) {
        this.enabled = enabled;
        this.llmDirectorPlanner = llmDirectorPlanner;
    }

    public Optional<List<PatchOp>> propose(PatchRequest request) {
        return proposeWithFeedback(request, List.of());
    }

    public Optional<List<PatchOp>> proposeWithFeedback(PatchRequest request, List<String> feedbackHints) {
        if (!enabled) {
            return Optional.empty();
        }

        if (request.goal() == Goal.SEASON_DIRECTOR_CHECKPOINT) {
            return llmDirectorPlanner.propose(request, feedbackHints);
        }

        logger.info(
                "llm planner enabled but not implemented yet requestId={} goal={} seed={} tick={}",
                request.requestId(),
                request.goal(),
                request.seed(),
                request.tick()
        );

        // TODO: Integrate LLM provider and return candidate patch ops.
        return Optional.empty();
    }
}
