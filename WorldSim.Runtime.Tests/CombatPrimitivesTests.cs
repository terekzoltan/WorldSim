using System.Linq;
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
    public void PredatorHumanAttacks_On_ProducesStableCombatCounters()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 222)
        {
            EnablePredatorHumanAttacks = true
        };

        world._animals.Clear();
        var predator = new Predator((10, 10));
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
    }
}
