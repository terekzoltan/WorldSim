package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.io.InputStream;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

class RefineryArtifactCatalogTest {
    @Test
    void spikeArtifactIsResolvableFromClasspath() {
        assertResourceExists(RefineryArtifactCatalog.directorSpikeProblemResourcePath());
    }

    @Test
    void familyRootsAreMaterializedInClasspath() {
        for (RefineryArtifactFamily family : RefineryArtifactFamily.values()) {
            assertResourceExists(RefineryArtifactCatalog.familyMarkerResourcePath(family));
        }
    }

    @Test
    void spikePathStaysSeparateFromCanonicalDirectorTargets() {
        String spike = RefineryArtifactCatalog.directorSpikeProblemResourcePath();
        assertFalse(spike.equals(RefineryArtifactCatalog.directorDesignProblemResourcePath()));
        assertFalse(spike.equals(RefineryArtifactCatalog.directorModelProblemResourcePath()));
        assertFalse(spike.equals(RefineryArtifactCatalog.directorRuntimeProblemResourcePath()));
        assertFalse(spike.equals(RefineryArtifactCatalog.directorOutputProblemResourcePath()));
    }

    @Test
    void canonicalDirectorArtifactsAreResolvableFromClasspath() {
        for (String path : RefineryArtifactCatalog.directorCanonicalProblemResourcePaths()) {
            assertResourceExists(path);
        }
    }

    @Test
    void canonicalDirectorArtifactPathsAreDistinct() {
        var paths = RefineryArtifactCatalog.directorCanonicalProblemResourcePaths();
        long distinctCount = paths.stream().distinct().count();
        assertEquals(paths.size(), distinctCount, "Canonical director artifact paths must be unique");
    }

    private static void assertResourceExists(String resourcePath) {
        try (InputStream stream = RefineryArtifactCatalogTest.class.getResourceAsStream(resourcePath)) {
            assertNotNull(stream, "Expected classpath resource to exist: " + resourcePath);
        } catch (Exception ex) {
            throw new AssertionError("Failed to load classpath resource: " + resourcePath, ex);
        }
    }
}
