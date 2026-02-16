package hu.zoltanterek.worldsim.refinery.planner;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

import java.util.List;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

class RefineryPlannerTest {
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void techTreeAddTechIsRepairedWhenResearchMissing() {
        RefineryPlanner planner = new RefineryPlanner(true, objectMapper);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-1",
                123,
                42,
                Goal.TECH_TREE_PATCH,
                objectMapper.createObjectNode(),
                null
        );

        ObjectNode emptyCost = objectMapper.createObjectNode();
        PatchOp.AddTech op = new PatchOp.AddTech(
                "op_1",
                "agriculture",
                List.of("agriculture", "woodcutting"),
                emptyCost,
                objectMapper.createObjectNode()
        );

        List<PatchOp> repaired = planner.validateAndRepair(request, List.of(op));
        PatchOp.AddTech repairedOp = (PatchOp.AddTech) repaired.get(0);

        assertEquals(80, repairedOp.cost().path("research").asInt());
        assertEquals(List.of("woodcutting"), repairedOp.prereqTechIds());
    }

    @Test
    void techTreeSliceRejectsNonAddTechOps() {
        RefineryPlanner planner = new RefineryPlanner(true, objectMapper);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-2",
                123,
                42,
                Goal.TECH_TREE_PATCH,
                objectMapper.createObjectNode(),
                null
        );

        PatchOp badOp = new PatchOp.AddWorldEvent(
                "op_bad",
                "WEATHER_1",
                "RAIN_BONUS",
                objectMapper.createObjectNode(),
                10
        );

        assertThrows(IllegalArgumentException.class, () -> planner.validateAndRepair(request, List.of(badOp)));
    }

    @Test
    void techTreeSliceRejectsUnknownPrereqIds() {
        RefineryPlanner planner = new RefineryPlanner(true, objectMapper);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-3",
                123,
                42,
                Goal.TECH_TREE_PATCH,
                objectMapper.createObjectNode(),
                null
        );

        PatchOp badOp = new PatchOp.AddTech(
                "op_bad2",
                "agriculture",
                List.of("unknown_prereq"),
                objectMapper.createObjectNode(),
                objectMapper.createObjectNode()
        );

        assertThrows(IllegalArgumentException.class, () -> planner.validateAndRepair(request, List.of(badOp)));
    }
}
