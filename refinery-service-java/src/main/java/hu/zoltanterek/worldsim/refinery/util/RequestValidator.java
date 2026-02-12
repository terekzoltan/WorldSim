package hu.zoltanterek.worldsim.refinery.util;

import org.springframework.stereotype.Component;

import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

@Component
public class RequestValidator {
    public void validateSchema(PatchRequest request) {
        if (!"v1".equals(request.schemaVersion())) {
            throw new IllegalArgumentException("Unsupported schemaVersion, expected 'v1'.");
        }
    }
}
