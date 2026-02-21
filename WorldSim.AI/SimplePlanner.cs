namespace WorldSim.AI;

public sealed class SimplePlanner : IPlanner
{
    private Goal? _goal;
    private bool _goalChanged;
    public string Name => "Simple";

    public void SetGoal(Goal goal)
    {
        _goalChanged = _goal?.Name != goal.Name;
        _goal = goal;
    }

    public PlannerDecision GetNextCommand(in NpcAiContext context)
    {
        if (_goal == null)
            return new PlannerDecision(NpcCommand.Idle, 0, Array.Empty<NpcCommand>(), 0, "NoGoal", "SimpleRule");

        var command = _goal.Name switch
        {
            "GatherWood" => NpcCommand.GatherWood,
            "GatherStone" => NpcCommand.GatherStone,
            "SecureFood" => context.Hunger >= 68f && context.HomeFood > 0 ? NpcCommand.EatFood : NpcCommand.GatherFood,
            "RecoverStamina" => NpcCommand.Rest,
            "StabilizeResources" => NpcCommand.GatherStone,
            "ExpandHousing" => context.HomeWood >= context.HouseWoodCost ? NpcCommand.BuildHouse : NpcCommand.GatherWood,
            "BuildHouse" => context.HomeWood >= context.HouseWoodCost
                ? NpcCommand.BuildHouse
                : NpcCommand.GatherWood,
            _ => NpcCommand.Idle
        };

        if (command == NpcCommand.Idle)
            return new PlannerDecision(command, 0, Array.Empty<NpcCommand>(), 0, _goalChanged ? "GoalChanged" : "NoRule", "SimpleRule");

        var reason = _goalChanged ? "GoalChanged" : "RuleMatch";
        _goalChanged = false;
        return new PlannerDecision(command, 1, new[] { command }, 1, reason, "SimpleRule");
    }
}
