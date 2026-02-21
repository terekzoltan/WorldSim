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
    int HouseStoneCost);

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
