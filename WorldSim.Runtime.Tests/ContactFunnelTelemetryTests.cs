using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using WorldSim.Simulation;
using WorldSim.Simulation.Diplomacy;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class ContactFunnelTelemetryTests
{
    [Fact]
    public void BuildScenarioContactTelemetrySnapshot_EmptyWorld_ReturnsEmptySnapshot()
    {
        var world = new World(16, 16, 8, randomSeed: 42);
        world._people.Clear();

        var telemetry = world.BuildScenarioContactTelemetrySnapshot();

        Assert.Equal(0, telemetry.HostileSensed);
        Assert.Equal(0, telemetry.PursueStarts);
        Assert.Equal(0, telemetry.AdjacentContacts);
        Assert.Equal(0, telemetry.FactionCombatDamageEvents);
        Assert.Equal(0, telemetry.FactionCombatDeaths);
        Assert.Equal(0, telemetry.RoutingStarts);
        Assert.Equal(0, telemetry.BattlePairings);
        Assert.Equal(0, telemetry.BattleTicksWithDamage);
        Assert.Equal(0, telemetry.BattleTicksWithDeaths);
        Assert.Equal(0, telemetry.RoutingBeforeDamage);
        Assert.Null(telemetry.FirstHostileSenseTick);
        Assert.Null(telemetry.FirstPursueTick);
        Assert.Null(telemetry.FirstAdjacentContactTick);
        Assert.Null(telemetry.FirstFactionCombatDamageTick);
        Assert.Null(telemetry.FirstFactionCombatDeathTick);
        Assert.Null(telemetry.FirstBattlePairingTick);
        Assert.Null(telemetry.FirstBattleDamageTick);
        Assert.Null(telemetry.FirstBattleDeathTick);
        Assert.Null(telemetry.FirstRoutingTick);
        Assert.Null(telemetry.FirstRoutingBeforeDamageTick);
    }

    [Fact]
    public void FightAction_ReportsHostileSenseAndPursueStart()
    {
        var world = CreateCombatWorld(seed: 9401);
        var fighter = world._people.First(person => person.Home == world._colonies[0]);
        var enemy = world._people.First(person => person.Home == world._colonies[1]);

        fighter.Profession = Profession.Hunter;
        fighter.Pos = (10, 10);
        enemy.Pos = (13, 10);
        FreezeNonParticipants(world, fighter, enemy);
        SetJobTicks(fighter, Job.Fight, 1);
        SetJobTicks(enemy, Job.Rest, 50);

        world.Update(0.25f);

        var telemetry = world.BuildScenarioContactTelemetrySnapshot();
        Assert.Equal(1, telemetry.HostileSensed);
        Assert.Equal(1, telemetry.PursueStarts);
        Assert.Equal(0, telemetry.AdjacentContacts);
        Assert.Equal(1, telemetry.FirstHostileSenseTick);
        Assert.Equal(1, telemetry.FirstPursueTick);
        Assert.Null(telemetry.FirstAdjacentContactTick);
    }

    [Fact]
    public void FightAction_ReportsAdjacentContactAndFactionCombatDamage()
    {
        var world = CreateCombatWorld(seed: 9402);
        var fighter = world._people.First(person => person.Home == world._colonies[0]);
        var enemy = world._people.First(person => person.Home == world._colonies[1]);

        fighter.Profession = Profession.Hunter;
        fighter.Pos = (10, 10);
        fighter.Health = 250f;
        enemy.Pos = (11, 10);
        enemy.Health = 250f;
        FreezeNonParticipants(world, fighter, enemy);
        SetJobTicks(fighter, Job.Fight, 1);
        SetJobTicks(enemy, Job.Rest, 50);

        world.Update(0.25f);

        var telemetry = world.BuildScenarioContactTelemetrySnapshot();
        Assert.Equal(1, telemetry.HostileSensed);
        Assert.Equal(0, telemetry.PursueStarts);
        Assert.Equal(1, telemetry.AdjacentContacts);
        Assert.Equal(1, telemetry.FactionCombatDamageEvents);
        Assert.Equal(0, telemetry.FactionCombatDeaths);
        Assert.Equal(1, telemetry.FirstAdjacentContactTick);
        Assert.Equal(1, telemetry.FirstFactionCombatDamageTick);
        Assert.Null(telemetry.FirstFactionCombatDeathTick);
    }

    [Fact]
    public void RaidBorder_ReportsHostileSensePursueAndAdjacentContact()
    {
        var world = CreateCombatWorld(seed: 9403);
        var raider = world._people.First(person => person.Home == world._colonies[0]);
        var enemy = world._people.First(person => person.Home == world._colonies[1]);

        raider.Pos = (10, 10);
        enemy.Pos = (13, 10);
        FreezeNonParticipants(world, raider, enemy);
        SetJobTicks(raider, Job.RaidBorder, 1);
        SetJobTicks(enemy, Job.Rest, 50);
        world.Update(0.25f);

        enemy.Pos = (11, 10);
        SetJobTicks(raider, Job.RaidBorder, 1);
        SetJobTicks(enemy, Job.Rest, 50);
        world.Update(0.25f);

        var telemetry = world.BuildScenarioContactTelemetrySnapshot();
        Assert.Equal(2, telemetry.HostileSensed);
        Assert.Equal(1, telemetry.PursueStarts);
        Assert.Equal(1, telemetry.AdjacentContacts);
        Assert.Equal(1, telemetry.FirstHostileSenseTick);
        Assert.Equal(1, telemetry.FirstPursueTick);
        Assert.Equal(2, telemetry.FirstAdjacentContactTick);
    }

    [Fact]
    public void GroupCombatPhase_ReportsBattlePairingDamageAndDeath()
    {
        var world = CreateCombatWorld(seed: 9404);
        var left = world._people.First(person => person.Home == world._colonies[0]);
        var right = world._people.First(person => person.Home == world._colonies[1]);

        left.Pos = (10, 10);
        left.Health = 250f;
        left.Profession = Profession.Hunter;
        right.Pos = (16, 10);
        right.Health = 1f;
        right.Profession = Profession.Hunter;
        FreezeNonParticipants(world, left, right);
        SetJobTicks(left, Job.Fight, 8);
        SetJobTicks(right, Job.Fight, 8);

        world.Update(0.25f);

        var telemetry = world.BuildScenarioContactTelemetrySnapshot();
        Assert.True(telemetry.BattlePairings > 0);
        Assert.True(telemetry.BattleTicksWithDamage > 0);
        Assert.True(telemetry.BattleTicksWithDeaths > 0);
        Assert.Equal(1, telemetry.FirstBattlePairingTick);
        Assert.Equal(1, telemetry.FirstBattleDamageTick);
        Assert.Equal(1, telemetry.FirstBattleDeathTick);
    }

    [Fact]
    public void TryStartRouting_ReportsRoutingBeforeDamage_FromBattleContext()
    {
        var world = CreateCombatWorld(seed: 9405);
        var left = world._people.First(person => person.Home == world._colonies[0]);
        var right = world._people.First(person => person.Home == world._colonies[1]);

        left.Pos = (10, 10);
        left.Health = 250f;
        right.Pos = (16, 10);
        right.Health = 250f;
        FreezeNonParticipants(world, left, right);
        SetJobTicks(left, Job.Fight, 8);
        SetJobTicks(right, Job.Fight, 8);

        world.Update(0.25f);

        var battles = GetPrivateList(world, "_activeBattles");
        Assert.NotEmpty(battles.Cast<object>());
        var battle = battles.Cast<object>().First();
        var group = battle.GetType().GetProperty("Left")!.GetValue(battle)!;
        battle.GetType().GetProperty("HadDamageThisTick")!.SetValue(battle, false);

        foreach (var member in (IEnumerable)group.GetType().GetProperty("Members")!.GetValue(group)!)
            SetCombatMorale((Person)member, 0f);

        var tryStartRouting = typeof(World).GetMethod("TryStartRouting", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(tryStartRouting);
        tryStartRouting!.Invoke(world, new[] { battle, group });

        var telemetry = world.BuildScenarioContactTelemetrySnapshot();
        Assert.True(telemetry.RoutingStarts > 0);
        Assert.True(telemetry.RoutingBeforeDamage > 0);
        Assert.Equal(1, telemetry.FirstRoutingTick);
        Assert.Equal(1, telemetry.FirstRoutingBeforeDamageTick);
    }

    [Fact]
    public void ActorContactEvents_AreActorPerTickDeduped()
    {
        var world = CreateCombatWorld(seed: 9406);
        var fighter = world._people.First(person => person.Home == world._colonies[0]);
        var enemy = world._people.First(person => person.Home == world._colonies[1]);

        fighter.Pos = (10, 10);
        enemy.Pos = (15, 10);
        FreezeNonParticipants(world, fighter, enemy);

        var attackOrPursue = typeof(Person).GetMethod("TryAttackOrPursueHostilePerson", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(attackOrPursue);

        _ = (bool)attackOrPursue!.Invoke(fighter, new object[] { world, 8, false, "fight_chase" })!;
        _ = (bool)attackOrPursue.Invoke(fighter, new object[] { world, 8, false, "fight_chase" })!;

        var telemetry = world.BuildScenarioContactTelemetrySnapshot();
        Assert.Equal(1, telemetry.HostileSensed);
        Assert.Equal(1, telemetry.PursueStarts);
    }

    private static World CreateCombatWorld(int seed)
    {
        var world = new World(width: 32, height: 20, initialPop: 24, randomSeed: seed)
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

    private static IList GetPrivateList(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<IList>(field!.GetValue(instance));
    }

    private static void SetCombatMorale(Person person, float value)
    {
        var property = typeof(Person).GetProperty(nameof(Person.CombatMorale), BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        var setter = property!.GetSetMethod(nonPublic: true);
        Assert.NotNull(setter);
        setter!.Invoke(person, new object[] { value });
    }
}
