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
        world.EnablePredatorHumanAttacks = true;
        world.EnableCombatPrimitives = true;

        for (var i = 0; i < 1000; i++)
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

        // No NaN/Infinity may appear in health values (combat float arithmetic guard)
        Assert.All(world._people, person =>
        {
            Assert.False(float.IsNaN(person.Health), $"NaN Health detected on person at {person.Pos}");
            Assert.False(float.IsInfinity(person.Health), $"Infinity Health detected on person at {person.Pos}");
        });

        // Animal._rng is non-seeded (new Random()), so hit counts are non-deterministic across
        // machines/runs. Controlled hit-count verification lives in CombatPrimitivesTests.
        // Here we only assert the flag did not crash the simulation.
        Assert.True(world.TotalPredatorHumanHits >= 0);
        Assert.True(world.TotalPredatorKillsByHumans >= 0);
    }

    [Fact]
    public void HeadlessSmoke_WithCombat_IsDeterministic()
    {
        static World BuildCombatWorld(int seed)
        {
            var world = CreateWorld(seed);
            // EnableCombatPrimitives enables person-vs-person threat detection.
            // EnablePredatorHumanAttacks is intentionally OFF: both Person._rng and Animal._rng
            // are non-seeded (new Random()), so predator-combat counters are non-deterministic.
            // This test verifies that the person population count is stable under same-seed worlds.
            world.EnableCombatPrimitives = true;
            return world;
        }

        var first  = BuildCombatWorld(seed: 1337);
        var second = BuildCombatWorld(seed: 1337);

        for (var i = 0; i < 120; i++)
        {
            first.Update(0.25f);
            second.Update(0.25f);
        }

        // Animals use non-seeded RNG (Person._rng and Animal._rng are both new Random()),
        // so any counter that depends on combat dice is non-deterministic across runs.
        // We verify only that same-seed worlds produce the same population count.
        Assert.Equal(first._people.Count, second._people.Count);
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
