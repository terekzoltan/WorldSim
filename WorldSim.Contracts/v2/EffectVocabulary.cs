using System.Text.Json.Serialization;

namespace WorldSim.Contracts.V2;

public sealed record EffectEntry
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("domain")]
    public required string Domain { get; init; }

    [JsonPropertyName("modifier")]
    public required double Modifier { get; init; }

    [JsonPropertyName("durationTicks")]
    public required int DurationTicks { get; init; }
}

public sealed record GoalBiasEntry
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("goalCategory")]
    public required string GoalCategory { get; init; }

    [JsonPropertyName("weight")]
    public required double Weight { get; init; }

    [JsonPropertyName("durationTicks")]
    public int? DurationTicks { get; init; }
}
