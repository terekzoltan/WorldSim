package hu.zoltanterek.worldsim.refinery.service;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;
import hu.zoltanterek.worldsim.refinery.planner.PatchPlanner;
import hu.zoltanterek.worldsim.refinery.util.RequestValidator;

@Service
public class PatchService {
    private static final Logger logger = LoggerFactory.getLogger(PatchService.class);

    private final PatchPlanner patchPlanner;
    private final RequestValidator requestValidator;

    public PatchService(PatchPlanner patchPlanner, RequestValidator requestValidator) {
        this.patchPlanner = patchPlanner;
        this.requestValidator = requestValidator;
    }

    public PatchResponse createPatch(PatchRequest request) {
        logger.info(
                "patch request received requestId={} goal={} seed={} tick={}",
                request.requestId(),
                request.goal(),
                request.seed(),
                request.tick()
        );

        requestValidator.validateSchema(request);

        PatchResponse response = patchPlanner.plan(request);

        logger.info(
                "patch request completed requestId={} goal={} seed={} tick={} patchOps={}",
                request.requestId(),
                request.goal(),
                request.seed(),
                request.tick(),
                response.patch().size()
        );

        return response;
    }
}
