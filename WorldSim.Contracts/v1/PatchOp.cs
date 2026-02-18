using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WorldSim.Contracts.V1;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(AddTechOp), "addTech")]
[JsonDerivedType(typeof(TweakTechOp), "tweakTech")]
[JsonDerivedType(typeof(AddWorldEventOp), "addWorldEvent")]
public abstract record PatchOp
{
    [JsonPropertyName("opId")]
    public required string OpId { get; init; }
}

public sealed record AddTechOp : PatchOp
{
    [JsonPropertyName("techId")]
    public required string TechId { get; init; }

    [JsonPropertyName("prereqTechIds")]
    public required IReadOnlyList<string> PrereqTechIds { get; init; }

    [JsonPropertyName("cost")]
    public required JsonObject Cost { get; init; }

    [JsonPropertyName("effects")]
    public required JsonObject Effects { get; init; }
}

public sealed record TweakTechOp : PatchOp
{
    [JsonPropertyName("techId")]
    public required string TechId { get; init; }

    [JsonPropertyName("fieldPath")]
    public required string FieldPath { get; init; }

    [JsonPropertyName("deltaNumber")]
    public required double DeltaNumber { get; init; }
}

public sealed record AddWorldEventOp : PatchOp
{
    [JsonPropertyName("eventId")]
    public required string EventId { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("params")]
    public required JsonObject Params { get; init; }

    [JsonPropertyName("durationTicks")]
    public required long DurationTicks { get; init; }
}
