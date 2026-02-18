using System.Text.Json.Serialization;

namespace WorldSim.Contracts.V1;

public sealed record PatchResponse(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("seed")] long Seed,
    [property: JsonPropertyName("patch")] IReadOnlyList<PatchOp> Patch,
    [property: JsonPropertyName("explain")] IReadOnlyList<string> Explain,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings
);
