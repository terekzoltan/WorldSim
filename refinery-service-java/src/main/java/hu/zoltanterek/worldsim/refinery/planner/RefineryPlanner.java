package hu.zoltanterek.worldsim.refinery.planner;

import java.util.ArrayList;
import java.util.List;
import java.util.Set;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

@Component
public class RefineryPlanner {
    private static final Set<String> KNOWN_TECH_IDS = Set.of(
            "woodcutting",
            "construction",
            "mining",
            "agriculture",
            "medicine",
            "tools",
            "housing",
            "logistics",
            "education",
            "fitness",
            "exploration",
            "demographics",
            "masonry"
    );

    private final boolean refineryEnabled;
    private final ObjectMapper objectMapper;

    public RefineryPlanner(
            @Value("${planner.refinery.enabled:false}") boolean refineryEnabled,
            ObjectMapper objectMapper
    ) {
        this.refineryEnabled = refineryEnabled;
        this.objectMapper = objectMapper;
    }

    public boolean isRefineryEnabled() {
        return refineryEnabled;
    }

    public List<PatchOp> validateAndRepair(PatchRequest request, List<PatchOp> candidatePatch) {
        if (!refineryEnabled) {
            return candidatePatch;
        }

        if (request.goal() != Goal.TECH_TREE_PATCH) {
            return candidatePatch;
        }

        List<PatchOp> repaired = new ArrayList<>(candidatePatch.size());
        for (PatchOp op : candidatePatch) {
            if (op instanceof PatchOp.AddTech addTech) {
                repaired.add(repairAddTech(addTech));
                continue;
            }
            throw new IllegalArgumentException(
                    "Refinery tech-tree slice currently supports only addTech ops for TECH_TREE_PATCH."
            );
        }

        return repaired;
    }

    private PatchOp.AddTech repairAddTech(PatchOp.AddTech addTech) {
        if (addTech.techId() == null || addTech.techId().isBlank()) {
            throw new IllegalArgumentException("addTech.techId must be non-empty.");
        }
        if (!KNOWN_TECH_IDS.contains(addTech.techId())) {
            throw new IllegalArgumentException("addTech.techId must be known: " + addTech.techId());
        }

        List<String> prereqs = addTech.prereqTechIds() == null
                ? List.of()
                : addTech.prereqTechIds().stream()
                .filter(id -> id != null && !id.isBlank())
                .filter(id -> !id.equals(addTech.techId()))
                .distinct()
                .toList();

        for (String prereq : prereqs) {
            if (!KNOWN_TECH_IDS.contains(prereq)) {
                throw new IllegalArgumentException("addTech.prereqTechIds contains unknown id: " + prereq);
            }
        }

        ObjectNode repairedCost = ensureObject(addTech.cost());
        double research = repairedCost.path("research").asDouble(-1d);
        if (research <= 0) {
            repairedCost.put("research", 80);
        }

        ObjectNode repairedEffects = ensureObject(addTech.effects());

        return new PatchOp.AddTech(
                addTech.opId(),
                addTech.techId(),
                prereqs,
                repairedCost,
                repairedEffects
        );
    }

    private ObjectNode ensureObject(JsonNode node) {
        if (node instanceof ObjectNode objectNode) {
            return objectNode.deepCopy();
        }
        return objectMapper.createObjectNode();
    }
}
