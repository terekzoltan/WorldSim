using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WorldSim.AI;
using WorldSim.Simulation;
using WorldSim.Simulation.Combat;
using WorldSim.Simulation.Diplomacy;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave10ScoutAiConsumeTests
{
    private const int OrganicCampaignCadenceTicks = 20;

    [Fact]
    public void OrganicTarget_WithFreshScoutIntel_IsKnownToStrategist()
    {
        var strategist = new RecordingStrategist();
        var runtime = CreateRuntime(strategist);
        AdvanceToOrganicCadence(runtime);
        var world = PrepareStrategistRuntime(runtime, Faction.Obsidari, Faction.Aetheri);
        AddScoutIntel(runtime, world, Faction.Obsidari, Faction.Aetheri, createdTick: 20);

        runtime.AdvanceTick(0f);

        var target = GetRecordedTarget(strategist, Faction.Obsidari, Faction.Aetheri, world);
        Assert.True(target.IsKnown);
        Assert.True(target.HasScoutIntel);
        Assert.True(target.ScoutIntelTicksSinceRefresh <= 1);
        Assert.True(target.ScoutIntelConfidence > 0f);
    }

    [Fact]
    public void OrganicTarget_WithoutScoutIntel_IsNotKnownToStrategist()
    {
        var strategist = new RecordingStrategist();
        var runtime = CreateRuntime(strategist);
        AdvanceToOrganicCadence(runtime);
        var world = PrepareStrategistRuntime(runtime, Faction.Obsidari, Faction.Aetheri);

        runtime.AdvanceTick(0f);

        var target = GetRecordedTarget(strategist, Faction.Obsidari, Faction.Aetheri, world);
        Assert.False(target.IsKnown);
        Assert.False(target.HasScoutIntel);
        Assert.Equal(int.MaxValue, target.ScoutIntelTicksSinceRefresh);
        Assert.Equal(0f, target.ScoutIntelConfidence);
    }

    [Fact]
    public void OrganicTarget_WithStaleActiveScoutIntel_IsExportedButNotKnownToStrategist()
    {
        var strategist = new RecordingStrategist();
        var runtime = CreateRuntime(strategist);
        AdvanceTicks(runtime, 40);
        var world = PrepareStrategistRuntime(runtime, Faction.Obsidari, Faction.Aetheri);
        AddScoutIntel(runtime, world, Faction.Obsidari, Faction.Aetheri, createdTick: 0);
        Assert.Single(runtime.ScoutIntel);
        Assert.True(Assert.Single(runtime.ScoutIntel).TicksSinceRefresh > 30);

        runtime.AdvanceTick(0f);

        var target = GetRecordedTarget(strategist, Faction.Obsidari, Faction.Aetheri, world);
        Assert.False(target.IsKnown);
        Assert.True(target.HasScoutIntel);
        Assert.True(target.ScoutIntelTicksSinceRefresh > 30);
        Assert.Single(runtime.GetSnapshot().ScoutIntel);
    }

    [Fact]
    public void OrganicTarget_RefreshedScoutIntel_BecomesKnownAgain()
    {
        var strategist = new RecordingStrategist();
        var runtime = CreateRuntime(strategist);
        AdvanceTicks(runtime, 40);
        var world = PrepareStrategistRuntime(runtime, Faction.Obsidari, Faction.Aetheri);
        AddScoutIntel(runtime, world, Faction.Obsidari, Faction.Aetheri, createdTick: 39);

        runtime.AdvanceTick(0f);

        var target = GetRecordedTarget(strategist, Faction.Obsidari, Faction.Aetheri, world);
        Assert.True(target.IsKnown);
        Assert.True(target.HasScoutIntel);
        Assert.True(target.ScoutIntelTicksSinceRefresh <= 1);
    }

    [Fact]
    public void OrganicTarget_CurrentNeutralStanceInvalidatesOlderScoutIntel()
    {
        var strategist = new RecordingStrategist();
        var runtime = CreateRuntime(strategist);
        AdvanceToOrganicCadence(runtime);
        var world = PrepareStrategistRuntime(runtime, Faction.Obsidari, Faction.Aetheri);
        AddScoutIntel(runtime, world, Faction.Obsidari, Faction.Aetheri, createdTick: 20);
        world.SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.Neutral);
        runtime.AdvanceTick(0f);

        var ownerContext = strategist.Contexts.Last(context => context.FactionId == (int)Faction.Obsidari);
        Assert.DoesNotContain(ownerContext.Targets ?? Array.Empty<CampaignTargetOption>(),
            target => target.TargetFactionId == (int)Faction.Aetheri);
        Assert.Single(runtime.ScoutIntel);
    }

    private static World PrepareStrategistRuntime(
        SimulationRuntime runtime,
        Faction ownerFaction,
        Faction targetFaction)
    {
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        SetAllCampaignCandidatesIneligible(world);
        var owner = GetColony(world, ownerFaction);
        var target = GetColony(world, targetFaction);
        var warriors = SelectPeople(world, ownerFaction, 2);
        foreach (var warrior in warriors)
            warrior.AssignRole(PersonRole.Warrior);

        world.SetFactionStance(owner.Faction, target.Faction, Stance.War);
        return world;
    }

    private static SimulationRuntime CreateRuntime(ICampaignStrategist strategist)
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 12, techPath, aiOptions: null, randomSeed: 9703, campaignStrategist: strategist);
    }

    private static CampaignTargetOption GetRecordedTarget(
        RecordingStrategist strategist,
        Faction ownerFaction,
        Faction targetFaction,
        World world)
    {
        var targetColony = GetColony(world, targetFaction);
        var ownerContext = strategist.Contexts.Last(context => context.FactionId == (int)ownerFaction);
        return Assert.Single(ownerContext.Targets ?? Array.Empty<CampaignTargetOption>(),
            target => target.TargetFactionId == (int)targetFaction && target.TargetColonyId == targetColony.Id);
    }

    private static void EnableDiplomacyAndCombat(SimulationRuntime runtime)
    {
        var world = GetWorld(runtime);
        world.EnableDiplomacy = true;
        world.EnableCombatPrimitives = true;
    }

    private static void SetAllCampaignCandidatesIneligible(World world)
    {
        foreach (var person in world._people)
        {
            person.ClearRole(PersonRole.Warrior | PersonRole.SupplyCarrier | PersonRole.Scout | PersonRole.Commander);
            person.Profession = Profession.Generalist;
            person.Current = Job.Idle;
            person.SetCombatAssignment(null, null, Formation.Line, isCommander: false);
        }
    }

    private static Person[] SelectPeople(World world, Faction faction, int count)
    {
        var people = world._people
            .Where(person => person.Home.Faction == faction && person.Health > 0f)
            .OrderBy(person => person.Id)
            .Take(count)
            .ToArray();
        Assert.True(people.Length >= count);
        return people;
    }

    private static void AddScoutIntel(
        SimulationRuntime runtime,
        World world,
        Faction ownerFaction,
        Faction targetFaction,
        long createdTick)
    {
        var owner = GetColony(world, ownerFaction);
        var target = GetColony(world, targetFaction);
        var scout = SelectPeople(world, ownerFaction, 1)[0];
        scout.AssignRole(PersonRole.Scout);
        var scoutIntel = GetScoutIntelStates(runtime);
        scoutIntel.Add(new ScoutIntelState(
            intelId: scoutIntel.Count + 1,
            ownerFaction: owner.Faction,
            observedFaction: target.Faction,
            observedColonyId: target.Id,
            observationKind: ScoutIntelObservationKind.Colony,
            x: target.Origin.x,
            y: target.Origin.y,
            sourceActorId: scout.Id,
            createdTick: createdTick,
            ttlTicks: 60,
            confidence: 0.8f));
    }

    private static List<ScoutIntelState> GetScoutIntelStates(SimulationRuntime runtime)
    {
        var scoutIntelField = typeof(SimulationRuntime).GetField("_scoutIntel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(scoutIntelField);
        return Assert.IsAssignableFrom<List<ScoutIntelState>>(scoutIntelField!.GetValue(runtime));
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

    private static void AdvanceToOrganicCadence(SimulationRuntime runtime)
        => AdvanceTicks(runtime, OrganicCampaignCadenceTicks);

    private static void AdvanceTicks(SimulationRuntime runtime, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            runtime.AdvanceTick(0f);
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

    private sealed class RecordingStrategist : ICampaignStrategist
    {
        public List<CampaignStrategyContext> Contexts { get; } = new();

        public CampaignStrategyDecision Decide(in CampaignStrategyContext context)
        {
            Contexts.Add(context);
            return new CampaignStrategyDecision(
                CampaignStrategyDecisionKind.HoldDefensivePosture,
                CampaignStrategyReasonCode.LaunchDisabled);
        }
    }
}
