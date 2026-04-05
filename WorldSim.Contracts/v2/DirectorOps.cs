using System.Text.Json.Serialization;
using WorldSim.Contracts.V1;

namespace WorldSim.Contracts.V2;

public sealed record AddStoryBeatOp : PatchOp
{
    [JsonPropertyName("severity")]
    public string? Severity { get; init; }

    [JsonPropertyName("beatId")]
    public required string BeatId { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("durationTicks")]
    public required long DurationTicks { get; init; }

    [JsonPropertyName("effects")]
    public IReadOnlyList<EffectEntry>? Effects { get; init; }

    [JsonPropertyName("causalChain")]
    public CausalChainEntry? CausalChain { get; init; }
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

public sealed record CausalChainEntry
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("condition")]
    public required CausalCondition Condition { get; init; }

    [JsonPropertyName("followUpBeat")]
    public required CausalFollowUpBeat FollowUpBeat { get; init; }

    [JsonPropertyName("windowTicks")]
    public required int WindowTicks { get; init; }

    [JsonPropertyName("maxTriggers")]
    public required int MaxTriggers { get; init; }
}

public sealed record CausalCondition
{
    [JsonPropertyName("metric")]
    public required string Metric { get; init; }

    [JsonPropertyName("operator")]
    public required string Operator { get; init; }

    [JsonPropertyName("threshold")]
    public required double Threshold { get; init; }
}

public sealed record CausalFollowUpBeat
{
    [JsonPropertyName("beatId")]
    public required string BeatId { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("durationTicks")]
    public required long DurationTicks { get; init; }

    [JsonPropertyName("severity")]
    public string? Severity { get; init; }

    [JsonPropertyName("effects")]
    public IReadOnlyList<EffectEntry>? Effects { get; init; }
}
