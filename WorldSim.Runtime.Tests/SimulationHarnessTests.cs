using System.Collections.Generic;
using System.IO;
using System.Linq;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class SimulationHarnessTests
{
    [Fact]
    public void SimulationRuntime_WithFixedSeed_ProducesStableAiTraceSequence()
    {
        var first = RunAiTrace(seed: 1337, ticks: 120);
        var second = RunAiTrace(seed: 1337, ticks: 120);

        Assert.Equal(first.Count, second.Count);
        Assert.DoesNotContain("NoData", first);
        Assert.DoesNotContain("NoData", second);
    }

    [Fact]
    public void HeadlessSmoke_1000Ticks_MaintainsBasicInvariants()
    {
        var world = CreateWorld(seed: 4242);

        for (var i = 0; i < 1000; i++)
            world.Update(0.25f);

        Assert.True(world._colonies.Any(colony => world._people.Any(person => person.Home == colony && person.Health > 0f)), "At least one colony should remain viable.");
        Assert.All(world._colonies, colony =>
        {
            Assert.True(colony.Stock[Resource.Food] >= 0);
            Assert.True(colony.Stock[Resource.Wood] >= 0);
            Assert.True(colony.Stock[Resource.Stone] >= 0);
            Assert.True(colony.Stock[Resource.Iron] >= 0);
            Assert.True(colony.Stock[Resource.Gold] >= 0);
        });
    }

    private static List<string> RunAiTrace(int seed, int ticks)
    {
        var world = CreateWorld(seed);
        var trace = new List<string>(ticks);
        for (var i = 0; i < ticks; i++)
        {
            world.Update(0.25f);
            var latest = world._people
                .Select(person => person.LastAiDecision)
                .Where(decision => decision != null)
                .Select(decision => decision!)
                .OrderByDescending(decision => decision.Sequence)
                .FirstOrDefault();
            trace.Add(latest != null
                ? $"{latest.Trace.PlannerName}|{latest.Trace.PolicyName}|{latest.Trace.SelectedGoal}|{latest.Job}|{latest.Trace.ReplanReason}|{latest.Trace.MethodName}"
                : "NoData");
        }

        return trace;
    }

    private static World CreateWorld(int seed)
    {
        var options = new RuntimeAiOptions
        {
            PlannerMode = NpcPlannerMode.Goap,
            PolicyMode = NpcPolicyMode.GlobalPlanner
        };

        var world = new World(
            width: 24,
            height: 16,
            initialPop: 8,
            brainFactory: colony => new RuntimeNpcBrain(options.ResolveFactionPlanner(colony.Faction), $"Harness:{options.PlannerMode}"),
            randomSeed: seed);
        world.BirthRateMultiplier = 0f;
        return world;
    }
}
