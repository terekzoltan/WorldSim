package hu.zoltanterek.worldsim.refinery.planner.director;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ArrayNode;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

class DirectorSnapshotMapperTest {
    private final ObjectMapper objectMapper = new ObjectMapper();
    private final DirectorSnapshotMapper mapper = new DirectorSnapshotMapper();

    @Test
    void map_PopulatesActiveBeatsAndDirectives() {
        ObjectNode world = objectMapper.createObjectNode();
        world.put("colonyCount", 3);
        world.put("storyBeatCooldownTicks", 5);

        ArrayNode activeBeats = world.putArray("activeBeats");
        activeBeats.addObject()
                .put("beatId", "BEAT_MAJOR_1")
                .put("severity", "major")
                .put("remainingTicks", 12);

        ArrayNode activeDirectives = world.putArray("activeDirectives");
        activeDirectives.addObject()
                .put("colonyId", 1)
                .put("directive", "PrioritizeFood");

        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.set("world", world);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-map",
                11L,
                128L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );

        DirectorRuntimeFacts facts = mapper.map(request);

        assertEquals(1, facts.activeBeats().size());
        assertEquals("major", facts.activeBeats().get(0).severity());
        assertEquals(1, facts.activeDirectives().size());
        assertEquals("PrioritizeFood", facts.activeDirectives().get(0).directive());
    }

    @Test
    void map_MissingArraysProduceEmptyFacts() {
        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.set("world", objectMapper.createObjectNode().put("colonyCount", 2));

        PatchRequest request = new PatchRequest(
                "v1",
                "req-map-empty",
                11L,
                128L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );

        DirectorRuntimeFacts facts = mapper.map(request);

        assertTrue(facts.activeBeats().isEmpty());
        assertTrue(facts.activeDirectives().isEmpty());
    }
}
