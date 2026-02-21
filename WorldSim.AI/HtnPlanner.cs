using System.Collections.Generic;
using System.Linq;

namespace WorldSim.AI;

public sealed class HtnPlanner : IPlanner
{
    private Goal? _goal;
    private readonly Queue<NpcCommand> _plan = new();

    public string Name => "Htn";

    public void SetGoal(Goal goal)
    {
        _goal = goal;
        _plan.Clear();
    }

    public PlannerDecision GetNextCommand(in NpcAiContext context)
    {
        if (_goal == null)
            return new PlannerDecision(NpcCommand.Idle, 0, System.Array.Empty<NpcCommand>());

        if (_plan.Count == 0)
            Decompose(_goal.Name, context);

        if (_plan.Count == 0)
            return new PlannerDecision(NpcCommand.Idle, 0, System.Array.Empty<NpcCommand>());

        var command = _plan.Dequeue();
        var planLength = 1 + _plan.Count;
        var preview = new List<NpcCommand> { command };
        preview.AddRange(_plan.Take(4));
        return new PlannerDecision(command, planLength, preview);
    }

    private void Decompose(string goalName, in NpcAiContext context)
    {
        switch (goalName)
        {
            case "ExpandHousing":
            case "BuildHouse":
                if (context.HomeWood >= context.HouseWoodCost)
                {
                    _plan.Enqueue(NpcCommand.BuildHouse);
                }
                else
                {
                    _plan.Enqueue(NpcCommand.GatherWood);
                    _plan.Enqueue(NpcCommand.BuildHouse);
                }
                break;

            case "SecureFood":
                if (context.Hunger >= 70f && context.HomeFood > 0)
                    _plan.Enqueue(NpcCommand.EatFood);
                else
                    _plan.Enqueue(NpcCommand.GatherFood);
                break;

            case "RecoverStamina":
                _plan.Enqueue(NpcCommand.Rest);
                break;

            case "StabilizeResources":
            case "GatherStone":
                _plan.Enqueue(NpcCommand.GatherStone);
                break;

            case "GatherWood":
                _plan.Enqueue(NpcCommand.GatherWood);
                break;

            default:
                _plan.Enqueue(NpcCommand.Idle);
                break;
        }
    }
}
