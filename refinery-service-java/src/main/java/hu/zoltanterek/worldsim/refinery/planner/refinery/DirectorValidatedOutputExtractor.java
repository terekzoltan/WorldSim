package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.util.ArrayList;
import java.util.List;

import tools.refinery.generator.ModelFacade;
import tools.refinery.language.semantics.ProblemTrace;
import tools.refinery.logic.AbstractValue;
import tools.refinery.logic.term.truthvalue.TruthValue;
import tools.refinery.store.reasoning.representation.PartialFunction;
import tools.refinery.store.tuple.Tuple;

public final class DirectorValidatedOutputExtractor {
    public ExtractionResult extract(ModelFacade modelFacade) {
        ProblemTrace trace = modelFacade.getProblemTrace();
        int outputAreaId = trace.getNodeId(DirectorOutputAssertionsProblemMapper.OUTPUT_AREA_ID);
        List<String> diagnostics = new ArrayList<>();

        List<Integer> storyIds = collectTrueTargets(modelFacade, trace, "DesignatedOutputArea::storyBeatSlot", outputAreaId);
        if (storyIds.size() > 1) {
            diagnostics.add("extractFailure:multiple_true_story_slots");
            return new ExtractionResult(false, null, diagnostics);
        }

        List<Integer> directiveIds = collectTrueTargets(modelFacade, trace, "DesignatedOutputArea::directiveSlot", outputAreaId);
        if (directiveIds.size() > 1) {
            diagnostics.add("extractFailure:multiple_true_directive_slots");
            return new ExtractionResult(false, null, diagnostics);
        }

        DirectorValidatedCoreOutput.StoryBeatCore story = null;
        if (!storyIds.isEmpty()) {
            story = extractStory(modelFacade, trace, storyIds.get(0), diagnostics);
            if (story == null) {
                return new ExtractionResult(false, null, diagnostics);
            }
            diagnostics.add("validatedCoverage:story_core");
        }

        DirectorValidatedCoreOutput.DirectiveCore directive = null;
        if (!directiveIds.isEmpty()) {
            directive = extractDirective(modelFacade, trace, directiveIds.get(0), diagnostics);
            if (directive == null) {
                return new ExtractionResult(false, null, diagnostics);
            }
            diagnostics.add("validatedCoverage:directive_core");
        }

        DirectorValidatedCoreOutput validatedOutput = new DirectorValidatedCoreOutput(story, directive);
        if (validatedOutput.isEmpty()) {
            diagnostics.add("validatedOutput:empty");
        }
        return new ExtractionResult(true, validatedOutput, diagnostics);
    }

    private static DirectorValidatedCoreOutput.StoryBeatCore extractStory(
            ModelFacade modelFacade,
            ProblemTrace trace,
            int storyId,
            List<String> diagnostics
    ) {
        String beatId = readConcreteFunction(modelFacade, trace, "StoryBeatOutput::storyBeatId", storyId);
        String text = readConcreteFunction(modelFacade, trace, "StoryBeatOutput::text", storyId);
        Integer durationTicks = readConcreteFunction(modelFacade, trace, "StoryBeatOutput::storyDurationTicks", storyId);
        if (beatId == null || text == null || durationTicks == null) {
            diagnostics.add("extractFailure:story_core_fields_missing:beatId=" + beatId + ",text=" + text + ",duration=" + durationTicks);
            return null;
        }

        List<Integer> severityIds = collectTrueTargets(modelFacade, trace, "StoryBeatOutput::severity", storyId);
        if (severityIds.size() != 1) {
            diagnostics.add("extractFailure:story_severity_not_single");
            return null;
        }

        int severityId = severityIds.get(0);
        String severity;
        if (severityId == trace.getNodeId("severity_minor")) {
            severity = "minor";
        } else if (severityId == trace.getNodeId("severity_major")) {
            severity = "major";
        } else if (severityId == trace.getNodeId("severity_epic")) {
            severity = "epic";
        } else {
            severity = null;
        }
        if (severity == null) {
            diagnostics.add("extractFailure:story_severity_unknown");
            return null;
        }

        return new DirectorValidatedCoreOutput.StoryBeatCore(beatId, text, durationTicks.longValue(), severity);
    }

    private static DirectorValidatedCoreOutput.DirectiveCore extractDirective(
            ModelFacade modelFacade,
            ProblemTrace trace,
            int directiveId,
            List<String> diagnostics
    ) {
        Integer colonyId = readConcreteFunction(modelFacade, trace, "ColonyDirectiveOutput::directiveColonyId", directiveId);
        String directiveKey = readConcreteFunction(modelFacade, trace, "ColonyDirectiveOutput::directiveName", directiveId);
        Integer durationTicks = readConcreteFunction(modelFacade, trace, "ColonyDirectiveOutput::directiveDurationTicks", directiveId);
        if (colonyId == null || directiveKey == null || durationTicks == null) {
            diagnostics.add("extractFailure:directive_core_fields_missing:colonyId=" + colonyId + ",directiveKey=" + directiveKey + ",duration=" + durationTicks);
            return null;
        }

        List<Integer> directiveTargets = collectTrueTargets(modelFacade, trace, "ColonyDirectiveOutput::directive", directiveId);
        if (directiveTargets.size() != 1) {
            diagnostics.add("extractFailure:directive_kind_not_single");
            return null;
        }

        int expectedDirectiveId = trace.getNodeId("directive_" + DirectorOutputAssertionsProblemMapper.safeIdentifierPart(directiveKey));
        if (directiveTargets.get(0) != expectedDirectiveId) {
            diagnostics.add("extractFailure:directive_kind_mismatch");
            return null;
        }

        return new DirectorValidatedCoreOutput.DirectiveCore(colonyId, directiveKey, durationTicks.longValue());
    }

    private static List<Integer> collectTrueTargets(
            ModelFacade modelFacade,
            ProblemTrace trace,
            String relationName,
            int sourceId
    ) {
        var relation = trace.getPartialRelation(relationName);
        var interpretation = modelFacade.getPartialInterpretation(relation);
        var cursor = interpretation.getAll();
        List<Integer> targetIds = new ArrayList<>();
        while (cursor.move()) {
            if (cursor.getValue() != TruthValue.TRUE) {
                continue;
            }
            Tuple key = cursor.getKey();
            if (key.getSize() == 2 && key.get(0) == sourceId) {
                targetIds.add(key.get(1));
            }
        }
        return targetIds;
    }

    @SuppressWarnings("unchecked")
    private static <A extends AbstractValue<A, C>, C> C readConcreteFunction(
            ModelFacade modelFacade,
            ProblemTrace trace,
            String functionName,
            int objectId
    ) {
        var function = (PartialFunction<A, C>) trace.getPartialFunction(functionName);
        var interpretation = modelFacade.getPartialInterpretation(function);
        A value = interpretation.get(Tuple.of(objectId));
        C concreteValue = value == null ? null : value.getConcrete();
        if (concreteValue != null) {
            return concreteValue;
        }

        var cursor = interpretation.getAll();
        while (cursor.move()) {
            Tuple key = cursor.getKey();
            if (key.getSize() != 1 || key.get(0) != objectId) {
                continue;
            }

            A cursorValue = cursor.getValue();
            if (cursorValue != null && cursorValue.getConcrete() != null) {
                return cursorValue.getConcrete();
            }
        }

        return null;
    }

    public record ExtractionResult(boolean success, DirectorValidatedCoreOutput validatedOutput, List<String> diagnostics) {
        public ExtractionResult {
            diagnostics = List.copyOf(diagnostics == null ? List.of() : diagnostics);
        }
    }
}
