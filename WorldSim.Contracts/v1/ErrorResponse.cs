using System.Text.Json.Serialization;

namespace WorldSim.Contracts.V1;

public sealed record ErrorResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details")] IReadOnlyList<string> Details
);
