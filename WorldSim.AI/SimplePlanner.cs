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
            return new PlannerDecision(
                Command: NpcCommand.Idle,
                PlanLength: 0,
                PlanPreview: Array.Empty<NpcCommand>(),
                PlanCost: 0,
                ReplanReason: "NoGoal",
                MethodName: "SimpleRule",
                MethodScore: 0f,
                RunnerUpMethod: "None",
                RunnerUpScore: 0f);

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
            return new PlannerDecision(
                Command: command,
                PlanLength: 0,
                PlanPreview: Array.Empty<NpcCommand>(),
                PlanCost: 0,
                ReplanReason: _goalChanged ? "GoalChanged" : "NoRule",
                MethodName: "SimpleRule",
                MethodScore: 0f,
                RunnerUpMethod: "None",
                RunnerUpScore: 0f);

        var reason = _goalChanged ? "GoalChanged" : "RuleMatch";
        _goalChanged = false;
        return new PlannerDecision(
            Command: command,
            PlanLength: 1,
            PlanPreview: new[] { command },
            PlanCost: 1,
            ReplanReason: reason,
            MethodName: "SimpleRule",
            MethodScore: 1f,
            RunnerUpMethod: "Fallback",
            RunnerUpScore: 0f);
    }
}
