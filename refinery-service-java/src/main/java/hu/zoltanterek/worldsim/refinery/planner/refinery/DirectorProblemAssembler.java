package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeAssertions;

public final class DirectorProblemAssembler {
    public String assemble(DirectorRuntimeAssertions runtimeAssertions) {
        List<String> sections = new ArrayList<>();
        for (String resourcePath : RefineryArtifactCatalog.directorCanonicalProblemResourcePaths()) {
            sections.add(readResource(resourcePath));
        }

        String fragment = runtimeAssertions == null ? "" : runtimeAssertions.problemFragment();
        if (!fragment.isBlank()) {
            sections.add(fragment);
        }

        return String.join(System.lineSeparator() + System.lineSeparator(), sections);
    }

    private static String readResource(String resourcePath) {
        try (InputStream stream = DirectorProblemAssembler.class.getResourceAsStream(resourcePath)) {
            if (stream == null) {
                throw new IllegalStateException("Missing classpath resource: " + resourcePath);
            }
            return new String(stream.readAllBytes(), StandardCharsets.UTF_8);
        } catch (IOException ex) {
            throw new IllegalStateException("Cannot read classpath resource: " + resourcePath, ex);
        }
    }
}
