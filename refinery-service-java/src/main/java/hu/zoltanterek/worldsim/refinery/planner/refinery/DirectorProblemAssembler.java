package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeAssertions;

public final class DirectorProblemAssembler {
    public String assemble(DirectorRuntimeAssertions runtimeAssertions) {
        return assemble(runtimeAssertions == null
                ? List.of()
                : List.of(new DirectorProblemFragment(runtimeAssertions.lines())));
    }

    public String assemble(List<DirectorProblemFragment> fragments) {
        List<String> sections = new ArrayList<>();
        for (String resourcePath : RefineryArtifactCatalog.directorCanonicalProblemResourcePaths()) {
            sections.add(readResource(resourcePath));
        }

        for (DirectorProblemFragment item : fragments == null ? List.<DirectorProblemFragment>of() : fragments) {
            String fragment = item == null ? "" : item.problemFragment();
            if (!fragment.isBlank()) {
                sections.add(fragment);
            }
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
