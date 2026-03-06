namespace WorldSim.AI;

public enum NpcCommand
{
    Idle,
    GatherWood,
    GatherStone,
    GatherIron,
    GatherGold,
    GatherFood,
    EatFood,
    Rest,
    BuildHouse,
    CraftTools,
    BuildWall,
    BuildWatchtower,
    RaidBorder,
    AttackStructure,
    Fight,
    Flee
}

public readonly record struct NpcAiContext(
    float SimulationTimeSeconds,
    float Hunger,
    float Stamina,
    int HomeWood,
    int HomeStone,
    int HomeIron,
    int HomeGold,
    int HomeFood,
    int HomeHouseCount,
    int HouseWoodCost,
    int ColonyPopulation,
    int HouseCapacity,
    bool StoneBuildingsEnabled,
    bool CanBuildWithStone,
    int HouseStoneCost,
    float Health = 100f,
    int Strength = 0,
    int Defense = 0,
    int NearbyPredators = 0,
    int NearbyHostilePeople = 0,
    float BiasFarming = 0f,
    float BiasGathering = 0f,
    float BiasBuilding = 0f,
    float BiasCrafting = 0f,
    float BiasRest = 0f,
    float BiasSocial = 0f,
    float BiasMilitary = 0f,
    bool IsWarStance = false,
    bool IsHostileStance = false,
    bool IsContestedTile = false,
    bool HasContestedTilesNearby = false,
    bool IsWarriorRole = false,
    int NearbyEnemyCount = 0,
    float HostileProximityScore = 0f,
    float LocalThreatScore = 0f);

public interface IPlanner
{
    string Name { get; }
    void SetGoal(Goal goal);
    PlannerDecision GetNextCommand(in NpcAiContext context);
}

public interface INpcDecisionBrain
{
    AiDecisionResult Think(in NpcAiContext context);
}
