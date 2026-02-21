using System.Collections.Generic;
using System.Linq;

namespace WorldSim.AI;

public sealed class HtnPlanner : IPlanner
{
    private Goal? _goal;
    private readonly Queue<NpcCommand> _plan = new();
    private string _methodName = "None";
    private bool _goalChanged;

    public string Name => "Htn";

    public void SetGoal(Goal goal)
    {
        _goalChanged = _goal?.Name != goal.Name;
        _goal = goal;
        _plan.Clear();
        _methodName = "None";
    }

    public PlannerDecision GetNextCommand(in NpcAiContext context)
    {
        if (_goal == null)
            return new PlannerDecision(NpcCommand.Idle, 0, System.Array.Empty<NpcCommand>(), 0, "NoGoal", "None");

        var reason = _goalChanged ? "GoalChanged" : "PlanContinue";

        if (_plan.Count == 0)
        {
            Decompose(_goal.Name, context);
            reason = _goalChanged ? "GoalChanged" : "MethodDecomposed";
            _goalChanged = false;
        }

        if (_plan.Count == 0)
            return new PlannerDecision(NpcCommand.Idle, 0, System.Array.Empty<NpcCommand>(), 0, "NoMethod", _methodName);

        var command = _plan.Dequeue();
        var planLength = 1 + _plan.Count;
        var preview = new List<NpcCommand> { command };
        preview.AddRange(_plan.Take(4));
        return new PlannerDecision(command, planLength, preview, planLength, reason, _methodName);
    }

    private void Decompose(string goalName, in NpcAiContext context)
    {
        switch (goalName)
        {
            case "ExpandHousing":
            case "BuildHouse":
                _methodName = "BuildHouseByWood";
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
                _methodName = "FoodStabilization";
                if (context.Hunger >= 70f && context.HomeFood > 0)
                    _plan.Enqueue(NpcCommand.EatFood);
                else
                    _plan.Enqueue(NpcCommand.GatherFood);
                break;

            case "RecoverStamina":
                _methodName = "StaminaRecovery";
                _plan.Enqueue(NpcCommand.Rest);
                break;

            case "StabilizeResources":
            case "GatherStone":
                _methodName = "StoneReserve";
                _plan.Enqueue(NpcCommand.GatherStone);
                break;

            case "GatherWood":
                _methodName = "WoodReserve";
                _plan.Enqueue(NpcCommand.GatherWood);
                break;

            default:
                _methodName = "FallbackIdle";
                _plan.Enqueue(NpcCommand.Idle);
                break;
        }
    }
}
