package hu.zoltanterek.worldsim.refinery.planner.director;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;

class DirectorModelValidatorTest {
    private final DirectorModelValidator validator = new DirectorModelValidator(true);
    private final DirectorModelValidator gateOffValidator = new DirectorModelValidator(false);

    @Test
    void validateAndRepair_RejectsDuplicateOpId() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat("dup_op", "BEAT_1", "Valid beat text", 20),
                new PatchOp.SetColonyDirective("dup_op", 0, "PrioritizeFood", 16)
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains(DirectorDesign.INV_11));
    }

    @Test
    void validateAndRepair_NormalizesOrderDeterministically() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.SetColonyDirective("op_dir_1", 1, "BoostIndustry", 14),
                new PatchOp.AddStoryBeat("op_story_1", "BEAT_1", "Valid beat text", 20),
                new PatchOp.SetColonyDirective("op_dir_2", 0, "PrioritizeFood", 14)
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(candidate, facts);

        assertEquals(3, outcome.patch().size());
        assertTrue(outcome.patch().get(0) instanceof PatchOp.AddStoryBeat);
        assertTrue(outcome.patch().get(1) instanceof PatchOp.SetColonyDirective);
        assertTrue(outcome.repaired());
        assertTrue(outcome.warnings().stream().anyMatch(msg -> msg.contains(DirectorDesign.INV_13)));
    }

    @Test
    void conservativeRetryPatch_DropsInvalidOpsWithoutAddingNewOnes() {
        DirectorRuntimeFacts facts = facts(128L, 1, 4L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat("op_story", "BEAT_1", "Valid beat text", 20),
                new PatchOp.SetColonyDirective("op_bad_dir", 0, "UnknownDirective", 10),
                new PatchOp.SetColonyDirective("op_good_dir", 0, "PrioritizeFood", 10)
        );

        List<PatchOp> repaired = validator.conservativeRetryPatch(candidate, facts);

        assertEquals(1, repaired.size());
        PatchOp.SetColonyDirective directive = (PatchOp.SetColonyDirective) repaired.get(0);
        assertEquals("op_good_dir", directive.opId());
    }

    @Test
    void validateAndRepair_RejectsMajorWhenMajorAlreadyActive() {
        DirectorRuntimeFacts facts = facts(
                128L,
                2,
                0L,
                List.of(new DirectorRuntimeFacts.ActiveBeatFact("BEAT_EXISTING_MAJOR", "major", 6L))
        );

        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_major",
                        "BEAT_MAJOR_2",
                        "Major weather pressure arrives.",
                        20,
                        "major",
                        List.of(new PatchOp.EffectEntry("domain_modifier", "food", -0.05, 20))
                )
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains(DirectorDesign.INV_08));
    }

    @Test
    void validateAndRepair_AllowsMajorWhenNoActiveMajor() {
        DirectorRuntimeFacts facts = facts(
                128L,
                2,
                0L,
                List.of(new DirectorRuntimeFacts.ActiveBeatFact("BEAT_EXISTING_MINOR", "minor", 6L))
        );

        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_major",
                        "BEAT_MAJOR_2",
                        "Major weather pressure arrives.",
                        20,
                        "major",
                        List.of(new PatchOp.EffectEntry("domain_modifier", "food", -0.05, 20))
                )
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(candidate, facts);
        assertEquals(1, outcome.patch().size());
        assertTrue(outcome.patch().get(0) instanceof PatchOp.AddStoryBeat);
    }

    @Test
    void validateAndRepair_RejectsEpicWhenEpicAlreadyActive() {
        DirectorRuntimeFacts facts = facts(
                128L,
                2,
                0L,
                List.of(new DirectorRuntimeFacts.ActiveBeatFact("BEAT_EXISTING_EPIC", "epic", 10L))
        );

        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_epic",
                        "BEAT_EPIC_2",
                        "Epic pressure builds at all borders.",
                        24,
                        "epic",
                        List.of(
                                new PatchOp.EffectEntry("domain_modifier", "food", -0.05, 24),
                                new PatchOp.EffectEntry("domain_modifier", "morale", -0.05, 24),
                                new PatchOp.EffectEntry("domain_modifier", "economy", -0.05, 24)
                        )
                )
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains(DirectorDesign.INV_09));
    }

    @Test
    void validateAndRepair_AllowsEpicWhenNoActiveEpic() {
        DirectorRuntimeFacts facts = facts(
                128L,
                2,
                0L,
                List.of(new DirectorRuntimeFacts.ActiveBeatFact("BEAT_EXISTING_MAJOR", "major", 10L))
        );

        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_epic",
                        "BEAT_EPIC_2",
                        "Epic pressure builds at all borders.",
                        24,
                        "epic",
                        List.of(
                                new PatchOp.EffectEntry("domain_modifier", "food", -0.05, 24),
                                new PatchOp.EffectEntry("domain_modifier", "morale", -0.05, 24),
                                new PatchOp.EffectEntry("domain_modifier", "economy", -0.05, 24)
                        )
                )
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(candidate, facts);
        assertEquals(1, outcome.patch().size());
        assertTrue(outcome.patch().get(0) instanceof PatchOp.AddStoryBeat);
    }

    @Test
    void validateAndRepair_RejectsContradictorySameDomainModifiers() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_conflict",
                        "BEAT_CONFLICT",
                        "Major pressure with contradictory modifiers",
                        20,
                        "major",
                        List.of(
                                new PatchOp.EffectEntry("domain_modifier", "food", 0.20, 10),
                                new PatchOp.EffectEntry("domain_modifier", "food", -0.10, 10)
                        )
                )
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains(DirectorDesign.INV_20));
    }

    @Test
    void validateAndRepair_AllowsSameSignSameDomainModifiers() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_same_sign",
                        "BEAT_STACK",
                        "Major pressure without contradiction",
                        20,
                        "major",
                        List.of(
                                new PatchOp.EffectEntry("domain_modifier", "food", 0.10, 10),
                                new PatchOp.EffectEntry("domain_modifier", "food", 0.15, 10)
                        )
                )
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(candidate, facts);
        assertEquals(1, outcome.patch().size());
        assertTrue(outcome.patch().get(0) instanceof PatchOp.AddStoryBeat);
        PatchOp.AddStoryBeat repaired = (PatchOp.AddStoryBeat) outcome.patch().get(0);
        assertEquals(20, repaired.effects().get(0).durationTicks());
        assertEquals(20, repaired.effects().get(1).durationTicks());
        assertTrue(outcome.warnings().stream().anyMatch(msg -> msg.contains(DirectorDesign.INV_06)));
    }

    @Test
    void validateAndRepair_AllowsOppositeSignsAcrossDifferentDomains() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_diff_domains",
                        "BEAT_DIFF",
                        "Mixed domain directions",
                        20,
                        "major",
                        List.of(
                                new PatchOp.EffectEntry("domain_modifier", "food", -0.10, 10),
                                new PatchOp.EffectEntry("domain_modifier", "morale", 0.10, 10)
                        )
                )
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(candidate, facts);
        assertEquals(1, outcome.patch().size());
        PatchOp.AddStoryBeat repaired = (PatchOp.AddStoryBeat) outcome.patch().get(0);
        assertEquals(20, repaired.effects().get(0).durationTicks());
        assertEquals(20, repaired.effects().get(1).durationTicks());
    }

    @Test
    void validateAndRepair_AlignsStoryEffectDurationToBeatDuration() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_align_duration",
                        "BEAT_ALIGN_DURATION",
                        "Duration alignment sample",
                        30,
                        "major",
                        List.of(new PatchOp.EffectEntry("domain_modifier", "food", 0.10, 5))
                )
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(candidate, facts);
        assertEquals(1, outcome.patch().size());
        PatchOp.AddStoryBeat repaired = (PatchOp.AddStoryBeat) outcome.patch().get(0);
        assertEquals(30, repaired.durationTicks());
        assertEquals(30, repaired.effects().get(0).durationTicks());
        assertTrue(outcome.repaired());
        assertTrue(outcome.warnings().stream().anyMatch(msg -> msg.contains(DirectorDesign.INV_06)));
    }

    @Test
    void validateAndRepair_NormalizesStorySeverityToEffectCount() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_severity_fix",
                        "BEAT_SEVERITY_FIX",
                        "Story severity normalization sample",
                        20,
                        "minor",
                        List.of(new PatchOp.EffectEntry("domain_modifier", "food", -0.05, 20))
                )
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(candidate, facts);
        PatchOp.AddStoryBeat repaired = (PatchOp.AddStoryBeat) outcome.patch().get(0);

        assertEquals("major", repaired.severity());
        assertTrue(outcome.repaired());
        assertTrue(outcome.warnings().stream().anyMatch(msg -> msg.contains("Normalized story beat severity")));
    }

    @Test
    void validateAndRepair_NormalizesFollowUpSeverityToEffectCount() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_follow_up_fix",
                        "BEAT_PARENT",
                        "Story with follow-up severity normalization",
                        20,
                        "minor",
                        List.of(),
                        new PatchOp.CausalChainEntry(
                                "causal_chain",
                                new PatchOp.CausalCondition("food_reserves_pct", "lt", 35),
                                new PatchOp.CausalFollowUpBeat(
                                        "BEAT_CHILD",
                                        "Follow-up intensity rises.",
                                        12,
                                        "minor",
                                        List.of(new PatchOp.EffectEntry("domain_modifier", "morale", -0.05, 12))
                                ),
                                20,
                                1
                        )
                )
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(candidate, facts);
        PatchOp.AddStoryBeat repaired = (PatchOp.AddStoryBeat) outcome.patch().get(0);

        assertEquals("minor", repaired.severity());
        assertEquals("major", repaired.causalChain().followUpBeat().severity());
    }

    @Test
    void validateAndRepair_RejectsUnknownEffectDomain() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_story_bad_domain",
                        "BEAT_BAD",
                        "Invalid domain",
                        20,
                        "major",
                        List.of(new PatchOp.EffectEntry("domain_modifier", "unknown_domain", 0.10, 10))
                )
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains(DirectorDesign.INV_02));
    }

    @Test
    void validateAndRepair_RejectsWhenBudgetExceeded() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, 0.5, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_budget_over",
                        "BEAT_BUDGET_OVER",
                        "Heavy pressure event",
                        30,
                        "major",
                        List.of(new PatchOp.EffectEntry("domain_modifier", "food", 0.20, 30))
                )
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains(DirectorDesign.INV_15));
    }

    @Test
    void validateAndRepair_RejectsWhenBudgetExceededAfterDurationAlignment() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, 0.5, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_budget_after_align",
                        "BEAT_BUDGET_ALIGN",
                        "Alignment should still enforce budget",
                        30,
                        "major",
                        List.of(new PatchOp.EffectEntry("domain_modifier", "food", 0.20, 1))
                )
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains(DirectorDesign.INV_15));
    }

    @Test
    void validateAndRepair_AllowsWhenBudgetAtBoundary() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, 3.0, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.AddStoryBeat(
                        "op_budget_edge",
                        "BEAT_BUDGET_EDGE",
                        "Edge budget event",
                        30,
                        "major",
                        List.of(new PatchOp.EffectEntry("domain_modifier", "food", 0.20, 30))
                )
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(candidate, facts);
        assertEquals(1, outcome.patch().size());
    }

    @Test
    void validateAndRepair_AcceptsSingleCampaignOp() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.DeclareWar("op_campaign_1", 1, 2, "border pressure")
        );

        DirectorValidationOutcome outcome = validator.validateAndRepair(candidate, facts);
        assertEquals(1, outcome.patch().size());
        assertTrue(outcome.patch().get(0) instanceof PatchOp.DeclareWar);
    }

    @Test
    void validateAndRepair_RejectsMultipleCampaignOps() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.DeclareWar("op_campaign_1", 1, 2, "border pressure"),
                new PatchOp.ProposeTreaty("op_campaign_2", 2, 1, "ceasefire", "cooldown")
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains(DirectorDesign.INV_12));
    }

    @Test
    void validateAndRepair_RejectsCampaignSelfTarget() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.ProposeTreaty("op_campaign_self", 1, 1, "ceasefire", "invalid")
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains("proposeTreaty requires proposerFactionId != receiverFactionId"));
    }

    @Test
    void validateAndRepair_RejectsCampaignOpsWhenGateDisabled() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.DeclareWar("op_campaign_disabled", 1, 2, "blocked")
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> gateOffValidator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains("campaignEnabled=false"));
    }

    @Test
    void validateAndRepair_RejectsCampaignFactionOutOfRange() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.DeclareWar("op_campaign_range", -1, 2, "invalid")
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains("out of range"));
    }

    @Test
    void validateAndRepair_RejectsCampaignTreatyKindOutsideAllowlist() {
        DirectorRuntimeFacts facts = facts(128L, 2, 0L, List.of());
        List<PatchOp> candidate = List.of(
                new PatchOp.ProposeTreaty("op_campaign_kind", 1, 2, "alliance", "invalid")
        );

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validator.validateAndRepair(candidate, facts)
        );

        assertTrue(ex.getMessage().contains("Unsupported proposeTreaty.treatyKind"));
    }

    private static DirectorRuntimeFacts facts(
            long tick,
            int colonyCount,
            long cooldownTicks,
            List<DirectorRuntimeFacts.ActiveBeatFact> activeBeats
    ) {
        return facts(tick, colonyCount, cooldownTicks, DirectorDesign.DEFAULT_INFLUENCE_BUDGET, activeBeats);
    }

    private static DirectorRuntimeFacts facts(
            long tick,
            int colonyCount,
            long cooldownTicks,
            double remainingBudget,
            List<DirectorRuntimeFacts.ActiveBeatFact> activeBeats
    ) {
        return new DirectorRuntimeFacts(
                tick,
                colonyCount,
                cooldownTicks,
                remainingBudget,
                activeBeats,
                List.of()
        );
    }
}
