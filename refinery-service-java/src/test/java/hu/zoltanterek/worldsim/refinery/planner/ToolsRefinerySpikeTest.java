package hu.zoltanterek.worldsim.refinery.planner;

import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.planner.refinery.RefineryArtifactCatalog;
import tools.refinery.generator.GeneratorResult;
import tools.refinery.generator.standalone.StandaloneRefinery;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class ToolsRefinerySpikeTest {
    @Test
    void loadsProblemArtifactAndReadsRelationFacts() throws IOException {
        String problemText = loadProblemResource(RefineryArtifactCatalog.directorSpikeProblemResourcePath());
        var problem = StandaloneRefinery.getProblemLoader().loadString(problemText);

        try (var generator = StandaloneRefinery.getGeneratorFactory().createGenerator(problem)) {
            var generationResult = generator.tryGenerate();
            assertEquals(GeneratorResult.SUCCESS, generationResult, "TR1-A spike generation must succeed");

            var trace = generator.getProblemTrace();
            var childrenRelation = trace.getPartialRelation("Directory::children");
            assertNotNull(childrenRelation, "Expected Directory::children relation in problem trace");

            var childrenInterpretation = generator.getPartialInterpretation(childrenRelation);
            var cursor = childrenInterpretation.getAll();

            int tupleCount = 0;
            int trueTupleCount = 0;
            while (cursor.move()) {
                tupleCount++;
                if ("TRUE".equalsIgnoreCase(String.valueOf(cursor.getValue()))) {
                    trueTupleCount++;
                }
            }

            assertTrue(tupleCount > 0, "Expected at least one relation tuple to be readable");
            assertTrue(trueTupleCount > 0, "Expected at least one TRUE Directory::children fact");
        }
    }

    private static String loadProblemResource(String resourcePath) throws IOException {
        try (InputStream stream = ToolsRefinerySpikeTest.class.getResourceAsStream(resourcePath)) {
            if (stream == null) {
                throw new IOException("Missing problem resource: " + resourcePath);
            }
            return new String(stream.readAllBytes(), StandardCharsets.UTF_8);
        }
    }
}
