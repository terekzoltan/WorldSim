package hu.zoltanterek.worldsim.refinery.planner.director;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

class DirectorDeterministicFallbackPlannerTest {
    private final ObjectMapper objectMapper = new ObjectMapper();
    private final DirectorSnapshotMapper snapshotMapper = new DirectorSnapshotMapper();
    private final DirectorDeterministicFallbackPlanner planner = new DirectorDeterministicFallbackPlanner();

    @Test
    void build_IsBoundedAndUsesDeterministicOrderWithoutRichPayloads() {
        PatchRequest request = directorRequest(128L, 2, 0);
        DirectorRuntimeFacts facts = snapshotMapper.map(request, DirectorDesign.DEFAULT_INFLUENCE_BUDGET);

        List<PatchOp> fallback = planner.build(request, facts, false);

        assertEquals(2, fallback.size());
        PatchOp.AddStoryBeat story = assertInstanceOf(PatchOp.AddStoryBeat.class, fallback.get(0));
        PatchOp.SetColonyDirective directive = assertInstanceOf(PatchOp.SetColonyDirective.class, fallback.get(1));
        assertTrue(story.opId().startsWith("op_"));
        assertTrue(story.beatId().startsWith("BEAT_FALLBACK_"));
        assertEquals(List.of(), story.effects());
        assertNull(story.causalChain());
        assertEquals(List.of(), directive.biases());
    }

    @Test
    void build_SuppressesStoryDuringCooldown() {
        PatchRequest request = directorRequest(128L, 2, 12);
        DirectorRuntimeFacts facts = snapshotMapper.map(request, DirectorDesign.DEFAULT_INFLUENCE_BUDGET);

        List<PatchOp> fallback = planner.build(request, facts, false);

        assertEquals(1, fallback.size());
        assertInstanceOf(PatchOp.SetColonyDirective.class, fallback.get(0));
    }

    @Test
    void build_SuppressesDirectiveWhenNoColonyExists() {
        PatchRequest request = directorRequest(128L, 0, 0);
        DirectorRuntimeFacts facts = new DirectorRuntimeFacts(
                request.tick(),
                0,
                0,
                DirectorDesign.DEFAULT_INFLUENCE_BUDGET,
                List.of(),
                List.of()
        );

        List<PatchOp> fallback = planner.build(request, facts, false);

        assertEquals(1, fallback.size());
        assertInstanceOf(PatchOp.AddStoryBeat.class, fallback.get(0));
    }

    @Test
    void build_CampaignFallbackRespectsExistingCampaignEnabledGate() {
        PatchRequest request = directorRequest(128L, 2, 0);
        DirectorRuntimeFacts facts = snapshotMapper.map(request, DirectorDesign.DEFAULT_INFLUENCE_BUDGET);

        List<PatchOp> gateOff = planner.build(request, facts, false);
        List<PatchOp> gateOn = planner.build(request, facts, true);

        assertTrue(gateOff.stream().noneMatch(DirectorDeterministicFallbackPlannerTest::isCampaignOp));
        assertEquals(2, gateOff.size());
        assertEquals(3, gateOn.size());
        assertInstanceOf(PatchOp.AddStoryBeat.class, gateOn.get(0));
        assertInstanceOf(PatchOp.SetColonyDirective.class, gateOn.get(1));
        assertTrue(gateOn.stream().anyMatch(DirectorDeterministicFallbackPlannerTest::isCampaignOp));
        assertTrue(isCampaignOp(gateOn.get(2)));
    }

    private static boolean isCampaignOp(PatchOp op) {
        return op instanceof PatchOp.DeclareWar || op instanceof PatchOp.ProposeTreaty;
    }

    private PatchRequest directorRequest(long tick, int colonyCount, long cooldownTicks) {
        ObjectNode snapshot = objectMapper.createObjectNode();
        snapshot.putObject("world")
                .put("colonyCount", colonyCount)
                .put("storyBeatCooldownTicks", cooldownTicks);

        return new PatchRequest(
                "v1",
                "req-director",
                321L,
                tick,
                Goal.SEASON_DIRECTOR_CHECKPOINT,
                snapshot,
                null
        );
    }
}
