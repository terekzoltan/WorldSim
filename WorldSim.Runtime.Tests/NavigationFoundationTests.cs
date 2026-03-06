using System.Linq;
using WorldSim.Simulation;
using WorldSim.Simulation.Navigation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class NavigationFoundationTests
{
    [Fact]
    public void World_TopologyVersion_Increments_WhenStructuresChange()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 77);
        var colony = world._colonies[0];
        var initial = world.NavigationTopologyVersion;

        var p1 = FindFirstBuildable(world);
        world.AddHouse(colony, p1);
        var afterHouse = world.NavigationTopologyVersion;

        var p2 = FindAnotherBuildable(world, p1);
        world.AddSpecializedBuilding(colony, p2, SpecializedBuildingKind.FarmPlot);
        var afterSpecialized = world.NavigationTopologyVersion;

        Assert.True(afterHouse > initial);
        Assert.True(afterSpecialized > afterHouse);
    }

    [Fact]
    public void NavigationPathCache_Invalidates_OnTopologyVersionChange()
    {
        var cache = new NavigationPathCache();
        cache.Set(target: (5, 5), topologyVersion: 2, steps: new[] { (1, 1), (2, 1), (3, 1) });

        Assert.True(cache.IsValid((5, 5), 2));
        Assert.False(cache.IsValid((5, 5), 3));

        cache.Invalidate();
        Assert.False(cache.HasPath);
    }

    [Fact]
    public void NavigationPathfinder_FindsDetour_WhenDirectTileBlocked()
    {
        var world = new World(width: 32, height: 20, initialPop: 8, randomSeed: 42);
        var grid = new NavigationGrid(world);
        var colony = world._colonies[0];

        var pattern = FindDetourPattern(world);
        Assert.True(pattern.HasValue);

        var (start, blocked, goal) = pattern!.Value;
        world.AddHouse(colony, blocked);

        var path = NavigationPathfinder.FindPath(
            grid,
            start,
            goal,
            moverColonyId: colony.Id,
            maxExpansions: 4096,
            out var budgetExceeded);

        Assert.False(budgetExceeded);
        Assert.NotEmpty(path);
        Assert.Equal(start, path.First());
        Assert.Equal(goal, path.Last());
        Assert.DoesNotContain(blocked, path);
    }

    [Fact]
    public void NavigationPathCache_InvalidatesAndReplans_WhenNextStepBecomesBlocked()
    {
        var world = new World(width: 36, height: 24, initialPop: 10, randomSeed: 99);
        var grid = new NavigationGrid(world);
        var colony = world._colonies[0];
        var hostileColony = world._colonies.First(c => c.Faction != colony.Faction);

        var pattern = FindDetourPattern(world);
        Assert.True(pattern.HasValue);
        var (start, blocked, goal) = pattern!.Value;

        var initial = NavigationPathfinder.FindPath(grid, start, goal, hostileColony.Id, 4096, out var initialBudgetExceeded);
        Assert.False(initialBudgetExceeded);
        Assert.True(initial.Count >= 3);

        var cache = new NavigationPathCache();
        cache.Set(goal, grid.TopologyVersion, initial);
        var nextBefore = cache.PeekNext();
        Assert.True(nextBefore.HasValue);

        world.TryAddWoodWall(colony, nextBefore!.Value);

        Assert.False(cache.IsValid(goal, grid.TopologyVersion));

        var replanned = NavigationPathfinder.FindPath(grid, start, goal, hostileColony.Id, 4096, out var replannedBudgetExceeded);
        Assert.False(replannedBudgetExceeded);
        Assert.NotEmpty(replanned);
        Assert.DoesNotContain(nextBefore.Value, replanned);
    }

    [Fact]
    public void NavigationPathfinder_FindsRoute_ThroughWallRingGap()
    {
        var world = new World(width: 40, height: 30, initialPop: 10, randomSeed: 123);
        var colony = world._colonies[0];
        var center = FindCenterBuildable(world);
        var start = (center.x - 4, center.y);
        var goal = (center.x + 4, center.y);

        Assert.True(IsLand(world, start));
        Assert.True(IsLand(world, goal));

        foreach (var tile in RingTiles(center, radius: 2))
        {
            if (!InBounds(world, tile) || !IsLand(world, tile))
                continue;

            // Keep one east-side opening in the ring.
            if (tile == (center.x + 2, center.y))
                continue;

            world.TryAddWoodWall(colony, tile);
        }

        var grid = new NavigationGrid(world);
        var path = NavigationPathfinder.FindPath(grid, start, goal, colony.Id, 4096, out var budgetExceeded);

        Assert.False(budgetExceeded);
        Assert.NotEmpty(path);
        Assert.Equal(start, path.First());
        Assert.Equal(goal, path.Last());
        Assert.Contains((center.x + 2, center.y), path);
    }

    private static (int x, int y) FindFirstBuildable(World world)
    {
        for (int y = 1; y < world.Height - 1; y++)
        {
            for (int x = 1; x < world.Width - 1; x++)
            {
                if (world.GetTile(x, y).Ground != Ground.Water)
                    return (x, y);
            }
        }

        throw new InvalidOperationException("No buildable tile found.");
    }

    private static (int x, int y) FindAnotherBuildable(World world, (int x, int y) used)
    {
        for (int y = 1; y < world.Height - 1; y++)
        {
            for (int x = 1; x < world.Width - 1; x++)
            {
                if ((x, y) == used)
                    continue;
                if (world.GetTile(x, y).Ground != Ground.Water)
                    return (x, y);
            }
        }

        throw new InvalidOperationException("No second buildable tile found.");
    }

    private static ((int x, int y) start, (int x, int y) blocked, (int x, int y) goal)? FindDetourPattern(World world)
    {
        for (int y = 1; y < world.Height - 1; y++)
        {
            for (int x = 1; x < world.Width - 2; x++)
            {
                var start = (x, y);
                var blocked = (x + 1, y);
                var goal = (x + 2, y);
                var alt1 = (x + 1, y + 1);
                var alt2 = (x + 2, y + 1);

                if (world.GetTile(start.x, start.y).Ground == Ground.Water) continue;
                if (world.GetTile(blocked.Item1, blocked.Item2).Ground == Ground.Water) continue;
                if (world.GetTile(goal.Item1, goal.Item2).Ground == Ground.Water) continue;
                if (world.GetTile(alt1.Item1, alt1.Item2).Ground == Ground.Water) continue;
                if (world.GetTile(alt2.Item1, alt2.Item2).Ground == Ground.Water) continue;

                return (start, blocked, goal);
            }
        }

        return null;
    }

    private static (int x, int y) FindCenterBuildable(World world)
    {
        var cx = world.Width / 2;
        var cy = world.Height / 2;
        for (int radius = 0; radius <= Math.Max(world.Width, world.Height); radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var p = (x: cx + dx, y: cy + dy);
                    if (!InBounds(world, p))
                        continue;
                    if (IsLand(world, p))
                        return p;
                }
            }
        }

        throw new InvalidOperationException("No center buildable tile found.");
    }

    private static IEnumerable<(int x, int y)> RingTiles((int x, int y) center, int radius)
    {
        for (int y = center.y - radius; y <= center.y + radius; y++)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                int md = Math.Abs(x - center.x) + Math.Abs(y - center.y);
                if (md == radius)
                    yield return (x, y);
            }
        }
    }

    private static bool InBounds(World world, (int x, int y) p)
        => p.x >= 0 && p.y >= 0 && p.x < world.Width && p.y < world.Height;

    private static bool IsLand(World world, (int x, int y) p)
        => world.GetTile(p.x, p.y).Ground != Ground.Water;
}
