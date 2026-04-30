package hu.zoltanterek.worldsim.refinery.planner.director;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

class DirectorRuntimeAssertionsMapperTest {
    private final DirectorRuntimeAssertionsMapper mapper = new DirectorRuntimeAssertionsMapper();

    @Test
    void map_EmitsD5ScalarRuntimeFacts() {
        DirectorRuntimeFacts facts = new DirectorRuntimeFacts(123L, 4, 12L, 3.5, List.of(), List.of());

        DirectorRuntimeAssertions assertions = mapper.map(facts);

        assertTrue(assertions.lines().contains("RuntimeCheckpointContext(runtimeCheckpoint)."));
        assertTrue(assertions.lines().contains("tick(runtimeCheckpoint): 123."));
        assertTrue(assertions.lines().contains("colonyCount(runtimeCheckpoint): 4."));
        assertTrue(assertions.lines().contains("beatCooldownRemainingTicks(runtimeCheckpoint): 12."));
        assertTrue(assertions.lines().contains("remainingInfluenceBudget(runtimeCheckpoint): 3.5."));
    }

    @Test
    void map_EmptyActiveFactsOnlyEmitContextScalars() {
        DirectorRuntimeFacts facts = new DirectorRuntimeFacts(1L, 1, 0L, 0d, List.of(), List.of());

        DirectorRuntimeAssertions assertions = mapper.map(facts);

        assertEquals(5, assertions.lines().size());
        assertFalse(assertions.problemFragment().contains("ActiveBeatFact("));
        assertFalse(assertions.problemFragment().contains("ActiveDirectiveFact("));
    }

    @Test
    void map_ActiveBeatsUseStableOrderingAndCurrentFactShape() {
        DirectorRuntimeFacts facts = new DirectorRuntimeFacts(
                1L,
                2,
                0L,
                5d,
                List.of(
                        new DirectorRuntimeFacts.ActiveBeatFact("BEAT_Z", "epic", 30),
                        new DirectorRuntimeFacts.ActiveBeatFact("BEAT_A", "major", 20)
                ),
                List.of()
        );

        String fragment = mapper.map(facts).problemFragment();

        assertTrue(fragment.indexOf("beatId(activeBeat_000): \"BEAT_A\".")
                < fragment.indexOf("beatId(activeBeat_001): \"BEAT_Z\"."));
        assertTrue(fragment.contains("remainingTicks(activeBeat_000): 20."));
        assertTrue(fragment.contains("Severity(severity_major)."));
        assertTrue(fragment.contains("ActiveBeatFact::severity(activeBeat_000, severity_major)."));
    }

    @Test
    void map_ActiveDirectivesUseStableOrderingAndCurrentFactShape() {
        DirectorRuntimeFacts facts = new DirectorRuntimeFacts(
                1L,
                2,
                0L,
                5d,
                List.of(),
                List.of(
                        new DirectorRuntimeFacts.ActiveDirectiveFact(2, "PrioritizeWood"),
                        new DirectorRuntimeFacts.ActiveDirectiveFact(0, "PrioritizeFood")
                )
        );

        String fragment = mapper.map(facts).problemFragment();

        assertTrue(fragment.indexOf("colonyId(activeDirective_000): 0.")
                < fragment.indexOf("colonyId(activeDirective_001): 2."));
        assertTrue(fragment.contains("directiveKey(activeDirective_000): \"PrioritizeFood\"."));
        assertTrue(fragment.contains("DirectiveKind(directive_prioritizefood)."));
        assertTrue(fragment.contains("ActiveDirectiveFact::directive(activeDirective_000, directive_prioritizefood)."));
    }

    @Test
    void map_EscapesStringsAndSanitizesObjectIdentifiers() {
        DirectorRuntimeFacts facts = new DirectorRuntimeFacts(
                1L,
                1,
                0L,
                5d,
                List.of(new DirectorRuntimeFacts.ActiveBeatFact("BEAT \"quoted\"\\path", "Major Pressure", 4)),
                List.of(new DirectorRuntimeFacts.ActiveDirectiveFact(0, "Prioritize Food!"))
        );

        String fragment = mapper.map(facts).problemFragment();

        assertTrue(fragment.contains("beatId(activeBeat_000): \"BEAT \\\"quoted\\\"\\\\path\"."));
        assertTrue(fragment.contains("Severity(severity_major_pressure)."));
        assertTrue(fragment.contains("DirectiveKind(directive_prioritize_food)."));
    }
}
