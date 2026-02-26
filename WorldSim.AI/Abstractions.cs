namespace WorldSim.AI;

public enum NpcWarState
{
    Peace,
    Tense,
    War
}

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
    CraftTools
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
    int Strength = 5,
    float Defense = 0f,
    int NearbyPredators = 0,
    int NearbyHostilePeople = 0,
    NpcWarState WarState = NpcWarState.Peace,
    bool TileContestedNearby = false,
    bool IsWarrior = false,
    int ColonyWarriorCount = 0);

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
