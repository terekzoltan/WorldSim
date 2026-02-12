package hu.zoltanterek.worldsim.refinery.planner;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.List;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;

class MockPlannerTest {

    private final ObjectMapper objectMapper = new ObjectMapper();
    private final MockPlanner mockPlanner = new MockPlanner(objectMapper);

    @Test
    void worldEventPlanningIsDeterministicForSameInputs() {
        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.put("state", "test");

        PatchRequest request = new PatchRequest(
                "v1",
                "0da6ce2c-57aa-44ca-aafa-bf29caea89ef",
                99L,
                101L,
                Goal.WORLD_EVENT,
                snapshot,
                null
        );

        PatchResponse first = mockPlanner.plan(request);
        PatchResponse second = mockPlanner.plan(request);

        assertEquals(first.patch(), second.patch());
        assertEquals(List.of(
                "MockPlanner produced a deterministic response for pipeline testing.",
                "Given the same goal, seed, and tick, this service returns the same patch."
        ), first.explain());
    }
}
