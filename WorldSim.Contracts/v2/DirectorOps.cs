using System.Text.Json.Serialization;
using WorldSim.Contracts.V1;

namespace WorldSim.Contracts.V2;

public sealed record AddStoryBeatOp : PatchOp
{
    [JsonPropertyName("beatId")]
    public required string BeatId { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("durationTicks")]
    public required long DurationTicks { get; init; }

    [JsonPropertyName("effects")]
    public IReadOnlyList<EffectEntry>? Effects { get; init; }
}

public sealed record SetColonyDirectiveOp : PatchOp
{
    [JsonPropertyName("colonyId")]
    public required int ColonyId { get; init; }

    [JsonPropertyName("directive")]
    public required string Directive { get; init; }

    [JsonPropertyName("durationTicks")]
    public required long DurationTicks { get; init; }

    [JsonPropertyName("biases")]
    public IReadOnlyList<GoalBiasEntry>? Biases { get; init; }
}
