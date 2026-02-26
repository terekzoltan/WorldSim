namespace WorldSim.Simulation.Navigation;

public static class NavigationPathfinder
{
    private static readonly (int dx, int dy)[] Directions =
    {
        (1, 0),
        (-1, 0),
        (0, 1),
        (0, -1)
    };

    public static IReadOnlyList<(int x, int y)> FindPath(
        NavigationGrid grid,
        (int x, int y) start,
        (int x, int y) goal,
        int moverColonyId,
        int maxExpansions,
        out bool expansionBudgetExceeded)
    {
        expansionBudgetExceeded = false;

        if (!grid.InBounds(start.x, start.y) || !grid.InBounds(goal.x, goal.y))
            return Array.Empty<(int x, int y)>();

        if (start == goal)
            return new[] { start };

        var queue = new Queue<(int x, int y)>();
        var cameFrom = new Dictionary<(int x, int y), (int x, int y)>();
        var visited = new HashSet<(int x, int y)> { start };
        queue.Enqueue(start);

        int expanded = 0;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            expanded++;
            if (expanded > Math.Max(1, maxExpansions))
            {
                expansionBudgetExceeded = true;
                return Array.Empty<(int x, int y)>();
            }

            foreach (var dir in Directions)
            {
                var next = (x: current.x + dir.dx, y: current.y + dir.dy);
                if (visited.Contains(next))
                    continue;
                if (grid.IsBlocked(next.x, next.y, moverColonyId))
                    continue;

                visited.Add(next);
                cameFrom[next] = current;

                if (next == goal)
                    return ReconstructPath(cameFrom, start, goal);

                queue.Enqueue(next);
            }
        }

        return Array.Empty<(int x, int y)>();
    }

    private static IReadOnlyList<(int x, int y)> ReconstructPath(
        Dictionary<(int x, int y), (int x, int y)> cameFrom,
        (int x, int y) start,
        (int x, int y) goal)
    {
        var path = new List<(int x, int y)> { goal };
        var current = goal;
        while (current != start)
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
