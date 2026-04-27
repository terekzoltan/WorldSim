using System;
using System.IO;
using System.Linq;
using WorldSim.AI;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave8SupplySnapshotTests
{
    [Fact]
    public void PersonRenderData_DefaultInventoryFields_AreExported()
    {
        var world = CreateSupplyWorld(seed: 6501);
        var person = world._people[0];

        var snapshotPerson = WorldSnapshotBuilder.Build(world).People.First(p => p.ActorId == person.Id);

        Assert.Equal(0, snapshotPerson.InventoryFood);
        Assert.Equal(0, snapshotPerson.InventoryUsedSlots);
        Assert.Equal(3, snapshotPerson.InventoryCapacitySlots);
        Assert.False(snapshotPerson.HasFood);
    }

    [Fact]
    public void PersonRenderData_CarriedFoodAndCapacity_AreExported()
    {
        var world = CreateSupplyWorld(seed: 6502);
        var person = world._people[0];
        person.Inventory.SetCapacityBonusSlots(2);
        Assert.True(person.Inventory.TryAdd(ItemType.Food, 4));

        var snapshotPerson = WorldSnapshotBuilder.Build(world).People.First(p => p.ActorId == person.Id);

        Assert.Equal(4, snapshotPerson.InventoryFood);
        Assert.Equal(4, snapshotPerson.InventoryUsedSlots);
        Assert.Equal(5, snapshotPerson.InventoryCapacitySlots);
        Assert.True(snapshotPerson.HasFood);
    }

    [Fact]
    public void PersonRenderData_HasFoodMeansCarriedInventoryFoodOnly()
    {
        var world = CreateSupplyWorld(seed: 6503);
        var person = world._people[0];
        person.Home.Stock[Resource.Food] = 12;

        var snapshotPerson = WorldSnapshotBuilder.Build(world).People.First(p => p.ActorId == person.Id);

        Assert.Equal(0, snapshotPerson.InventoryFood);
        Assert.False(snapshotPerson.HasFood);
    }

    [Fact]
    public void ColonyHudData_SupplyTechFields_AreExported()
    {
        TechTree.Load(GetTechPath());
        var world = CreateSupplyWorld(seed: 6504);
        var colony = world._colonies[0];
        Assert.True(TechTree.TryUnlock("backpacks", world, colony).Success);
        Assert.True(TechTree.TryUnlock("rationing", world, colony).Success);

        var snapshotColony = WorldSnapshotBuilder.Build(world).Colonies.First(c => c.Id == colony.Id);

        Assert.Equal(2, snapshotColony.InventoryCapacityBonusSlots);
        Assert.Equal(1.25f, snapshotColony.InventorySupplyEfficiencyMultiplier, precision: 3);
    }

    [Fact]
    public void EcoHudData_InventoryFoodConsumed_IsExported()
    {
        var world = CreateSupplyWorld(seed: 6505);
        var person = world._people[0];
        person.Home.Stock[Resource.Food] = 0;
        Assert.True(person.Inventory.TryAdd(ItemType.Food));
        person.Needs["Hunger"] = 96f;

        world.Update(0.25f);
        var snapshot = WorldSnapshotBuilder.Build(world);

        Assert.Equal(1, world.TotalInventoryFoodConsumed);
        Assert.Equal(world.TotalInventoryFoodConsumed, snapshot.Ecology.InventoryFoodConsumed);
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
