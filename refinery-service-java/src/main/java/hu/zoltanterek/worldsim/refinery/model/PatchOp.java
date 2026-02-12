package hu.zoltanterek.worldsim.refinery.model;

import java.util.List;

import com.fasterxml.jackson.annotation.JsonSubTypes;
import com.fasterxml.jackson.annotation.JsonTypeInfo;
import com.fasterxml.jackson.databind.JsonNode;

@JsonTypeInfo(use = JsonTypeInfo.Id.NAME, include = JsonTypeInfo.As.PROPERTY, property = "op")
@JsonSubTypes({
        @JsonSubTypes.Type(value = PatchOp.AddTech.class, name = "addTech"),
        @JsonSubTypes.Type(value = PatchOp.TweakTech.class, name = "tweakTech"),
        @JsonSubTypes.Type(value = PatchOp.AddWorldEvent.class, name = "addWorldEvent")
})
public sealed interface PatchOp permits PatchOp.AddTech, PatchOp.TweakTech, PatchOp.AddWorldEvent {

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
}
