package hu.zoltanterek.worldsim.refinery.planner;

import java.util.List;
import java.util.Optional;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

@Component
public class LlmPlanner {
    private static final Logger logger = LoggerFactory.getLogger(LlmPlanner.class);

    private final boolean enabled;

    public LlmPlanner(@Value("${planner.llm.enabled:false}") boolean enabled) {
        this.enabled = enabled;
    }

    public Optional<List<PatchOp>> propose(PatchRequest request) {
        if (!enabled) {
            return Optional.empty();
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
