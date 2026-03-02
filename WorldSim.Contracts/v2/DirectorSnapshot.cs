using System.Text.Json.Serialization;

namespace WorldSim.Contracts.V2;

public sealed record DirectorSnapshotData
{
    [JsonPropertyName("currentTick")]
    public required int CurrentTick { get; init; }

    [JsonPropertyName("currentSeason")]
    public required string CurrentSeason { get; init; }

    [JsonPropertyName("colonyPopulation")]
    public required int ColonyPopulation { get; init; }

    [JsonPropertyName("foodReservesPct")]
    public required double FoodReservesPct { get; init; }

    [JsonPropertyName("moraleAvg")]
    public required double MoraleAvg { get; init; }

    [JsonPropertyName("economyOutput")]
    public required double EconomyOutput { get; init; }

    [JsonPropertyName("beatCooldownRemainingTicks")]
    public required int BeatCooldownRemainingTicks { get; init; }

    [JsonPropertyName("remainingInfluenceBudget")]
    public double? RemainingInfluenceBudget { get; init; }
}
