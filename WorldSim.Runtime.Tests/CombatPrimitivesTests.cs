using System;
using System.Linq;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Combat;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class CombatPrimitivesTests
{
    [Fact]
    public void DamageModel_StrengthAndDefense_AffectDamageAsExpected()
    {
        var weak = CombatResolver.CalculateDamage(baseDamage: 8f, strength: 3, defense: 0, randomFactor: 1f);
        var strong = CombatResolver.CalculateDamage(baseDamage: 8f, strength: 20, defense: 0, randomFactor: 1f);
        var armored = CombatResolver.CalculateDamage(baseDamage: 8f, strength: 20, defense: 50, randomFactor: 1f);
        var clamped = CombatResolver.CalculateDamage(baseDamage: 1f, strength: 0, defense: 1000, randomFactor: 0.1f);

        Assert.True(strong > weak);
        Assert.True(armored < strong);
        Assert.True(clamped >= 1f);
    }

    [Fact]
    public void ApplyCombatDamage_SetsCombatDeathReason()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 101);
        var person = world._people[0];

        person.ApplyCombatDamage(world, amount: 999f, source: "UnitTest");

        Assert.True(person.Health <= 0f);
        Assert.Equal(PersonDeathReason.Combat, person.LastDeathReason);
    }

    [Fact]
    public void WorldUpdate_PreservesCombatDeathReason_ForDeferredRemoval()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 111);
        var person = world._people[0];

        person.ApplyCombatDamage(world, amount: 999f, source: "UnitTest");

        world.Update(0.25f);

        Assert.Equal(1, world.TotalCombatDeaths);
        Assert.Equal(1, world.TotalDeathsOther);
        Assert.DoesNotContain(person, world._people);
    }

    [Fact]
    public void WorldUpdate_PreservesPredatorDeathReason_ForDeferredRemoval()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 112);
        var person = world._people[0];

        person.ApplyDamage(amount: 999f, source: "Predator");

        world.Update(0.25f);

        Assert.Equal(1, world.TotalDeathsPredator);
        Assert.Equal(0, world.TotalCombatDeaths);
        Assert.DoesNotContain(person, world._people);
    }

    [Fact]
    public void PredatorHumanAttacks_On_ProducesStableCombatCounters()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 222)
        {
            EnablePredatorHumanAttacks = true
        };

        world._animals.Clear();
        var predator = new Predator((10, 10), new Random(17));
        world._animals.Add(predator);

        var primary = world._people.OrderByDescending(p => p.Strength).First();
        primary.Pos = (10, 10);
        primary.Strength = 20;
        primary.Defense = 40;
        primary.Health = 300f;

        foreach (var person in world._people.Where(p => p != primary))
            person.Pos = (0, 0);

        for (int i = 0; i < 120; i++)
            world.Update(0.25f);

        Assert.True(world.TotalPredatorHumanHits > 0);
        Assert.True(world.TotalPredatorKillsByHumans >= 0);
        Assert.True(world.TotalCombatEngagements >= 0);
        Assert.True(primary.LastCombatTick >= 0);
    }

    [Fact]
    public void PredatorHumanAttacks_Off_DisablesBothDirections()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 223)
        {
            EnablePredatorHumanAttacks = false,
            EnableCombatPrimitives = true
        };

        world._animals.Clear();
        var predator = new Predator((10, 10), new Random(18));
        world._animals.Add(predator);

        var fighter = world._people.OrderByDescending(p => p.Strength).First();
        fighter.Pos = (10, 10);
        fighter.Current = Job.Fight;
        fighter.Health = 300f;

        foreach (var person in world._people.Where(p => p != fighter))
            person.Pos = (0, 0);

        var startHealth = fighter.Health;
        for (int i = 0; i < 80; i++)
            world.Update(0.25f);

        Assert.Equal(0, world.TotalPredatorHumanHits);
        Assert.Equal(0, world.TotalPredatorKillsByHumans);
        Assert.True(fighter.Health >= startHealth - 0.001f);
    }

    [Fact]
    public void ThreatResponse_AdjacentEnemies_TriggersFightOrFlee()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 42)
        {
            EnableCombatPrimitives = true
        };

        // Remove all animals so only person-vs-person threat can trigger response
        world._animals.Clear();

        // Pick one actor from colony 0 and one enemy from colony 1
        var actor = world._people.First(p => p.Home == world._colonies[0]);
        var enemy = world._people.First(p => p.Home == world._colonies[1]);

        // Place them adjacent (Manhattan dist = 1, well within radius 4)
        actor.Pos = (6, 6);
        enemy.Pos = (7, 6);

        // Ensure actor is in Idle state before ticking (force _doingJob = 0 via a fresh reset tick)
        // Run a single update tick - TryHandleThreatResponse fires inside the Idle block
        world.Update(0.25f);

        Assert.True(
            actor.Current == Job.Fight || actor.Current == Job.Flee,
            $"Expected actor to Fight or Flee but got {actor.Current}");
    }

    [Fact]
    public void SnapshotBuilder_PopulatesPersonCombatFields()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 303)
        {
            EnablePredatorHumanAttacks = true
        };

        world._animals.Clear();
        world._animals.Add(new Predator((8, 8), new Random(29)));

        var target = world._people[0];
        target.Pos = (8, 8);
        target.Health = 120f;
        foreach (var person in world._people.Skip(1))
            person.Pos = (0, 0);

        for (int i = 0; i < 12; i++)
            world.Update(0.25f);

        var snapshot = WorldSnapshotBuilder.Build(world);
        var renderPerson = snapshot.People.First(p => p.ColonyId == target.Home.Id && p.X == target.Pos.x && p.Y == target.Pos.y);

        Assert.Equal(target.Health, renderPerson.Health, 3);
        Assert.Equal(target.IsInCombat, renderPerson.IsInCombat);
        Assert.Equal(target.LastCombatTick, renderPerson.LastCombatTick);
    }
}
