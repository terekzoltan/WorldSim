package hu.zoltanterek.worldsim.refinery.controller;

import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;
import hu.zoltanterek.worldsim.refinery.service.PatchService;
import jakarta.validation.Valid;

@Validated
@RestController
@RequestMapping("/v1")
public class PatchController {
    private final PatchService patchService;

    public PatchController(PatchService patchService) {
        this.patchService = patchService;
    }

    @PostMapping("/patch")
    public PatchResponse patch(@Valid @RequestBody PatchRequest request) {
        return patchService.createPatch(request);
    }
}
