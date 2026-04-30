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
        ObjectNode director = objectMapper.createObjectNode();
        director.put("colonyPopulation", 47);
        director.put("beatCooldownRemainingTicks", 5);
        director.put("remainingInfluenceBudget", 4.25);

        ObjectNode world = objectMapper.createObjectNode();
        world.put("colonyCount", 3);

        ArrayNode activeBeats = director.putArray("activeBeats");
        activeBeats.addObject()
                .put("beatId", "BEAT_MAJOR_1")
                .put("severity", "major")
                .put("remainingTicks", 12);

        ArrayNode activeDirectives = director.putArray("activeDirectives");
        activeDirectives.addObject()
                .put("colonyId", 1)
                .put("directive", "PrioritizeFood");

        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.set("world", world);
        snapshot.set("director", director);

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
        assertEquals(3, facts.colonyCount());
        assertEquals("major", facts.activeBeats().get(0).severity());
        assertEquals(1, facts.activeDirectives().size());
        assertEquals("PrioritizeFood", facts.activeDirectives().get(0).directive());
        assertEquals(4.25, facts.remainingInfluenceBudget());
    }

    @Test
    void map_UsesWorldColonyCountInsteadOfDirectorPopulation() {
        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.putObject("world").put("colonyCount", 2);
        snapshot.putObject("director")
                .put("colonyPopulation", 47)
                .put("beatCooldownRemainingTicks", 0)
                .put("remainingInfluenceBudget", 4.0);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-map-colony-count",
                21L,
                100L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );

        DirectorRuntimeFacts facts = mapper.map(request, 5.0);
        assertEquals(2, facts.colonyCount());
    }

    @Test
    void map_FallsBackToLegacyWorldFields() {
        ObjectNode world = objectMapper.createObjectNode();
        world.put("colonyCount", 2);
        world.put("storyBeatCooldownTicks", 7);
        ArrayNode activeBeats = world.putArray("activeBeats");
        activeBeats.addObject()
                .put("beatId", "BEAT_MINOR_1")
                .put("severity", "minor")
                .put("remainingTicks", 4);

        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.set("world", world);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-map-legacy",
                42L,
                256L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );

        DirectorRuntimeFacts facts = mapper.map(request);

        assertEquals(2, facts.colonyCount());
        assertEquals(7, facts.beatCooldownTicks());
        assertEquals(1, facts.activeBeats().size());
        assertEquals(5.0, facts.remainingInfluenceBudget());
    }

    @Test
    void map_DirectorCooldownWinsOverLegacyWorldCooldown() {
        ObjectNode world = objectMapper.createObjectNode();
        world.put("colonyCount", 2);
        world.put("storyBeatCooldownTicks", 99);

        ObjectNode director = objectMapper.createObjectNode();
        director.put("beatCooldownRemainingTicks", 8);
        director.put("remainingInfluenceBudget", 4.0);

        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.set("world", world);
        snapshot.set("director", director);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-map-cooldown-precedence",
                42L,
                256L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );

        DirectorRuntimeFacts facts = mapper.map(request);

        assertEquals(8, facts.beatCooldownTicks());
    }

    @Test
    void map_ConstraintsMaxBudgetOverridesSnapshotBudget() {
        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.putObject("world").put("colonyCount", 2);
        snapshot.putObject("director")
                .put("colonyPopulation", 47)
                .put("beatCooldownRemainingTicks", 0)
                .put("remainingInfluenceBudget", 4.0);

        ObjectNode constraints = objectMapper.createObjectNode();
        constraints.put("maxBudget", 2.5);
        constraints.putObject("director").put("maxBudget", 1.5);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-map-budget",
                21L,
                100L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                constraints
        );

        DirectorRuntimeFacts facts = mapper.map(request, 5.0);
        assertEquals(2.5, facts.remainingInfluenceBudget());
    }

    @Test
    void map_NestedConstraintsDirectorMaxBudgetFallsBackWhenRootMissing() {
        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.putObject("world").put("colonyCount", 2);
        snapshot.putObject("director")
                .put("colonyPopulation", 47)
                .put("beatCooldownRemainingTicks", 0)
                .put("remainingInfluenceBudget", 4.0);

        ObjectNode constraints = objectMapper.createObjectNode();
        constraints.putObject("director").put("maxBudget", 1.75);

        PatchRequest request = new PatchRequest(
                "v1",
                "req-map-nested-budget",
                21L,
                100L,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                constraints
        );

        DirectorRuntimeFacts facts = mapper.map(request, 5.0);
        assertEquals(1.75, facts.remainingInfluenceBudget());
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
