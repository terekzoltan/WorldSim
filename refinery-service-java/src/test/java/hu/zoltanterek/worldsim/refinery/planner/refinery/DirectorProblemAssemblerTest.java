package hu.zoltanterek.worldsim.refinery.planner.refinery;

import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.io.IOException;
import java.util.List;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeAssertions;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeAssertionsMapper;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;
import tools.refinery.generator.standalone.StandaloneRefinery;

class DirectorProblemAssemblerTest {
    private final DirectorProblemAssembler assembler = new DirectorProblemAssembler();
    private final DirectorRuntimeAssertionsMapper mapper = new DirectorRuntimeAssertionsMapper();

    @Test
    void assemble_ContainsCanonicalLayerAnchorsAndRuntimeAssertions() {
        DirectorRuntimeAssertions assertions = mapper.map(sampleFacts());

        String assembled = assembler.assemble(assertions);

        assertTrue(assembled.contains("DirectorDesignLayerAnchor"));
        assertTrue(assembled.contains("DirectorModelLayerAnchor"));
        assertTrue(assembled.contains("DirectorRuntimeLayerAnchor"));
        assertTrue(assembled.contains("DirectorOutputLayerAnchor"));
        assertTrue(assembled.contains("RuntimeCheckpointContext(runtimeCheckpoint)."));
        assertTrue(assembled.contains("tick(runtimeCheckpoint): 987."));
        assertTrue(assembled.contains("beatId(activeBeat_000): \"BEAT_MAJOR_1\"."));
    }

    @Test
    void assemble_LoadsAsRefineryProblem() throws IOException {
        DirectorRuntimeAssertions assertions = mapper.map(sampleFacts());

        var problem = StandaloneRefinery.getProblemLoader().loadString(assembler.assemble(assertions));

        assertNotNull(problem, "Assembled director problem with runtime assertions should parse/load successfully");
    }

    @Test
    void assemble_UsesCanonicalCatalogOrder() {
        String assembled = assembler.assemble(new DirectorRuntimeAssertions(List.of("RuntimeCheckpointContext(runtimeCheckpoint).")));

        assertTrue(assembled.indexOf("DirectorDesignLayerAnchor") < assembled.indexOf("DirectorModelLayerAnchor"));
        assertTrue(assembled.indexOf("DirectorModelLayerAnchor") < assembled.indexOf("DirectorRuntimeLayerAnchor"));
        assertTrue(assembled.indexOf("DirectorRuntimeLayerAnchor") < assembled.indexOf("DirectorOutputLayerAnchor"));
        assertTrue(assembled.indexOf("DirectorOutputLayerAnchor") < assembled.indexOf("RuntimeCheckpointContext(runtimeCheckpoint)."));
    }

    private static DirectorRuntimeFacts sampleFacts() {
        return new DirectorRuntimeFacts(
                987L,
                4,
                6L,
                2.75d,
                List.of(new DirectorRuntimeFacts.ActiveBeatFact("BEAT_MAJOR_1", "major", 20L)),
                List.of(new DirectorRuntimeFacts.ActiveDirectiveFact(0, "PrioritizeFood"))
        );
    }
}
