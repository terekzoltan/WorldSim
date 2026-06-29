namespace WorldSim.Runtime.Diagnostics;

public sealed record ScenarioEcologyTimelineSnapshot(
    int Herbivores,
    int Predators,
    int ActiveFoodNodes,
    int DepletedFoodNodes,
    int HerbivoreReplenishmentSpawns,
    int PredatorReplenishmentSpawns,
    int EmergencyRescues,
    string EmergencyRescuePolicy,
    string LastEmergencyRescueReason,
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
        EmergencyRescues: 0,
        EmergencyRescuePolicy: "disabled",
        LastEmergencyRescueReason: "none",
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
    int EmergencyRescues,
    string EmergencyRescuePolicy,
    string LastEmergencyRescueReason,
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
        EmergencyRescues: 0,
        EmergencyRescuePolicy: "disabled",
        LastEmergencyRescueReason: "none",
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
            EmergencyRescues,
            EmergencyRescuePolicy,
            LastEmergencyRescueReason,
            TicksWithZeroHerbivores,
            TicksWithZeroPredators);
}

public sealed record ScenarioEcologyBalanceSnapshot(
    float AnimalReplenishmentChancePerSecond,
    float PredatorReplenishmentChance,
    float FoodRegrowthMinSeconds,
    float FoodRegrowthJitterSeconds,
    string EmergencyRescuePolicy)
{
    public static ScenarioEcologyBalanceSnapshot Empty { get; } = new(
        AnimalReplenishmentChancePerSecond: 0f,
        PredatorReplenishmentChance: 0f,
        FoodRegrowthMinSeconds: 0f,
        FoodRegrowthJitterSeconds: 0f,
        EmergencyRescuePolicy: "disabled");
}
