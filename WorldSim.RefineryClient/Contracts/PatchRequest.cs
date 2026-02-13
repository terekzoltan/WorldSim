using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WorldSimRefineryClient.Contracts;

public sealed record PatchRequest(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("seed")] long Seed,
    [property: JsonPropertyName("tick")] long Tick,
    [property: JsonPropertyName("goal")] string Goal,
    [property: JsonPropertyName("snapshot")] JsonObject Snapshot,
    [property: JsonPropertyName("constraints")] JsonObject? Constraints
);
