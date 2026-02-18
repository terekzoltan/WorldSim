namespace WorldSim.AI;

public enum NpcCommand
{
    Idle,
    GatherWood,
    GatherStone,
    GatherFood,
    EatFood,
    Rest,
    BuildHouse
}

public readonly record struct NpcAiContext(
    float SimulationTimeSeconds,
    float Hunger,
    int HomeWood,
    int HomeStone,
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
    void SetGoal(Goal goal);
    NpcCommand GetNextCommand(in NpcAiContext context);
}

public interface INpcDecisionBrain
{
    NpcCommand Think(in NpcAiContext context);
}
