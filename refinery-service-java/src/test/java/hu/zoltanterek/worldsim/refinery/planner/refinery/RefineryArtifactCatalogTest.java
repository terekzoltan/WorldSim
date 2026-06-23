package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.io.InputStream;
import java.nio.charset.StandardCharsets;

import org.junit.jupiter.api.Test;
import tools.refinery.generator.standalone.StandaloneRefinery;

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

    @Test
    void canonicalFamilyArtifactsAreResolvableFromClasspath() {
        for (RefineryArtifactFamily family : RefineryArtifactFamily.values()) {
            for (String path : RefineryArtifactCatalog.canonicalProblemResourcePaths(family)) {
                assertResourceExists(path);
            }
        }
    }

    @Test
    void canonicalFamilyArtifactPathsAreDistinct() {
        var paths = RefineryArtifactCatalog.canonicalProblemResourcePaths(RefineryArtifactFamily.DIRECTOR);
        paths = new java.util.ArrayList<>(paths);
        paths.addAll(RefineryArtifactCatalog.canonicalProblemResourcePaths(RefineryArtifactFamily.COMBAT));
        paths.addAll(RefineryArtifactCatalog.canonicalProblemResourcePaths(RefineryArtifactFamily.CAMPAIGN));
        paths.addAll(RefineryArtifactCatalog.canonicalProblemResourcePaths(RefineryArtifactFamily.COMMON));
        long distinctCount = paths.stream().distinct().count();
        assertEquals(paths.size(), distinctCount, "Canonical family artifact paths must be unique across families");
    }

    @Test
    void nonDirectorSkeletonArtifactsParseAsRefineryProblems() throws Exception {
        for (RefineryArtifactFamily family : java.util.List.of(
                RefineryArtifactFamily.COMMON,
                RefineryArtifactFamily.COMBAT,
                RefineryArtifactFamily.CAMPAIGN
        )) {
            for (String path : RefineryArtifactCatalog.canonicalProblemResourcePaths(family)) {
                StandaloneRefinery.getProblemLoader().loadString(readResource(path));
            }
        }
    }

    private static void assertResourceExists(String resourcePath) {
        try (InputStream stream = RefineryArtifactCatalogTest.class.getResourceAsStream(resourcePath)) {
            assertNotNull(stream, "Expected classpath resource to exist: " + resourcePath);
        } catch (Exception ex) {
            throw new AssertionError("Failed to load classpath resource: " + resourcePath, ex);
        }
    }

    private static String readResource(String resourcePath) throws Exception {
        try (InputStream stream = RefineryArtifactCatalogTest.class.getResourceAsStream(resourcePath)) {
            assertNotNull(stream, "Expected classpath resource to exist: " + resourcePath);
            return new String(stream.readAllBytes(), StandardCharsets.UTF_8);
        }
    }
}
