using System;
using System.Collections.Generic;

namespace WorldSim.AI;

public sealed class GoapPlanner : IPlanner
{
    private readonly List<GoapAction> _actions = CreateActions();
    private readonly Queue<GoapAction> _currentPlan = new();
    private Goal? _goal;

    public void SetGoal(Goal goal)
    {
        _goal = goal;
        _currentPlan.Clear();
    }

    public NpcCommand GetNextCommand(in NpcAiContext context)
    {
        if (_goal == null)
            return NpcCommand.Idle;

        if (_currentPlan.Count == 0)
        {
            var plan = BuildPlan(context, _goal);
            if (plan != null)
            {
                foreach (var action in plan)
                    _currentPlan.Enqueue(action);
            }
        }

        return _currentPlan.Count > 0 ? _currentPlan.Dequeue().Command : NpcCommand.Idle;
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
        state["house"] = context.HomeHouseCount;
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
                state["house"] = context.HomeHouseCount + 1;
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
