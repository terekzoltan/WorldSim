using System.Reflection;
using WorldSim.Runtime.Diagnostics;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class ScenarioEcologyTelemetryTests
{
    [Fact]
    public void LegacyInitialization_FixedSeedPreservesRosterAndWorldRngCadence()
    {
        var world = new World(16, 16, 8, randomSeed: 42);
        var expectedRoster = new[]
        {
            (AnimalKind.Herbivore, 11, 9),
            (AnimalKind.Herbivore, 14, 6),
            (AnimalKind.Predator, 1, 7),
            (AnimalKind.Predator, 10, 15),
            (AnimalKind.Predator, 4, 11),
            (AnimalKind.Herbivore, 8, 7),
            (AnimalKind.Herbivore, 2, 10),
            (AnimalKind.Herbivore, 7, 8),
            (AnimalKind.Herbivore, 10, 4),
            (AnimalKind.Herbivore, 7, 0)
        };

        var actualRoster = world._animals
            .Select(animal => (animal.Kind, animal.Pos.x, animal.Pos.y))
            .ToArray();

        Assert.Equal(expectedRoster, actualRoster);
        Assert.Equal(313862347, world.CreateEntityRng().Next());
    }

    [Fact]
    public void BuildScenarioInitialEcologyTelemetrySnapshot_IsFrozenDeterministicAndInternallyConsistent()
    {
        var world = new World(16, 16, 8, randomSeed: 0);
        var initial = world.BuildScenarioInitialEcologyTelemetrySnapshot();
        var repeated = world.BuildScenarioInitialEcologyTelemetrySnapshot();

        Assert.Same(initial, repeated);
        Assert.Equal("legacy_random", initial.InitialAnimalPolicy);
        Assert.Equal("runtime_default", initial.InitialAnimalPolicySource);
        Assert.Equal(5, initial.FoodVisionRadius);
        Assert.Equal(6, initial.PreyVisionRadius);
        Assert.Equal(2, initial.HumanHarassRadius);
        Assert.Equal(6, initial.EarlyHumanContactRadius);
        Assert.Equal(initial.TotalAnimals, initial.Herbivores + initial.Predators);
        Assert.Equal(3, initial.AnimalsOnWater);
        Assert.Equal(3, initial.AnimalsOnMovementBlockedTiles);

        var actualHerbivores = world._animals.OfType<Herbivore>().Count(animal => animal.IsAlive);
        var actualPredators = world._animals.OfType<Predator>().Count(animal => animal.IsAlive);
        var actualWater = world._animals.Count(animal =>
            animal.IsAlive && world.GetTile(animal.Pos.x, animal.Pos.y).Ground == Ground.Water);
        var actualBlocked = world._animals.Count(animal =>
            animal.IsAlive && world.IsMovementBlocked(animal.Pos.x, animal.Pos.y, moverColonyId: -1));

        Assert.Equal(actualHerbivores, initial.Herbivores);
        Assert.Equal(actualPredators, initial.Predators);
        Assert.Equal(actualWater, initial.AnimalsOnWater);
        Assert.Equal(actualBlocked, initial.AnimalsOnMovementBlockedTiles);
        Assert.NotNull(initial.PredatorHerbivoreRatio);
        Assert.InRange(
            Math.Abs(initial.PredatorHerbivoreRatio!.Value - actualPredators / (double)actualHerbivores),
            0d,
            0.000000000001d);

        var expectedRegions = BuildExpectedInitialRegions(world);
        Assert.Equal(expectedRegions, initial.Regions);
        Assert.Equal(initial.Herbivores, initial.Regions.Sum(region => region.Herbivores));
        Assert.Equal(initial.Predators, initial.Regions.Sum(region => region.Predators));
        Assert.Equal(initial.Regions.OrderBy(region => region.RegionId), initial.Regions);
        var expectedViableRegions = expectedRegions
            .Where(region => region.LandTileCount > 0 && region.PlantCapacityTotal > 0f)
            .ToArray();
        Assert.Equal(expectedViableRegions.Length, initial.ViableRegions);
        Assert.Equal(
            expectedViableRegions.Count(region => region.Herbivores == 0),
            initial.ViableRegionsWithoutHerbivores);
        Assert.Equal(
            expectedRegions.Where(region => region.Herbivores == 0).Sum(region => region.Predators),
            initial.PredatorsInPreyEmptyRegions);

        var livingHerbivorePositions = world._animals
            .OfType<Herbivore>()
            .Where(animal => animal.IsAlive)
            .Select(animal => animal.Pos)
            .ToArray();
        var livingPredatorPositions = world._animals
            .OfType<Predator>()
            .Where(animal => animal.IsAlive)
            .Select(animal => animal.Pos)
            .ToArray();
        var livingPersonPositions = world._people
            .Where(person => person.Health > 0f)
            .Select(person => person.Pos)
            .ToArray();
        var activeFoodPositions = FindActiveFoodPositions(world);

        AssertDistanceSummary(
            initial.HerbivoreToNearestFoodDistance,
            BuildExpectedDistances(livingHerbivorePositions, activeFoodPositions));
        AssertDistanceSummary(
            initial.PredatorToNearestPreyDistance,
            BuildExpectedDistances(livingPredatorPositions, livingHerbivorePositions));
        AssertDistanceSummary(
            initial.PredatorToNearestPersonDistance,
            BuildExpectedDistances(livingPredatorPositions, livingPersonPositions));
        Assert.Equal(
            CountWithinRadius(livingHerbivorePositions, activeFoodPositions, initial.FoodVisionRadius),
            initial.HerbivoresWithFoodInVision);
        Assert.Equal(
            CountWithinRadius(livingPredatorPositions, livingHerbivorePositions, initial.PreyVisionRadius),
            initial.PredatorsWithPreyInVision);
        Assert.Equal(
            CountWithinRadius(livingPredatorPositions, livingPersonPositions, initial.HumanHarassRadius),
            initial.PredatorsWithinHumanHarassRadius);
        Assert.Equal(
            CountWithinRadius(livingPredatorPositions, livingPersonPositions, initial.EarlyHumanContactRadius),
            initial.PredatorsWithinEarlyHumanContactRadius);

        var sameSeed = new World(16, 16, 8, randomSeed: 0)
            .BuildScenarioInitialEcologyTelemetrySnapshot();
        Assert.Equal(
            initial with { Regions = Array.Empty<ScenarioInitialEcologyRegionSnapshot>() },
            sameSeed with { Regions = Array.Empty<ScenarioInitialEcologyRegionSnapshot>() });
        Assert.Equal(initial.Regions, sameSeed.Regions);

        var regions = Assert.IsAssignableFrom<ICollection<ScenarioInitialEcologyRegionSnapshot>>(initial.Regions);
        Assert.True(regions.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => regions.Add(initial.Regions[0]));

        var food = activeFoodPositions[0];
        Assert.True(world.TryHarvest(food, Resource.Food, 1));
        world._animals.Clear();
        world.Update(0f);

        Assert.Same(initial, world.BuildScenarioInitialEcologyTelemetrySnapshot());
        Assert.Equal(actualHerbivores, initial.Herbivores);
        Assert.Equal(actualPredators, initial.Predators);
        Assert.Equal(expectedRegions, initial.Regions);

        var emptyInitial = ScenarioInitialEcologyTelemetrySnapshot.Empty;
        Assert.Null(emptyInitial.PredatorHerbivoreRatio);
        Assert.Equal(0, emptyInitial.HerbivoreToNearestFoodDistance.SampleCount);
        Assert.Null(emptyInitial.HerbivoreToNearestFoodDistance.Minimum);
        Assert.Null(emptyInitial.HerbivoreToNearestFoodDistance.Maximum);
        Assert.Null(emptyInitial.HerbivoreToNearestFoodDistance.Average);
    }

    [Fact]
    public void BuildScenarioEcologyDistanceSummary_UsesNearestManhattanDistanceAndNullableEmptySemantics()
    {
        var empty = World.BuildScenarioEcologyDistanceSummary(
            new[] { (x: 0, y: 0), (x: 10, y: 10) },
            Array.Empty<(int x, int y)>());

        Assert.Equal(0, empty.SampleCount);
        Assert.Null(empty.Minimum);
        Assert.Null(empty.Maximum);
        Assert.Null(empty.Average);

        var summary = World.BuildScenarioEcologyDistanceSummary(
            new[] { (x: 0, y: 0), (x: 10, y: 10) },
            new[] { (x: 0, y: 0), (x: 2, y: 0) });

        Assert.Equal(2, summary.SampleCount);
        Assert.Equal(0, summary.Minimum);
        Assert.Equal(18, summary.Maximum);
        Assert.Equal(9d, summary.Average);
        Assert.Equal(
            1,
            World.CountScenarioEcologySourcesWithinRadius(
                new[] { (x: 0, y: 0), (x: 3, y: 0) },
                new[] { (x: 1, y: 0) },
                radius: 1));
        Assert.Equal(
            2,
            World.CountScenarioEcologySourcesWithinRadius(
                new[] { (x: 0, y: 0), (x: 2, y: 0) },
                new[] { (x: 1, y: 0) },
                radius: 1));
        Assert.Equal(
            0,
            World.CountScenarioEcologySourcesWithinRadius(
                new[] { (x: 0, y: 0) },
                Array.Empty<(int x, int y)>(),
                radius: 6));
    }

    [Fact]
    public void CaptureInitialEcologyTelemetry_ExcludesDeadAnimalsAndPeopleFromDistanceSamples()
    {
        var world = new World(16, 16, 8, randomSeed: 700);
        var predatorPos = (x: 0, y: 0);
        var livingTargetPos = (x: 7, y: 0);
        world._animals.Clear();
        world._animals.Add(new Herbivore(livingTargetPos, new Random(700)));
        world._animals.Add(new Herbivore(predatorPos, new Random(701)) { IsAlive = false });
        world._animals.Add(new Predator(predatorPos, new Random(702)));
        world._animals.Add(new Predator(livingTargetPos, new Random(703)) { IsAlive = false });
        foreach (var person in world._people)
        {
            person.Health = 0f;
            person.Pos = predatorPos;
        }
        world._people[0].Health = 100f;
        world._people[0].Pos = livingTargetPos;

        var snapshot = CaptureInitialEcologyTelemetryForTest(world);

        Assert.Equal(2, snapshot.TotalAnimals);
        Assert.Equal(1, snapshot.Herbivores);
        Assert.Equal(1, snapshot.Predators);
        Assert.Equal(1, snapshot.PredatorToNearestPreyDistance.SampleCount);
        Assert.Equal(7, snapshot.PredatorToNearestPreyDistance.Minimum);
        Assert.Equal(1, snapshot.PredatorToNearestPersonDistance.SampleCount);
        Assert.Equal(7, snapshot.PredatorToNearestPersonDistance.Minimum);
        Assert.Equal(0, snapshot.PredatorsWithPreyInVision);
        Assert.Equal(0, snapshot.PredatorsWithinHumanHarassRadius);
        Assert.Equal(0, snapshot.PredatorsWithinEarlyHumanContactRadius);
    }

    [Fact]
    public void FirstEcologyEventTicks_ArePositiveAmountOnlyFirstWriteAndTimelineMapped()
    {
        var world = new World(16, 16, 8, randomSeed: 701);
        world._people.Clear();
        world._animals.Clear();

        var before = world.BuildScenarioEcologyTelemetrySnapshot();
        AssertAllFirstEventTicksNull(before);

        world.ReportPlantFoodConsumedByAnimals(0);
        world.ReportPlantFoodConsumedByAnimals(-1);
        world.ReportMeatFromHunt(0);
        world.ReportMeatFromHunt(-1);
        world.ReportPredatorHumanHit();
        world.ReportPredatorDeath();

        var tickZero = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(0, tickZero.PlantFoodConsumedByAnimals);
        Assert.Equal(0, tickZero.MeatFromHunt);
        Assert.Null(tickZero.FirstHerbivoreGrazingTick);
        Assert.Null(tickZero.FirstPredatorHuntTick);
        Assert.Equal(0, tickZero.FirstPredatorHumanContactTick);
        Assert.Equal(0, tickZero.FirstPredatorDeathTick);

        world.Update(0f);
        world.ReportPlantFoodConsumedByAnimals(3);
        world.ReportMeatFromHunt(2);

        var firstPositive = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(3, firstPositive.PlantFoodConsumedByAnimals);
        Assert.Equal(2, firstPositive.MeatFromHunt);
        Assert.Equal(1, firstPositive.FirstHerbivoreGrazingTick);
        Assert.Equal(1, firstPositive.FirstPredatorHuntTick);

        world.Update(0f);
        world.ReportPlantFoodConsumedByAnimals(5);
        world.ReportMeatFromHunt(4);
        world.ReportPredatorHumanHit();
        world.ReportPredatorDeath();

        var later = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(8, later.PlantFoodConsumedByAnimals);
        Assert.Equal(6, later.MeatFromHunt);
        Assert.Equal(1, later.FirstHerbivoreGrazingTick);
        Assert.Equal(1, later.FirstPredatorHuntTick);
        Assert.Equal(0, later.FirstPredatorHumanContactTick);
        Assert.Equal(0, later.FirstPredatorDeathTick);

        var timeline = later.ToTimelineSnapshot();
        Assert.Equal(0, timeline.FirstPredatorHumanContactTick);
        Assert.Equal(1, timeline.FirstPredatorHuntTick);
        Assert.Equal(1, timeline.FirstHerbivoreGrazingTick);
        Assert.Equal(0, timeline.FirstPredatorDeathTick);
        Assert.Null(timeline.FirstHerbivoreDeathTick);
        Assert.Null(timeline.FirstPredatorBirthTick);
        Assert.Null(timeline.FirstHerbivoreBirthTick);
    }

    [Fact]
    public void BirthAndHerbivoreDeathTicks_UseQueueAcceptanceAndRemovalObservation()
    {
        var herbivoreWorld = new World(32, 32, 8, randomSeed: 702);
        herbivoreWorld._animals.Clear();
        var herbivorePos = FindViableLandPosition(herbivoreWorld);
        var rejectedParent = new Herbivore(herbivorePos, new Random(702)) { IsAlive = false };
        Assert.False(herbivoreWorld.QueueHerbivoreBirth(rejectedParent));
        Assert.Null(herbivoreWorld.BuildScenarioEcologyTelemetrySnapshot().FirstHerbivoreBirthTick);

        var herbivoreParent = new Herbivore(herbivorePos, new Random(703));
        herbivoreWorld._animals.Add(herbivoreParent);
        Assert.True(herbivoreWorld.QueueHerbivoreBirth(herbivoreParent));
        Assert.Equal(0, herbivoreWorld.BuildScenarioEcologyTelemetrySnapshot().FirstHerbivoreBirthTick);
        SetCurrentTick(herbivoreWorld, 1);
        var secondHerbivorePos = FindViableLandPositionOutsideRegion(
            herbivoreWorld,
            herbivoreWorld.GetEcologyTileState(herbivorePos.x, herbivorePos.y).RegionId);
        var secondHerbivoreParent = new Herbivore(secondHerbivorePos, new Random(704));
        herbivoreWorld._animals.Add(secondHerbivoreParent);
        Assert.True(herbivoreWorld.QueueHerbivoreBirth(secondHerbivoreParent));
        Assert.Equal(0, herbivoreWorld.BuildScenarioEcologyTelemetrySnapshot().FirstHerbivoreBirthTick);

        var predatorWorld = new World(32, 32, 8, randomSeed: 705);
        predatorWorld._animals.Clear();
        var predatorPos = FindViableLandPosition(predatorWorld);
        for (var i = 0; i < 8; i++)
            predatorWorld._animals.Add(new Herbivore(predatorPos, new Random(710 + i)));
        var rejectedPredatorParent = new Predator(predatorPos, new Random(719)) { IsAlive = false };
        Assert.False(predatorWorld.QueuePredatorBirth(rejectedPredatorParent));
        Assert.Null(predatorWorld.BuildScenarioEcologyTelemetrySnapshot().FirstPredatorBirthTick);
        var predatorParent = new Predator(predatorPos, new Random(720));
        predatorWorld._animals.Add(predatorParent);

        Assert.True(predatorWorld.QueuePredatorBirth(predatorParent));
        Assert.Equal(0, predatorWorld.BuildScenarioEcologyTelemetrySnapshot().FirstPredatorBirthTick);
        SetCurrentTick(predatorWorld, 1);
        var secondPredatorPos = FindViableLandPositionOutsideRegion(
            predatorWorld,
            predatorWorld.GetEcologyTileState(predatorPos.x, predatorPos.y).RegionId);
        for (var i = 0; i < 8; i++)
            predatorWorld._animals.Add(new Herbivore(secondPredatorPos, new Random(730 + i)));
        var secondPredatorParent = new Predator(secondPredatorPos, new Random(740));
        predatorWorld._animals.Add(secondPredatorParent);
        Assert.True(predatorWorld.QueuePredatorBirth(secondPredatorParent));
        Assert.Equal(0, predatorWorld.BuildScenarioEcologyTelemetrySnapshot().FirstPredatorBirthTick);

        var deathWorld = new World(16, 16, 8, randomSeed: 706);
        deathWorld._animals.Clear();
        var deadHerbivore = new Herbivore(FindViableLandPosition(deathWorld), new Random(721)) { IsAlive = false };
        deathWorld._animals.Add(deadHerbivore);

        deathWorld.Update(0f);

        Assert.Equal(1, deathWorld.BuildScenarioEcologyTelemetrySnapshot().FirstHerbivoreDeathTick);
        deathWorld._animals.Add(
            new Herbivore(FindViableLandPosition(deathWorld), new Random(741)) { IsAlive = false });
        deathWorld.Update(0f);
        Assert.Equal(1, deathWorld.BuildScenarioEcologyTelemetrySnapshot().FirstHerbivoreDeathTick);
    }

    [Fact]
    public void BuildScenarioEcologyTelemetrySnapshot_EmptyWorld_ReturnsZeroSnapshot()
    {
        var world = new World(16, 16, 8, randomSeed: 42);
        world._people.Clear();
        world._animals.Clear();

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();

        Assert.Equal(0, telemetry.Herbivores);
        Assert.Equal(0, telemetry.Predators);
        Assert.True(telemetry.ActiveFoodNodes >= 0);
        Assert.True(telemetry.DepletedFoodNodes >= 0);
        Assert.Equal(0, telemetry.HerbivoreReplenishmentSpawns);
        Assert.Equal(0, telemetry.PredatorReplenishmentSpawns);
        Assert.Equal(0, telemetry.EmergencyRescues);
        Assert.Equal("disabled", telemetry.EmergencyRescuePolicy);
        Assert.Equal("none", telemetry.LastEmergencyRescueReason);
        Assert.Equal(0, telemetry.TicksWithZeroHerbivores);
        Assert.Equal(0, telemetry.TicksWithZeroPredators);
        Assert.Null(telemetry.FirstZeroHerbivoreTick);
        Assert.Null(telemetry.FirstZeroPredatorTick);
        Assert.Equal(0, telemetry.PredatorDeaths);
        Assert.Equal(0, telemetry.PredatorHumanHits);
        Assert.Null(telemetry.FirstPredatorHumanContactTick);
        Assert.Null(telemetry.FirstPredatorHuntTick);
        Assert.Null(telemetry.FirstHerbivoreGrazingTick);
        Assert.Null(telemetry.FirstPredatorDeathTick);
        Assert.Null(telemetry.FirstHerbivoreDeathTick);
        Assert.Null(telemetry.FirstPredatorBirthTick);
        Assert.Null(telemetry.FirstHerbivoreBirthTick);
    }

    [Fact]
    public void ZeroSpeciesCounters_TrackWorldTicks_PreReplenishment()
    {
        var world = new World(16, 16, 8, randomSeed: 84);
        world._animals.Clear();

        world.Update(0f);
        world.Update(0f);
        world.Update(0f);

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(3, telemetry.TicksWithZeroHerbivores);
        Assert.Equal(3, telemetry.TicksWithZeroPredators);
        Assert.Equal(1, telemetry.FirstZeroHerbivoreTick);
        Assert.Equal(1, telemetry.FirstZeroPredatorTick);
    }

    [Fact]
    public void ReplenishmentCounters_IncrementOnlyOnReplenishmentSpawn()
    {
        var world = new World(16, 16, 8, randomSeed: 91);
        world._animals.Clear();

        world.EmergencyRescuePolicy = EmergencyRescuePolicy.Enabled;
        var method = typeof(World).GetMethod("UpdateEmergencyAnimalRescue", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(world, new object[] { 200f });
        var afterSpawn = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(1, afterSpawn.HerbivoreReplenishmentSpawns);
        Assert.Equal(0, afterSpawn.PredatorReplenishmentSpawns);
        Assert.Equal(1, afterSpawn.EmergencyRescues);
        Assert.Equal("enabled", afterSpawn.EmergencyRescuePolicy);
        Assert.Equal("herbivore_floor", afterSpawn.LastEmergencyRescueReason);

        method.Invoke(world, new object[] { 0f });
        var afterNoCheck = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(1, afterNoCheck.HerbivoreReplenishmentSpawns);
        Assert.Equal(0, afterNoCheck.PredatorReplenishmentSpawns);
    }

    [Fact]
    public void EcologyFoodCounts_MatchSnapshotEcologySemantics()
    {
        var world = new World(24, 16, 10, randomSeed: 137);
        DepleteSingleFoodNode(world);

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();
        var snapshot = WorldSnapshotBuilder.Build(world);

        Assert.Equal(snapshot.Ecology.ActiveFoodNodes, telemetry.ActiveFoodNodes);
        Assert.Equal(snapshot.Ecology.DepletedFoodNodes, telemetry.DepletedFoodNodes);
    }

    [Fact]
    public void ToTimelineSnapshot_MapsEcologyFields()
    {
        var snapshot = new WorldSim.Runtime.Diagnostics.ScenarioEcologyTelemetrySnapshot(
            Herbivores: 4,
            Predators: 2,
            ActiveFoodNodes: 18,
            DepletedFoodNodes: 3,
            HerbivoreReplenishmentSpawns: 5,
            PredatorReplenishmentSpawns: 1,
            EmergencyRescues: 3,
            PlantFoodProduced: 13,
            MeatFoodProduced: 2,
            PlantFoodConsumedByAnimals: 8,
            MeatFromHunt: 4,
            SupplyBridgeSkippedByNoBiomass: 6,
            EmergencyRescuePolicy: "enabled",
            LastEmergencyRescueReason: "predator_extinct_with_prey",
            TicksWithZeroHerbivores: 2,
            TicksWithZeroPredators: 7,
            FirstZeroHerbivoreTick: 11,
            FirstZeroPredatorTick: 6,
            PredatorDeaths: 9,
            PredatorHumanHits: 12,
            FirstPredatorHumanContactTick: 3,
            FirstPredatorHuntTick: 4,
            FirstHerbivoreGrazingTick: 5,
            FirstPredatorDeathTick: 6,
            FirstHerbivoreDeathTick: 7,
            FirstPredatorBirthTick: 8,
            FirstHerbivoreBirthTick: 9);

        var timeline = snapshot.ToTimelineSnapshot();

        Assert.Equal(4, timeline.Herbivores);
        Assert.Equal(2, timeline.Predators);
        Assert.Equal(18, timeline.ActiveFoodNodes);
        Assert.Equal(3, timeline.DepletedFoodNodes);
        Assert.Equal(5, timeline.HerbivoreReplenishmentSpawns);
        Assert.Equal(1, timeline.PredatorReplenishmentSpawns);
        Assert.Equal(3, timeline.EmergencyRescues);
        Assert.Equal(13, timeline.PlantFoodProduced);
        Assert.Equal(2, timeline.MeatFoodProduced);
        Assert.Equal(8, timeline.PlantFoodConsumedByAnimals);
        Assert.Equal(4, timeline.MeatFromHunt);
        Assert.Equal(6, timeline.SupplyBridgeSkippedByNoBiomass);
        Assert.Equal("enabled", timeline.EmergencyRescuePolicy);
        Assert.Equal("predator_extinct_with_prey", timeline.LastEmergencyRescueReason);
        Assert.Equal(2, timeline.TicksWithZeroHerbivores);
        Assert.Equal(7, timeline.TicksWithZeroPredators);
        Assert.Equal(3, timeline.FirstPredatorHumanContactTick);
        Assert.Equal(4, timeline.FirstPredatorHuntTick);
        Assert.Equal(5, timeline.FirstHerbivoreGrazingTick);
        Assert.Equal(6, timeline.FirstPredatorDeathTick);
        Assert.Equal(7, timeline.FirstHerbivoreDeathTick);
        Assert.Equal(8, timeline.FirstPredatorBirthTick);
        Assert.Equal(9, timeline.FirstHerbivoreBirthTick);
    }

    [Fact]
    public void EcologyBalanceDefaults_AreTunedPw8B2Values()
    {
        var world = new World(16, 16, 8, randomSeed: 181);

        Assert.Equal(World.DefaultAnimalReplenishmentChancePerSecond, world.AnimalReplenishmentChancePerSecond);
        Assert.Equal(World.DefaultPredatorReplenishmentChance, world.PredatorReplenishmentChance);
        Assert.Equal(World.DefaultFoodRegrowthMinSeconds, world.FoodRegrowthMinSeconds);
        Assert.Equal(World.DefaultFoodRegrowthJitterSeconds, world.FoodRegrowthJitterSeconds);
        Assert.Equal(EmergencyRescuePolicy.Disabled, world.EmergencyRescuePolicy);

        var balance = world.BuildScenarioEcologyBalanceSnapshot();
        Assert.Equal(0.04f, balance.AnimalReplenishmentChancePerSecond);
        Assert.Equal(1.0f, balance.PredatorReplenishmentChance);
        Assert.Equal(18f, balance.FoodRegrowthMinSeconds);
        Assert.Equal(18f, balance.FoodRegrowthJitterSeconds);
        Assert.Equal("disabled", balance.EmergencyRescuePolicy);
    }

    [Fact]
    public void EcologyBalanceKnobs_ClampInvalidValues()
    {
        var world = new World(16, 16, 8, randomSeed: 182)
        {
            AnimalReplenishmentChancePerSecond = 3f,
            PredatorReplenishmentChance = -1f,
            FoodRegrowthMinSeconds = float.NaN,
            FoodRegrowthJitterSeconds = float.PositiveInfinity
        };

        Assert.Equal(1f, world.AnimalReplenishmentChancePerSecond);
        Assert.Equal(0f, world.PredatorReplenishmentChance);
        Assert.True(world.FoodRegrowthMinSeconds > 0f);
        Assert.Equal(3600f, world.FoodRegrowthJitterSeconds);
    }

    [Fact]
    public void ConfiguredReplenishmentChanceAndPredatorChance_CanSpawnPredatorWhenEligible()
    {
        var world = new World(16, 16, 8, randomSeed: 183)
        {
            AnimalReplenishmentChancePerSecond = 1f,
            PredatorReplenishmentChance = 1f,
            EmergencyRescuePolicy = EmergencyRescuePolicy.Enabled
        };
        world._animals.Clear();
        for (var i = 0; i < 8; i++)
            world._animals.Add(new Herbivore((i, 1), new Random(200 + i)));

        var method = typeof(World).GetMethod("UpdateEmergencyAnimalRescue", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(world, new object[] { 1f });

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(1, telemetry.PredatorReplenishmentSpawns);
        Assert.Equal(1, telemetry.Predators);
    }

    [Fact]
    public void ReplenishmentTick_CanRunHerbivoreAndPredatorBranchesTogether()
    {
        var world = new World(16, 16, 8, randomSeed: 185)
        {
            AnimalReplenishmentChancePerSecond = 1f,
            PredatorReplenishmentChance = 1f,
            EmergencyRescuePolicy = EmergencyRescuePolicy.Enabled
        };
        world._animals.Clear();
        for (var i = 0; i < 7; i++)
            world._animals.Add(new Herbivore((i, 2), new Random(300 + i)));

        var method = typeof(World).GetMethod("UpdateEmergencyAnimalRescue", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(world, new object[] { 1f });

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(1, telemetry.HerbivoreReplenishmentSpawns);
        Assert.Equal(1, telemetry.PredatorReplenishmentSpawns);
        Assert.Equal(8, telemetry.Herbivores);
        Assert.Equal(1, telemetry.Predators);
    }

    [Fact]
    public void PredatorRescue_CanRunBelowHerbivoreFloor_WhenPredatorsAreExtinctAndPreyExists()
    {
        var world = new World(16, 16, 8, randomSeed: 186)
        {
            AnimalReplenishmentChancePerSecond = 1f,
            PredatorReplenishmentChance = 1f,
            EmergencyRescuePolicy = EmergencyRescuePolicy.Enabled
        };
        world._animals.Clear();
        for (var i = 0; i < 4; i++)
            world._animals.Add(new Herbivore((i, 3), new Random(400 + i)));

        var method = typeof(World).GetMethod("UpdateEmergencyAnimalRescue", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(world, new object[] { 1f });

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(1, telemetry.HerbivoreReplenishmentSpawns);
        Assert.Equal(1, telemetry.PredatorReplenishmentSpawns);
        Assert.Equal(5, telemetry.Herbivores);
        Assert.Equal(1, telemetry.Predators);
    }

    [Fact]
    public void ConfiguredFoodRegrowthDelay_IsHonored()
    {
        var world = new World(24, 16, 10, randomSeed: 184)
        {
            FoodRegrowthMinSeconds = 0.1f,
            FoodRegrowthJitterSeconds = 0f
        };
        var pos = DepleteSingleFoodNode(world);

        InvokeFoodRegrowthTick(world, 0.05f);
        Assert.Equal(0, world.GetTile(pos.x, pos.y).Node?.Amount ?? 0);

        InvokeFoodRegrowthTick(world, 0.05f);
        Assert.True(world.GetTile(pos.x, pos.y).Node?.Amount > 0);
    }

    private static (int x, int y) DepleteSingleFoodNode(World world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var tile = world.GetTile(x, y);
                if (tile.Node is { Type: Resource.Food, Amount: > 0 })
                {
                    var amount = tile.Node.Amount;
                    Assert.True(world.TryHarvest((x, y), Resource.Food, amount));
                    return (x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No harvestable food tile found for depletion path.");
    }

    private static void InvokeFoodRegrowthTick(World world, float dt)
    {
        var method = typeof(World).GetMethod("UpdateFoodRegrowth", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(world, new object[] { dt });
    }

    private static IReadOnlyList<ScenarioInitialEcologyRegionSnapshot> BuildExpectedInitialRegions(World world)
    {
        var activeFoodByRegion = FindActiveFoodPositions(world)
            .GroupBy(pos => world.GetEcologyTileState(pos.x, pos.y).RegionId)
            .ToDictionary(group => group.Key, group => group.Count());

        return world.BuildEcologyRegionSnapshots()
            .OrderBy(region => region.RegionId)
            .Select(region => new ScenarioInitialEcologyRegionSnapshot(
                region.RegionId,
                region.LandTileCount,
                region.PlantCapacityTotal,
                activeFoodByRegion.GetValueOrDefault(region.RegionId),
                region.HerbivoreCount,
                region.PredatorCount))
            .ToArray();
    }

    private static (int x, int y)[] FindActiveFoodPositions(World world)
    {
        var positions = new List<(int x, int y)>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (World.IsActiveFoodNode(world.GetTile(x, y).Node))
                    positions.Add((x, y));
            }
        }

        return positions.ToArray();
    }

    private static int[] BuildExpectedDistances(
        IReadOnlyList<(int x, int y)> sources,
        IReadOnlyList<(int x, int y)> targets)
    {
        if (targets.Count == 0)
            return Array.Empty<int>();

        return sources
            .Select(source => targets.Min(target => Manhattan(source, target)))
            .ToArray();
    }

    private static int CountWithinRadius(
        IReadOnlyList<(int x, int y)> sources,
        IReadOnlyList<(int x, int y)> targets,
        int radius)
        => targets.Count == 0
            ? 0
            : sources.Count(source => targets.Any(target => Manhattan(source, target) <= radius));

    private static int Manhattan((int x, int y) left, (int x, int y) right)
        => Math.Abs(left.x - right.x) + Math.Abs(left.y - right.y);

    private static void AssertDistanceSummary(
        ScenarioEcologyDistanceSummarySnapshot actual,
        IReadOnlyList<int> expectedDistances)
    {
        Assert.Equal(expectedDistances.Count, actual.SampleCount);
        if (expectedDistances.Count == 0)
        {
            Assert.Null(actual.Minimum);
            Assert.Null(actual.Maximum);
            Assert.Null(actual.Average);
            return;
        }

        Assert.Equal(expectedDistances.Min(), actual.Minimum);
        Assert.Equal(expectedDistances.Max(), actual.Maximum);
        Assert.Equal(expectedDistances.Average(), actual.Average);
    }

    private static (int x, int y) FindViableLandPosition(World world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetTile(x, y).Ground != Ground.Water
                    && world.GetEcologyTileState(x, y).PlantCapacity > 0f)
                {
                    return (x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No viable land position found.");
    }

    private static (int x, int y) FindViableLandPositionOutsideRegion(World world, int excludedRegionId)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var ecology = world.GetEcologyTileState(x, y);
                if (ecology.RegionId != excludedRegionId
                    && ecology.PlantCapacity > 0f
                    && world.GetTile(x, y).Ground != Ground.Water)
                {
                    return (x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No viable land position found outside the excluded region.");
    }

    private static ScenarioInitialEcologyTelemetrySnapshot CaptureInitialEcologyTelemetryForTest(World world)
    {
        var method = typeof(World).GetMethod(
            "CaptureInitialEcologyTelemetry",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<ScenarioInitialEcologyTelemetrySnapshot>(method!.Invoke(world, null));
    }

    private static void SetCurrentTick(World world, int tick)
    {
        var field = typeof(World).GetField("_tickCounter", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(world, tick);
    }

    private static void AssertAllFirstEventTicksNull(ScenarioEcologyTelemetrySnapshot snapshot)
    {
        Assert.Null(snapshot.FirstPredatorHumanContactTick);
        Assert.Null(snapshot.FirstPredatorHuntTick);
        Assert.Null(snapshot.FirstHerbivoreGrazingTick);
        Assert.Null(snapshot.FirstPredatorDeathTick);
        Assert.Null(snapshot.FirstHerbivoreDeathTick);
        Assert.Null(snapshot.FirstPredatorBirthTick);
        Assert.Null(snapshot.FirstHerbivoreBirthTick);
    }
}
