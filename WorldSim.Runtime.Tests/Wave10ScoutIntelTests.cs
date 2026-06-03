using System;
using System.IO;
using System.Linq;
using System.Reflection;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Combat;
using WorldSim.Simulation.Diplomacy;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave10ScoutIntelTests
{
    [Fact]
    public void LiveScoutCreatesHostileColonyIntelInRadius()
    {
        var runtime = CreateRuntime();
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var target = GetColony(world, Faction.Aetheri);
        var scout = PrepareScout(world, owner, target.Origin);
        world.SetFactionStance(owner.Faction, target.Faction, Stance.Hostile);

        runtime.AdvanceTick(0f);

        var intel = Assert.Single(runtime.ScoutIntel);
        Assert.Equal(owner.Faction, intel.OwnerFaction);
        Assert.Equal(target.Faction, intel.ObservedFaction);
        Assert.Equal(target.Id, intel.ObservedColonyId);
        Assert.Equal(ScoutIntelObservationKind.Colony, intel.ObservationKind);
        Assert.Equal(target.Origin, (intel.X, intel.Y));
        Assert.Equal(scout.Id, intel.SourceActorId);
        Assert.Equal(0, intel.TicksSinceRefresh);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ScoutIntelObserved);
    }

    [Fact]
    public void NonScoutOrDeadScoutDoesNotCreateIntel()
    {
        var nonScoutRuntime = CreateRuntime();
        var nonScoutWorld = GetWorld(nonScoutRuntime);
        var owner = GetColony(nonScoutWorld, Faction.Obsidari);
        var target = GetColony(nonScoutWorld, Faction.Aetheri);
        var nonScout = SelectPerson(nonScoutWorld, owner);
        ResetRoleState(nonScout);
        nonScout.Pos = target.Origin;
        nonScoutWorld.SetFactionStance(owner.Faction, target.Faction, Stance.War);

        nonScoutRuntime.AdvanceTick(0f);

        Assert.Empty(nonScoutRuntime.ScoutIntel);

        var deadScoutRuntime = CreateRuntime();
        var deadScoutWorld = GetWorld(deadScoutRuntime);
        owner = GetColony(deadScoutWorld, Faction.Obsidari);
        target = GetColony(deadScoutWorld, Faction.Aetheri);
        var deadScout = PrepareScout(deadScoutWorld, owner, target.Origin);
        deadScout.Health = 0f;
        deadScoutWorld.SetFactionStance(owner.Faction, target.Faction, Stance.War);

        deadScoutRuntime.AdvanceTick(0f);

        Assert.Empty(deadScoutRuntime.ScoutIntel);
    }

    [Fact]
    public void OutOfRadiusHostileColonyDoesNotCreateIntel()
    {
        var runtime = CreateRuntime();
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var target = GetColony(world, Faction.Aetheri);
        owner.Origin = (0, 0);
        target.Origin = (31, 31);
        PrepareScout(world, owner, owner.Origin);
        world.SetFactionStance(owner.Faction, target.Faction, Stance.Hostile);

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.ScoutIntel);
        Assert.Equal(0, runtime.CampaignLogisticsCounters.ScoutIntelObserved);
    }

    [Fact]
    public void NeutralColonyDoesNotCreateFirstSliceIntel()
    {
        var neutralRuntime = CreateRuntime();
        var neutralWorld = GetWorld(neutralRuntime);
        var owner = GetColony(neutralWorld, Faction.Obsidari);
        var target = GetColony(neutralWorld, Faction.Aetheri);
        PrepareScout(neutralWorld, owner, target.Origin);
        neutralWorld.SetFactionStance(owner.Faction, target.Faction, Stance.Neutral);

        neutralRuntime.AdvanceTick(0f);

        Assert.Empty(neutralRuntime.ScoutIntel);
    }

    [Fact]
    public void MultipleScoutsRefreshSameColonyIntelWithoutDuplicateRecords()
    {
        var runtime = CreateRuntime();
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var target = GetColony(world, Faction.Aetheri);
        PrepareScouts(world, owner, target.Origin, count: 2);
        world.SetFactionStance(owner.Faction, target.Faction, Stance.War);

        runtime.AdvanceTick(0f);

        Assert.Single(runtime.ScoutIntel);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ScoutIntelObserved);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ScoutIntelRefreshed);
    }

    [Fact]
    public void ScoutIntelFreshnessAgesWhileActiveAndResetsOnRefresh()
    {
        var runtime = CreateRuntime();
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var target = GetColony(world, Faction.Aetheri);
        var scout = PrepareScout(world, owner, target.Origin);
        world.SetFactionStance(owner.Faction, target.Faction, Stance.Hostile);
        runtime.AdvanceTick(0f);
        Assert.Equal(0, Assert.Single(runtime.ScoutIntel).TicksSinceRefresh);

        scout.Pos = owner.Origin;
        target.Origin = (31, 31);
        AdvanceTicks(runtime, 3);

        var stale = Assert.Single(runtime.ScoutIntel);
        Assert.True(stale.TicksSinceRefresh > 0);
        Assert.Equal(stale.TicksSinceRefresh, Assert.Single(runtime.GetSnapshot().ScoutIntel).TicksSinceRefresh);

        scout.Pos = target.Origin;
        runtime.AdvanceTick(0f);

        Assert.Equal(0, Assert.Single(runtime.ScoutIntel).TicksSinceRefresh);
        Assert.Equal(0, Assert.Single(runtime.GetSnapshot().ScoutIntel).TicksSinceRefresh);
    }

    [Fact]
    public void DifferentOwnerFactionsObservingSameTargetKeepSeparateIntelRecords()
    {
        var runtime = CreateRuntime();
        var world = GetWorld(runtime);
        var ownerA = GetColony(world, Faction.Obsidari);
        var ownerB = GetColony(world, Faction.Sylvars);
        var target = GetColony(world, Faction.Aetheri);
        Assert.NotEqual(ownerA.Faction, ownerB.Faction);
        Assert.NotEqual(ownerA.Faction, target.Faction);
        Assert.NotEqual(ownerB.Faction, target.Faction);
        ClearScoutRoles(world);
        PrepareScoutWithoutClearing(world, ownerA, target.Origin);
        PrepareScoutWithoutClearing(world, ownerB, target.Origin);
        world.SetFactionStance(ownerA.Faction, target.Faction, Stance.Hostile);
        world.SetFactionStance(ownerB.Faction, target.Faction, Stance.War);
        Assert.True(world.GetFactionStance(ownerA.Faction, target.Faction) is Stance.Hostile or Stance.War);
        Assert.True(world.GetFactionStance(ownerB.Faction, target.Faction) is Stance.Hostile or Stance.War);

        runtime.AdvanceTick(0f);

        var records = runtime.ScoutIntel.OrderBy(record => (int)record.OwnerFaction).ToArray();
        Assert.Equal(2, records.Length);
        Assert.Equal(new[] { ownerB.Faction, ownerA.Faction }.OrderBy(faction => (int)faction), records.Select(record => record.OwnerFaction));
        Assert.All(records, record => Assert.Equal(target.Id, record.ObservedColonyId));
        Assert.All(records, record => Assert.Equal(ScoutIntelObservationKind.Colony, record.ObservationKind));
    }

    [Fact]
    public void ExpiredScoutIntelIsRemovedFromRuntimeAndReadModelExport()
    {
        var runtime = CreateRuntime();
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var target = GetColony(world, Faction.Aetheri);
        var scout = PrepareScout(world, owner, target.Origin);
        world.SetFactionStance(owner.Faction, target.Faction, Stance.Hostile);
        runtime.AdvanceTick(0f);
        var snapshotBeforeExpiry = runtime.GetSnapshot();
        Assert.Single(snapshotBeforeExpiry.ScoutIntel);

        scout.Pos = owner.Origin;
        target.Origin = (31, 31);
        AdvanceTicks(runtime, 62);

        Assert.Empty(runtime.ScoutIntel);
        Assert.Empty(runtime.GetSnapshot().ScoutIntel);
        Assert.Single(snapshotBeforeExpiry.ScoutIntel);
        Assert.True(runtime.CampaignLogisticsCounters.ScoutIntelExpired > 0);
    }

    [Fact]
    public void SnapshotExportsDetachedScoutIntelReadModel()
    {
        var runtime = CreateRuntime();
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var target = GetColony(world, Faction.Aetheri);
        PrepareScout(world, owner, target.Origin);
        world.SetFactionStance(owner.Faction, target.Faction, Stance.War);

        runtime.AdvanceTick(0f);

        var runtimeIntel = Assert.Single(runtime.ScoutIntel);
        ScoutIntelRenderData renderIntel = Assert.Single(runtime.GetSnapshot().ScoutIntel);
        Assert.Equal(runtimeIntel.IntelId, renderIntel.IntelId);
        Assert.Equal((int)runtimeIntel.OwnerFaction, renderIntel.OwnerFactionId);
        Assert.Equal((int)runtimeIntel.ObservedFaction, renderIntel.ObservedFactionId);
        Assert.Equal(runtimeIntel.ObservedColonyId, renderIntel.ObservedColonyId);
        Assert.Equal("colony", renderIntel.ObservationKind);
        Assert.Equal(runtimeIntel.TicksSinceRefresh, renderIntel.TicksSinceRefresh);
        Assert.Equal(runtimeIntel.Confidence, renderIntel.Confidence);
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 12, techPath, aiOptions: null, randomSeed: 9701, campaignStrategist: null);
    }

    private static Person PrepareScout(World world, Colony owner, (int x, int y) pos)
        => PrepareScouts(world, owner, pos, count: 1).Single();

    private static Person[] PrepareScouts(World world, Colony owner, (int x, int y) pos, int count)
    {
        ClearScoutRoles(world);
        return PrepareScoutsWithoutClearing(world, owner, pos, count);
    }

    private static Person PrepareScoutWithoutClearing(World world, Colony owner, (int x, int y) pos)
        => PrepareScoutsWithoutClearing(world, owner, pos, count: 1).Single();

    private static Person[] PrepareScoutsWithoutClearing(World world, Colony owner, (int x, int y) pos, int count)
    {
        var scouts = world._people
            .Where(person => person.Home.Id == owner.Id && person.Health > 0f)
            .OrderBy(person => person.Id)
            .Take(count)
            .ToArray();
        Assert.True(scouts.Length >= count);
        foreach (var scout in scouts)
        {
            ResetRoleState(scout);
            scout.AssignRole(PersonRole.Scout);
            scout.Pos = pos;
        }

        return scouts;
    }

    private static void ClearScoutRoles(World world)
    {
        foreach (var person in world._people)
            ResetRoleState(person);
    }

    private static void ResetRoleState(Person person)
    {
        person.ClearRole(PersonRole.Warrior | PersonRole.SupplyCarrier | PersonRole.Scout | PersonRole.Commander);
        person.Profession = Profession.Generalist;
        person.Current = Job.Idle;
        person.SetCombatAssignment(null, null, Formation.Line, isCommander: false);
    }

    private static Person SelectPerson(World world, Colony owner)
    {
        var person = world._people.FirstOrDefault(candidate => candidate.Home.Id == owner.Id && candidate.Health > 0f);
        Assert.NotNull(person);
        return person!;
    }

    private static void AdvanceTicks(SimulationRuntime runtime, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            runtime.AdvanceTick(0f);
    }

    private static Colony GetColony(World world, Faction faction)
    {
        var colony = world._colonies.FirstOrDefault(candidate => candidate.Faction == faction);
        Assert.NotNull(colony);
        return colony!;
    }

    private static World GetWorld(SimulationRuntime runtime)
    {
        var worldField = typeof(SimulationRuntime).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(worldField);
        var world = worldField!.GetValue(runtime) as World;
        Assert.NotNull(world);
        return world!;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var techPath = Path.Combine(current.FullName, "Tech", "technologies.json");
            if (File.Exists(techPath))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json");
    }
}
