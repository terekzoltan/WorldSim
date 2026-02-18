namespace WorldSim.AI;

public sealed class SimplePlanner : IPlanner
{
    private Goal? _goal;

    public void SetGoal(Goal goal)
    {
        _goal = goal;
    }

    public NpcCommand GetNextCommand(in NpcAiContext context)
    {
        if (_goal == null)
            return NpcCommand.Idle;

        return _goal.Name switch
        {
            "GatherWood" => NpcCommand.GatherWood,
            "GatherStone" => NpcCommand.GatherStone,
            "BuildHouse" => context.HomeWood >= context.HouseWoodCost
                ? NpcCommand.BuildHouse
                : NpcCommand.GatherWood,
            _ => NpcCommand.Idle
        };
    }
}
