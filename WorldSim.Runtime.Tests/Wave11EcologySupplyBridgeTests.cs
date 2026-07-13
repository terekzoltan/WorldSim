using System.Reflection;
using WorldSim.AI;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave11EcologySupplyBridgeTests
{
    [Fact]
    public void HumanGatherFood_ReportsFinalPlantFoodProducedAmount()
    {
        var world = CreateControlledWorld(seed: 11401);
        var colony = world._colonies[0];
        var pos = FindLandTile(world);
        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 5));
        var person = Person.Spawn(colony, pos, new RuntimeNpcBrain(new FixedBrain(NpcCommand.GatherFood)), world.CreateEntityRng(), world.AllocatePersonId());
        person.Current = Job.GatherFood;
        SetDoingJob(person, 1);
        world._people.Add(person);

        person.Update(world, dt: 0f, births: new List<Person>());

        var expected = ComputeGatherAmount(person, world, Resource.Food, world.FoodYield);
        var counters = world.BuildEcologySupplyCounters();
        Assert.Equal(expected, colony.Stock[Resource.Food]);
        Assert.Equal(expected, counters.PlantFoodProduced);
        Assert.Equal(0, counters.PlantFoodConsumedByAnimals);
        Assert.Equal(0, counters.MeatFoodProduced);
        Assert.Equal(0, counters.MeatFromHunt);
    }

    [Fact]
    public void HerbivoreGrazing_ReportsAnimalPlantConsumptionOnly()
    {
        var world = CreateControlledWorld(seed: 11402);
        var pos = FindLandTile(world);
        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 3));
        world._animals.Add(new Herbivore(pos, world.CreateEntityRng(), energy: 40f, reproductionCooldown: 30f));

        world.Update(1f);

        var counters = world.BuildEcologySupplyCounters();
        Assert.Equal(1, counters.PlantFoodConsumedByAnimals);
        Assert.Equal(0, counters.PlantFoodProduced);
        Assert.Equal(0, counters.MeatFoodProduced);
        Assert.Equal(0, counters.MeatFromHunt);
    }

    [Fact]
    public void PredatorHerbivoreCapture_ReportsMeatFromHuntButNotSupplyMeat()
    {
        var world = CreateControlledWorld(seed: 11403);
        var pos = FindLandTile(world);
        var predator = new Predator(pos, new AlwaysCaptureRandom(), energy: 80f, reproductionCooldown: 30f);
        var prey = new Herbivore(pos, world.CreateEntityRng(), energy: 50f, reproductionCooldown: 30f);
        world._animals.Add(predator);
        world._animals.Add(prey);

        world.Update(0f);
        world.Update(0f);

        var counters = world.BuildEcologySupplyCounters();
        Assert.False(prey.IsAlive);
        Assert.Equal(1, counters.MeatFromHunt);
        Assert.Equal(0, counters.MeatFoodProduced);
        Assert.Equal(0, counters.PlantFoodProduced);
    }

    [Fact]
    public void HumanHunt_ReportsSupplyFacingMeatFoodProduced()
    {
        var world = CreateControlledWorld(seed: 11404);
        var colony = world._colonies[0];
        var pos = FindLandTile(world);
        var person = Person.Spawn(colony, pos, new RuntimeNpcBrain(new FixedBrain(NpcCommand.GatherFood)), world.CreateEntityRng(), world.AllocatePersonId());
        person.Profession = Profession.Hunter;
        person.Current = Job.GatherFood;
        SetDoingJob(person, 1);
        world._people.Add(person);
        world._animals.Add(new Herbivore(pos, world.CreateEntityRng(), energy: 50f, reproductionCooldown: 30f));

        person.Update(world, dt: 0f, births: new List<Person>());

        var counters = world.BuildEcologySupplyCounters();
        Assert.True(colony.Stock[Resource.Food] > 0);
        Assert.Equal(colony.Stock[Resource.Food], counters.MeatFoodProduced);
        Assert.Equal(0, counters.MeatFromHunt);
        Assert.Equal(0, counters.PlantFoodProduced);
    }

    [Fact]
    public void EmergencyRescue_DoesNotReportSupplyProductionOrHuntMeat()
    {
        var world = CreateControlledWorld(seed: 11405);
        world.EmergencyRescuePolicy = EmergencyRescuePolicy.Enabled;
        world.AnimalReplenishmentChancePerSecond = 1f;
        world.PredatorReplenishmentChance = 1f;
        world._animals.Clear();

        InvokeEmergencyRescue(world, 1f);

        var counters = world.BuildEcologySupplyCounters();
        Assert.True(world.BuildEcologyLifecycleCounters().EmergencyRescues > 0);
        Assert.Equal(0, counters.PlantFoodProduced);
        Assert.Equal(0, counters.MeatFoodProduced);
        Assert.Equal(0, counters.PlantFoodConsumedByAnimals);
        Assert.Equal(0, counters.MeatFromHunt);
    }

    [Fact]
    public void SupplyBridgeSkippedByNoBiomass_CountsOnlyExplicitNoBiomassHarvestFailure()
    {
        var world = CreateControlledWorld(seed: 11406);
        var pos = FindLandTile(world);
        world.GetTile(pos.x, pos.y).ReplaceNode(null);

        Assert.False(world.TryHarvestPlantFoodForSupply(pos, 1));
        var afterExplicitFailure = world.BuildEcologySupplyCounters();
        Assert.Equal(1, afterExplicitFailure.SupplyBridgeSkippedByNoBiomass);

        var person = Person.Spawn(world._colonies[0], pos, new RuntimeNpcBrain(new FixedBrain(NpcCommand.Idle)), world.CreateEntityRng(), world.AllocatePersonId());
        person.Current = Job.Idle;
        world._people.Add(person);
        person.Update(world, dt: 0f, births: new List<Person>());

        Assert.Equal(1, world.BuildEcologySupplyCounters().SupplyBridgeSkippedByNoBiomass);
    }

    [Fact]
    public void TryHarvestPlantFoodForSupply_WrongResource_DoesNotReportNoBiomassSkip()
    {
        var world = CreateControlledWorld(seed: 11409);
        var pos = FindLandTile(world);
        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Wood, amount: 3));

        Assert.False(world.TryHarvestPlantFoodForSupply(pos, 1));
        Assert.Equal(0, world.BuildEcologySupplyCounters().SupplyBridgeSkippedByNoBiomass);
        Assert.Equal(Resource.Wood, world.GetTile(pos.x, pos.y).Node?.Type);
        Assert.Equal(3, world.GetTile(pos.x, pos.y).Node?.Amount);
    }

    [Fact]
    public void TryHarvestPlantFoodForSupply_DepletedFood_ReportsNoBiomassSkip()
    {
        var world = CreateControlledWorld(seed: 11410);
        var pos = FindLandTile(world);
        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 0));

        Assert.False(world.TryHarvestPlantFoodForSupply(pos, 1));
        Assert.Equal(1, world.BuildEcologySupplyCounters().SupplyBridgeSkippedByNoBiomass);
    }

    [Fact]
    public void TryHarvestPlantFoodForSupply_OutOfBounds_DoesNotReportNoBiomassSkip()
    {
        var world = CreateControlledWorld(seed: 11411);

        Assert.False(world.TryHarvestPlantFoodForSupply((-1, 0), 1));
        Assert.Equal(0, world.BuildEcologySupplyCounters().SupplyBridgeSkippedByNoBiomass);
    }

    [Fact]
    public void TryHarvestPlantFoodForSupply_NonPositiveQuantity_DoesNotReportNoBiomassSkip()
    {
        var world = CreateControlledWorld(seed: 11412);
        var pos = FindLandTile(world);
        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 3));

        Assert.False(world.TryHarvestPlantFoodForSupply(pos, 0));
        Assert.Equal(0, world.BuildEcologySupplyCounters().SupplyBridgeSkippedByNoBiomass);
        Assert.Equal(3, world.GetTile(pos.x, pos.y).Node?.Amount);
    }

    [Fact]
    public void CampaignForaging_ReportsExplicitSupplyFacingPlantProductionAndNoBiomassSkip()
    {
        var world = CreateControlledWorld(seed: 11407);
        var colony = world._colonies[0];
        var pos = FindLandTile(world);
        var forager = Person.Spawn(colony, pos, new RuntimeNpcBrain(new FixedBrain(NpcCommand.Idle)), world.CreateEntityRng(), world.AllocatePersonId());
        forager.Health = 100f;
        var rationPool = new ArmyRationPoolState();
        var state = new ArmyForagingState();
        var options = new ArmyForagingOptions(MaxFoodPerAttempt: 2, MaxFoodPerConsumer: 8, MaxSourceDistance: 1);

        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 3));
        var success = ArmyForagingModel.TryForageToRationPool(world, forager, rationPool, state, pos.x, pos.y, "army:1", options);

        Assert.Equal(ArmyForageStatus.Succeeded, success.Status);
        Assert.Equal(2, world.BuildEcologySupplyCounters().PlantFoodProduced);
        Assert.Equal(0, world.BuildEcologySupplyCounters().SupplyBridgeSkippedByNoBiomass);

        var emptyPos = FindAnotherLandTile(world, pos);
        world.GetTile(emptyPos.x, emptyPos.y).ReplaceNode(null);
        var failed = ArmyForagingModel.TryForageToRationPool(world, forager, rationPool, state, emptyPos.x, emptyPos.y, "army:2", options);

        Assert.Equal(ArmyForageStatus.Failed, failed.Status);
        Assert.Equal(ArmyForageFailureReason.NoResourceNode, failed.FailureReason);
        Assert.Equal(1, world.BuildEcologySupplyCounters().SupplyBridgeSkippedByNoBiomass);
    }

    [Theory]
    [MemberData(nameof(ArmyForageFailureReasonNoBiomassPolicyCases))]
    public void ArmyForageFailureReason_NoBiomassSkipPolicy_IsExplicit(ArmyForageFailureReason reason, bool expected)
    {
        Assert.Equal(expected, ArmyForagingModel.CountsAsNoBiomassSupplySkip(reason));
    }

    [Fact]
    public void CampaignForageFailureReasons_ReportNoBiomassSkipOnlyForPlantSourceFailures()
    {
        var world = CreateControlledWorld(seed: 11408);
        var colony = world._colonies[0];
        var pos = FindLandTile(world);
        var forager = Person.Spawn(colony, pos, new RuntimeNpcBrain(new FixedBrain(NpcCommand.Idle)), world.CreateEntityRng(), world.AllocatePersonId());
        forager.Health = 100f;
        var rationPool = new ArmyRationPoolState();
        var options = new ArmyForagingOptions(MaxFoodPerAttempt: 2, MaxFoodPerConsumer: 8, MaxSourceDistance: 1);

        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Wood, amount: 3));
        var wrongResource = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            rationPool,
            new ArmyForagingState(),
            pos.x,
            pos.y,
            "army:wrong",
            options);

        Assert.Equal(ArmyForageFailureReason.WrongResource, wrongResource.FailureReason);
        Assert.Equal(0, world.BuildEcologySupplyCounters().SupplyBridgeSkippedByNoBiomass);

        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 0));
        var depleted = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            rationPool,
            new ArmyForagingState(),
            pos.x,
            pos.y,
            "army:depleted",
            options);

        Assert.Equal(ArmyForageFailureReason.DepletedFood, depleted.FailureReason);
        Assert.Equal(1, world.BuildEcologySupplyCounters().SupplyBridgeSkippedByNoBiomass);

        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 5));
        var capReached = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            rationPool,
            new ArmyForagingState(),
            pos.x,
            pos.y,
            "army:cap",
            options with { MaxFoodPerConsumer = 0 });

        Assert.Equal(ArmyForageFailureReason.ConsumerCapReached, capReached.FailureReason);
        Assert.Equal(1, world.BuildEcologySupplyCounters().SupplyBridgeSkippedByNoBiomass);
    }

    static World CreateControlledWorld(int seed)
    {
        var world = new World(width: 16, height: 16, initialPop: 0, randomSeed: seed)
        {
            EmergencyRescuePolicy = EmergencyRescuePolicy.Disabled
        };
        ResetGround(world, Ground.Dirt);
        world._animals.Clear();
        world._people.Clear();
        foreach (var colony in world._colonies)
            colony.Stock[Resource.Food] = 0;
        return world;
    }

    public static IEnumerable<object[]> ArmyForageFailureReasonNoBiomassPolicyCases()
    {
        foreach (var reason in Enum.GetValues<ArmyForageFailureReason>())
        {
            var expected = reason is ArmyForageFailureReason.NoResourceNode
                or ArmyForageFailureReason.DepletedFood
                or ArmyForageFailureReason.HarvestFailed;
            yield return new object[] { reason, expected };
        }
    }

    static void ResetGround(World world, Ground ground)
    {
        var map = new Tile[world.Width, world.Height];
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
                map[x, y] = new Tile(ground);
        }

        typeof(World).GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(world, map);
        typeof(World).GetField("_ecologyState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            world,
            EcologyState.Create(map, world.Width, world.Height));
    }

    static int ComputeGatherAmount(Person person, World world, Resource resource, int baseYield)
    {
        var method = typeof(Person).GetMethod("GetGatherAmount", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (int)method!.Invoke(person, new object[] { world, resource, baseYield })!;
    }

    static void SetDoingJob(Person person, int value)
        => typeof(Person).GetField("_doingJob", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(person, value);

    static void InvokeEmergencyRescue(World world, float dt)
    {
        var method = typeof(World).GetMethod("UpdateEmergencyAnimalRescue", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(world, new object[] { dt });
    }

    static (int x, int y) FindLandTile(World world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetTile(x, y).Ground != Ground.Water)
                    return (x, y);
            }
        }

        throw new InvalidOperationException("Expected land tile.");
    }

    static (int x, int y) FindAnotherLandTile(World world, (int x, int y) excluded)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if ((x, y) != excluded && world.GetTile(x, y).Ground != Ground.Water)
                    return (x, y);
            }
        }

        throw new InvalidOperationException("Expected second land tile.");
    }

    sealed class AlwaysCaptureRandom : Random
    {
        public override double NextDouble() => 0.0;
    }

    sealed class FixedBrain : INpcDecisionBrain
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
                PolicyName: "Fixed",
                PlanLength: 1,
                PlanPreview: new[] { _command },
                PlanCost: 1,
                ReplanReason: "Fixed",
                MethodName: "Fixed",
                GoalScores: Array.Empty<GoalScoreEntry>());
            return new AiDecisionResult(_command, trace);
        }
    }
}
