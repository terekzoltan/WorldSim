package hu.zoltanterek.worldsim.refinery.planner;

import java.util.List;
import java.util.Random;

import org.springframework.stereotype.Component;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;
import hu.zoltanterek.worldsim.refinery.util.DeterministicIds;

@Component
public class MockPlanner implements PatchPlanner {
    private static final String SCHEMA_VERSION = "v1";
    private final ObjectMapper objectMapper;

    public MockPlanner(ObjectMapper objectMapper) {
        this.objectMapper = objectMapper;
    }

    @Override
    public PatchResponse plan(PatchRequest request) {
        Random random = new Random(DeterministicIds.combineSeed(request.seed(), request.tick(), request.goal().name()));

        PatchOp op = switch (request.goal()) {
            case Goal.TECH_TREE_PATCH -> planTechTreePatch(request);
            case Goal.WORLD_EVENT -> planWorldEvent(request, random);
            case Goal.NPC_POLICY -> planNpcPolicy(request, random);
        };

        List<String> explain = List.of(
                "MockPlanner produced a deterministic response for pipeline testing.",
                "Given the same goal, seed, and tick, this service returns the same patch."
        );

        // TODO: Replace with LLM -> Refinery validate/repair planner chain.
        return new PatchResponse(
                SCHEMA_VERSION,
                request.requestId(),
                request.seed(),
                List.of(op),
                explain,
                List.of()
        );
    }

    private PatchOp planTechTreePatch(PatchRequest request) {
        ObjectNode cost = objectMapper.createObjectNode();
        cost.put("research", 80);

        ObjectNode effects = objectMapper.createObjectNode();
        effects.put("foodYieldDelta", 1);
        effects.put("waterEfficiencyDelta", 1);

        String opId = DeterministicIds.opId(
                request.seed(),
                request.tick(),
                request.goal().name(),
                "addTech",
                "agriculture"
        );

        return new PatchOp.AddTech(
                opId,
                "agriculture",
                List.of("woodcutting"),
                cost,
                effects
        );
    }

    private PatchOp planWorldEvent(PatchRequest request, Random random) {
        String eventId = "WEATHER_" + DeterministicIds.shortStableId(
                request.seed(),
                request.tick(),
                request.goal().name(),
                request.goal().name()
        );
        String type = random.nextBoolean() ? "DROUGHT" : "RAIN_BONUS";

        ObjectNode params = objectMapper.createObjectNode();
        params.put("severity", 1 + random.nextInt(3));
        params.put("region", "GLOBAL");

        long duration = 10L + random.nextInt(11);
        String opId = DeterministicIds.opId(request.seed(), request.tick(), request.goal().name(), "addWorldEvent", eventId);
        return new PatchOp.AddWorldEvent(opId, eventId, type, params, duration);
    }

    private PatchOp planNpcPolicy(PatchRequest request, Random random) {
        String[] fields = {"economy.taxRate", "military.readiness", "science.focus"};
        String field = fields[random.nextInt(fields.length)];
        double delta = random.nextBoolean() ? 0.05 : -0.05;
        String opId = DeterministicIds.opId(request.seed(), request.tick(), request.goal().name(), "tweakTech", field + ":" + delta);
        return new PatchOp.TweakTech(opId, "POLICY_GLOBAL", field, delta);
    }
}
