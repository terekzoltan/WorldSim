using System;
using System.IO;
using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave8SupplyTechTests
{
    [Fact]
    public void TechnologyFile_ContainsWave8SupplyTechs()
    {
        TechTree.Load(GetTechPath());

        var backpacks = TechTree.Techs.First(tech => tech.Id == "backpacks");
        var rationing = TechTree.Techs.First(tech => tech.Id == "rationing");

        Assert.Equal("inventory_capacity_bonus", backpacks.Effect);
        Assert.Contains("logistics", backpacks.Prerequisites);
        Assert.Equal("supply_efficiency", rationing.Effect);
        Assert.Contains("backpacks", rationing.Prerequisites);
    }

    [Fact]
    public void BackpacksUnlock_IncreasesExistingPersonInventoryCapacity()
    {
        TechTree.Load(GetTechPath());
        var world = CreateSupplyWorld(seed: 6401);
        var colony = world._colonies[0];
        var person = world._people.First(p => p.Home == colony);

        Assert.Equal(3, person.Inventory.CapacitySlots);

        var unlock = TechTree.TryUnlock("backpacks", world, colony);

        Assert.True(unlock.Success);
        Assert.Equal(2, colony.InventoryCapacityBonusSlots);
        Assert.Equal(5, person.Inventory.CapacitySlots);
        Assert.Equal(2, person.Inventory.CapacityBonusSlots);
    }

    [Fact]
    public void BackpacksUnlock_OnlyAffectsUnlockedColonyPeople()
    {
        TechTree.Load(GetTechPath());
        var world = CreateSupplyWorld(seed: 6410);
        var colony = world._colonies[0];
        var otherColony = world._colonies[1];
        var colonyPerson = world._people.First(p => p.Home == colony);
        var otherPerson = Person.Spawn(
            otherColony,
            otherColony.Origin,
            new RuntimeNpcBrain(new FixedBrain(NpcCommand.Idle)),
            new Random(64101));
        world._people.Add(otherPerson);

        Assert.True(TechTree.TryUnlock("backpacks", world, colony).Success);
        var futureOtherPerson = Person.Spawn(
            otherColony,
            otherColony.Origin,
            new RuntimeNpcBrain(new FixedBrain(NpcCommand.Idle)),
            new Random(64102));

        Assert.Equal(5, colonyPerson.Inventory.CapacitySlots);
        Assert.Equal(3, otherPerson.Inventory.CapacitySlots);
        Assert.Equal(3, futureOtherPerson.Inventory.CapacitySlots);
        Assert.Equal(2, colony.InventoryCapacityBonusSlots);
        Assert.Equal(0, otherColony.InventoryCapacityBonusSlots);
    }

    [Fact]
    public void BackpacksUnlock_IsIdempotentAndDoesNotStack()
    {
        TechTree.Load(GetTechPath());
        var world = CreateSupplyWorld(seed: 6402);
        var colony = world._colonies[0];
        var person = world._people.First(p => p.Home == colony);

        Assert.True(TechTree.TryUnlock("backpacks", world, colony).Success);
        var repeat = TechTree.TryUnlock("backpacks", world, colony);

        Assert.False(repeat.Success);
        Assert.Equal(2, colony.InventoryCapacityBonusSlots);
        Assert.Equal(5, person.Inventory.CapacitySlots);
    }

    [Fact]
    public void BackpacksUnlock_AppliesToFutureSpawnedPerson()
    {
        TechTree.Load(GetTechPath());
        var world = CreateSupplyWorld(seed: 6403);
        var colony = world._colonies[0];
        Assert.True(TechTree.TryUnlock("backpacks", world, colony).Success);

        var spawned = Person.Spawn(colony, colony.Origin, new RuntimeNpcBrain(new FixedBrain(NpcCommand.Idle)), new Random(64031));

        Assert.Equal(2, spawned.Inventory.CapacityBonusSlots);
        Assert.Equal(5, spawned.Inventory.CapacitySlots);
    }

    [Fact]
    public void BackpacksUnlock_IncreasesStorehouseRefillCapacityWithoutDuping()
    {
        TechTree.Load(GetTechPath());
        var world = CreateSupplyWorld(seed: 6404);
        var colony = world._colonies[0];
        var person = world._people.First(p => p.Home == colony);
        var (storehouse, access) = FindStorehouseWithAccess(world, colony);
        world.AddSpecializedBuilding(colony, storehouse, SpecializedBuildingKind.Storehouse);
        person.Pos = access;
        colony.Stock[Resource.Food] = 8;
        Assert.True(TechTree.TryUnlock("backpacks", world, colony).Success);
        var before = colony.Stock[Resource.Food] + person.Inventory.GetCount(ItemType.Food);

        Assert.True(person.TryRefillInventoryFromStorehouse(world));

        Assert.Equal(5, person.Inventory.GetCount(ItemType.Food));
        Assert.Equal(before, colony.Stock[Resource.Food] + person.Inventory.GetCount(ItemType.Food));
    }

    [Fact]
    public void RationingUnlock_IncreasesInventoryFoodHungerReduction()
    {
        TechTree.Load(GetTechPath());
        var normal = CreateSupplyWorld(seed: 6405);
        var rationed = CreateSupplyWorld(seed: 6406);
        var normalPerson = normal._people[0];
        var rationedPerson = rationed._people[0];
        normalPerson.Home.Stock[Resource.Food] = 0;
        rationedPerson.Home.Stock[Resource.Food] = 0;
        Assert.True(normalPerson.Inventory.TryAdd(ItemType.Food));
        Assert.True(rationedPerson.Inventory.TryAdd(ItemType.Food));
        normalPerson.Needs["Hunger"] = 96f;
        rationedPerson.Needs["Hunger"] = 96f;
        Assert.True(TechTree.TryUnlock("rationing", rationed, rationedPerson.Home).Success);

        normal.Update(0.25f);
        rationed.Update(0.25f);

        Assert.Equal(1, normal.TotalInventoryFoodConsumed);
        Assert.Equal(1, rationed.TotalInventoryFoodConsumed);
        Assert.True(rationedPerson.Needs["Hunger"] <= normalPerson.Needs["Hunger"] - 6.5f);
    }

    [Fact]
    public void RationingUnlock_DoesNotAffectColonyStockFallback()
    {
        TechTree.Load(GetTechPath());
        var normal = CreateSupplyWorld(seed: 6407);
        var rationed = CreateSupplyWorld(seed: 6408);
        var normalPerson = normal._people[0];
        var rationedPerson = rationed._people[0];
        normalPerson.Home.Stock[Resource.Food] = 1;
        rationedPerson.Home.Stock[Resource.Food] = 1;
        normalPerson.Needs["Hunger"] = 96f;
        rationedPerson.Needs["Hunger"] = 96f;
        Assert.True(TechTree.TryUnlock("rationing", rationed, rationedPerson.Home).Success);

        normal.Update(0.25f);
        rationed.Update(0.25f);

        Assert.Equal(0, normal.TotalInventoryFoodConsumed);
        Assert.Equal(0, rationed.TotalInventoryFoodConsumed);
        Assert.Equal(0, normalPerson.Home.Stock[Resource.Food]);
        Assert.Equal(0, rationedPerson.Home.Stock[Resource.Food]);
        Assert.Equal(normalPerson.Needs["Hunger"], rationedPerson.Needs["Hunger"], precision: 3);
    }

    [Fact]
    public void RationingUnlock_IsIdempotentAndDoesNotStack()
    {
        TechTree.Load(GetTechPath());
        var world = CreateSupplyWorld(seed: 6409);
        var colony = world._colonies[0];

        Assert.True(TechTree.TryUnlock("rationing", world, colony).Success);
        var repeat = TechTree.TryUnlock("rationing", world, colony);

        Assert.False(repeat.Success);
        Assert.Equal(1.25f, colony.InventorySupplyEfficiencyMultiplier, precision: 3);
    }

    private static World CreateSupplyWorld(int seed)
    {
        var world = new World(
            width: 24,
            height: 18,
            initialPop: 1,
            brainFactory: _ => new RuntimeNpcBrain(new FixedBrain(NpcCommand.Idle)),
            randomSeed: seed)
        {
            AllowFreeTechUnlocks = true,
            BirthRateMultiplier = 0f
        };

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
                world.GetTile(x, y).ReplaceNode(null);
        }

        return world;
    }

    private static ((int x, int y) Storehouse, (int x, int y) Access) FindStorehouseWithAccess(World world, Colony colony)
    {
        for (var radius = 1; radius < 8; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var storehouse = (x: colony.Origin.x + dx, y: colony.Origin.y + dy);
                    if (!world.CanPlaceStructureAt(storehouse.x, storehouse.y))
                        continue;

                    var candidates = new[]
                    {
                        (x: storehouse.x + 1, y: storehouse.y),
                        (x: storehouse.x - 1, y: storehouse.y),
                        (x: storehouse.x, y: storehouse.y + 1),
                        (x: storehouse.x, y: storehouse.y - 1)
                    };

                    foreach (var access in candidates)
                    {
                        if (access.x >= 0
                            && access.y >= 0
                            && access.x < world.Width
                            && access.y < world.Height
                            && !world.IsMovementBlocked(access.x, access.y, colony.Id))
                            return (storehouse, access);
                    }
                }
            }
        }

        throw new InvalidOperationException("Could not find storehouse test placement with access tile.");
    }

    private static string GetTechPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var path = Path.Combine(current.FullName, "Tech", "technologies.json");
            if (File.Exists(path))
                return path;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json");
    }

    private sealed class FixedBrain : INpcDecisionBrain
    {
        readonly NpcCommand _command;

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
