using WorldSim.Simulation.Ecology;

namespace WorldSim.Runtime.Diagnostics;

public sealed record ScenarioEcologyDistanceSummarySnapshot(
    int SampleCount,
    int? Minimum,
    int? Maximum,
    double? Average)
{
    public static ScenarioEcologyDistanceSummarySnapshot Empty { get; } = new(
        SampleCount: 0,
        Minimum: null,
        Maximum: null,
        Average: null);
}

public sealed record ScenarioInitialEcologyRegionSnapshot(
    int RegionId,
    int LandTileCount,
    float PlantCapacityTotal,
    int ActiveFoodNodes,
    int Herbivores,
    int Predators);

public sealed record ScenarioInitialEcologyFallbackSnapshot(
    string Reason,
    int Count);

public sealed record ScenarioInitialEcologyTelemetrySnapshot(
    string InitialAnimalPolicy,
    string InitialAnimalPolicySource,
    int TotalAnimals,
    int Herbivores,
    int Predators,
    double? PredatorHerbivoreRatio,
    int AnimalsOnWater,
    int AnimalsOnMovementBlockedTiles,
    int ViableRegions,
    int ViableRegionsWithoutHerbivores,
    int PredatorsInPreyEmptyRegions,
    int HerbivoresWithFoodInVision,
    int PredatorsWithPreyInVision,
    int PredatorsWithinHumanHarassRadius,
    int PredatorsWithinEarlyHumanContactRadius,
    int FoodVisionRadius,
    int PreyVisionRadius,
    int HumanHarassRadius,
    int EarlyHumanContactRadius,
    ScenarioEcologyDistanceSummarySnapshot HerbivoreToNearestFoodDistance,
    ScenarioEcologyDistanceSummarySnapshot PredatorToNearestPreyDistance,
    ScenarioEcologyDistanceSummarySnapshot PredatorToNearestPersonDistance,
    IReadOnlyList<ScenarioInitialEcologyRegionSnapshot> Regions)
{
    private IReadOnlyList<ScenarioInitialEcologyFallbackSnapshot> _initialSeedingFallbacks =
        Array.Empty<ScenarioInitialEcologyFallbackSnapshot>();

    public int InitialAnimalBudgetCeiling { get; init; }
    public int? InitialHerbivoreBudget { get; init; }
    public int? InitialPredatorBudget { get; init; }
    public int InitialHerbivoresSpawned { get; init; }
    public int InitialPredatorsSpawned { get; init; }
    public int AnimalCeilingUnallocated { get; init; }
    public int HerbivoreBudgetUnfilled { get; init; }
    public int PredatorBudgetUnfilled { get; init; }
    public int InitialSeedingFallbackCount { get; init; }
    public IReadOnlyList<ScenarioInitialEcologyFallbackSnapshot> InitialSeedingFallbacks
    {
        get => _initialSeedingFallbacks;
        init => _initialSeedingFallbacks = value ?? Array.Empty<ScenarioInitialEcologyFallbackSnapshot>();
    }
    public int? PreferredPersonOrColonyDistance { get; init; }
    public int? PreferredHerbivoreFoodRadius { get; init; }
    public int? PreferredPredatorPreyRadius { get; init; }
    public int? PredatorsWithinPreferredPersonOrColonyDistance { get; init; }

    public static ScenarioInitialEcologyTelemetrySnapshot Empty { get; } = new(
        InitialAnimalPolicy: InitialAnimalSeedingWireValues.HabitatAware,
        InitialAnimalPolicySource: InitialAnimalSeedingWireValues.RuntimeDefault,
        TotalAnimals: 0,
        Herbivores: 0,
        Predators: 0,
        PredatorHerbivoreRatio: null,
        AnimalsOnWater: 0,
        AnimalsOnMovementBlockedTiles: 0,
        ViableRegions: 0,
        ViableRegionsWithoutHerbivores: 0,
        PredatorsInPreyEmptyRegions: 0,
        HerbivoresWithFoodInVision: 0,
        PredatorsWithPreyInVision: 0,
        PredatorsWithinHumanHarassRadius: 0,
        PredatorsWithinEarlyHumanContactRadius: 0,
        FoodVisionRadius: 5,
        PreyVisionRadius: 6,
        HumanHarassRadius: 2,
        EarlyHumanContactRadius: 6,
        HerbivoreToNearestFoodDistance: ScenarioEcologyDistanceSummarySnapshot.Empty,
        PredatorToNearestPreyDistance: ScenarioEcologyDistanceSummarySnapshot.Empty,
        PredatorToNearestPersonDistance: ScenarioEcologyDistanceSummarySnapshot.Empty,
        Regions: Array.Empty<ScenarioInitialEcologyRegionSnapshot>());
}

public sealed record ScenarioEcologyTimelineSnapshot(
    int Herbivores,
    int Predators,
    int ActiveFoodNodes,
    int DepletedFoodNodes,
    int HerbivoreReplenishmentSpawns,
    int PredatorReplenishmentSpawns,
    int EmergencyRescues,
    int PlantFoodProduced,
    int MeatFoodProduced,
    int PlantFoodConsumedByAnimals,
    int MeatFromHunt,
    int SupplyBridgeSkippedByNoBiomass,
    string EmergencyRescuePolicy,
    string LastEmergencyRescueReason,
    int TicksWithZeroHerbivores,
    int TicksWithZeroPredators,
    int? FirstPredatorHumanContactTick,
    int? FirstPredatorHuntTick,
    int? FirstHerbivoreGrazingTick,
    int? FirstPredatorDeathTick,
    int? FirstHerbivoreDeathTick,
    int? FirstPredatorBirthTick,
    int? FirstHerbivoreBirthTick)
{
    public static ScenarioEcologyTimelineSnapshot Empty { get; } = new(
        Herbivores: 0,
        Predators: 0,
        ActiveFoodNodes: 0,
        DepletedFoodNodes: 0,
        HerbivoreReplenishmentSpawns: 0,
        PredatorReplenishmentSpawns: 0,
        EmergencyRescues: 0,
        PlantFoodProduced: 0,
        MeatFoodProduced: 0,
        PlantFoodConsumedByAnimals: 0,
        MeatFromHunt: 0,
        SupplyBridgeSkippedByNoBiomass: 0,
        EmergencyRescuePolicy: "disabled",
        LastEmergencyRescueReason: "none",
        TicksWithZeroHerbivores: 0,
        TicksWithZeroPredators: 0,
        FirstPredatorHumanContactTick: null,
        FirstPredatorHuntTick: null,
        FirstHerbivoreGrazingTick: null,
        FirstPredatorDeathTick: null,
        FirstHerbivoreDeathTick: null,
        FirstPredatorBirthTick: null,
        FirstHerbivoreBirthTick: null);
}

public sealed record ScenarioEcologyTelemetrySnapshot(
    int Herbivores,
    int Predators,
    int ActiveFoodNodes,
    int DepletedFoodNodes,
    int HerbivoreReplenishmentSpawns,
    int PredatorReplenishmentSpawns,
    int EmergencyRescues,
    int PlantFoodProduced,
    int MeatFoodProduced,
    int PlantFoodConsumedByAnimals,
    int MeatFromHunt,
    int SupplyBridgeSkippedByNoBiomass,
    string EmergencyRescuePolicy,
    string LastEmergencyRescueReason,
    int TicksWithZeroHerbivores,
    int TicksWithZeroPredators,
    int? FirstZeroHerbivoreTick,
    int? FirstZeroPredatorTick,
    int PredatorDeaths,
    int PredatorHumanHits,
    int? FirstPredatorHumanContactTick,
    int? FirstPredatorHuntTick,
    int? FirstHerbivoreGrazingTick,
    int? FirstPredatorDeathTick,
    int? FirstHerbivoreDeathTick,
    int? FirstPredatorBirthTick,
    int? FirstHerbivoreBirthTick)
{
    public static ScenarioEcologyTelemetrySnapshot Empty { get; } = new(
        Herbivores: 0,
        Predators: 0,
        ActiveFoodNodes: 0,
        DepletedFoodNodes: 0,
        HerbivoreReplenishmentSpawns: 0,
        PredatorReplenishmentSpawns: 0,
        EmergencyRescues: 0,
        PlantFoodProduced: 0,
        MeatFoodProduced: 0,
        PlantFoodConsumedByAnimals: 0,
        MeatFromHunt: 0,
        SupplyBridgeSkippedByNoBiomass: 0,
        EmergencyRescuePolicy: "disabled",
        LastEmergencyRescueReason: "none",
        TicksWithZeroHerbivores: 0,
        TicksWithZeroPredators: 0,
        FirstZeroHerbivoreTick: null,
        FirstZeroPredatorTick: null,
        PredatorDeaths: 0,
        PredatorHumanHits: 0,
        FirstPredatorHumanContactTick: null,
        FirstPredatorHuntTick: null,
        FirstHerbivoreGrazingTick: null,
        FirstPredatorDeathTick: null,
        FirstHerbivoreDeathTick: null,
        FirstPredatorBirthTick: null,
        FirstHerbivoreBirthTick: null);

    public ScenarioEcologyTimelineSnapshot ToTimelineSnapshot()
        => new(
            Herbivores,
            Predators,
            ActiveFoodNodes,
            DepletedFoodNodes,
            HerbivoreReplenishmentSpawns,
            PredatorReplenishmentSpawns,
            EmergencyRescues,
            PlantFoodProduced,
            MeatFoodProduced,
            PlantFoodConsumedByAnimals,
            MeatFromHunt,
            SupplyBridgeSkippedByNoBiomass,
            EmergencyRescuePolicy,
            LastEmergencyRescueReason,
            TicksWithZeroHerbivores,
            TicksWithZeroPredators,
            FirstPredatorHumanContactTick,
            FirstPredatorHuntTick,
            FirstHerbivoreGrazingTick,
            FirstPredatorDeathTick,
            FirstHerbivoreDeathTick,
            FirstPredatorBirthTick,
            FirstHerbivoreBirthTick);
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
