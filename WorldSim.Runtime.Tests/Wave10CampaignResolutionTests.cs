using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WorldSim.Runtime;
using WorldSim.Simulation;
using WorldSim.Simulation.Combat;
using WorldSim.Simulation.Defense;
using WorldSim.Simulation.Diplomacy;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave10CampaignResolutionTests
{
    [Fact]
    public void CampaignEncounter_ReportsSiegePressureWithoutDuplicateEntryOrResolutionSideEffects()
    {
        var fixture = CreateEncounterCampaign(addTargetWall: true);
        var beforeStance = fixture.World.GetFactionStance(Faction.Obsidari, Faction.Aetheri);
        var ownerStockBefore = SnapshotStock(fixture.Owner);
        var targetStockBefore = SnapshotStock(fixture.Target);
        var wallHpBefore = fixture.TargetWall!.Hp;

        fixture.Runtime.AdvanceTick(1f);
        var first = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.Active, first.Siege.Status);
        Assert.Equal(1, first.Siege.SiegesEntered);
        Assert.Equal(1, first.Siege.SiegePressureTicks);
        Assert.Equal(fixture.TargetWall.Id, first.Siege.TargetStructureId);
        Assert.Equal(fixture.Target.Id, first.Siege.DefenderColonyId);
        Assert.Equal("siege_active", GetEncounterOutcome(fixture.Runtime));

        MakePressureCapable(fixture.Member);
        fixture.Runtime.AdvanceTick(1f);
        var second = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.Active, second.Siege.Status);
        Assert.Equal(1, second.Siege.SiegesEntered);
        Assert.Equal(2, second.Siege.SiegePressureTicks);
        Assert.Equal(1, fixture.World.TotalSiegesStarted);
        Assert.Equal(beforeStance, fixture.World.GetFactionStance(Faction.Obsidari, Faction.Aetheri));
        Assert.Equal(ownerStockBefore, SnapshotStock(fixture.Owner));
        Assert.Equal(targetStockBefore, SnapshotStock(fixture.Target));
        Assert.Equal(wallHpBefore, fixture.TargetWall.Hp);
    }

    [Fact]
    public void CampaignEncounter_WithoutTargetStructure_ExposesNoTargetWithoutFakeSiege()
    {
        var fixture = CreateEncounterCampaign(addTargetWall: false);

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.NoTarget, campaign.Siege.Status);
        Assert.Equal(0, campaign.Siege.SiegesEntered);
        Assert.Equal(0, campaign.Siege.SiegePressureTicks);
        Assert.Equal(0, fixture.World.TotalSiegesStarted);
        Assert.Equal("no_siege_target", GetEncounterOutcome(fixture.Runtime));
    }

    [Fact]
    public void CampaignEncounter_ObservesBreachOncePerBreachEvent()
    {
        var fixture = CreateEncounterCampaign(addTargetWall: true);
        fixture.Runtime.AdvanceTick(1f);
        Assert.Equal(CampaignSiegeStatus.Active, Assert.Single(fixture.Runtime.Campaigns).Siege.Status);

        MakePressureCapable(fixture.Member);
        Assert.True(fixture.World.TryDamageDefensiveStructure(
            fixture.TargetWall!.Pos,
            fixture.TargetWall.Hp + 1f,
            fixture.Owner));
        var recentBreach = Assert.Single(fixture.World.GetRecentBreaches());
        Assert.Equal(fixture.TargetWall.Id, recentBreach.StructureId);
        Assert.Equal(fixture.Owner.Id, recentBreach.AttackerColonyId);
        fixture.Runtime.AdvanceTick(1f);

        var breached = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.Breached, breached.Siege.Status);
        Assert.Equal(1, breached.Siege.BreachesObserved);
        Assert.Equal("siege_breached", GetEncounterOutcome(fixture.Runtime));

        fixture.Runtime.AdvanceTick(1f);

        var repeated = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.Breached, repeated.Siege.Status);
        Assert.Equal(1, repeated.Siege.BreachesObserved);
    }

    [Fact]
    public void CampaignEncounter_InvalidMemberStopsNewSiegePressureWithoutClearingPriorWorldSessionImmediately()
    {
        var fixture = CreateEncounterCampaign(addTargetWall: true);
        fixture.Runtime.AdvanceTick(1f);
        var active = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.Active, active.Siege.Status);
        Assert.Equal(1, active.Siege.SiegePressureTicks);
        Assert.Single(fixture.World.GetActiveSieges());

        fixture.Member.Health = 0f;
        fixture.Runtime.AdvanceTick(1f);

        var invalid = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.None, invalid.Siege.Status);
        Assert.Equal(-1, invalid.Siege.TargetStructureId);
        Assert.Equal(-1, invalid.Siege.DefenderColonyId);
        Assert.Equal(-1, invalid.Siege.ObservedSiegeId);
        Assert.Equal(1, invalid.Siege.SiegePressureTicks);
        Assert.Equal(1, fixture.World.TotalSiegesStarted);
        Assert.Empty(fixture.World.GetActiveSieges());
        Assert.Equal("non_resolving", GetEncounterOutcome(fixture.Runtime));
    }

    [Fact]
    public void CampaignEncounter_AliveInvalidMemberStopsNewSiegePressure()
    {
        var fixture = CreateEncounterCampaign(addTargetWall: true);
        fixture.Runtime.AdvanceTick(1f);
        var active = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.Active, active.Siege.Status);
        Assert.Equal(1, active.Siege.SiegePressureTicks);

        fixture.Member.Current = Job.AttackStructure;
        fixture.Runtime.AdvanceTick(1f);

        var invalid = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.None, invalid.Siege.Status);
        Assert.Equal(-1, invalid.Siege.TargetStructureId);
        Assert.Equal(-1, invalid.Siege.DefenderColonyId);
        Assert.Equal(-1, invalid.Siege.ObservedSiegeId);
        Assert.Equal(1, invalid.Siege.SiegePressureTicks);
        Assert.Equal(1, fixture.World.TotalSiegesStarted);
        Assert.Empty(fixture.World.GetActiveSieges());
        Assert.Equal("non_resolving", GetEncounterOutcome(fixture.Runtime));
    }

    [Fact]
    public void CampaignEncounter_BreachTruthStaysStickyWithoutAdditionalPressure()
    {
        var fixture = CreateEncounterCampaign(addTargetWall: true);
        fixture.Runtime.AdvanceTick(1f);
        MakePressureCapable(fixture.Member);
        Assert.True(fixture.World.TryDamageDefensiveStructure(
            fixture.TargetWall!.Pos,
            fixture.TargetWall.Hp + 1f,
            fixture.Owner));
        fixture.Runtime.AdvanceTick(1f);
        var breached = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.Breached, breached.Siege.Status);
        Assert.Equal(1, breached.Siege.SiegePressureTicks);
        Assert.Equal(fixture.TargetWall!.Id, breached.Siege.TargetStructureId);
        Assert.Equal(fixture.Target.Id, breached.Siege.DefenderColonyId);

        fixture.World.EnableSiege = false;
        fixture.Runtime.AdvanceTick(1f);
        breached = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.Breached, breached.Siege.Status);
        Assert.Equal(fixture.TargetWall.Id, breached.Siege.TargetStructureId);
        Assert.Equal(fixture.Target.Id, breached.Siege.DefenderColonyId);
        Assert.Equal(1, breached.Siege.BreachesObserved);

        for (var i = 0; i < 125; i++)
            fixture.Runtime.AdvanceTick(1f);

        var sticky = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.Breached, sticky.Siege.Status);
        Assert.Equal(1, sticky.Siege.BreachesObserved);
        Assert.Equal(1, sticky.Siege.SiegePressureTicks);
        Assert.Equal("siege_breached", GetEncounterOutcome(fixture.Runtime));
    }

    [Fact]
    public void SamePairNoTargetCampaign_DoesNotInheritPriorBreachEvidence()
    {
        var fixtures = CreateEncounterCampaigns(campaignCount: 2, addTargetWall: true);
        var first = fixtures[0];
        var second = fixtures[1];
        first.Runtime.AdvanceTick(1f);
        Assert.Equal(CampaignSiegeStatus.Active, first.Runtime.Campaigns[0].Siege.Status);
        Assert.Equal(CampaignSiegeStatus.None, first.Runtime.Campaigns[1].Siege.Status);

        MakePressureCapable(first.Member);
        MakePressureCapable(second.Member);
        Assert.True(first.World.TryDamageDefensiveStructure(
            first.TargetWall!.Pos,
            first.TargetWall.Hp + 1f,
            first.Owner));
        first.Runtime.AdvanceTick(1f);

        var campaigns = first.Runtime.Campaigns.OrderBy(campaign => campaign.CampaignId).ToArray();
        Assert.Equal(CampaignSiegeStatus.Breached, campaigns[0].Siege.Status);
        Assert.Equal(CampaignSiegeStatus.NoTarget, campaigns[1].Siege.Status);
        Assert.Equal(0, campaigns[1].Siege.BreachesObserved);
        Assert.Equal("siege_breached", GetEncounterOutcome(first.Runtime, campaigns[0].CampaignId));
        Assert.Equal("no_siege_target", GetEncounterOutcome(second.Runtime, campaigns[1].CampaignId));
    }

    [Fact]
    public void SamePairEncounterCampaigns_UseSingleDriverAndAllowDeterministicTakeover()
    {
        var fixtures = CreateEncounterCampaigns(campaignCount: 2, addTargetWall: true);
        var runtime = fixtures[0].Runtime;
        var world = fixtures[0].World;
        var campaigns = runtime.Campaigns.OrderBy(campaign => campaign.CampaignId).ToArray();

        runtime.AdvanceTick(1f);
        campaigns = runtime.Campaigns.OrderBy(campaign => campaign.CampaignId).ToArray();
        Assert.Equal(CampaignSiegeStatus.Active, campaigns[0].Siege.Status);
        Assert.Equal(CampaignSiegeStatus.None, campaigns[1].Siege.Status);
        Assert.Equal(1, campaigns[0].Siege.SiegePressureTicks);
        Assert.Equal(0, campaigns[1].Siege.SiegePressureTicks);
        Assert.Equal(1, world.TotalSiegesStarted);
        Assert.Equal("siege_active", GetEncounterOutcome(runtime, campaigns[0].CampaignId));
        Assert.Equal("non_resolving", GetEncounterOutcome(runtime, campaigns[1].CampaignId));

        fixtures[0].Member.Health = 0f;
        MakePressureCapable(fixtures[1].Member);
        runtime.AdvanceTick(1f);

        campaigns = runtime.Campaigns.OrderBy(campaign => campaign.CampaignId).ToArray();
        Assert.Equal(CampaignSiegeStatus.None, campaigns[0].Siege.Status);
        Assert.Equal(-1, campaigns[0].Siege.TargetStructureId);
        Assert.Equal(-1, campaigns[0].Siege.DefenderColonyId);
        Assert.Equal(-1, campaigns[0].Siege.ObservedSiegeId);
        Assert.Equal(CampaignSiegeStatus.Active, campaigns[1].Siege.Status);
        Assert.Equal(1, campaigns[0].Siege.SiegePressureTicks);
        Assert.Equal(1, campaigns[1].Siege.SiegePressureTicks);
        Assert.Equal(1, world.TotalSiegesStarted);
        Assert.Equal("non_resolving", GetEncounterOutcome(runtime, campaigns[0].CampaignId));
        Assert.Equal("siege_active", GetEncounterOutcome(runtime, campaigns[1].CampaignId));
    }

    [Fact]
    public void SamePairEncounterCampaigns_TakeoverWhenPriorDriverBecomesAliveInvalid()
    {
        var fixtures = CreateEncounterCampaigns(campaignCount: 2, addTargetWall: true);
        var runtime = fixtures[0].Runtime;
        var world = fixtures[0].World;

        runtime.AdvanceTick(1f);
        var campaigns = runtime.Campaigns.OrderBy(campaign => campaign.CampaignId).ToArray();
        Assert.Equal(CampaignSiegeStatus.Active, campaigns[0].Siege.Status);
        Assert.Equal(CampaignSiegeStatus.None, campaigns[1].Siege.Status);

        fixtures[0].Member.Current = Job.AttackStructure;
        MakePressureCapable(fixtures[1].Member);
        runtime.AdvanceTick(1f);

        campaigns = runtime.Campaigns.OrderBy(campaign => campaign.CampaignId).ToArray();
        Assert.Equal(CampaignSiegeStatus.None, campaigns[0].Siege.Status);
        Assert.Equal(-1, campaigns[0].Siege.TargetStructureId);
        Assert.Equal(-1, campaigns[0].Siege.DefenderColonyId);
        Assert.Equal(-1, campaigns[0].Siege.ObservedSiegeId);
        Assert.Equal(CampaignSiegeStatus.Active, campaigns[1].Siege.Status);
        Assert.Equal(1, campaigns[0].Siege.SiegePressureTicks);
        Assert.Equal(1, campaigns[1].Siege.SiegePressureTicks);
        Assert.Equal(1, world.TotalSiegesStarted);
        Assert.Equal("non_resolving", GetEncounterOutcome(runtime, campaigns[0].CampaignId));
        Assert.Equal("siege_active", GetEncounterOutcome(runtime, campaigns[1].CampaignId));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CampaignEncounter_DisabledSiegeFlow_DoesNotOverclaimPressure(bool enableSiege, bool enableCombatPrimitives)
    {
        var fixture = CreateEncounterCampaign(addTargetWall: true);
        fixture.World.EnableSiege = enableSiege;
        fixture.World.EnableCombatPrimitives = enableCombatPrimitives;

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.None, campaign.Siege.Status);
        Assert.Equal(0, campaign.Siege.SiegePressureTicks);
        Assert.Equal(0, fixture.World.TotalSiegesStarted);
        Assert.Empty(fixture.World.GetActiveSieges());
        Assert.Equal("non_resolving", GetEncounterOutcome(fixture.Runtime));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CampaignEncounter_DisabledAfterTargetAcquired_DoesNotObserveLaterBreachAfterReenabled(
        bool enableSiegeWhileDisabled,
        bool enableCombatPrimitivesWhileDisabled)
    {
        var fixture = CreateEncounterCampaign(addTargetWall: true);
        fixture.Runtime.AdvanceTick(1f);
        var active = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.Active, active.Siege.Status);
        Assert.Equal(fixture.TargetWall!.Id, active.Siege.TargetStructureId);

        fixture.World.EnableSiege = enableSiegeWhileDisabled;
        fixture.World.EnableCombatPrimitives = enableCombatPrimitivesWhileDisabled;
        Assert.True(fixture.World.TryDamageDefensiveStructure(
            fixture.TargetWall.Pos,
            fixture.TargetWall.Hp + 1f,
            fixture.Owner));
        fixture.Runtime.AdvanceTick(1f);

        var suppressed = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignSiegeStatus.None, suppressed.Siege.Status);
        Assert.Equal(-1, suppressed.Siege.TargetStructureId);
        Assert.Equal(-1, suppressed.Siege.DefenderColonyId);
        Assert.Equal(-1, suppressed.Siege.ObservedSiegeId);
        Assert.Equal(0, suppressed.Siege.BreachesObserved);
        Assert.Equal(1, suppressed.Siege.SiegePressureTicks);
        Assert.Equal("non_resolving", GetEncounterOutcome(fixture.Runtime));

        fixture.World.EnableSiege = true;
        fixture.World.EnableCombatPrimitives = true;
        MakePressureCapable(fixture.Member);
        fixture.Runtime.AdvanceTick(1f);

        var reenabled = Assert.Single(fixture.Runtime.Campaigns);
        Assert.NotEqual(CampaignSiegeStatus.Breached, reenabled.Siege.Status);
        Assert.Equal(-1, reenabled.Siege.TargetStructureId);
        Assert.Equal(-1, reenabled.Siege.DefenderColonyId);
        Assert.Equal(0, reenabled.Siege.BreachesObserved);
        Assert.Equal("no_siege_target", GetEncounterOutcome(fixture.Runtime));
    }

    [Fact]
    public void SamePairSuppressedPriorDriver_DoesNotInheritTakeoverDriverBreach()
    {
        var fixtures = CreateEncounterCampaigns(campaignCount: 2, addTargetWall: true);
        var runtime = fixtures[0].Runtime;

        runtime.AdvanceTick(1f);
        var campaigns = runtime.Campaigns.OrderBy(campaign => campaign.CampaignId).ToArray();
        Assert.Equal(CampaignSiegeStatus.Active, campaigns[0].Siege.Status);
        Assert.Equal(CampaignSiegeStatus.None, campaigns[1].Siege.Status);

        fixtures[0].Member.Current = Job.AttackStructure;
        MakePressureCapable(fixtures[1].Member);
        runtime.AdvanceTick(1f);
        campaigns = runtime.Campaigns.OrderBy(campaign => campaign.CampaignId).ToArray();
        Assert.Equal(CampaignSiegeStatus.None, campaigns[0].Siege.Status);
        Assert.Equal(-1, campaigns[0].Siege.TargetStructureId);
        Assert.Equal(CampaignSiegeStatus.Active, campaigns[1].Siege.Status);
        var targetWall = fixtures[0].TargetWall!;
        Assert.Equal(targetWall.Id, campaigns[1].Siege.TargetStructureId);

        MakePressureCapable(fixtures[0].Member);
        MakePressureCapable(fixtures[1].Member);
        Assert.True(fixtures[0].World.TryDamageDefensiveStructure(
            targetWall.Pos,
            targetWall.Hp + 1f,
            fixtures[0].Owner));
        runtime.AdvanceTick(1f);

        campaigns = runtime.Campaigns.OrderBy(campaign => campaign.CampaignId).ToArray();
        Assert.NotEqual(CampaignSiegeStatus.Breached, campaigns[0].Siege.Status);
        Assert.Equal(0, campaigns[0].Siege.BreachesObserved);
        Assert.Equal(-1, campaigns[0].Siege.TargetStructureId);
        Assert.Equal(-1, campaigns[0].Siege.DefenderColonyId);
        Assert.Equal("no_siege_target", GetEncounterOutcome(runtime, campaigns[0].CampaignId));
        Assert.Equal(CampaignSiegeStatus.Breached, campaigns[1].Siege.Status);
        Assert.Equal(1, campaigns[1].Siege.BreachesObserved);
        Assert.Equal("siege_breached", GetEncounterOutcome(runtime, campaigns[1].CampaignId));
    }

    [Fact]
    public void AdvanceTick_UnderstrengthCampaignCannotReachEncounterOrSiegePressure()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyCombatAndSiege(runtime);
        SetAllCampaignCandidatesIneligible(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var target = GetColony(world, Faction.Aetheri);
        Assert.Equal(1, runtime.PrepareWave9CampaignScenario(Faction.Obsidari, candidateCount: 1, carriedFoodPerCandidate: 2));
        Assert.True(world.TryAddWoodWall(target, FindBuildableNear(world, target, minDistanceFromOrigin: 3)));

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 2);
        Assert.True(result.Success);
        world.EnableCombatPrimitives = false;

        for (var i = 0; i < 24; i++)
            runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.NotEqual(CampaignPhase.Encounter, campaign.Phase);
        Assert.True(campaign.Phase is CampaignPhase.AssemblingPending or CampaignPhase.Assembling);
        Assert.True(campaign.Army.AssignedMemberCount < campaign.Army.RequestedMemberCount);
        Assert.Equal(0, campaign.Siege.SiegesEntered);
        Assert.Equal(0, campaign.Siege.SiegePressureTicks);
        Assert.Equal(0, world.TotalSiegesStarted);
        Assert.Empty(runtime.GetSnapshot().Campaigns.Single().Encounters);
        Assert.Equal(owner.Id, campaign.OriginColonyId);
    }

    private static EncounterCampaignFixture CreateEncounterCampaign(bool addTargetWall)
        => CreateEncounterCampaigns(campaignCount: 1, addTargetWall).Single();

    private static IReadOnlyList<EncounterCampaignFixture> CreateEncounterCampaigns(int campaignCount, bool addTargetWall)
    {
        var runtime = CreateRuntime();
        EnableDiplomacyCombatAndSiege(runtime);
        var world = GetWorld(runtime);
        world.BirthRateMultiplier = 0f;
        world._animals.Clear();
        SetAllCampaignCandidatesIneligible(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var target = GetColony(world, Faction.Aetheri);
        Assert.Equal(campaignCount, runtime.PrepareWave9CampaignScenario(Faction.Obsidari, campaignCount, carriedFoodPerCandidate: 2));

        for (var i = 0; i < campaignCount; i++)
        {
            var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
            Assert.True(result.Success);
        }

        world.EnableCombatPrimitives = false;

        for (var i = 0; i < 80 && runtime.Campaigns.Any(campaign => campaign.Army.AssignedMemberCount < 1); i++)
            runtime.AdvanceTick(0f);

        Assert.All(runtime.Campaigns, campaign => Assert.Equal(1, campaign.Army.AssignedMemberCount));
        foreach (var campaign in runtime.Campaigns)
        {
            var member = world._people.Single(person => campaign.Army.MemberActorIds.Contains(person.Id));
            member.Pos = (campaign.Army.RallyX, campaign.Army.RallyY);
        }

        for (var i = 0; i < 8 && runtime.Campaigns.Any(campaign => campaign.Phase != CampaignPhase.Marching); i++)
            runtime.AdvanceTick(0f);

        Assert.All(runtime.Campaigns, campaign => Assert.Equal(CampaignPhase.Marching, campaign.Phase));
        var members = runtime.Campaigns
            .Select(campaign => world._people.Single(person => campaign.Army.MemberActorIds.Contains(person.Id)))
            .OrderBy(person => person.Id)
            .ToArray();
        for (var i = 0; i < members.Length; i++)
            members[i].Pos = FindEncounterTestPosition(world, target, i);

        runtime.AdvanceTick(0f);
        Assert.All(runtime.Campaigns, campaign => Assert.Equal(CampaignPhase.Encounter, campaign.Phase));

        NeutralizeNonCampaignActors(world, members.Select(member => member.Id).ToHashSet());
        foreach (var member in members)
        {
            member.Profession = Profession.Generalist;
            member.Current = Job.Idle;
            member.Needs["Hunger"] = 0f;
            member.ApplyStaminaDelta(100f);
        }

        world.EnableCombatPrimitives = true;
        world.EnableSiege = true;
        world.SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.Neutral);

        DefensiveStructure? wall = null;
        if (addTargetWall)
        {
            var wallPos = FindBuildableNear(world, target, minDistanceFromOrigin: 3);
            Assert.True(world.TryAddWoodWall(target, wallPos));
            wall = Assert.Single(world.DefensiveStructures, structure => structure.Pos == wallPos);
        }

        return members
            .Select(member => new EncounterCampaignFixture(runtime, world, owner, target, member, wall))
            .ToArray();
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 12, techPath, aiOptions: null, randomSeed: 9601);
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

    private static void EnableDiplomacyCombatAndSiege(SimulationRuntime runtime)
    {
        var world = GetWorld(runtime);
        world.EnableDiplomacy = true;
        world.EnableCombatPrimitives = true;
        world.EnableSiege = true;
    }

    private static void SetAllCampaignCandidatesIneligible(SimulationRuntime runtime)
    {
        foreach (var person in GetWorld(runtime)._people)
        {
            person.ClearRole(PersonRole.Warrior | PersonRole.SupplyCarrier | PersonRole.Scout | PersonRole.Commander);
            person.Profession = Profession.Generalist;
            person.Current = Job.Idle;
            person.SetCombatAssignment(null, null, Formation.Line, isCommander: false);
        }
    }

    private static void NeutralizeNonCampaignActors(World world, HashSet<int> campaignMemberIds)
    {
        foreach (var person in world._people)
        {
            if (campaignMemberIds.Contains(person.Id))
                continue;

            person.Health = 0f;
        }
    }

    private static void MakePressureCapable(Person person)
    {
        person.Current = Job.Idle;
        person.SetCombatAssignment(null, null, Formation.Line, isCommander: false);
        var isInCombatField = typeof(Person).GetField("<IsInCombat>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(isInCombatField);
        isInCombatField!.SetValue(person, false);
        var isRoutingField = typeof(Person).GetField("<IsRouting>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(isRoutingField);
        isRoutingField!.SetValue(person, false);
        var routingTicksField = typeof(Person).GetField("<RoutingTicksRemaining>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(routingTicksField);
        routingTicksField!.SetValue(person, 0);
    }

    private static Colony GetColony(World world, Faction faction)
    {
        var colony = world._colonies.FirstOrDefault(candidate => candidate.Faction == faction);
        Assert.NotNull(colony);
        return colony!;
    }

    private static IReadOnlyList<Person> GetPeople(World world, Faction faction)
        => world._people
            .Where(person => person.Home.Faction == faction && person.Health > 0f)
            .OrderBy(person => person.Id)
            .ToArray();

    private static (int x, int y) FindBuildableNear(World world, Colony colony, int minDistanceFromOrigin)
    {
        for (var radius = Math.Max(1, minDistanceFromOrigin); radius <= Math.Max(world.Width, world.Height); radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) < minDistanceFromOrigin)
                        continue;

                    int x = colony.Origin.x + dx;
                    int y = colony.Origin.y + dy;
                    if (world.CanPlaceStructureAt(x, y))
                        return (x, y);
                }
            }
        }

        throw new InvalidOperationException("Could not find a buildable campaign test tile.");
    }

    private static (int x, int y) FindEncounterTestPosition(World world, Colony target, int index)
    {
        var offsets = new[]
        {
            (dx: 0, dy: 0),
            (dx: 1, dy: 0),
            (dx: -1, dy: 0),
            (dx: 0, dy: 1),
            (dx: 0, dy: -1)
        };

        for (var i = 0; i < offsets.Length; i++)
        {
            var offset = offsets[(index + i) % offsets.Length];
            int x = target.Origin.x + offset.dx;
            int y = target.Origin.y + offset.dy;
            if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
                continue;
            if (!world.IsMovementBlocked(x, y, target.Id))
                return (x, y);
        }

        return target.Origin;
    }

    private static IReadOnlyDictionary<Resource, int> SnapshotStock(Colony colony)
        => colony.Stock.ToDictionary(entry => entry.Key, entry => entry.Value);

    private static string GetEncounterOutcome(SimulationRuntime runtime)
        => Assert.Single(runtime.GetSnapshot().Campaigns.Single().Encounters).Outcome;

    private static string GetEncounterOutcome(SimulationRuntime runtime, int campaignId)
        => Assert.Single(runtime.GetSnapshot().Campaigns.Single(campaign => campaign.CampaignId == campaignId).Encounters).Outcome;

    private static World GetWorld(SimulationRuntime runtime)
    {
        var worldField = typeof(SimulationRuntime).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(worldField);
        var world = worldField!.GetValue(runtime) as World;
        Assert.NotNull(world);
        return world!;
    }

    private sealed record EncounterCampaignFixture(
        SimulationRuntime Runtime,
        World World,
        Colony Owner,
        Colony Target,
        Person Member,
        DefensiveStructure? TargetWall);
}
