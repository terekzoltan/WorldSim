package hu.zoltanterek.worldsim.refinery.contracts;

import java.util.List;
import java.util.Set;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;

class RefineryVocabularyTest {
    @Test
    void sharedOutputModesExcludeAdapterLocalAuto() {
        assertEquals(List.of("both", "story_only", "nudge_only", "off"), RefineryVocabulary.SHARED_OUTPUT_MODES);
        assertFalse(RefineryVocabulary.SHARED_OUTPUT_MODES.contains("auto"));
    }

    @Test
    void directorDesignUsesSharedSymbolicVocabulary() {
        assertEquals(RefineryVocabulary.DOMAINS, DirectorDesign.VALID_DOMAINS);
        assertEquals(RefineryVocabulary.GOAL_CATEGORIES, DirectorDesign.VALID_GOAL_CATEGORIES);
        assertEquals(RefineryVocabulary.SEVERITIES, DirectorDesign.VALID_SEVERITIES);
        assertEquals(RefineryVocabulary.CAMPAIGN_KINDS, DirectorDesign.VALID_CAMPAIGN_KINDS);
        assertEquals(RefineryVocabulary.TREATY_KINDS, DirectorDesign.VALID_TREATY_KINDS);
    }

    @Test
    void symbolicVocabularyKeepsExpectedStableValues() {
        assertEquals(Set.of("minor", "major", "epic"), RefineryVocabulary.SEVERITIES);
        assertEquals(Set.of("food", "morale", "economy", "military", "research"), RefineryVocabulary.DOMAINS);
        assertEquals(
                Set.of("farming", "gathering", "crafting", "building", "social", "military", "research", "rest"),
                RefineryVocabulary.GOAL_CATEGORIES
        );
        assertEquals(Set.of("ceasefire", "peace_talks"), RefineryVocabulary.TREATY_KINDS);
    }
}
