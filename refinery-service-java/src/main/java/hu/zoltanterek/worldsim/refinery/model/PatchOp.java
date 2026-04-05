package hu.zoltanterek.worldsim.refinery.model;

import java.util.List;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonSubTypes;
import com.fasterxml.jackson.annotation.JsonTypeInfo;
import com.fasterxml.jackson.databind.JsonNode;

@JsonTypeInfo(use = JsonTypeInfo.Id.NAME, include = JsonTypeInfo.As.PROPERTY, property = "op")
@JsonSubTypes({
        @JsonSubTypes.Type(value = PatchOp.AddTech.class, name = "addTech"),
        @JsonSubTypes.Type(value = PatchOp.TweakTech.class, name = "tweakTech"),
        @JsonSubTypes.Type(value = PatchOp.AddWorldEvent.class, name = "addWorldEvent"),
        @JsonSubTypes.Type(value = PatchOp.AddStoryBeat.class, name = "addStoryBeat"),
        @JsonSubTypes.Type(value = PatchOp.SetColonyDirective.class, name = "setColonyDirective")
})
public sealed interface PatchOp permits PatchOp.AddTech, PatchOp.TweakTech, PatchOp.AddWorldEvent,
        PatchOp.AddStoryBeat, PatchOp.SetColonyDirective {

    record AddTech(
            String opId,
            String techId,
            List<String> prereqTechIds,
            JsonNode cost,
            JsonNode effects
    ) implements PatchOp {
    }

    record TweakTech(
            String opId,
            String techId,
            String fieldPath,
            double deltaNumber
    ) implements PatchOp {
    }

    record AddWorldEvent(
            String opId,
            String eventId,
            String type,
            JsonNode params,
            long durationTicks
    ) implements PatchOp {
    }

    @JsonInclude(JsonInclude.Include.NON_EMPTY)
    record AddStoryBeat(
            String opId,
            String beatId,
            String text,
            long durationTicks,
            String severity,
            List<EffectEntry> effects,
            CausalChainEntry causalChain
    ) implements PatchOp {
        public AddStoryBeat(String opId, String beatId, String text, long durationTicks) {
            this(opId, beatId, text, durationTicks, null, List.of(), null);
        }

        public AddStoryBeat(
                String opId,
                String beatId,
                String text,
                long durationTicks,
                String severity,
                List<EffectEntry> effects
        ) {
            this(opId, beatId, text, durationTicks, severity, effects, null);
        }
    }

    @JsonInclude(JsonInclude.Include.NON_EMPTY)
    record SetColonyDirective(
            String opId,
            int colonyId,
            String directive,
            long durationTicks,
            List<GoalBiasEntry> biases
    ) implements PatchOp {
        public SetColonyDirective(String opId, int colonyId, String directive, long durationTicks) {
            this(opId, colonyId, directive, durationTicks, List.of());
        }
    }

    record EffectEntry(
            String type,
            String domain,
            double modifier,
            long durationTicks
    ) {
    }

    record GoalBiasEntry(
            String type,
            String goalCategory,
            double weight,
            Long durationTicks
    ) {
    }

    record CausalChainEntry(
            String type,
            CausalCondition condition,
            CausalFollowUpBeat followUpBeat,
            long windowTicks,
            int maxTriggers
    ) {
    }

    record CausalCondition(
            String metric,
            String operator,
            double threshold
    ) {
    }

    record CausalFollowUpBeat(
            String beatId,
            String text,
            long durationTicks,
            String severity,
            List<EffectEntry> effects
    ) {
    }
}
