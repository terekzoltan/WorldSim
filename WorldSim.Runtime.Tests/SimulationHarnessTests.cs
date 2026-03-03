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
        Assert.Equal(first, second);
        Assert.DoesNotContain("NoData", first);
        Assert.DoesNotContain("NoData", second);
    }

    [Fact]
    public void HeadlessSmoke_1000Ticks_MaintainsBasicInvariants()
    {
        var first = RunHeadlessSmoke(seed: 4242, ticks: 1000);
        var second = RunHeadlessSmoke(seed: 4242, ticks: 1000);

        Assert.Equal(first, second);
    }

    [Fact]
    public void HeadlessSmoke_WithCombat_IsDeterministic()
    {
        static World BuildCombatWorld(int seed)
        {
            var world = CreateWorld(seed);
            world.EnableCombatPrimitives = true;
            world.EnablePredatorHumanAttacks = true;
            return world;
        }

        var first  = BuildCombatWorld(seed: 1337);
        var second = BuildCombatWorld(seed: 1337);

        for (var i = 0; i < 120; i++)
        {
            first.Update(0.25f);
            second.Update(0.25f);
        }

        Assert.Equal(first._people.Count, second._people.Count);
        Assert.Equal(first.TotalPredatorHumanHits, second.TotalPredatorHumanHits);
        Assert.Equal(first.TotalPredatorKillsByHumans, second.TotalPredatorKillsByHumans);
        Assert.Equal(first.TotalCombatEngagements, second.TotalCombatEngagements);
    }

    private static HeadlessSmokeSnapshot RunHeadlessSmoke(int seed, int ticks)
    {
        var world = CreateWorld(seed);
        world.EnablePredatorHumanAttacks = true;
        world.EnableCombatPrimitives = true;

        for (var i = 0; i < ticks; i++)
            world.Update(0.25f);

        Assert.True(world._colonies.Any(colony => world._people.Any(person => person.Home == colony && person.Health > 0f)), "At least one colony should remain viable.");
        Assert.True(world._people.Count(p => p.Health > 0f) > 0, "At least one person must be alive after 1000 ticks.");

        Assert.All(world._colonies, colony =>
        {
            Assert.True(colony.Stock[Resource.Food] >= 0);
            Assert.True(colony.Stock[Resource.Wood] >= 0);
            Assert.True(colony.Stock[Resource.Stone] >= 0);
            Assert.True(colony.Stock[Resource.Iron] >= 0);
            Assert.True(colony.Stock[Resource.Gold] >= 0);
        });

        Assert.All(world._people, person =>
        {
            Assert.False(float.IsNaN(person.Health), $"NaN Health detected on person at {person.Pos}");
            Assert.False(float.IsInfinity(person.Health), $"Infinity Health detected on person at {person.Pos}");
        });

        var foodSignature = string.Join(",",
            world._colonies
                .OrderBy(colony => colony.Id)
                .Select(colony => $"{colony.Id}:{colony.Stock[Resource.Food]}:{colony.Stock[Resource.Wood]}:{colony.Stock[Resource.Stone]}:{colony.Stock[Resource.Iron]}:{colony.Stock[Resource.Gold]}"));

        var populationSignature = string.Join(",",
            world._colonies
                .OrderBy(colony => colony.Id)
                .Select(colony => $"{colony.Id}:{world._people.Count(person => person.Home == colony && person.Health > 0f)}"));

        return new HeadlessSmokeSnapshot(
            AlivePeople: world._people.Count(person => person.Health > 0f),
            PredatorHits: world.TotalPredatorHumanHits,
            PredatorKillsByHumans: world.TotalPredatorKillsByHumans,
            CombatEngagements: world.TotalCombatEngagements,
            FoodSignature: foodSignature,
            PopulationSignature: populationSignature);
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

    private readonly record struct HeadlessSmokeSnapshot(
        int AlivePeople,
        int PredatorHits,
        int PredatorKillsByHumans,
        int CombatEngagements,
        string FoodSignature,
        string PopulationSignature);
}
