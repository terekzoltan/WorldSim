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
        return solve(facts, assertions, List.of());
    }

    public DirectorRefinerySolveResult solve(
            DirectorRuntimeFacts facts,
            DirectorOutputAssertions assertions,
            List<String> additionalUnsupportedFeatures
    ) {
        DirectorRuntimeAssertions runtimeAssertions = runtimeMapper.map(facts);
        DirectorOutputAssertionsProblemMapper.OutputAreaMapping outputMapping = outputMapper.map(assertions);
        List<String> unsupportedFeatures = new ArrayList<>(outputMapping.unsupportedFeatures());
        unsupportedFeatures.addAll(additionalUnsupportedFeatures == null ? List.of() : additionalUnsupportedFeatures);
        String problemText = assembler.assemble(List.of(
                new DirectorProblemFragment(runtimeAssertions.lines()),
                outputMapping.fragment()
        ));

        return solveProblemText(problemText, outputMapping.diagnostics(), unsupportedFeatures);
    }

    DirectorRefinerySolveResult solveProblemText(String problemText, List<String> unsupportedFeatures) {
        return solveProblemText(problemText, List.of(), unsupportedFeatures);
    }

    DirectorRefinerySolveResult solveProblemText(
            String problemText,
            List<String> preExtractionDiagnostics,
            List<String> unsupportedFeatures
    ) {
        var diagnostics = new ArrayList<>(preExtractionDiagnostics == null ? List.<String>of() : preExtractionDiagnostics);
        try {
            var problem = StandaloneRefinery.getProblemLoader().loadString(problemText);
            try (var generator = StandaloneRefinery.getGeneratorFactory().createGenerator(problem)) {
                GeneratorResult result = generator.tryGenerate();
                if (result == GeneratorResult.SUCCESS) {
                    diagnostics.add("solverResult:success");
                    DirectorValidatedOutputExtractor.ExtractionResult extractionResult = new DirectorValidatedOutputExtractor().extract(generator);
                    diagnostics.addAll(extractionResult.diagnostics());
                    if (!extractionResult.success()) {
                        return new DirectorRefinerySolveResult(
                                DirectorRefinerySolveStatus.NON_SUCCESS,
                                result.name(),
                                null,
                                diagnostics,
                                unsupportedFeatures
                        );
                    }

                    return new DirectorRefinerySolveResult(
                            DirectorRefinerySolveStatus.SUCCESS,
                            result.name(),
                            extractionResult.validatedOutput(),
                            diagnostics,
                            unsupportedFeatures
                    );
                }

                diagnostics.add("solverResult:non_success:" + result.name());
                return new DirectorRefinerySolveResult(
                        DirectorRefinerySolveStatus.NON_SUCCESS,
                        result.name(),
                        null,
                        diagnostics,
                        unsupportedFeatures
                );
            }
        } catch (Exception ex) {
            diagnostics.add("solverResult:load_failure");
            diagnostics.add(ex.getClass().getSimpleName() + ": " + ex.getMessage());
            return new DirectorRefinerySolveResult(
                    DirectorRefinerySolveStatus.LOAD_FAILURE,
                    "loadFailure",
                    null,
                    diagnostics,
                    unsupportedFeatures
            );
        }
    }
}
