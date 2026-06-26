package hu.zoltanterek.worldsim.refinery.planner.director;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.io.IOException;
import java.io.InputStream;

import org.junit.jupiter.api.Test;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.PatchRequest;

class DirectorRuntimeFactsFixtureTest {
    private static final String FIXTURE_ROOT = "fixtures/director-runtime-facts/";

    private final ObjectMapper objectMapper = new ObjectMapper();
    private final DirectorSnapshotMapper mapper = new DirectorSnapshotMapper();

    @Test
    void canonicalCurrentShapeMinimal_MapsCSharpAuthorityFields() throws IOException {
        PatchRequest request = readCanonicalFixture("canonical-current-shape-minimal.json");

        DirectorRuntimeFacts facts = mapper.map(request);

        assertEquals(128L, facts.tick());
        assertEquals(4, facts.colonyCount());
        assertEquals(0L, facts.beatCooldownTicks());
        assertEquals(5.0, facts.remainingInfluenceBudget());
        assertTrue(facts.activeBeats().isEmpty());
        assertTrue(facts.activeDirectives().isEmpty());
    }

    @Test
    void canonicalActiveCooldown_UsesDirectorCooldownOverLegacyWorldFallback() throws IOException {
        PatchRequest request = readCanonicalFixture("canonical-active-cooldown.json");

        DirectorRuntimeFacts facts = mapper.map(request);

        assertEquals(240L, facts.tick());
        assertEquals(4, facts.colonyCount());
        assertEquals(12L, facts.beatCooldownTicks());
        assertEquals(4.5, facts.remainingInfluenceBudget());
    }

    @Test
    void canonicalActiveMajorBeat_PreservesSeverityAndRemainingTicks() throws IOException {
        PatchRequest request = readCanonicalFixture("canonical-active-major-beat.json");

        DirectorRuntimeFacts facts = mapper.map(request);

        assertEquals(1, facts.activeBeats().size());
        var beat = facts.activeBeats().get(0);
        assertEquals("BEAT_MAJOR_RFM_D2", beat.beatId());
        assertEquals("major", beat.severity());
        assertEquals(30L, beat.remainingTicks());
    }

    @Test
    void canonicalActiveEpicBeat_PreservesSeverityAndRemainingTicks() throws IOException {
        PatchRequest request = readCanonicalFixture("canonical-active-epic-beat.json");

        DirectorRuntimeFacts facts = mapper.map(request);
        assertEquals(1, facts.activeBeats().size());
        var beat = facts.activeBeats().get(0);
        assertEquals("BEAT_EPIC_RFM_D2", beat.beatId());
        assertEquals("epic", beat.severity());
        assertEquals(45L, beat.remainingTicks());
    }

    @Test
    void canonicalActiveDirective_PreservesValidDirectiveFields() throws IOException {
        PatchRequest request = readCanonicalFixture("canonical-active-directive.json");

        DirectorRuntimeFacts facts = mapper.map(request);
        assertEquals(1, facts.activeDirectives().size());
        var directive = facts.activeDirectives().get(0);
        assertEquals(2, directive.colonyId());
        assertEquals("PrioritizeFood", directive.directive());
    }

    @Test
    void canonicalBudgetSnapshotOnly_UsesSnapshotRemainingBudgetWhenNoRequestOverrideExists() throws IOException {
        PatchRequest request = readCanonicalFixture("canonical-budget-snapshot-only.json");

        DirectorRuntimeFacts facts = mapper.map(request, 9.0);
        assertEquals(3.125, facts.remainingInfluenceBudget());
    }

    @Test
    void canonicalBudgetRequestOverride_RootConstraintBeatsNestedAndSnapshotBudget() throws IOException {
        PatchRequest request = readCanonicalFixture("canonical-budget-request-override.json");

        DirectorRuntimeFacts facts = mapper.map(request, 9.0);
        assertEquals(2.5, facts.remainingInfluenceBudget());
    }

    @Test
    void canonicalMultipleColonyWorld_UsesWorldColonyCountNotDirectorPopulationOrFallbackCount() throws IOException {
        PatchRequest request = readCanonicalFixture("canonical-multiple-colony-world.json");

        DirectorRuntimeFacts facts = mapper.map(request);
        assertEquals(6, facts.colonyCount());
    }

    @Test
    void canonicalCausalContextPresent_IsNotPromotedIntoRuntimeFacts() throws IOException {
        PatchRequest request = readCanonicalFixture("canonical-causal-context-present-not-consumed.json");

        DirectorRuntimeFacts facts = mapper.map(request);
        assertEquals(1024L, facts.tick());
        assertEquals(4, facts.colonyCount());
        assertEquals(4.75, facts.remainingInfluenceBudget());
        assertTrue(facts.activeBeats().isEmpty());
        assertTrue(facts.activeDirectives().isEmpty());
    }

    @Test
    void legacyWorldCooldownFallback_RemainsCompatibilityNotAuthorityProof() throws IOException {
        PatchRequest request = readLegacyFixture("legacy-world-cooldown-fallback.json");

        DirectorRuntimeFacts facts = mapper.map(request);
        assertEquals(2, facts.colonyCount());
        assertEquals(7L, facts.beatCooldownTicks());
        assertEquals(5.0, facts.remainingInfluenceBudget());
        assertEquals(1, facts.activeBeats().size());
        assertEquals(1, facts.activeDirectives().size());
    }

    @Test
    void legacyNestedBudgetFallback_RemainsCompatibilityNotCSharpAuthority() throws IOException {
        PatchRequest request = readLegacyFixture("legacy-nested-budget-fallback.json");

        DirectorRuntimeFacts facts = mapper.map(request, 9.0);
        assertEquals(1.75, facts.remainingInfluenceBudget());
    }

    @Test
    void canonicalFixtureValidation_RejectsMissingWorldColonyCount() throws IOException {
        ObjectNode root = readFixtureRoot("canonical-current-shape-minimal.json");
        root.withObject("snapshot").withObject("world").remove("colonyCount");

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validateCanonicalRuntimeFactsFixture(root)
        );
        assertTrue(ex.getMessage().contains("snapshot.world.colonyCount"));
    }

    @Test
    void canonicalFixtureValidation_RejectsMissingDirectorCooldown() throws IOException {
        ObjectNode root = readFixtureRoot("canonical-current-shape-minimal.json");
        root.withObject("snapshot").withObject("director").remove("beatCooldownRemainingTicks");

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validateCanonicalRuntimeFactsFixture(root)
        );
        assertTrue(ex.getMessage().contains("snapshot.director.beatCooldownRemainingTicks"));
    }

    @Test
    void canonicalFixtureValidation_RejectsMissingSnapshotBudgetWithoutConstraintOverride() throws IOException {
        ObjectNode root = readFixtureRoot("canonical-budget-snapshot-only.json");
        root.withObject("snapshot").withObject("director").remove("remainingInfluenceBudget");

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validateCanonicalRuntimeFactsFixture(root)
        );
        assertTrue(ex.getMessage().contains("snapshot.director.remainingInfluenceBudget"));
    }

    @Test
    void canonicalFixtureValidation_AllowsMissingSnapshotBudgetWhenRootConstraintOverrideExists() throws IOException {
        ObjectNode root = readFixtureRoot("canonical-budget-request-override.json");
        root.withObject("snapshot").withObject("director").remove("remainingInfluenceBudget");

        validateCanonicalRuntimeFactsFixture(root);
    }

    @Test
    void canonicalFixtureValidation_RejectsMalformedActiveBeat() throws IOException {
        ObjectNode root = readFixtureRoot("canonical-active-major-beat.json");
        ((ObjectNode) root.withObject("snapshot").withObject("director").withArray("activeBeats").get(0))
                .remove("remainingTicks");

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validateCanonicalRuntimeFactsFixture(root)
        );
        assertTrue(ex.getMessage().contains("activeBeats[0].remainingTicks"));
    }

    @Test
    void canonicalFixtureValidation_RejectsMalformedActiveDirective() throws IOException {
        ObjectNode root = readFixtureRoot("canonical-active-directive.json");
        ((ObjectNode) root.withObject("snapshot").withObject("director").withArray("activeDirectives").get(0))
                .remove("directive");

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validateCanonicalRuntimeFactsFixture(root)
        );
        assertTrue(ex.getMessage().contains("activeDirectives[0].directive"));
    }

    @Test
    void canonicalFixtureValidation_RejectsCampaignEnabledAsCSharpRuntimeFact() throws IOException {
        ObjectNode root = readFixtureRoot("canonical-current-shape-minimal.json");
        root.withObject("snapshot").withObject("director").put("campaignEnabled", true);

        IllegalArgumentException ex = assertThrows(
                IllegalArgumentException.class,
                () -> validateCanonicalRuntimeFactsFixture(root)
        );
        assertTrue(ex.getMessage().contains("campaignEnabled"));
    }

    private PatchRequest readCanonicalFixture(String name) throws IOException {
        ObjectNode root = readFixtureRoot(name);
        validateCanonicalRuntimeFactsFixture(root);
        return objectMapper.treeToValue(root, PatchRequest.class);
    }

    private PatchRequest readLegacyFixture(String name) throws IOException {
        return objectMapper.treeToValue(readFixtureRoot(name), PatchRequest.class);
    }

    private ObjectNode readFixtureRoot(String name) throws IOException {
        try (InputStream stream = getClass().getClassLoader().getResourceAsStream(FIXTURE_ROOT + name)) {
            if (stream == null) {
                throw new IOException("Missing fixture: " + name);
            }
            return (ObjectNode) objectMapper.readTree(stream);
        }
    }

    private static void validateCanonicalRuntimeFactsFixture(ObjectNode root) {
        requireNumber(root, "tick");
        JsonNode snapshot = requireObject(root, "snapshot");
        JsonNode world = requireObject(snapshot, "world");
        JsonNode director = requireObject(snapshot, "director");

        requireNumber(world, "colonyCount", "snapshot.world.colonyCount");
        requireNumber(director, "beatCooldownRemainingTicks", "snapshot.director.beatCooldownRemainingTicks");
        requireArray(director, "activeBeats", "snapshot.director.activeBeats");
        requireArray(director, "activeDirectives", "snapshot.director.activeDirectives");

        if (director.has("campaignEnabled")) {
            throw new IllegalArgumentException("campaignEnabled is Java config-owned, not a C# runtime fact");
        }

        boolean hasRootBudgetOverride = root.path("constraints").path("maxBudget").isNumber();
        if (!hasRootBudgetOverride) {
            requireNumber(director, "remainingInfluenceBudget", "snapshot.director.remainingInfluenceBudget");
        }

        JsonNode activeBeats = director.path("activeBeats");
        for (int i = 0; i < activeBeats.size(); i++) {
            JsonNode beat = activeBeats.get(i);
            requireText(beat, "beatId", "snapshot.director.activeBeats[" + i + "].beatId");
            requireText(beat, "severity", "snapshot.director.activeBeats[" + i + "].severity");
            requireNumber(beat, "remainingTicks", "snapshot.director.activeBeats[" + i + "].remainingTicks");
        }

        JsonNode activeDirectives = director.path("activeDirectives");
        for (int i = 0; i < activeDirectives.size(); i++) {
            JsonNode directive = activeDirectives.get(i);
            requireNumber(directive, "colonyId", "snapshot.director.activeDirectives[" + i + "].colonyId");
            requireText(directive, "directive", "snapshot.director.activeDirectives[" + i + "].directive");
        }
    }

    private static JsonNode requireObject(JsonNode node, String field) {
        JsonNode value = node.path(field);
        if (!value.isObject()) {
            throw new IllegalArgumentException(field + " must be an object");
        }
        return value;
    }

    private static void requireArray(JsonNode node, String field, String label) {
        if (!node.path(field).isArray()) {
            throw new IllegalArgumentException(label + " must be an array");
        }
    }

    private static void requireNumber(JsonNode node, String field) {
        requireNumber(node, field, field);
    }

    private static void requireNumber(JsonNode node, String field, String label) {
        if (!node.path(field).isNumber()) {
            throw new IllegalArgumentException(label + " must be numeric");
        }
    }

    private static void requireText(JsonNode node, String field, String label) {
        JsonNode value = node.path(field);
        if (!value.isTextual() || value.asText().isBlank()) {
            throw new IllegalArgumentException(label + " must be nonblank text");
        }
    }
}
