using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation;

public sealed class GoapPlanner : IPlanner
{
    private readonly List<Action> _actions = CreateActions();
    private Goal? _currentGoal;
    private readonly Queue<Action> _currentPlan = new();

    public void SetGoal(Goal goal)
    {
        _currentGoal = goal;
        _currentPlan.Clear();
    }

    public Job GetNextJob(Person p, World w)
    {
        if (_currentGoal == null) return Job.Idle;
        if (_currentPlan.Count == 0)
        {
            var plan = BuildPlan(p, w, _currentGoal);
            if (plan != null)
            {
                foreach (var act in plan)
                    _currentPlan.Enqueue(act);
            }
        }
        return _currentPlan.Count > 0 ? _currentPlan.Dequeue().Job : Job.Idle;
    }

    private List<Action>? BuildPlan(Person p, World w, Goal goal)
    {
        var start = GetWorldState(p, w);
        var target = GetGoalState(goal, p, w);

        var open = new PriorityQueue<Node, int>();
        var closed = new HashSet<State>();
        open.Enqueue(new Node(start, null, null, 0, Heuristic(start, target)), 0);

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
                int g = current.G + action.Cost;
                int f = g + Heuristic(next, target);
                open.Enqueue(new Node(next, current, action, g, f), f);
            }
        }

        return null;
    }

    private static List<Action> CreateActions()
    {
        var list = new List<Action>();

        var gatherWood = new Action("GatherWood", Job.GatherWood);
        gatherWood.Effects["wood"] = 50; // assume yields house cost
        list.Add(gatherWood);

        var gatherStone = new Action("GatherStone", Job.GatherStone);
        gatherStone.Effects["stone"] = 15; // sample amount
        list.Add(gatherStone);

        var buildHouse = new Action("BuildHouse", Job.BuildHouse);
        buildHouse.Preconditions["wood"] = 50;
        buildHouse.Effects["wood"] = -50;
        buildHouse.Effects["house"] = 1;
        list.Add(buildHouse);

        return list;
    }

    private static State GetWorldState(Person p, World w)
    {
        var s = new State();
        s["wood"] = p.Home.Stock[Resource.Wood];
        s["stone"] = p.Home.Stock[Resource.Stone];
        s["house"] = p.Home.HouseCount;
        return s;
    }

    private static State GetGoalState(Goal goal, Person p, World w)
    {
        var s = new State();
        switch (goal.Name)
        {
            case "GatherWood":
                s["wood"] = p.Home.Stock[Resource.Wood] + 50;
                break;
            case "BuildHouse":
                s["house"] = p.Home.HouseCount + 1;
                break;
        }
        return s;
    }

    private static int Heuristic(State from, State to)
    {
        int h = 0;
        foreach (var kv in to.Values)
            h += Math.Max(0, kv.Value - from[kv.Key]);
        return h;
    }

    private static List<Action> Reconstruct(Node n)
    {
        var list = new List<Action>();
        while (n.Parent != null && n.Action != null)
        {
            list.Insert(0, n.Action);
            n = n.Parent;
        }
        return list;
    }

    private sealed record Node(State State, Node? Parent, Action? Action, int G, int F);
}
