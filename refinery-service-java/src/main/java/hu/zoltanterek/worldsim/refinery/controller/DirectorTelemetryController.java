package hu.zoltanterek.worldsim.refinery.controller;

import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorPipelineTelemetry;

@RestController
@RequestMapping("/v1/director")
public class DirectorTelemetryController {
    private final DirectorPipelineTelemetry telemetry;

    public DirectorTelemetryController(DirectorPipelineTelemetry telemetry) {
        this.telemetry = telemetry;
    }

    @GetMapping("/telemetry")
    public DirectorPipelineTelemetry.Snapshot telemetry() {
        return telemetry.snapshot();
    }
}
