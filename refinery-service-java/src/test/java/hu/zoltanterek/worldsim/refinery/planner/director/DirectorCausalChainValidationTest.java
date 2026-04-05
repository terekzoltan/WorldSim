package hu.zoltanterek.worldsim.refinery.planner.director;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;

class DirectorCausalChainValidationTest {
    private final DirectorModelValidator validator = new DirectorModelValidator();

    @Test
    void validateAndRepair_AcceptsValidCausalChain() {
        DirectorRuntimeFacts facts = facts(5.0);
        PatchOp.AddStoryBeat storyBeat = buildStoryWithChain(
                "food_reserves_pct",
                "lt",
                35,
                20,
                1,
                "BEAT_PARENT",
                "BEAT_FOLLOW"
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(List.of(storyBeat), facts);
        assertEquals(1, outcome.patch().size());
        PatchOp.AddStoryBeat repaired = (PatchOp.AddStoryBeat) outcome.patch().get(0);
        assertTrue(repaired.causalChain() != null);
        assertEquals(1, repaired.causalChain().maxTriggers());
        assertEquals("food_reserves_pct", repaired.causalChain().condition().metric());
    }

    @Test
    void validateAndRepair_RejectsCausalChainLoop_INV16() {
        DirectorRuntimeFacts facts = facts(5.0);
        PatchOp.AddStoryBeat storyBeat = buildStoryWithChain(
                "food_reserves_pct",
                "lt",
                35,
                20,
                1,
                "BEAT_PARENT",
                "BEAT_PARENT"
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(List.of(storyBeat), facts)
        );
        assertTrue(ex.getMessage().startsWith("INV-16"));
    }

    @Test
    void validateAndRepair_RejectsCombinedBudget_INV17() {
        DirectorRuntimeFacts facts = facts(1.0);
        PatchOp.AddStoryBeat storyBeat = buildStoryWithChain(
                "food_reserves_pct",
                "lt",
                35,
                20,
                1,
                "BEAT_PARENT",
                "BEAT_FOLLOW"
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(List.of(storyBeat), facts)
        );
        assertTrue(ex.getMessage().startsWith("INV-17"));
    }

    @Test
    void validateAndRepair_RejectsUnknownMetric_INV18() {
        DirectorRuntimeFacts facts = facts(5.0);
        PatchOp.AddStoryBeat storyBeat = buildStoryWithChain(
                "military_strength",
                "lt",
                35,
                20,
                1,
                "BEAT_PARENT",
                "BEAT_FOLLOW"
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(List.of(storyBeat), facts)
        );
        assertTrue(ex.getMessage().startsWith("INV-18"));
    }

    @Test
    void validateAndRepair_RejectsPopulationEqWithFractionalThreshold_INV18() {
        DirectorRuntimeFacts facts = facts(5.0);
        PatchOp.AddStoryBeat storyBeat = buildStoryWithChain(
                "population",
                "eq",
                24.5,
                20,
                1,
                "BEAT_PARENT",
                "BEAT_FOLLOW"
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(List.of(storyBeat), facts)
        );
        assertTrue(ex.getMessage().startsWith("INV-18"));
    }

    @Test
    void validateAndRepair_RejectsWindowOutOfBounds_INV19() {
        DirectorRuntimeFacts facts = facts(5.0);
        PatchOp.AddStoryBeat storyBeat = buildStoryWithChain(
                "food_reserves_pct",
                "lt",
                35,
                5,
                1,
                "BEAT_PARENT",
                "BEAT_FOLLOW"
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(List.of(storyBeat), facts)
        );
        assertTrue(ex.getMessage().startsWith("INV-19"));
    }

    @Test
    void validateAndRepair_RejectsMaxTriggersNotOne_INV19() {
        DirectorRuntimeFacts facts = facts(5.0);
        PatchOp.AddStoryBeat storyBeat = buildStoryWithChain(
                "food_reserves_pct",
                "lt",
                35,
                20,
                2,
                "BEAT_PARENT",
                "BEAT_FOLLOW"
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(List.of(storyBeat), facts)
        );
        assertTrue(ex.getMessage().startsWith("INV-19"));
    }

    private static DirectorRuntimeFacts facts(double budget) {
        return new DirectorRuntimeFacts(
                100,
                2,
                0,
                budget,
                List.of(),
                List.of()
        );
    }

    private static PatchOp.AddStoryBeat buildStoryWithChain(
            String metric,
            String operator,
            double threshold,
            long windowTicks,
            int maxTriggers,
            String beatId,
            String followUpBeatId
    ) {
        return new PatchOp.AddStoryBeat(
                "op_story_chain",
                beatId,
                "Primary story",
                20,
                "major",
                List.of(new PatchOp.EffectEntry("domain_modifier", "food", -0.08, 20)),
                new PatchOp.CausalChainEntry(
                        "causal_chain",
                        new PatchOp.CausalCondition(metric, operator, threshold),
                        new PatchOp.CausalFollowUpBeat(
                                followUpBeatId,
                                "Follow-up story",
                                12,
                                "major",
                                List.of(new PatchOp.EffectEntry("domain_modifier", "morale", -0.08, 12))
                        ),
                        windowTicks,
                        maxTriggers
                )
        );
    }
}
