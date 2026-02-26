namespace WorldSim.Runtime.ReadModel;

public enum TileGroundView
{
    Dirt,
    Water,
    Grass
}

public enum ResourceView
{
    None,
    Wood,
    Stone,
    Iron,
    Gold,
    Food,
    Water
}

public enum AnimalKindView
{
    Herbivore,
    Predator
}

public enum SeasonView
{
    Spring,
    Summer,
    Autumn,
    Winter
}

public enum SpecializedBuildingKindView
{
    FarmPlot,
    Workshop,
    Storehouse
}

public sealed record WorldRenderSnapshot(
    int Width,
    int Height,
    IReadOnlyList<TileRenderData> Tiles,
    IReadOnlyList<HouseRenderData> Houses,
    IReadOnlyList<SpecializedBuildingRenderData> SpecializedBuildings,
    IReadOnlyList<PersonRenderData> People,
    IReadOnlyList<AnimalRenderData> Animals,
    IReadOnlyList<ColonyHudData> Colonies,
    EcoHudData Ecology,
    SeasonView CurrentSeason,
    bool IsDroughtActive,
    IReadOnlyList<string> RecentEvents
);

public sealed record TileRenderData(int X, int Y, TileGroundView Ground, ResourceView NodeType, int NodeAmount);

public sealed record HouseRenderData(int X, int Y, int ColonyId);

public sealed record SpecializedBuildingRenderData(int X, int Y, int ColonyId, SpecializedBuildingKindView Kind);

public sealed record PersonRenderData(int X, int Y, int ColonyId);

public sealed record AnimalRenderData(int X, int Y, AnimalKindView Kind);

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
    int FarmPlots,
    int Workshops,
    int Storehouses,
    int ToolCharges,
    int People,
    float FoodPerPerson,
    int DeathsOldAge,
    int DeathsStarvation,
    int DeathsPredator,
    int DeathsOther,
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
    int PredatorHumanHits,
    int DeathsOldAge,
    int DeathsStarvation,
    int DeathsPredator,
    int DeathsOther,
    int DeathsStarvationRecent60s,
    int DeathsStarvationWithFood,
    bool PredatorHumanAttacksEnabled,
    float AverageFoodPerPerson,
    int ColoniesInFoodEmergency,
    float FoodPerPersonSpread
);
