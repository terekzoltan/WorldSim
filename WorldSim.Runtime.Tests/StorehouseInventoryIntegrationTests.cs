using System;
using System.Collections.Generic;
using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class StorehouseInventoryIntegrationTests
{
    [Fact]
    public void OwnedStorehouseAccessTile_IsRecognizedForSameColony()
    {
        var (world, colony, storehouse, access) = CreateWorldWithStorehouse(seed: 6201);

        Assert.True(world.IsOwnedStorehouseAccessTile(colony, access));
        Assert.False(world.IsOwnedStorehouseAccessTile(colony, storehouse));
    }

    [Fact]
    public void ForeignStorehouse_CannotBeUsed()
    {
        var world = CreateEmptyResourceWorld(seed: 6202);
        var home = world._colonies[0];
        var foreign = world._colonies[1];
        var (storehouse, access) = FindStorehouseWithAccess(world, foreign);
        world.AddSpecializedBuilding(foreign, storehouse, SpecializedBuildingKind.Storehouse);

        Assert.False(world.IsOwnedStorehouseAccessTile(home, access));
        Assert.False(world.TryFindNearestOwnedStorehouseAccessTile(home, access, out _));
    }

    [Fact]
    public void NearestOwnedStorehouseAccessTile_UsesDeterministicTieBreak()
    {
        var world = CreateEmptyResourceWorld(seed: 6203);
        var colony = world._colonies[0];
        var first = FindStorehouseWithAccess(world, colony);
        world.AddSpecializedBuilding(colony, first.Storehouse, SpecializedBuildingKind.Storehouse);
        var second = FindStorehouseWithAccess(world, colony, excluded: new[] { first.Storehouse, first.Access });
        world.AddSpecializedBuilding(colony, second.Storehouse, SpecializedBuildingKind.Storehouse);
        var from = colony.Origin;

        Assert.True(world.TryFindNearestOwnedStorehouseAccessTile(colony, from, out var actual));
        Assert.True(world.TryFindNearestOwnedStorehouseAccessTile(colony, from, out var repeated));

        var expected = ComputeExpectedAccessTile(world, colony, from);
        Assert.Equal(expected, actual);
        Assert.Equal(expected, repeated);
    }

    [Fact]
    public void RefillInventory_TransfersFoodFromHomeStockToInventory()
    {
        var (world, colony, _, access) = CreateWorldWithStorehouse(seed: 6204);
        var person = world._people.First(p => p.Home == colony);
        person.Pos = access;
        colony.Stock[Resource.Food] = 10;

        Assert.True(person.TryRefillInventoryFromStorehouse(world));

        Assert.Equal(3, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(7, colony.Stock[Resource.Food]);
    }

    [Fact]
    public void RefillInventory_StopsAtFreeSlots()
    {
        var (world, colony, _, access) = CreateWorldWithStorehouse(seed: 6205);
        var person = world._people.First(p => p.Home == colony);
        person.Pos = access;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 2));
        colony.Stock[Resource.Food] = 10;

        Assert.True(person.TryRefillInventoryFromStorehouse(world));

        Assert.Equal(3, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(9, colony.Stock[Resource.Food]);
    }

    [Fact]
    public void RefillInventory_StopsAtAvailableHomeFood()
    {
        var (world, colony, _, access) = CreateWorldWithStorehouse(seed: 6206);
        var person = world._people.First(p => p.Home == colony);
        person.Pos = access;
        colony.Stock[Resource.Food] = 2;

        Assert.True(person.TryRefillInventoryFromStorehouse(world));

        Assert.Equal(2, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(0, colony.Stock[Resource.Food]);
    }

    [Fact]
    public void RefillInventory_NoOpsWhenInventoryFull()
    {
        var (world, colony, _, access) = CreateWorldWithStorehouse(seed: 6207);
        var person = world._people.First(p => p.Home == colony);
        person.Pos = access;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 3));
        colony.Stock[Resource.Food] = 10;

        Assert.False(person.TryRefillInventoryFromStorehouse(world));

        Assert.Equal(3, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(10, colony.Stock[Resource.Food]);
    }

    [Fact]
    public void RefillInventory_NoOpsWithoutOwnedStorehouse()
    {
        var world = CreateEmptyResourceWorld(seed: 6208);
        var person = world._people[0];
        var colony = person.Home;
        colony.Stock[Resource.Food] = 10;

        Assert.False(person.TryRefillInventoryFromStorehouse(world));

        Assert.Equal(0, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(10, colony.Stock[Resource.Food]);
    }

    [Fact]
    public void DepositInventory_TransfersFoodBackToHomeStock()
    {
        var (world, colony, _, access) = CreateWorldWithStorehouse(seed: 6209);
        var person = world._people.First(p => p.Home == colony);
        person.Pos = access;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 2));
        colony.Stock[Resource.Food] = 5;

        Assert.True(person.TryDepositInventoryToStorehouse(world));

        Assert.Equal(0, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(7, colony.Stock[Resource.Food]);
    }

    [Fact]
    public void DepositInventory_NoOpsWithoutStorehouseOrFood()
    {
        var world = CreateEmptyResourceWorld(seed: 6210);
        var person = world._people[0];
        person.Home.Stock[Resource.Food] = 5;

        Assert.False(person.TryDepositInventoryToStorehouse(world));
        Assert.Equal(5, person.Home.Stock[Resource.Food]);

        var (storehouse, access) = FindStorehouseWithAccess(world, person.Home);
        world.AddSpecializedBuilding(person.Home, storehouse, SpecializedBuildingKind.Storehouse);
        person.Pos = access;

        Assert.False(person.TryDepositInventoryToStorehouse(world));
        Assert.Equal(5, person.Home.Stock[Resource.Food]);
    }

    [Fact]
    public void RefillAndDeposit_PreserveNoDupingInvariant()
    {
        var (world, colony, _, access) = CreateWorldWithStorehouse(seed: 6211);
        var person = world._people.First(p => p.Home == colony);
        person.Pos = access;
        colony.Stock[Resource.Food] = 8;

        var before = colony.Stock[Resource.Food] + person.Inventory.GetCount(ItemType.Food);
        Assert.True(person.TryRefillInventoryFromStorehouse(world));
        var afterRefill = colony.Stock[Resource.Food] + person.Inventory.GetCount(ItemType.Food);
        Assert.True(person.TryDepositInventoryToStorehouse(world));
        var afterDeposit = colony.Stock[Resource.Food] + person.Inventory.GetCount(ItemType.Food);

        Assert.Equal(before, afterRefill);
        Assert.Equal(before, afterDeposit);
    }

    [Fact]
    public void RefillInventoryCommand_FromFixedBrain_MovesTowardAccessTile()
    {
        var world = CreateEmptyResourceWorld(seed: 6212, command: NpcCommand.RefillInventory);
        var person = world._people[0];
        person.Profession = Profession.Generalist;
        var colony = person.Home;
        var (storehouse, access) = FindStorehouseWithAccess(world, colony);
        world.AddSpecializedBuilding(colony, storehouse, SpecializedBuildingKind.Storehouse);
        person.Pos = FindMovableTileAwayFrom(world, colony, access, minDistance: 3);
        person.Home.Stock[Resource.Food] = 10;
        var before = person.Pos;

        world.Update(0.25f);

        Assert.NotEqual(before, person.Pos);
        Assert.True(Distance(person.Pos, access) < Distance(before, access));
    }

    [Fact]
    public void RefillInventoryCommand_FromFixedBrain_RefillsWhenAdjacent()
    {
        var world = CreateEmptyResourceWorld(seed: 6213, command: NpcCommand.RefillInventory);
        var person = world._people[0];
        person.Profession = Profession.Generalist;
        var colony = person.Home;
        var (storehouse, access) = FindStorehouseWithAccess(world, colony);
        world.AddSpecializedBuilding(colony, storehouse, SpecializedBuildingKind.Storehouse);
        person.Pos = access;
        person.Home.Stock[Resource.Food] = 10;

        world.Update(0.25f);

        Assert.Equal(3, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(7, person.Home.Stock[Resource.Food]);
    }

    private static World CreateEmptyResourceWorld(int seed, NpcCommand command = NpcCommand.Idle)
    {
        var world = new World(
            width: 24,
            height: 18,
            initialPop: 8,
            brainFactory: _ => new RuntimeNpcBrain(new FixedBrain(command)),
            randomSeed: seed);

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
                world.GetTile(x, y).ReplaceNode(null);
        }

        return world;
    }

    private static (World World, Colony Colony, (int x, int y) Storehouse, (int x, int y) Access) CreateWorldWithStorehouse(int seed)
    {
        var world = CreateEmptyResourceWorld(seed);
        var colony = world._colonies[0];
        var (storehouse, access) = FindStorehouseWithAccess(world, colony);
        world.AddSpecializedBuilding(colony, storehouse, SpecializedBuildingKind.Storehouse);
        return (world, colony, storehouse, access);
    }

    private static ((int x, int y) Storehouse, (int x, int y) Access) FindStorehouseWithAccess(
        World world,
        Colony owner,
        IReadOnlyCollection<(int x, int y)>? excluded = null)
    {
        excluded ??= Array.Empty<(int x, int y)>();
        for (int radius = 1; radius <= Math.Max(world.Width, world.Height); radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) != radius)
                        continue;

                    var storehouse = (x: owner.Origin.x + dx, y: owner.Origin.y + dy);
                    if (excluded.Contains(storehouse) || !world.CanPlaceStructureAt(storehouse.x, storehouse.y))
                        continue;

                    var access = CardinalNeighbors(storehouse)
                        .Where(tile => !excluded.Contains(tile))
                        .Where(tile => tile.x >= 0 && tile.y >= 0 && tile.x < world.Width && tile.y < world.Height)
                        .Where(tile => !world.IsMovementBlocked(tile.x, tile.y, owner.Id))
                        .OrderBy(tile => tile.x)
                        .ThenBy(tile => tile.y)
                        .FirstOrDefault();

                    if (access != default)
                        return (storehouse, access);
                }
            }
        }

        throw new InvalidOperationException("Could not find storehouse test placement with access tile.");
    }

    private static (int x, int y) FindMovableTileAwayFrom(World world, Colony owner, (int x, int y) target, int minDistance)
    {
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                var pos = (x, y);
                if (Distance(pos, target) >= minDistance && !world.IsMovementBlocked(x, y, owner.Id))
                    return pos;
            }
        }

        throw new InvalidOperationException("Could not find movable test tile.");
    }

    private static (int x, int y) ComputeExpectedAccessTile(World world, Colony owner, (int x, int y) from)
        => world.SpecializedBuildings
            .Where(building => building.Kind == SpecializedBuildingKind.Storehouse && ReferenceEquals(building.Owner, owner))
            .SelectMany(building => CardinalNeighbors(building.Pos)
                .Where(tile => !world.IsMovementBlocked(tile.x, tile.y, owner.Id))
                .Select(tile => new
                {
                    Storehouse = building.Pos,
                    AccessTile = tile,
                    Distance = Distance(from, tile)
                }))
            .OrderBy(entry => entry.Distance)
            .ThenBy(entry => entry.Storehouse.x)
            .ThenBy(entry => entry.Storehouse.y)
            .ThenBy(entry => entry.AccessTile.x)
            .ThenBy(entry => entry.AccessTile.y)
            .First()
            .AccessTile;

    private static IEnumerable<(int x, int y)> CardinalNeighbors((int x, int y) pos)
    {
        yield return (pos.x - 1, pos.y);
        yield return (pos.x + 1, pos.y);
        yield return (pos.x, pos.y - 1);
        yield return (pos.x, pos.y + 1);
    }

    private static int Distance((int x, int y) left, (int x, int y) right)
        => Math.Abs(left.x - right.x) + Math.Abs(left.y - right.y);

    private sealed class FixedBrain : INpcDecisionBrain
    {
        private readonly NpcCommand _command;

        public FixedBrain(NpcCommand command)
        {
            _command = command;
        }

        public AiDecisionResult Think(in NpcAiContext context)
        {
            var trace = new AiDecisionTrace(
                SelectedGoal: "Fixed",
                PlannerName: "Fixed",
                PolicyName: "Test",
                PlanLength: 1,
                PlanPreview: new[] { _command },
                PlanCost: 1,
                ReplanReason: "Fixed",
                MethodName: "FixedMethod",
                GoalScores: Array.Empty<GoalScoreEntry>());
            return new AiDecisionResult(_command, trace);
        }
    }
}
