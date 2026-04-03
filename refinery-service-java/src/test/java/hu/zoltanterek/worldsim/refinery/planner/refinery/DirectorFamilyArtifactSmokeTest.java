package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.LinkedHashMap;
import java.util.Map;

import org.junit.jupiter.api.Test;

import tools.refinery.generator.standalone.StandaloneRefinery;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class DirectorFamilyArtifactSmokeTest {
    @Test
    void directorFamilyArtifactsParseAsSingleLayeredProblem() throws IOException {
        Map<String, String> artifactContents = loadCanonicalDirectorArtifacts();

        String design = artifactContents.get(RefineryArtifactCatalog.directorDesignProblemResourcePath());
        String model = artifactContents.get(RefineryArtifactCatalog.directorModelProblemResourcePath());
        String runtime = artifactContents.get(RefineryArtifactCatalog.directorRuntimeProblemResourcePath());
        String output = artifactContents.get(RefineryArtifactCatalog.directorOutputProblemResourcePath());

        assertContains(design, "DirectorDesignLayerAnchor");
        assertContains(model, "DirectorModelLayerAnchor");
        assertContains(runtime, "DirectorRuntimeLayerAnchor");
        assertContains(output, "DirectorOutputLayerAnchor");

        String assembledProblem = String.join("\n\n", artifactContents.values());
        var problem = StandaloneRefinery.getProblemLoader().loadString(assembledProblem);
        assertNotNull(problem, "Layered director family problem should parse/load successfully");
    }

    @Test
    void canonicalFamilyAssemblyStaysSeparateFromHistoricalSpike() throws IOException {
        String assembledProblem = String.join("\n\n", loadCanonicalDirectorArtifacts().values());
        String spikeText = readResource(RefineryArtifactCatalog.directorSpikeProblemResourcePath());
        assertFalse(assembledProblem.equals(spikeText), "Canonical family assembly must stay separate from TR1-A spike");
    }

    private static Map<String, String> loadCanonicalDirectorArtifacts() throws IOException {
        Map<String, String> result = new LinkedHashMap<>();
        for (String path : RefineryArtifactCatalog.directorCanonicalProblemResourcePaths()) {
            result.put(path, readResource(path));
        }
        return result;
    }

    private static String readResource(String resourcePath) throws IOException {
        try (InputStream stream = DirectorFamilyArtifactSmokeTest.class.getResourceAsStream(resourcePath)) {
            if (stream == null) {
                throw new IOException("Missing classpath resource: " + resourcePath);
            }
            return new String(stream.readAllBytes(), StandardCharsets.UTF_8);
        }
    }

    private static void assertContains(String text, String token) {
        assertTrue(text.contains(token), "Expected token not found: " + token);
    }
}
