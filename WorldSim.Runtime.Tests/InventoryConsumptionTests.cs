using System;
using System.Linq;
using System.Reflection;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class InventoryConsumptionTests
{
    [Fact]
    public void EatFood_ConsumesInventoryBeforeColonyStock()
    {
        var (world, person) = CreateEatingWorld(seed: 6301);
        person.Home.Stock[Resource.Food] = 5;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 1));
        StartEatJob(person, hunger: 50f);

        world.Update(0.25f);

        Assert.Equal(0, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(5, person.Home.Stock[Resource.Food]);
        Assert.Equal(1, world.TotalInventoryFoodConsumed);
        Assert.True(person.Needs["Hunger"] < 50f);
    }

    [Fact]
    public void EatFood_FallsBackToColonyStockWhenInventoryEmpty()
    {
        var (world, person) = CreateEatingWorld(seed: 6302);
        person.Home.Stock[Resource.Food] = 5;
        StartEatJob(person, hunger: 50f);

        world.Update(0.25f);

        Assert.Equal(0, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(4, person.Home.Stock[Resource.Food]);
        Assert.Equal(0, world.TotalInventoryFoodConsumed);
        Assert.True(person.Needs["Hunger"] < 50f);
    }

    [Fact]
    public void EatFood_NoOpsWhenNoInventoryOrColonyFood()
    {
        var (world, person) = CreateEatingWorld(seed: 6303);
        person.Home.Stock[Resource.Food] = 0;
        StartEatJob(person, hunger: 50f);

        world.Update(0.25f);

        Assert.Equal(0, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(0, person.Home.Stock[Resource.Food]);
        Assert.Equal(0, world.TotalInventoryFoodConsumed);
        Assert.True(person.Needs["Hunger"] >= 50f);
    }

    [Fact]
    public void EatFood_DecrementsExactlyOneTotalFoodUnit()
    {
        var (world, person) = CreateEatingWorld(seed: 6304);
        person.Home.Stock[Resource.Food] = 5;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 2));
        StartEatJob(person, hunger: 50f);
        var before = TotalFood(person);

        world.Update(0.25f);

        Assert.Equal(before - 1, TotalFood(person));
    }

    [Fact]
    public void ColonyFallback_DoesNotIncrementInventoryConsumptionCounter()
    {
        var (world, person) = CreateEatingWorld(seed: 6305);
        person.Home.Stock[Resource.Food] = 1;
        StartEatJob(person, hunger: 50f);

        world.Update(0.25f);

        Assert.Equal(0, world.TotalInventoryFoodConsumed);
        Assert.Equal(0, person.Home.Stock[Resource.Food]);
    }

    [Fact]
    public void CriticalHunger_DoesNotUseColonyStockWhenInventoryFoodExists()
    {
        var (world, person) = CreateEatingWorld(seed: 6306);
        person.Home.Stock[Resource.Food] = 5;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 1));
        person.Needs["Hunger"] = 96f;

        world.Update(0.25f);

        Assert.Equal(0, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(5, person.Home.Stock[Resource.Food]);
        Assert.Equal(1, world.TotalInventoryFoodConsumed);
        Assert.True(person.Needs["Hunger"] < 80f);
    }

    [Fact]
    public void CriticalHungerPreemption_ConsumesInventoryBeforeColonyStock()
    {
        var (world, person) = CreateEatingWorld(seed: 6307);
        person.Home.Stock[Resource.Food] = 2;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 1));
        person.Needs["Hunger"] = 99f;
        var before = TotalFood(person);

        world.Update(0.25f);

        Assert.Equal(before - 1, TotalFood(person));
        Assert.Equal(0, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(2, person.Home.Stock[Resource.Food]);
    }

    [Fact]
    public void CriticalHungerPreemption_RescuesLowHealthWithInventoryWhenColonyStockEmpty()
    {
        var (world, person) = CreateEatingWorld(seed: 6308);
        person.Home.Stock[Resource.Food] = 0;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 1));
        person.Needs["Hunger"] = 96f;
        person.Health = 0.1f;

        world.Update(0.25f);

        Assert.True(person.Health > 0.1f);
        Assert.Equal(0, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(1, world.TotalInventoryFoodConsumed);
        Assert.Contains(person, world._people);
    }

    [Fact]
    public void ChildHunger_CanStartEatingWithCarriedFood()
    {
        var (world, person) = CreateEatingWorld(seed: 6309);
        person.Age = 8f;
        person.Home.Stock[Resource.Food] = 0;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 1));
        person.Needs["Hunger"] = 70f;

        world.Update(0.25f);

        Assert.Equal(Job.EatFood, person.Current);
        Assert.Equal(1, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(0, person.Home.Stock[Resource.Food]);
        Assert.Equal(0, world.TotalInventoryFoodConsumed);
    }

    [Fact]
    public void InventoryConsumptionCounter_IncrementsOnlyForInventoryFood()
    {
        var (world, person) = CreateEatingWorld(seed: 6310);
        person.Home.Stock[Resource.Food] = 1;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 1));

        StartEatJob(person, hunger: 50f);
        world.Update(0.25f);
        StartEatJob(person, hunger: 50f);
        world.Update(0.25f);

        Assert.Equal(1, world.TotalInventoryFoodConsumed);
        Assert.Equal(0, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(0, person.Home.Stock[Resource.Food]);
    }

    [Fact]
    public void StorehouseRefillThenEat_ConsumesCarriedFoodWithoutAdditionalColonyStockDraw()
    {
        var (world, person) = CreateEatingWorld(seed: 6311);
        var (storehouse, access) = FindStorehouseWithAccess(world, person.Home);
        world.AddSpecializedBuilding(person.Home, storehouse, SpecializedBuildingKind.Storehouse);
        person.Pos = access;
        person.Home.Stock[Resource.Food] = 4;
        Assert.True(person.TryRefillInventoryFromStorehouse(world));
        Assert.Equal(1, person.Home.Stock[Resource.Food]);

        StartEatJob(person, hunger: 50f);
        world.Update(0.25f);

        Assert.Equal(1, person.Home.Stock[Resource.Food]);
        Assert.Equal(2, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(1, world.TotalInventoryFoodConsumed);
    }

    [Fact]
    public void StarvationWithFoodCounter_CountsCarriedInventoryFood()
    {
        var (world, person) = CreateEatingWorld(seed: 6312);
        person.Home.Stock[Resource.Food] = 0;
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 1));
        person.Needs["Hunger"] = 20f;
        person.Health = -1f;
        SetLastDeathReason(person, PersonDeathReason.Starvation);

        world.Update(0.25f);

        Assert.DoesNotContain(person, world._people);
        Assert.Equal(1, world.TotalDeathsStarvation);
        Assert.Equal(1, world.TotalStarvationDeathsWithFood);
    }

    private static (World World, Person Person) CreateEatingWorld(int seed)
    {
        var world = new World(
            width: 24,
            height: 18,
            initialPop: 1,
            brainFactory: _ => new RuntimeNpcBrain(new FixedBrain(NpcCommand.Idle)),
            randomSeed: seed)
        {
            BirthRateMultiplier = 0f
        };

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
                world.GetTile(x, y).ReplaceNode(null);
        }

        foreach (var colony in world._colonies)
            colony.Stock[Resource.Food] = 0;

        var person = world._people[0];
        person.Age = 30f;
        person.Health = 80f;
        person.StaminaForTest(50f);
        person.Needs["Hunger"] = 20f;
        return (world, person);
    }

    private static void StartEatJob(Person person, float hunger)
    {
        person.Current = Job.EatFood;
        person.Needs["Hunger"] = hunger;
        SetDoingJob(person, 1);
    }

    private static void SetDoingJob(Person person, int ticks)
    {
        typeof(Person)
            .GetField("_doingJob", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(person, ticks);
    }

    private static void SetLastDeathReason(Person person, PersonDeathReason reason)
    {
        typeof(Person)
            .GetProperty(nameof(Person.LastDeathReason), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(person, reason);
    }

    private static int TotalFood(Person person)
        => person.Home.Stock[Resource.Food] + person.Inventory.GetCount(ItemType.Food);

    private static ((int x, int y) Storehouse, (int x, int y) Access) FindStorehouseWithAccess(World world, Colony owner)
    {
        for (int radius = 1; radius <= Math.Max(world.Width, world.Height); radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) != radius)
                        continue;

                    var storehouse = (x: owner.Origin.x + dx, y: owner.Origin.y + dy);
                    if (!world.CanPlaceStructureAt(storehouse.x, storehouse.y))
                        continue;

                    var access = CardinalNeighbors(storehouse)
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

    private static System.Collections.Generic.IEnumerable<(int x, int y)> CardinalNeighbors((int x, int y) pos)
    {
        yield return (pos.x - 1, pos.y);
        yield return (pos.x + 1, pos.y);
        yield return (pos.x, pos.y - 1);
        yield return (pos.x, pos.y + 1);
    }

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

internal static class PersonInventoryConsumptionTestExtensions
{
    public static void StaminaForTest(this Person person, float stamina)
    {
        typeof(Person)
            .GetProperty(nameof(Person.Stamina), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(person, stamina);
    }
}
