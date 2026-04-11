package hu.zoltanterek.worldsim.refinery.planner.director;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.List;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;

class DirectorInfluenceBudgetTest {
    @Test
    void calculateBudgetUsed_IncludesCausalChainBaseAndFollowUpCost() {
        PatchOp.AddStoryBeat story = new PatchOp.AddStoryBeat(
                "op_story_chain",
                "BEAT_A",
                "Primary story",
                20,
                "major",
                List.of(new PatchOp.EffectEntry("domain_modifier", "food", -0.10, 20)),
                new PatchOp.CausalChainEntry(
                        "causal_chain",
                        new PatchOp.CausalCondition("food_reserves_pct", "lt", 35),
                        new PatchOp.CausalFollowUpBeat(
                                "BEAT_A_FOLLOW",
                                "Follow-up story",
                                10,
                                "major",
                                List.of(new PatchOp.EffectEntry("domain_modifier", "morale", -0.20, 10))
                        ),
                        20,
                        1
                )
        );

        double budget = DirectorInfluenceBudget.calculateBudgetUsed(List.of(story));
        // parent: 0.10 * 20 * 0.5 = 1.0
        // follow-up: 0.20 * 10 * 0.5 = 1.0
        // causal base: 2.0
        assertEquals(4.0d, budget, 0.0001d);
    }

    @Test
    void calculateBudgetUsed_CampaignOpsAreBudgetNeutral() {
        PatchOp.DeclareWar declareWar = new PatchOp.DeclareWar("op_war", 1, 2, "pressure");
        PatchOp.ProposeTreaty treaty = new PatchOp.ProposeTreaty("op_treaty", 2, 1, "ceasefire", "pause");

        double budget = DirectorInfluenceBudget.calculateBudgetUsed(List.of(declareWar, treaty));
        assertEquals(0.0d, budget, 0.0001d);
    }
}
