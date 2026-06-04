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

public sealed class Wave10OrganicCampaignLaunchTests
{
    private const int OrganicCampaignCadenceTicks = 20;

    [Fact]
    public void OrganicCampaign_CallsStrategistOnlyAtCadence_WhenRuntimeGatesAreEnabled()
    {
        var strategist = new RecordingStrategist();
        var runtime = CreateRuntime(strategist);
        EnableDiplomacyAndCombat(runtime);

        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);

        Assert.Equal(0, strategist.CallCount);

        runtime.AdvanceTick(0f);

        Assert.True(strategist.CallCount > 0);
    }

    [Fact]
    public void OrganicCampaign_DoesNotLaunchBeforeCadence_ThenLaunchesAtFactionCadence()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);

        Assert.Empty(runtime.Campaigns);

        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic cadence test");
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);
        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(Faction.Obsidari, campaign.OwnerFaction);
        Assert.Equal(Faction.Aetheri, campaign.TargetFaction);
        Assert.Equal(1, campaign.Army.RequestedMemberCount);
    }

    [Fact]
    public void OrganicCampaign_HostileTargetCanLaunch_WhenEligibleMembersAreSufficient()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        GetWorld(runtime).SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.Hostile);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(Faction.Obsidari, campaign.OwnerFaction);
        Assert.Equal(Faction.Aetheri, campaign.TargetFaction);
    }

    [Fact]
    public void OrganicCampaign_WarTargetIsPreferredOverHostileTarget()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        var world = GetWorld(runtime);
        world.SetFactionStance(Faction.Obsidari, Faction.Sylvars, Stance.Hostile);
        world.SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.War);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Sylvars);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(Faction.Aetheri, campaign.TargetFaction);
    }

    [Fact]
    public void OrganicCampaign_DeterministicallySelectsTheSameTargetForSameSeedAndFacts()
    {
        var first = CreateRuntime();
        var second = CreateRuntime();
        PrepareTieBreakRuntime(first);
        PrepareTieBreakRuntime(second);

        AdvanceOneCadenceTick(first);
        AdvanceOneCadenceTick(second);

        Assert.Equal(Assert.Single(first.Campaigns).TargetFaction, Assert.Single(second.Campaigns).TargetFaction);
        Assert.Equal(Assert.Single(first.Campaigns).TargetColonyId, Assert.Single(second.Campaigns).TargetColonyId);
    }

    [Fact]
    public void OrganicCampaign_DisabledRuntimeGatesSuppressLaunch()
    {
        var runtime = CreateRuntime();
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        GetWorld(runtime).SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.War);

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_PeacefulStanceSuppressesLaunch_EvenWhenStrategistRequestsLaunch()
    {
        var runtime = CreateRuntime();
        var targetColonyId = GetColony(GetWorld(runtime), Faction.Aetheri).Id;
        runtime = CreateRuntime(FixedLaunchStrategist.For(Faction.Aetheri, targetColonyId, requestedWarriors: 1));
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        GetWorld(runtime).SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.Neutral);

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_SameFactionInjectedDecisionSuppressesLaunch()
    {
        var runtime = CreateRuntime();
        var ownerColonyId = GetColony(GetWorld(runtime), Faction.Obsidari).Id;
        runtime = CreateRuntime(FixedLaunchStrategist.For(Faction.Obsidari, ownerColonyId, requestedWarriors: 1));
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_InjectedLaunchWithoutScoutIntelSuppressesLaunch()
    {
        var runtime = CreateRuntime();
        var targetColonyId = GetColony(GetWorld(runtime), Faction.Aetheri).Id;
        runtime = CreateRuntime(FixedLaunchStrategist.For(Faction.Aetheri, targetColonyId, requestedWarriors: 1));
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic no-scout apply gate test");

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_InjectedLaunchWithStaleActiveScoutIntelSuppressesLaunch()
    {
        var runtime = CreateRuntime();
        var targetColonyId = GetColony(GetWorld(runtime), Faction.Aetheri).Id;
        runtime = CreateRuntime(FixedLaunchStrategist.For(Faction.Aetheri, targetColonyId, requestedWarriors: 1));
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks * 2);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic stale-scout apply gate test");
        AddScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri, createdTick: 0);
        Assert.Single(runtime.ScoutIntel);
        Assert.True(Assert.Single(runtime.ScoutIntel).TicksSinceRefresh > 30);

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
        Assert.Single(runtime.GetSnapshot().ScoutIntel);
    }

    [Fact]
    public void OrganicCampaign_InsufficientEligibleMembersSuppressLaunch()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 0);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic insufficient test");

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_HomeDefenseReserveSuppressesLaunch()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 1);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic reserve test");

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_OwnerCapPreventsRepeatedLaunchEveryCadence()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 3);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic cap test");
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        runtime.AdvanceTick(0f);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);

        Assert.Single(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_SamePairCapIgnoresResolvedHistoricalCampaigns()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 3);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic same-pair test");
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);
        runtime.AdvanceTick(0f);
        ResolveCampaign(GetLiveCampaigns(runtime).Single());

        AdvanceTicks(runtime, OrganicCampaignCadenceTicks - 1);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 3);
        runtime.AdvanceTick(0f);

        Assert.Equal(2, runtime.Campaigns.Count);
    }

    [Fact]
    public void OrganicCampaign_UnorderedSamePairCapPreventsMirrorLaunchInSameCadence()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Sylvars, count: 2);
        PrepareAdditionalEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2, PersonRole.Warrior);
        runtime.DeclareWar(Faction.Sylvars, Faction.Obsidari, "organic mirror cap test");
        AddActionableScoutIntel(runtime, Faction.Sylvars, Faction.Obsidari);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Sylvars);

        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.True(
            (campaign.OwnerFaction == Faction.Sylvars && campaign.TargetFaction == Faction.Obsidari)
            || (campaign.OwnerFaction == Faction.Obsidari && campaign.TargetFaction == Faction.Sylvars));
    }

    [Fact]
    public void OrganicCampaign_RoutePathPreflightSuppressesLaunchBeforeCreation()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        var world = GetWorld(runtime);
        var target = GetColony(world, Faction.Aetheri);
        MoveTargetOriginToBlockableTile(world, target);
        Assert.True(world.TryAddWoodWall(target, target.Origin));
        world.SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.War);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_PreservesSelectedTargetColonyThroughCreation()
    {
        var runtime = CreateRuntime();
        var world = GetWorld(runtime);
        var alternateTarget = AddTestColony(world, Faction.Aetheri);
        var strategist = FixedLaunchStrategist.For(Faction.Aetheri, alternateTarget.Id, requestedWarriors: 1);
        runtime = CreateRuntime(strategist);
        world = GetWorld(runtime);
        alternateTarget = AddTestColony(world, Faction.Aetheri);
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        world.SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.War);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri, alternateTarget.Id);

        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(alternateTarget.Id, campaign.TargetColonyId);
    }

    [Fact]
    public void OrganicCampaign_BlockedActorsAreExcludedFromEligibleCount()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        var members = PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        members[0].Current = Job.Flee;
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic blocked actor test");

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_WarriorOnlyEligiblePoolCanLaunch()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic warrior-only test");
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        runtime.AdvanceTick(0f);

        Assert.Single(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_CarrierOnlyEligiblePoolSuppressesLaunch()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareRoleOnlyCampaignMembers(runtime, Faction.Obsidari, count: 3, PersonRole.SupplyCarrier);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic carrier-only test");

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_HunterOnlyEligiblePoolSuppressesLaunch()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareHunterOnlyCampaignMembers(runtime, Faction.Obsidari, count: 3);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic hunter-only test");

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void OrganicCampaign_RequestedCountDoesNotExceedAssemblyEligiblePoolMinusReserve()
    {
        var runtime = CreateRuntime();
        var targetColonyId = GetColony(GetWorld(runtime), Faction.Aetheri).Id;
        runtime = CreateRuntime(FixedLaunchStrategist.For(Faction.Aetheri, targetColonyId, requestedWarriors: 10));
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 3);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic clamp test");
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(2, campaign.Army.RequestedMemberCount);
    }

    [Theory]
    [InlineData(CampaignStrategyDecisionKind.RequestConvoy)]
    [InlineData(CampaignStrategyDecisionKind.ReinforceCampaign)]
    [InlineData(CampaignStrategyDecisionKind.AbortCampaign)]
    public void OrganicCampaign_NonLaunchAdvisoryDecisionsDoNotCreateCampaigns(CampaignStrategyDecisionKind decisionKind)
    {
        var runtime = CreateRuntime(new FixedDecisionStrategist(new CampaignStrategyDecision(decisionKind, CampaignStrategyReasonCode.CampaignSupplyLow)));
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 3);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "organic advisory test");

        runtime.AdvanceTick(0f);

        Assert.Empty(runtime.Campaigns);
    }

    private static void PrepareTieBreakRuntime(SimulationRuntime runtime)
    {
        EnableDiplomacyAndCombat(runtime);
        AdvanceTicks(runtime, OrganicCampaignCadenceTicks);
        PrepareEligibleCampaignMembers(runtime, Faction.Obsidari, count: 2);
        var world = GetWorld(runtime);
        world.SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.Hostile);
        world.SetFactionStance(Faction.Obsidari, Faction.Chirita, Stance.Hostile);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Chirita);
    }

    private static SimulationRuntime CreateRuntime(ICampaignStrategist? strategist = null)
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 12, techPath, aiOptions: null, randomSeed: 9601, campaignStrategist: strategist);
    }

    private static void EnableDiplomacyAndCombat(SimulationRuntime runtime)
    {
        var world = GetWorld(runtime);
        world.EnableDiplomacy = true;
        world.EnableCombatPrimitives = true;
    }

    private static IReadOnlyList<Person> PrepareEligibleCampaignMembers(SimulationRuntime runtime, Faction faction, int count)
    {
        var world = GetWorld(runtime);
        SetAllCampaignCandidatesIneligible(world);
        return PrepareAdditionalEligibleCampaignMembers(runtime, faction, count, PersonRole.Warrior);
    }

    private static IReadOnlyList<Person> PrepareRoleOnlyCampaignMembers(SimulationRuntime runtime, Faction faction, int count, PersonRole role)
    {
        var world = GetWorld(runtime);
        SetAllCampaignCandidatesIneligible(world);
        return PrepareAdditionalEligibleCampaignMembers(runtime, faction, count, role);
    }

    private static IReadOnlyList<Person> PrepareHunterOnlyCampaignMembers(SimulationRuntime runtime, Faction faction, int count)
    {
        var world = GetWorld(runtime);
        SetAllCampaignCandidatesIneligible(world);
        var members = SelectCampaignMemberCandidates(world, faction, count);
        foreach (var member in members)
            member.Profession = Profession.Hunter;

        return members;
    }

    private static IReadOnlyList<Person> PrepareAdditionalEligibleCampaignMembers(SimulationRuntime runtime, Faction faction, int count, PersonRole role)
    {
        var members = SelectCampaignMemberCandidates(GetWorld(runtime), faction, count);
        foreach (var member in members)
            member.AssignRole(role);

        return members;
    }

    private static Person[] SelectCampaignMemberCandidates(World world, Faction faction, int count)
    {
        var members = world._people
            .Where(person => person.Home.Faction == faction && person.Health > 0f)
            .OrderBy(person => person.Id)
            .Take(count)
            .ToArray();
        Assert.True(members.Length >= count);
        return members;
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

    private static Colony GetColony(World world, Faction faction)
    {
        var colony = world._colonies.FirstOrDefault(candidate => candidate.Faction == faction);
        Assert.NotNull(colony);
        return colony!;
    }

    private static Colony AddTestColony(World world, Faction faction)
    {
        var source = GetColony(world, faction);
        var origin = FindBuildableNear(world, source, minDistanceFromOrigin: 4);
        var colony = new Colony(world._colonies.Max(candidate => candidate.Id) + 100, origin);
        SetColonyFaction(colony, faction);
        world._colonies.Add(colony);
        return colony;
    }

    private static void SetColonyFaction(Colony colony, Faction faction)
    {
        var factionField = typeof(Colony).GetField("<Faction>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(factionField);
        factionField!.SetValue(colony, faction);
    }

    private static void MoveTargetOriginToBlockableTile(World world, Colony target)
        => target.Origin = FindBuildableNear(world, target, minDistanceFromOrigin: 2);

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

        throw new InvalidOperationException("Could not find a buildable organic campaign test tile.");
    }

    private static void AdvanceOneCadenceTick(SimulationRuntime runtime)
    {
        runtime.AdvanceTick(0f);
    }

    private static void AdvanceTicks(SimulationRuntime runtime, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            runtime.AdvanceTick(0f);
    }

    private static void ResolveCampaign(CampaignState campaign)
    {
        Assert.True(campaign.Resolve(new CampaignResolutionApplication(
            Kind: CampaignResolutionKind.DefenderHeld,
            Reason: CampaignResolutionReasons.DefenderTimeout,
            ResolvedTick: 200,
            AttackerFaction: campaign.OwnerFaction,
            DefenderFaction: campaign.TargetFaction,
            OriginColonyId: campaign.OriginColonyId,
            TargetColonyId: campaign.TargetColonyId,
            TargetStructureId: -1,
            LootFood: 0,
            LootWood: 0,
            LootStone: 0,
            LootGold: 0,
            WarScoreDelta: 0,
            CumulativeWarScore: 0,
            PeaceEligible: false,
            PeaceApplied: false,
            TreatyKind: CampaignResolutionReasons.None)));
    }

    private static List<CampaignState> GetLiveCampaigns(SimulationRuntime runtime)
    {
        var campaignsField = typeof(SimulationRuntime).GetField("_campaigns", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(campaignsField);
        var campaigns = Assert.IsAssignableFrom<List<CampaignState>>(campaignsField!.GetValue(runtime));
        return campaigns;
    }

    private static void AddActionableScoutIntel(SimulationRuntime runtime, Faction ownerFaction, Faction targetFaction)
        => AddScoutIntel(runtime, ownerFaction, targetFaction, observedColonyId: null, runtime.Tick);

    private static void AddActionableScoutIntel(SimulationRuntime runtime, Faction ownerFaction, Faction targetFaction, int observedColonyId)
        => AddScoutIntel(runtime, ownerFaction, targetFaction, observedColonyId, runtime.Tick);

    private static void AddScoutIntel(SimulationRuntime runtime, Faction ownerFaction, Faction targetFaction, long createdTick)
        => AddScoutIntel(runtime, ownerFaction, targetFaction, observedColonyId: null, createdTick);

    private static void AddScoutIntel(
        SimulationRuntime runtime,
        Faction ownerFaction,
        Faction targetFaction,
        int? observedColonyId,
        long createdTick)
    {
        var world = GetWorld(runtime);
        var owner = GetColony(world, ownerFaction);
        var target = observedColonyId.HasValue
            ? world._colonies.First(colony => colony.Id == observedColonyId.Value && colony.Faction == targetFaction)
            : GetColony(world, targetFaction);
        var sourceActor = SelectCampaignMemberCandidates(world, ownerFaction, 1)[0];
        sourceActor.AssignRole(PersonRole.Scout);
        var scoutIntel = GetScoutIntelStates(runtime);
        scoutIntel.Add(new ScoutIntelState(
            intelId: scoutIntel.Count + 1,
            ownerFaction: owner.Faction,
            observedFaction: target.Faction,
            observedColonyId: target.Id,
            observationKind: ScoutIntelObservationKind.Colony,
            x: target.Origin.x,
            y: target.Origin.y,
            sourceActorId: sourceActor.Id,
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

    private sealed class FixedDecisionStrategist : ICampaignStrategist
    {
        private readonly CampaignStrategyDecision _decision;

        public FixedDecisionStrategist(CampaignStrategyDecision decision)
        {
            _decision = decision;
        }

        public CampaignStrategyDecision Decide(in CampaignStrategyContext context) => _decision;
    }

    private sealed class RecordingStrategist : ICampaignStrategist
    {
        public int CallCount { get; private set; }

        public CampaignStrategyDecision Decide(in CampaignStrategyContext context)
        {
            CallCount++;
            return new CampaignStrategyDecision(
                CampaignStrategyDecisionKind.HoldDefensivePosture,
                CampaignStrategyReasonCode.LaunchDisabled);
        }
    }

    private sealed class FixedLaunchStrategist : ICampaignStrategist
    {
        private readonly Faction _targetFaction;
        private readonly int _targetColonyId;
        private readonly int _requestedWarriors;

        private FixedLaunchStrategist(Faction targetFaction, int targetColonyId, int requestedWarriors)
        {
            _targetFaction = targetFaction;
            _targetColonyId = targetColonyId;
            _requestedWarriors = requestedWarriors;
        }

        public static FixedLaunchStrategist For(Faction targetFaction, int targetColonyId, int requestedWarriors)
            => new(targetFaction, targetColonyId, requestedWarriors);

        public CampaignStrategyDecision Decide(in CampaignStrategyContext context)
            => new(
                CampaignStrategyDecisionKind.LaunchCampaign,
                CampaignStrategyReasonCode.TargetPressureAndAdvantage,
                TargetFactionId: (int)_targetFaction,
                TargetColonyId: _targetColonyId,
                RequestedWarriors: _requestedWarriors,
                Score: 1.0f);
    }
}
