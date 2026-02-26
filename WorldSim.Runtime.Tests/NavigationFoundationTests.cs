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
}
