package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.util.ArrayList;
import java.util.List;

import hu.zoltanterek.worldsim.refinery.planner.director.DirectorOutputAssertions;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeAssertions;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeAssertionsMapper;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;
import tools.refinery.generator.GeneratorResult;
import tools.refinery.generator.standalone.StandaloneRefinery;

public final class DirectorRefinerySolver {
    private final DirectorRuntimeAssertionsMapper runtimeMapper = new DirectorRuntimeAssertionsMapper();
    private final DirectorOutputAssertionsProblemMapper outputMapper = new DirectorOutputAssertionsProblemMapper();
    private final DirectorProblemAssembler assembler = new DirectorProblemAssembler();

    public DirectorRefinerySolveResult solve(DirectorRuntimeFacts facts, DirectorOutputAssertions assertions) {
        DirectorRuntimeAssertions runtimeAssertions = runtimeMapper.map(facts);
        DirectorOutputAssertionsProblemMapper.OutputAreaMapping outputMapping = outputMapper.map(assertions);
        String problemText = assembler.assemble(List.of(
                new DirectorProblemFragment(runtimeAssertions.lines()),
                outputMapping.fragment()
        ));

        return solveProblemText(problemText, outputMapping.unsupportedFeatures());
    }

    DirectorRefinerySolveResult solveProblemText(String problemText, List<String> unsupportedFeatures) {
        try {
            var problem = StandaloneRefinery.getProblemLoader().loadString(problemText);
            try (var generator = StandaloneRefinery.getGeneratorFactory().createGenerator(problem)) {
                GeneratorResult result = generator.tryGenerate();
                if (result == GeneratorResult.SUCCESS) {
                    return new DirectorRefinerySolveResult(
                            DirectorRefinerySolveStatus.SUCCESS,
                            result.name(),
                            List.of("solverResult:success"),
                            unsupportedFeatures
                    );
                }

                return new DirectorRefinerySolveResult(
                        DirectorRefinerySolveStatus.NON_SUCCESS,
                        result.name(),
                        List.of("solverResult:non_success:" + result.name()),
                        unsupportedFeatures
                );
            }
        } catch (Exception ex) {
            List<String> diagnostics = new ArrayList<>();
            diagnostics.add("solverResult:load_failure");
            diagnostics.add(ex.getClass().getSimpleName() + ": " + ex.getMessage());
            return new DirectorRefinerySolveResult(
                    DirectorRefinerySolveStatus.LOAD_FAILURE,
                    "loadFailure",
                    diagnostics,
                    unsupportedFeatures
            );
        }
    }
}
