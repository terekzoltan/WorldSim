using System.Linq;
using WorldSim.Simulation;
using WorldSim.Simulation.Effects;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class GoalBiasEngineTests
{
    [Fact]
    public void RegisterBiases_DecaysAndExpires()
    {
        var engine = new GoalBiasEngine();
        engine.RegisterBiases(
            sourceId: "directive-food",
            colonyId: 1,
            biases: new[] { new GoalBiasSpec("farming", 0.4) },
            durationTicks: 4,
            dampeningFactor: 1.0);

        Assert.Equal(0.4, engine.GetEffectiveBias(1, "farming"), 6);

        engine.Tick();
        Assert.Equal(0.3, engine.GetEffectiveBias(1, "farming"), 6);

        engine.Tick();
        engine.Tick();
        engine.Tick();
        Assert.Equal(0d, engine.GetEffectiveBias(1, "farming"), 6);
    }

    [Fact]
    public void ReplaceDirective_UsesBlendWindow()
    {
        var engine = new GoalBiasEngine();
        engine.RegisterBiases("a", 2, new[] { new GoalBiasSpec("farming", 0.5) }, 20, 1.0);

        // force non-max effective, so blend transition is observable
        engine.Tick();
        engine.Tick();

        var before = engine.GetEffectiveBias(2, "farming");
        engine.ReplaceDirective("b", 2, new[] { new GoalBiasSpec("farming", 0.1) }, 20, 1.0);

        var immediateAfterReplace = engine.GetEffectiveBias(2, "farming");
        Assert.Equal(before, immediateAfterReplace, 6);

        engine.Tick();
        var duringBlend = engine.GetEffectiveBias(2, "farming");
        Assert.True(duringBlend < before);

        for (int i = 0; i < 8; i++)
            engine.Tick();

        var settled = engine.GetEffectiveBias(2, "farming");
        Assert.True(settled < duringBlend);
    }

    [Fact]
    public void PriorityThreshold_WorksAtPointTwentyFive()
    {
        var engine = new GoalBiasEngine();
        engine.RegisterBiases("prio", 3, new[] { new GoalBiasSpec("gathering", 0.25) }, 10, 1.0);

        Assert.True(engine.IsJobPriorityActive(3, "gathering"));

        engine.Tick();
        Assert.False(engine.IsJobPriorityActive(3, "gathering"));
    }

    [Fact]
    public void World_GoalBiasAccessors_AreWired()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 77);
        int colonyId = world._colonies[0].Id;

        world.RegisterGoalBiases(
            sourceId: "dir-1",
            colonyId: colonyId,
            biases: new[] { new GoalBiasSpec("crafting", 0.3) },
            durationTicks: 12,
            dampeningFactor: 1.0);

        Assert.True(world.GetEffectiveGoalBias(colonyId, "crafting") > 0d);
        Assert.True(world.IsGoalPriorityActive(colonyId, "crafting"));
        Assert.NotEmpty(world.GetActiveGoalBiases(colonyId));
    }

    [Fact]
    public void World_RebalanceProfessions_RespectsBuildingPriorityBias()
    {
        var baselineWorld = new World(width: 28, height: 18, initialPop: 20, randomSeed: 91);
        var biasedWorld = new World(width: 28, height: 18, initialPop: 20, randomSeed: 91);
        var baselineColony = baselineWorld._colonies[0];
        var biasedColony = biasedWorld._colonies[0];

        foreach (var person in baselineWorld._people.Where(p => p.Home == baselineColony))
        {
            person.Profession = Profession.Farmer;
            person.Current = Job.Idle;
        }

        foreach (var person in biasedWorld._people.Where(p => p.Home == biasedColony))
        {
            person.Profession = Profession.Farmer;
            person.Current = Job.Idle;
        }

        biasedWorld.RegisterGoalBiases(
            sourceId: "dir-build",
            colonyId: biasedColony.Id,
            biases: new[] { new GoalBiasSpec(WorldSim.AI.GoalBiasCategories.Building, 0.4) },
            durationTicks: 20,
            dampeningFactor: 1.0);

        baselineWorld.Update(12.1f);
        baselineWorld.Update(12.1f);
        baselineWorld.Update(12.1f);
        biasedWorld.Update(12.1f);
        biasedWorld.Update(12.1f);
        biasedWorld.Update(12.1f);

        var baselineBuilders = baselineWorld._people.Count(
            p => p.Home == baselineColony && p.Profession == Profession.Builder && p.Health > 0f);
        var biasedBuilders = biasedWorld._people.Count(
            p => p.Home == biasedColony && p.Profession == Profession.Builder && p.Health > 0f);

        Assert.True(biasedBuilders > baselineBuilders);
    }
}
