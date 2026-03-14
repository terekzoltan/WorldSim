package hu.zoltanterek.worldsim.refinery.service;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.stereotype.Service;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;
import hu.zoltanterek.worldsim.refinery.planner.PatchPlanner;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorPipelineTelemetry;
import hu.zoltanterek.worldsim.refinery.util.RequestValidator;

@Service
public class PatchService {
    private static final Logger logger = LoggerFactory.getLogger(PatchService.class);

    private final PatchPlanner patchPlanner;
    private final RequestValidator requestValidator;
    private final DirectorPipelineTelemetry directorPipelineTelemetry;

    public PatchService(
            PatchPlanner patchPlanner,
            RequestValidator requestValidator,
            DirectorPipelineTelemetry directorPipelineTelemetry
    ) {
        this.patchPlanner = patchPlanner;
        this.requestValidator = requestValidator;
        this.directorPipelineTelemetry = directorPipelineTelemetry;
    }

    public PatchResponse createPatch(PatchRequest request) {
        try (var c1 = MDC.putCloseable("requestId", request.requestId());
             var c2 = MDC.putCloseable("goal", request.goal().name());
             var c3 = MDC.putCloseable("seed", Long.toString(request.seed()));
             var c4 = MDC.putCloseable("tick", Long.toString(request.tick()))) {
            logger.info("patch request received goal={}", request.goal());

            requestValidator.validateSchema(request);
            if (request.goal() == Goal.SEASON_DIRECTOR_CHECKPOINT) {
                directorPipelineTelemetry.recordDirectorRequest();
                logger.info("director telemetry request counter incremented");
            }

            PatchResponse response = patchPlanner.plan(request);

            logger.info(
                    "patch request completed patchOps={} warnings={} explain={}",
                    response.patch().size(),
                    response.warnings().size(),
                    response.explain().size()
            );
            return response;
        }
    }
}
