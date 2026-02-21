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
            reason = _goalChanged ? "GoalChanged" : "MethodScored";
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
        var candidates = new List<MethodCandidate>();
        switch (goalName)
        {
            case "ExpandHousing":
            case "BuildHouse":
                candidates.Add(new MethodCandidate(
                    "BuildHouseByWood",
                    context.HomeWood >= context.HouseWoodCost ? 1.0f : 0.7f,
                    context.HomeWood >= context.HouseWoodCost
                        ? new[] { NpcCommand.BuildHouse }
                        : new[] { NpcCommand.GatherWood, NpcCommand.BuildHouse }));

                if (context.StoneBuildingsEnabled && context.CanBuildWithStone)
                {
                    candidates.Add(new MethodCandidate(
                        "BuildHouseByStone",
                        context.HomeStone >= context.HouseStoneCost ? 0.95f : 0.55f,
                        context.HomeStone >= context.HouseStoneCost
                            ? new[] { NpcCommand.BuildHouse }
                            : new[] { NpcCommand.GatherStone, NpcCommand.BuildHouse }));
                }
                break;

            case "SecureFood":
                candidates.Add(new MethodCandidate(
                    "EmergencyEat",
                    context.Hunger >= 75f && context.HomeFood > 0 ? 1.0f : 0.35f,
                    context.HomeFood > 0 ? new[] { NpcCommand.EatFood } : new[] { NpcCommand.GatherFood }));
                candidates.Add(new MethodCandidate(
                    "ForageAndStabilize",
                    context.HomeFood <= 2 ? 0.92f : 0.6f,
                    new[] { NpcCommand.GatherFood }));
                break;

            case "RecoverStamina":
                candidates.Add(new MethodCandidate(
                    "StaminaRecovery",
                    (100f - context.Stamina) / 100f,
                    new[] { NpcCommand.Rest }));
                break;

            case "StabilizeResources":
            case "GatherStone":
                candidates.Add(new MethodCandidate(
                    "StoneReserve",
                    context.HomeStone < 8 ? 0.95f : 0.4f,
                    new[] { NpcCommand.GatherStone }));
                candidates.Add(new MethodCandidate(
                    "WoodFallback",
                    context.HomeWood < 8 ? 0.75f : 0.3f,
                    new[] { NpcCommand.GatherWood }));
                break;

            case "GatherWood":
                candidates.Add(new MethodCandidate(
                    "WoodReserve",
                    context.HomeWood < 10 ? 0.95f : 0.45f,
                    new[] { NpcCommand.GatherWood }));
                break;

            default:
                candidates.Add(new MethodCandidate("FallbackIdle", 0.01f, new[] { NpcCommand.Idle }));
                break;
        }

        var selected = candidates.OrderByDescending(candidate => candidate.Score).FirstOrDefault();
        if (selected == null)
        {
            _methodName = "FallbackIdle";
            _plan.Enqueue(NpcCommand.Idle);
            return;
        }

        _methodName = selected.Name;
        foreach (var command in selected.Commands)
            _plan.Enqueue(command);
    }

    private sealed record MethodCandidate(string Name, float Score, IReadOnlyList<NpcCommand> Commands);
}
