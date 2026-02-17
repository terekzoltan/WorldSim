using WorldSim.Simulation;

namespace WorldSim.Runtime.ReadModel;

public sealed record WorldRenderSnapshot(
    int Width,
    int Height,
    IReadOnlyList<TileRenderData> Tiles,
    IReadOnlyList<HouseRenderData> Houses,
    IReadOnlyList<PersonRenderData> People,
    IReadOnlyList<AnimalRenderData> Animals,
    IReadOnlyList<ColonyHudData> Colonies,
    EcoHudData Ecology,
    Season CurrentSeason,
    bool IsDroughtActive,
    IReadOnlyList<string> RecentEvents
);

public sealed record TileRenderData(int X, int Y, Ground Ground, Resource NodeType, int NodeAmount);

public sealed record HouseRenderData(int X, int Y, int ColonyId);

public sealed record PersonRenderData(int X, int Y, int ColonyId);

public sealed record AnimalRenderData(int X, int Y, AnimalKind Kind);

public sealed record ColonyHudData(
    int Id,
    string Name,
    float Morale,
    int Food,
    int Wood,
    int Stone,
    int Iron,
    int Gold,
    int Houses,
    int People,
    float AverageHunger,
    float AverageStamina,
    string ProfessionSummary
);

public sealed record EcoHudData(
    int Herbivores,
    int Predators,
    int ActiveFoodNodes,
    int DepletedFoodNodes,
    int CriticalHungry,
    int AnimalStuckRecoveries,
    int PredatorDeaths,
    int PredatorHumanHits
);
