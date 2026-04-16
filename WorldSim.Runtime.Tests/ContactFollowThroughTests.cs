using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Diplomacy;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class ContactFollowThroughTests
{
    [Fact]
    public void FightAction_PursuesRecentHostile_BeforeFallingBackHome()
    {
        var world = CreateCombatWorld(seed: 9301);
        var fighter = world._people.First(person => person.Home == world._colonies[0]);
        var enemy = world._people.First(person => person.Home == world._colonies[1]);

        fighter.Profession = Profession.Hunter;
        fighter.Pos = (10, 10);
        enemy.Pos = (12, 10);
        FreezeNonParticipants(world, fighter, enemy);

        SetJobTicks(fighter, Job.Fight, 1);
        var beforeFirst = fighter.Pos;
        world.Update(0.25f);

        Assert.True(fighter.Pos.x > beforeFirst.x, $"Expected initial chase toward hostile. before={beforeFirst} after={fighter.Pos}");

        enemy.Pos = (18, 10);
        SetJobTicks(fighter, Job.Fight, 1);
        var beforeSecond = fighter.Pos;
        world.Update(0.25f);

        Assert.True(fighter.Pos.x > beforeSecond.x, $"Expected pursue of recent hostile contact. before={beforeSecond} after={fighter.Pos}");
    }

    [Fact]
    public void RaidBorder_ConvertsToFight_WhenHostileActorNearby()
    {
        var world = CreateCombatWorld(seed: 9302);
        var raider = world._people.First(person => person.Home == world._colonies[0]);
        var enemy = world._people.First(person => person.Home == world._colonies[1]);

        raider.Profession = Profession.Hunter;
        raider.Pos = (10, 10);
        enemy.Pos = (12, 10);
        FreezeNonParticipants(world, raider, enemy);

        SetJobTicks(raider, Job.RaidBorder, 1);
        world.Update(0.25f);

        Assert.Equal(Job.Fight, raider.Current);
        Assert.True(raider.Pos.x > 10, $"Expected raider to step toward hostile actor, got {raider.Pos}.");
    }

    [Fact]
    public void CombatGroups_PairAcrossExtendedDistance_WhenCombatIntentActive()
    {
        var world = CreateCombatWorld(seed: 9303);
        var teamA = world._people.Where(person => person.Home == world._colonies[0]).Take(3).ToList();
        var teamB = world._people.Where(person => person.Home == world._colonies[1]).Take(3).ToList();

        var teamAPositions = new[] { (10, 10), (10, 11), (11, 10) };
        var teamBPositions = new[] { (17, 10), (17, 11), (18, 10) };
        for (var i = 0; i < teamA.Count; i++)
        {
            teamA[i].Pos = teamAPositions[i];
            teamA[i].Health = 220f;
            teamA[i].Profession = Profession.Hunter;
            SetJobTicks(teamA[i], Job.Fight, 8);
        }

        for (var i = 0; i < teamB.Count; i++)
        {
            teamB[i].Pos = teamBPositions[i];
            teamB[i].Health = 220f;
            teamB[i].Profession = Profession.Hunter;
            SetJobTicks(teamB[i], Job.Fight, 8);
        }

        FreezeNonParticipants(world, teamA.Concat(teamB).ToArray());
        world.Update(0.25f);

        var snapshot = WorldSnapshotBuilder.Build(world);
        Assert.NotEmpty(snapshot.Battles);
        Assert.True(world.TotalBattleTicks > 0, "Expected combat groups with follow-through intent to pair into a battle.");
    }

    [Fact]
    public void LargeTopology_FightAction_PursuesHostileAtExtendedDistance()
    {
        var world = CreateCombatWorld(width: 192, height: 108, initialPop: 24, seed: 9304);
        var fighter = world._people.First(person => person.Home == world._colonies[0]);
        var enemy = world._people.First(person => person.Home == world._colonies[1]);

        fighter.Profession = Profession.Hunter;
        fighter.Pos = (10, 10);
        enemy.Pos = (18, 10);
        FreezeNonParticipants(world, fighter, enemy);

        SetJobTicks(fighter, Job.Fight, 1);
        SetJobTicks(enemy, Job.Rest, 50);
        world.Update(0.25f);

        var telemetry = world.BuildScenarioContactTelemetrySnapshot();
        Assert.Equal(1, telemetry.HostileSensed);
        Assert.Equal(1, telemetry.PursueStarts);
    }

    [Fact]
    public void MediumTopology_FightAction_DoesNotUseExtendedChaseRadius()
    {
        var world = CreateCombatWorld(width: 128, height: 72, initialPop: 24, seed: 9305);
        var fighter = world._people.First(person => person.Home == world._colonies[0]);
        var enemy = world._people.First(person => person.Home == world._colonies[1]);

        fighter.Profession = Profession.Hunter;
        fighter.Pos = (10, 10);
        enemy.Pos = (18, 10);
        FreezeNonParticipants(world, fighter, enemy);

        SetJobTicks(fighter, Job.Fight, 1);
        SetJobTicks(enemy, Job.Rest, 50);
        world.Update(0.25f);

        var telemetry = world.BuildScenarioContactTelemetrySnapshot();
        Assert.Equal(0, telemetry.HostileSensed);
        Assert.Equal(0, telemetry.PursueStarts);
    }

    [Fact]
    public void LargeTopology_PursuesRecentHostileAcrossExtendedMemoryRadius()
    {
        var world = CreateCombatWorld(width: 192, height: 108, initialPop: 24, seed: 9306);
        var fighter = world._people.First(person => person.Home == world._colonies[0]);
        var enemy = world._people.First(person => person.Home == world._colonies[1]);

        fighter.Profession = Profession.Hunter;
        fighter.Pos = (10, 10);
        enemy.Pos = (16, 10);
        FreezeNonParticipants(world, fighter, enemy);

        SetJobTicks(fighter, Job.Fight, 1);
        SetJobTicks(enemy, Job.Rest, 50);
        world.Update(0.25f);

        enemy.Pos = (23, 10);
        SetJobTicks(fighter, Job.Fight, 1);
        SetJobTicks(enemy, Job.Rest, 50);
        var beforeSecond = fighter.Pos;
        world.Update(0.25f);

        Assert.True(fighter.Pos.x > beforeSecond.x, $"Expected pursue of recent hostile across extended large-topology radius. before={beforeSecond} after={fighter.Pos}");
    }

    private static World CreateCombatWorld(int seed)
        => CreateCombatWorld(width: 32, height: 20, initialPop: 24, seed);

    private static World CreateCombatWorld(int width, int height, int initialPop, int seed)
    {
        var world = new World(width: width, height: height, initialPop: initialPop, randomSeed: seed)
        {
            EnableCombatPrimitives = true,
            EnableDiplomacy = false
        };

        world._animals.Clear();
        world.SetFactionStance(world._colonies[0].Faction, world._colonies[1].Faction, Stance.War);
        return world;
    }

    private static void FreezeNonParticipants(World world, params Person[] participants)
    {
        var keep = participants.ToHashSet();
        foreach (var person in world._people)
        {
            if (keep.Contains(person))
                continue;

            person.Pos = (0, 0);
            SetJobTicks(person, Job.Rest, 50);
        }
    }

    private static void SetJobTicks(Person person, Job job, int ticks)
    {
        person.Current = job;
        var field = typeof(Person).GetField("_doingJob", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(person, ticks);
    }

}
