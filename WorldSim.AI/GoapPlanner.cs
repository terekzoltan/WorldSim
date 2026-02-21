using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.AI;

public sealed class GoapPlanner : IPlanner
{
    private readonly List<GoapAction> _actions = CreateActions();
    private readonly Queue<GoapAction> _currentPlan = new();
    private Goal? _goal;
    private bool _goalChanged;
    public string Name => "Goap";

    public void SetGoal(Goal goal)
    {
        _goalChanged = _goal?.Name != goal.Name;
        _goal = goal;
        _currentPlan.Clear();
    }

    public PlannerDecision GetNextCommand(in NpcAiContext context)
    {
        if (_goal == null)
            return new PlannerDecision(NpcCommand.Idle, 0, Array.Empty<NpcCommand>(), 0, "NoGoal", "GoapSearch");

        var reason = _goalChanged ? "GoalChanged" : "PlanContinue";

        if (_currentPlan.Count == 0)
        {
            var plan = BuildPlan(context, _goal);
            if (plan != null)
            {
                foreach (var action in plan)
                    _currentPlan.Enqueue(action);
                reason = _goalChanged ? "GoalChanged" : "PlanBuilt";
            }
            else
            {
                reason = "NoPlan";
            }

            _goalChanged = false;
        }

        var command = _currentPlan.Count > 0 ? _currentPlan.Dequeue().Command : NpcCommand.Idle;
        if (command == NpcCommand.Idle)
            return new PlannerDecision(command, 0, Array.Empty<NpcCommand>(), 0, reason, "GoapSearch");

        var planLength = 1 + _currentPlan.Count;
        var preview = new List<NpcCommand>(1 + Math.Min(4, _currentPlan.Count)) { command };
        preview.AddRange(_currentPlan.Take(4).Select(action => action.Command));
        var cost = preview.Count;
        return new PlannerDecision(command, planLength, preview, cost, reason, "GoapSearch");
    }

    private List<GoapAction>? BuildPlan(in NpcAiContext context, Goal goal)
    {
        var start = GetWorldState(context);
        var target = GetGoalState(goal, context);

        var open = new PriorityQueue<Node, int>();
        var closed = new HashSet<GoapState>();
        open.Enqueue(new Node(start, null, null, 0), 0);

        while (open.TryDequeue(out var current, out _))
        {
            if (current.State.Contains(target))
                return Reconstruct(current);

            closed.Add(current.State);

            foreach (var action in _actions)
            {
                if (!current.State.Contains(action.Preconditions))
                    continue;

                var next = current.State.Apply(action.Effects);
                if (closed.Contains(next))
                    continue;

                var g = current.G + action.Cost;
                var f = g + Heuristic(next, target);
                open.Enqueue(new Node(next, current, action, g), f);
            }
        }

        return null;
    }

    private static List<GoapAction> CreateActions()
    {
        var actions = new List<GoapAction>();

        var gatherWood = new GoapAction("GatherWood", NpcCommand.GatherWood);
        gatherWood.Effects["wood"] = 50;
        actions.Add(gatherWood);

        var gatherStone = new GoapAction("GatherStone", NpcCommand.GatherStone);
        gatherStone.Effects["stone"] = 15;
        actions.Add(gatherStone);

        var gatherFood = new GoapAction("GatherFood", NpcCommand.GatherFood);
        gatherFood.Effects["food"] = 8;
        gatherFood.Effects["satiety"] = 8;
        actions.Add(gatherFood);

        var eatFood = new GoapAction("EatFood", NpcCommand.EatFood);
        eatFood.Preconditions["food"] = 1;
        eatFood.Effects["food"] = -1;
        eatFood.Effects["satiety"] = 35;
        actions.Add(eatFood);

        var rest = new GoapAction("Rest", NpcCommand.Rest);
        rest.Effects["stamina"] = 28;
        actions.Add(rest);

        var buildHouse = new GoapAction("BuildHouse", NpcCommand.BuildHouse);
        buildHouse.Preconditions["wood"] = 50;
        buildHouse.Effects["wood"] = -50;
        buildHouse.Effects["house"] = 1;
        actions.Add(buildHouse);

        return actions;
    }

    private static GoapState GetWorldState(in NpcAiContext context)
    {
        var state = new GoapState();
        state["wood"] = context.HomeWood;
        state["stone"] = context.HomeStone;
        state["food"] = context.HomeFood;
        state["house"] = context.HomeHouseCount;
        state["satiety"] = Math.Max(0, 100 - (int)MathF.Round(context.Hunger));
        state["stamina"] = Math.Max(0, (int)MathF.Round(context.Stamina));
        return state;
    }

    private static GoapState GetGoalState(Goal goal, in NpcAiContext context)
    {
        var state = new GoapState();
        switch (goal.Name)
        {
            case "GatherWood":
                state["wood"] = context.HomeWood + 50;
                break;
            case "BuildHouse":
            case "ExpandHousing":
                state["house"] = context.HomeHouseCount + 1;
                break;
            case "GatherStone":
            case "StabilizeResources":
                state["stone"] = context.HomeStone + 15;
                break;
            case "SecureFood":
                state["satiety"] = Math.Max(20, 100 - (int)MathF.Round(context.Hunger)) + 20;
                break;
            case "RecoverStamina":
                state["stamina"] = Math.Max(20, (int)MathF.Round(context.Stamina)) + 30;
                break;
        }

        return state;
    }

    private static int Heuristic(GoapState from, GoapState to)
    {
        var cost = 0;
        foreach (var kv in to.Values)
            cost += Math.Max(0, kv.Value - from[kv.Key]);
        return cost;
    }

    private static List<GoapAction> Reconstruct(Node node)
    {
        var plan = new List<GoapAction>();
        while (node.Parent != null && node.Action != null)
        {
            plan.Insert(0, node.Action);
            node = node.Parent;
        }

        return plan;
    }

    private sealed record Node(GoapState State, Node? Parent, GoapAction? Action, int G);
}
