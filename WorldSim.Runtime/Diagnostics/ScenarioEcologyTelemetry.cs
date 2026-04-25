namespace WorldSim.Runtime.Diagnostics;

public sealed record ScenarioEcologyTimelineSnapshot(
    int Herbivores,
    int Predators,
    int ActiveFoodNodes,
    int DepletedFoodNodes,
    int HerbivoreReplenishmentSpawns,
    int PredatorReplenishmentSpawns,
    int TicksWithZeroHerbivores,
    int TicksWithZeroPredators)
{
    public static ScenarioEcologyTimelineSnapshot Empty { get; } = new(
        Herbivores: 0,
        Predators: 0,
        ActiveFoodNodes: 0,
        DepletedFoodNodes: 0,
        HerbivoreReplenishmentSpawns: 0,
        PredatorReplenishmentSpawns: 0,
        TicksWithZeroHerbivores: 0,
        TicksWithZeroPredators: 0);
}

public sealed record ScenarioEcologyTelemetrySnapshot(
    int Herbivores,
    int Predators,
    int ActiveFoodNodes,
    int DepletedFoodNodes,
    int HerbivoreReplenishmentSpawns,
    int PredatorReplenishmentSpawns,
    int TicksWithZeroHerbivores,
    int TicksWithZeroPredators,
    int? FirstZeroHerbivoreTick,
    int? FirstZeroPredatorTick,
    int PredatorDeaths,
    int PredatorHumanHits)
{
    public static ScenarioEcologyTelemetrySnapshot Empty { get; } = new(
        Herbivores: 0,
        Predators: 0,
        ActiveFoodNodes: 0,
        DepletedFoodNodes: 0,
        HerbivoreReplenishmentSpawns: 0,
        PredatorReplenishmentSpawns: 0,
        TicksWithZeroHerbivores: 0,
        TicksWithZeroPredators: 0,
        FirstZeroHerbivoreTick: null,
        FirstZeroPredatorTick: null,
        PredatorDeaths: 0,
        PredatorHumanHits: 0);

    public ScenarioEcologyTimelineSnapshot ToTimelineSnapshot()
        => new(
            Herbivores,
            Predators,
            ActiveFoodNodes,
            DepletedFoodNodes,
            HerbivoreReplenishmentSpawns,
            PredatorReplenishmentSpawns,
            TicksWithZeroHerbivores,
            TicksWithZeroPredators);
}

public sealed record ScenarioEcologyBalanceSnapshot(
    float AnimalReplenishmentChancePerSecond,
    float PredatorReplenishmentChance,
    float FoodRegrowthMinSeconds,
    float FoodRegrowthJitterSeconds)
{
    public static ScenarioEcologyBalanceSnapshot Empty { get; } = new(
        AnimalReplenishmentChancePerSecond: 0f,
        PredatorReplenishmentChance: 0f,
        FoodRegrowthMinSeconds: 0f,
        FoodRegrowthJitterSeconds: 0f);
}
