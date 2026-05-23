using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WorldSim.Runtime;
using WorldSim.Simulation;
using WorldSim.Simulation.Combat;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave9CampaignRuntimeTests
{
    [Fact]
    public void TryCreateCampaign_Success_PersistsOneCampaignAndOneArmy()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 4);

        Assert.True(result.Success);
        Assert.Equal(CampaignCreationStatus.Created, result.Status);
        Assert.Equal(1, result.CampaignId);
        Assert.Equal(1, result.ArmyId);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(1, campaign.CampaignId);
        Assert.Equal(1, campaign.ArmyId);
        Assert.Equal(Faction.Obsidari, campaign.OwnerFaction);
        Assert.Equal(Faction.Aetheri, campaign.TargetFaction);
        Assert.Equal(CampaignPhase.AssemblingPending, campaign.Phase);
        Assert.Equal(0, campaign.CreatedTick);
        Assert.Equal(campaign.OriginColonyId, campaign.RouteIntent.OriginColonyId);
        Assert.Equal(campaign.TargetColonyId, campaign.RouteIntent.TargetColonyId);
        Assert.Equal(campaign.Army.ArmyId, campaign.ArmyId);
        Assert.Equal(Faction.Obsidari, campaign.Army.OwnerFaction);
        Assert.Equal(4, campaign.Army.RequestedMemberCount);
        Assert.Equal(0, campaign.Army.AssignedMemberCount);
        Assert.Empty(campaign.Army.MemberActorIds);
        Assert.False(campaign.Army.HasRallyPoint);
        Assert.False(campaign.Army.IsAssembled);
        Assert.Equal(-1, campaign.Army.AssemblyStartedTick);
        Assert.Equal(-1, campaign.Army.AssemblyCompletedTick);
        Assert.Equal("army:1", campaign.Army.ForageConsumerKey);
        Assert.Equal(0, campaign.Army.Supply.FractionalFoodDemand);
        Assert.Equal(0, campaign.Army.Supply.SustainedOutOfSupplyTicks);
        Assert.Equal(0, campaign.Army.RationPool.RationPoolFood);
        Assert.False(campaign.Army.Carrier.HasAssignedCarrier);
        Assert.Equal(0, campaign.Army.Foraging.Attempts);
    }

    [Fact]
    public void TryCreateCampaign_CreatesDeterministicMonotonicIds()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        var first = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 3);
        var second = runtime.TryCreateCampaign(Faction.Sylvars, Faction.Chirita, requestedMemberCount: 2);

        Assert.Equal(1, first.CampaignId);
        Assert.Equal(1, first.ArmyId);
        Assert.Equal(2, second.CampaignId);
        Assert.Equal(2, second.ArmyId);
        Assert.Equal(new[] { 1, 2 }, runtime.Campaigns.Select(campaign => campaign.CampaignId).ToArray());
        Assert.Equal(new[] { 1, 2 }, runtime.Campaigns.Select(campaign => campaign.ArmyId).ToArray());
    }

    [Fact]
    public void TryCreateCampaign_FailedAttemptDoesNotConsumeIds()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        var first = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 3);
        var failed = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Obsidari, requestedMemberCount: 3);
        var second = runtime.TryCreateCampaign(Faction.Sylvars, Faction.Chirita, requestedMemberCount: 2);

        Assert.Equal(1, first.CampaignId);
        Assert.Equal(1, first.ArmyId);
        Assert.False(failed.Success);
        Assert.Equal(CampaignCreationStatus.SameFaction, failed.Status);
        Assert.Null(failed.CampaignId);
        Assert.Null(failed.ArmyId);
        Assert.Equal(2, second.CampaignId);
        Assert.Equal(2, second.ArmyId);
        Assert.Equal(new[] { 1, 2 }, runtime.Campaigns.Select(campaign => campaign.CampaignId).ToArray());
        Assert.Equal(new[] { 1, 2 }, runtime.Campaigns.Select(campaign => campaign.ArmyId).ToArray());
    }

    [Theory]
    [InlineData(999, 2, 4, CampaignCreationStatus.InvalidOwnerFaction)]
    [InlineData(1, 999, 4, CampaignCreationStatus.InvalidTargetFaction)]
    [InlineData(1, 1, 4, CampaignCreationStatus.SameFaction)]
    [InlineData(1, 2, 0, CampaignCreationStatus.InvalidRequestedMemberCount)]
    [InlineData(1, 2, -3, CampaignCreationStatus.InvalidRequestedMemberCount)]
    public void TryCreateCampaign_InvalidDomainInput_ReturnsDeterministicFailureStatus(
        int ownerFaction,
        int targetFaction,
        int requestedMemberCount,
        CampaignCreationStatus expectedStatus)
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        var result = runtime.TryCreateCampaign((Faction)ownerFaction, (Faction)targetFaction, requestedMemberCount);

        Assert.False(result.Success);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.CampaignId);
        Assert.Null(result.ArmyId);
        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void TryCreateCampaign_RuntimeUnavailable_ReturnsStatusWithoutThrowing()
    {
        var runtime = CreateRuntime();

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 4);

        Assert.False(result.Success);
        Assert.Equal(CampaignCreationStatus.CampaignRuntimeUnavailable, result.Status);
        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void TryCreateCampaign_OwnerColonyNotFound_ReturnsStatus()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        GetWorld(runtime)._colonies.RemoveAll(colony => colony.Faction == Faction.Obsidari);

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 4);

        Assert.False(result.Success);
        Assert.Equal(CampaignCreationStatus.OwnerColonyNotFound, result.Status);
        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void TryCreateCampaign_TargetColonyNotFound_ReturnsStatus()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        GetWorld(runtime)._colonies.RemoveAll(colony => colony.Faction == Faction.Aetheri);

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 4);

        Assert.False(result.Success);
        Assert.Equal(CampaignCreationStatus.TargetColonyNotFound, result.Status);
        Assert.Empty(runtime.Campaigns);
    }

    [Fact]
    public void AdvanceTick_NoEligibleMembers_DoesNotCompleteAssemblyOrMutateSupplyState()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        SetAllCampaignCandidatesIneligible(runtime);
        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 4);
        Assert.True(result.Success);
        var campaign = Assert.Single(runtime.Campaigns);
        var army = campaign.Army;
        var originStockBefore = GetWorld(runtime)._colonies.First(colony => colony.Id == campaign.OriginColonyId).Stock[Resource.Food];
        var targetStockBefore = GetWorld(runtime)._colonies.First(colony => colony.Id == campaign.TargetColonyId).Stock[Resource.Food];

        for (var i = 0; i < 3; i++)
        {
            runtime.AdvanceTick(0.25f);
        }

        var currentCampaign = Assert.Single(runtime.Campaigns);
        var currentArmy = currentCampaign.Army;
        Assert.Equal(CampaignPhase.AssemblingPending, currentCampaign.Phase);
        Assert.Equal(CampaignPhase.AssemblingPending, campaign.Phase);
        Assert.Equal(0, currentArmy.AssignedMemberCount);
        Assert.Empty(currentArmy.MemberActorIds);
        Assert.Empty(army.MemberActorIds);
        Assert.Equal(0, currentCampaign.RouteCounters.PathRequests);
        Assert.Equal(0, currentCampaign.RouteCounters.PathCacheHits);
        Assert.Equal(0, currentCampaign.RouteCounters.BlockedMovementChecks);
        Assert.Equal(0, currentCampaign.RouteCounters.RouteRecomputes);
        Assert.Equal(0, currentCampaign.RouteCounters.MarchProgressTicks);
        Assert.Equal(0, currentCampaign.RouteCounters.EncounterTicks);
        Assert.Equal(0, currentCampaign.RouteCounters.NoProgressTicks);
        Assert.Equal(0, campaign.RouteCounters.PathRequests);
        Assert.Equal(0, currentArmy.Supply.FractionalFoodDemand);
        Assert.Equal(0, currentArmy.Supply.SustainedOutOfSupplyTicks);
        Assert.Equal(0, currentArmy.RationPool.RationPoolFood);
        Assert.False(currentArmy.Carrier.HasAssignedCarrier);
        Assert.Equal(0, currentArmy.Foraging.Attempts);
        Assert.Equal(0, currentArmy.Foraging.Successes);
        Assert.Equal(0, currentArmy.Foraging.Failures);
        Assert.Equal(0, currentArmy.Foraging.FoodGained);
        Assert.Equal(0, army.Foraging.Attempts);
        Assert.Equal(originStockBefore, GetWorld(runtime)._colonies.First(colony => colony.Id == campaign.OriginColonyId).Stock[Resource.Food]);
        Assert.Equal(targetStockBefore, GetWorld(runtime)._colonies.First(colony => colony.Id == campaign.TargetColonyId).Stock[Resource.Food]);
    }

    [Fact]
    public void AdvanceTick_NoEligibleMembersMatchesControlActorState()
    {
        var campaignRuntime = CreateRuntime();
        var controlRuntime = CreateRuntime();
        EnableDiplomacyAndCombat(campaignRuntime);
        EnableDiplomacyAndCombat(controlRuntime);
        SetAllCampaignCandidatesIneligible(campaignRuntime);
        SetAllCampaignCandidatesIneligible(controlRuntime);

        var result = campaignRuntime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 4);
        Assert.True(result.Success);

        for (var i = 0; i < 3; i++)
        {
            campaignRuntime.AdvanceTick(0.25f);
            controlRuntime.AdvanceTick(0.25f);
        }

        var campaignActors = SnapshotActors(campaignRuntime);
        var controlActors = SnapshotActors(controlRuntime);
        Assert.Equal(controlActors, campaignActors);
    }

    [Fact]
    public void Campaigns_QueryReturnsDetachedRuntimeSnapshots()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        SetAllCampaignCandidatesIneligible(runtime);
        _ = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 4);
        var firstView = runtime.Campaigns;
        var firstSnapshot = Assert.Single(firstView);
        var roster = firstSnapshot.Army.MemberActorIds;

        Assert.IsType<CampaignRuntimeSnapshot>((object)firstSnapshot);
        Assert.IsNotType<CampaignState>((object)firstSnapshot);
        Assert.IsType<ArmyRuntimeSnapshot>((object)firstSnapshot.Army);
        Assert.IsNotType<ArmyState>((object)firstSnapshot.Army);
        Assert.IsNotType<ArmySupplyState>((object)firstSnapshot.Army.Supply);
        Assert.IsNotType<ArmyRationPoolState>((object)firstSnapshot.Army.RationPool);
        Assert.IsNotType<ArmySupplyCarrierState>((object)firstSnapshot.Army.Carrier);
        Assert.IsNotType<ArmyForagingState>((object)firstSnapshot.Army.Foraging);

        _ = runtime.TryCreateCampaign(Faction.Sylvars, Faction.Chirita, requestedMemberCount: 2);
        runtime.AdvanceTick(0.25f);

        Assert.Single(firstView);
        Assert.Equal(2, runtime.Campaigns.Count);
        Assert.Equal(1, firstSnapshot.CampaignId);
        Assert.Equal(1, firstSnapshot.ArmyId);
        Assert.Equal(CampaignPhase.AssemblingPending, firstSnapshot.Phase);
        Assert.Equal(0, firstSnapshot.RouteCounters.PathRequests);
        Assert.Equal(0, firstSnapshot.Army.Foraging.Attempts);
        var mutableCollection = Assert.IsAssignableFrom<ICollection<int>>(roster);
        Assert.True(mutableCollection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableCollection.Add(999));
        Assert.Empty(runtime.Campaigns.First().Army.MemberActorIds);
    }

    [Fact]
    public void AdvanceTick_AssemblyAddsRosterIncrementallyAndMovesTowardRally()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        SetAllCampaignCandidatesIneligible(runtime);
        var candidates = GetPeople(world, Faction.Obsidari).Take(3).ToArray();
        Assert.Equal(3, candidates.Length);
        candidates[0].AssignRole(PersonRole.Warrior);
        candidates[1].AssignRole(PersonRole.SupplyCarrier);
        candidates[2].Profession = Profession.Hunter;
        PlaceActor(world, candidates[0], owner, minDistanceFromOrigin: 6);
        var beforePos = candidates[0].Pos;

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 3);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);
        runtime.AdvanceTick(0f);

        var first = Assert.Single(runtime.Campaigns);
        Assert.Equal(CampaignPhase.Assembling, first.Phase);
        Assert.True(first.Army.HasRallyPoint);
        Assert.Equal(1, first.Army.AssignedMemberCount);
        Assert.Equal(candidates[0].Id, Assert.Single(first.Army.MemberActorIds));
        Assert.True(Manhattan(candidates[0].Pos, (first.Army.RallyX, first.Army.RallyY)) < Manhattan(beforePos, (first.Army.RallyX, first.Army.RallyY)));
        Assert.InRange(Manhattan(candidates[0].Pos, beforePos), 0, 2);
        Assert.False(candidates[2].HasRole(PersonRole.Warrior));
        AssertAssemblyDoesNotRunMarchOrSupply(first);

        var retained = first;
        candidates[1].Current = Job.Idle;
        candidates[1].SetCombatAssignment(null, null, Formation.Line, isCommander: false);
        runtime.AdvanceTick(0f);

        var second = Assert.Single(runtime.Campaigns);
        Assert.Equal(new[] { candidates[0].Id }, retained.Army.MemberActorIds.ToArray());
        Assert.Equal(2, second.Army.AssignedMemberCount);
        Assert.Equal(new[] { candidates[0].Id, candidates[1].Id }, second.Army.MemberActorIds.ToArray());
        Assert.True(second.Army.Carrier.HasAssignedCarrier);
        Assert.Equal(candidates[1].Id, second.Army.Carrier.AssignedCarrierActorId);
        Assert.Equal(-1, second.Army.Carrier.LastSupplyTick);
        Assert.Equal(ArmySupplySourceMode.None, second.Army.Carrier.LastSupplySource);
        AssertAssemblyDoesNotRunMarchOrSupply(second);
    }

    [Fact]
    public void AdvanceTick_AssemblyCompletesToMarchingWithoutStartingMarchScope()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        SetAllCampaignCandidatesIneligible(runtime);
        var candidate = GetPeople(GetWorld(runtime), Faction.Obsidari).First();
        candidate.Profession = Profession.Hunter;

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);

        for (var i = 0; i < 16 && runtime.Campaigns.Single().Phase != CampaignPhase.Marching; i++)
            runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(CampaignPhase.Marching, campaign.Phase);
        Assert.True(campaign.Army.IsAssembled);
        Assert.Equal(1, campaign.Army.AssignedMemberCount);
        Assert.InRange(Manhattan(candidate.Pos, (campaign.Army.RallyX, campaign.Army.RallyY)), 0, 1);
        AssertAssemblyDoesNotRunMarchOrSupply(campaign);
    }

    [Fact]
    public void AdvanceTick_IncompleteRosterDoesNotCompleteAssemblyOrChurnLifecycleCounters()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        SetAllCampaignCandidatesIneligible(runtime);
        var candidate = GetPeople(GetWorld(runtime), Faction.Obsidari).First();
        candidate.Profession = Profession.Hunter;

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 3);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);

        for (var i = 0; i < 80; i++)
            runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        var wave9 = runtime.BuildScenarioWave9TelemetrySnapshot("partial_roster_regression");
        Assert.Equal(CampaignPhase.Assembling, campaign.Phase);
        Assert.False(campaign.Army.IsAssembled);
        Assert.Equal(1, campaign.Army.AssignedMemberCount);
        Assert.Equal(3, campaign.Army.RequestedMemberCount);
        Assert.Equal(-1, campaign.Army.AssemblyCompletedTick);
        Assert.Equal(0, wave9.AssemblyCompletedCount);
        Assert.Equal(0, wave9.MarchStartedCount);
        Assert.Equal(0, wave9.CampaignsReturnedOrAborted);
        Assert.Equal(0, campaign.RouteCounters.MarchProgressTicks);
        Assert.Equal(0, wave9.CampaignRouteProgress);
    }

    [Fact]
    public void AdvanceTick_AssemblyExcludesAssignedRoutingAndCombatActors()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        SetAllCampaignCandidatesIneligible(runtime);
        var people = GetPeople(GetWorld(runtime), Faction.Obsidari).Take(3).ToArray();
        Assert.Equal(3, people.Length);
        foreach (var person in people)
            person.Profession = Profession.Hunter;
        people[0].BeginRouting(ticks: 4);
        people[1].SetCombatAssignment(groupId: 10, battleId: 20, Formation.Line, isCommander: false);

        var first = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        var second = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Chirita, requestedMemberCount: 1);
        Assert.True(first.Success);
        Assert.True(second.Success);
        DisableCombatResolutionForCampaignTest(runtime);

        runtime.AdvanceTick(0f);

        var campaigns = runtime.Campaigns.OrderBy(campaign => campaign.CampaignId).ToArray();
        Assert.Equal(new[] { people[2].Id }, campaigns[0].Army.MemberActorIds.ToArray());
        Assert.Empty(campaigns[1].Army.MemberActorIds);
        Assert.DoesNotContain(people[0].Id, campaigns.SelectMany(campaign => campaign.Army.MemberActorIds));
        Assert.DoesNotContain(people[1].Id, campaigns.SelectMany(campaign => campaign.Army.MemberActorIds));
    }

    [Fact]
    public void AdvanceTick_AssemblyIgnoresHardCombatJobs()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        SetAllCampaignCandidatesIneligible(runtime);
        var people = GetPeople(GetWorld(runtime), Faction.Obsidari).Take(2).ToArray();
        Assert.Equal(2, people.Length);
        foreach (var person in people)
            person.Profession = Profession.Hunter;
        people[0].Current = Job.RaidBorder;

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);

        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(new[] { people[1].Id }, campaign.Army.MemberActorIds.ToArray());
    }

    [Fact]
    public void AdvanceTick_PrunesAssignedMemberThatBecomesRoutingAndRecruitsReplacement()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        SetAllCampaignCandidatesIneligible(runtime);
        var people = GetPeople(world, Faction.Obsidari).Take(2).ToArray();
        Assert.Equal(2, people.Length);
        foreach (var person in people)
            person.Profession = Profession.Hunter;
        PlaceActor(world, people[0], owner, minDistanceFromOrigin: 6);

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);
        runtime.AdvanceTick(0f);
        Assert.Equal(new[] { people[0].Id }, runtime.Campaigns.Single().Army.MemberActorIds.ToArray());

        people[0].BeginRouting(ticks: 4);
        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(new[] { people[1].Id }, campaign.Army.MemberActorIds.ToArray());
        Assert.DoesNotContain(people[0].Id, campaign.Army.MemberActorIds);
        AssertAssemblyDoesNotRunMarchOrSupply(campaign);
    }

    [Fact]
    public void AdvanceTick_PrunesAssignedMemberThatEntersActiveCombatAndRecruitsReplacement()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        SetAllCampaignCandidatesIneligible(runtime);
        var people = GetPeople(world, Faction.Obsidari).Take(2).ToArray();
        Assert.Equal(2, people.Length);
        foreach (var person in people)
            person.Profession = Profession.Hunter;
        PlaceActor(world, people[0], owner, minDistanceFromOrigin: 6);

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);
        runtime.AdvanceTick(0f);
        Assert.Equal(new[] { people[0].Id }, runtime.Campaigns.Single().Army.MemberActorIds.ToArray());

        people[0].SetCombatAssignment(groupId: 100, battleId: 200, Formation.Line, isCommander: false);
        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(new[] { people[1].Id }, campaign.Army.MemberActorIds.ToArray());
        Assert.DoesNotContain(people[0].Id, campaign.Army.MemberActorIds);
        AssertAssemblyDoesNotRunMarchOrSupply(campaign);
    }

    [Fact]
    public void AdvanceTick_PrunesAssignedMemberThatGetsHardCombatJobAndRecruitsReplacement()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        SetAllCampaignCandidatesIneligible(runtime);
        var people = GetPeople(world, Faction.Obsidari).Take(2).ToArray();
        Assert.Equal(2, people.Length);
        foreach (var person in people)
            person.Profession = Profession.Hunter;
        PlaceActor(world, people[0], owner, minDistanceFromOrigin: 6);

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);
        runtime.AdvanceTick(0f);
        Assert.Equal(new[] { people[0].Id }, runtime.Campaigns.Single().Army.MemberActorIds.ToArray());

        people[0].Current = Job.AttackStructure;
        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(new[] { people[1].Id }, campaign.Army.MemberActorIds.ToArray());
        Assert.DoesNotContain(people[0].Id, campaign.Army.MemberActorIds);
        AssertAssemblyDoesNotRunMarchOrSupply(campaign);
    }

    [Fact]
    public void AdvanceTick_PrunesRemovedAssignedMemberAndRecruitsReplacement()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        SetAllCampaignCandidatesIneligible(runtime);
        var people = GetPeople(world, Faction.Obsidari).Take(2).ToArray();
        Assert.Equal(2, people.Length);
        foreach (var person in people)
            person.Profession = Profession.Hunter;
        PlaceActor(world, people[0], owner, minDistanceFromOrigin: 6);

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);
        runtime.AdvanceTick(0f);
        Assert.Equal(new[] { people[0].Id }, runtime.Campaigns.Single().Army.MemberActorIds.ToArray());

        world._people.Remove(people[0]);
        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(new[] { people[1].Id }, campaign.Army.MemberActorIds.ToArray());
        Assert.DoesNotContain(people[0].Id, campaign.Army.MemberActorIds);
        AssertAssemblyDoesNotRunMarchOrSupply(campaign);
    }

    [Fact]
    public void AdvanceTick_FullRosterWithMissingMemberDoesNotCompleteWithoutReplacement()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        SetAllCampaignCandidatesIneligible(runtime);
        var person = GetPeople(world, Faction.Obsidari).First();
        person.Profession = Profession.Hunter;
        PlaceActor(world, person, owner, minDistanceFromOrigin: 6);

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);
        runtime.AdvanceTick(0f);
        Assert.Equal(new[] { person.Id }, runtime.Campaigns.Single().Army.MemberActorIds.ToArray());

        world._people.Remove(person);
        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(CampaignPhase.Assembling, campaign.Phase);
        Assert.Empty(campaign.Army.MemberActorIds);
        Assert.Equal(0, campaign.Army.AssignedMemberCount);
        Assert.False(campaign.Army.IsAssembled);
        Assert.Equal(0, campaign.RouteCounters.MarchProgressTicks);
    }

    [Fact]
    public void AdvanceTick_PrunesAssignedCarrierAndClearsStaleCarrierState()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        SetAllCampaignCandidatesIneligible(runtime);
        var carrier = GetPeople(world, Faction.Obsidari).First();
        carrier.AssignRole(PersonRole.Warrior | PersonRole.SupplyCarrier);
        PlaceActor(world, carrier, owner, minDistanceFromOrigin: 6);

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);
        runtime.AdvanceTick(0f);
        Assert.Equal(carrier.Id, runtime.Campaigns.Single().Army.Carrier.AssignedCarrierActorId);

        carrier.BeginRouting(ticks: 4);
        runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Empty(campaign.Army.MemberActorIds);
        Assert.False(campaign.Army.Carrier.HasAssignedCarrier);
        Assert.Equal(-1, campaign.Army.Carrier.AssignedCarrierActorId);
        Assert.False(carrier.HasRole(PersonRole.SupplyCarrier));
    }

    [Fact]
    public void AdvanceTick_PositiveDtAssemblyMovementRemainsFinalPostTickPosition()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        SetAllCampaignCandidatesIneligible(runtime);
        var person = GetPeople(world, Faction.Obsidari).First();
        person.Profession = Profession.Hunter;
        PlaceActor(world, person, owner, minDistanceFromOrigin: 8);

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);
        var before = person.Pos;

        runtime.AdvanceTick(0.25f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(new[] { person.Id }, campaign.Army.MemberActorIds.ToArray());
        Assert.True(Manhattan(person.Pos, (campaign.Army.RallyX, campaign.Army.RallyY)) < Manhattan(before, (campaign.Army.RallyX, campaign.Army.RallyY)));
        Assert.NotEqual(before, person.Pos);
        AssertAssemblyDoesNotRunMarchOrSupply(campaign);
    }

    [Fact]
    public void AdvanceTick_MarchingHealthZeroMemberReturnsToAssemblyBeforeMarch()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        fixture.Members[0].Health = 0f;

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignPhase.Assembling, campaign.Phase);
        Assert.False(campaign.Army.IsAssembled);
        Assert.Equal(-1, campaign.Army.AssemblyCompletedTick);
        Assert.Empty(campaign.Army.MemberActorIds);
        AssertAssemblyDoesNotRunMarchOrSupply(campaign);
    }

    [Fact]
    public void AdvanceTick_MarchingIsolatedInCombatMemberReturnsToAssemblyBeforeMarch()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        fixture.Members[0].MarkCombatPresence(fixture.World);

        fixture.Runtime.AdvanceTick(0f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignPhase.Assembling, campaign.Phase);
        Assert.False(campaign.Army.IsAssembled);
        Assert.Empty(campaign.Army.MemberActorIds);
        AssertAssemblyDoesNotRunMarchOrSupply(campaign);
    }

    [Fact]
    public void AdvanceTick_WorldUpdateInducedCombatInvalidationReturnsToAssemblyBeforeMarch()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        fixture.World.EnableCombatPrimitives = true;
        fixture.Runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "campaign invalidation test");
        var hostile = GetPeople(fixture.World, Faction.Aetheri).First();
        hostile.Pos = FindAdjacentLand(fixture.World, fixture.Members[0].Pos);

        fixture.Runtime.AdvanceTick(0.25f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignPhase.Assembling, campaign.Phase);
        Assert.False(campaign.Army.IsAssembled);
        Assert.Empty(campaign.Army.MemberActorIds);
        AssertAssemblyDoesNotRunMarchOrSupply(campaign);
    }

    [Fact]
    public void AdvanceTick_MarchingPruneAddsAtMostOneReplacementBeforeNextMarchTick()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 3);
        var original = fixture.Runtime.Campaigns.Single().Army.MemberActorIds.ToArray();
        var assigned = fixture.Members.Single(member => member.Id == original[0]);
        assigned.BeginRouting(ticks: 4);

        fixture.Runtime.AdvanceTick(0f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        var replacementIds = fixture.Members.Where(member => member.Id != assigned.Id).Select(member => member.Id).ToArray();
        Assert.DoesNotContain(assigned.Id, campaign.Army.MemberActorIds);
        var replacement = Assert.Single(campaign.Army.MemberActorIds, replacementIds.Contains);
        Assert.Contains(replacement, replacementIds);
        Assert.Equal(1, campaign.Army.AssignedMemberCount);
        Assert.Equal(original[0], assigned.Id);
        AssertAssemblyDoesNotRunMarchOrSupply(campaign);
    }

    [Fact]
    public void AdvanceTick_MarchingUpdatesRouteCountersAndUsesCampaignCache()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);

        fixture.Runtime.AdvanceTick(1f);
        var afterFirstMarch = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignPhase.Marching, afterFirstMarch.Phase);
        Assert.Equal(1, afterFirstMarch.RouteCounters.PathRequests);
        Assert.Equal(1, afterFirstMarch.RouteCounters.RouteRecomputes);
        Assert.True(afterFirstMarch.RouteCounters.BlockedMovementChecks >= 1);
        Assert.Equal(1, afterFirstMarch.RouteCounters.MarchProgressTicks);

        fixture.Runtime.AdvanceTick(1f);
        var afterSecondMarch = Assert.Single(fixture.Runtime.Campaigns);
        Assert.True(afterSecondMarch.RouteCounters.PathCacheHits >= 1);
    }

    [Fact]
    public void AdvanceTick_TopologyChangeInvalidatesCampaignRouteCacheAndAvoidsBlockedNextStep()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        fixture.Runtime.AdvanceTick(1f);
        var live = GetLiveCampaignState(fixture.Runtime);
        var blockedNext = live.RouteCache.PeekNext();
        Assert.True(blockedNext.HasValue);
        var targetColony = GetColony(fixture.World, Faction.Aetheri);
        Assert.True(fixture.World.TryAddWoodWall(targetColony, blockedNext.Value));

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.True(campaign.RouteCounters.RouteRecomputes >= 2);
        Assert.True(campaign.RouteCounters.BlockedMovementChecks >= 2);
        Assert.NotEqual(blockedNext.Value, fixture.Members[0].Pos);
    }

    [Fact]
    public void AdvanceTick_DelayedTopologyChangeInvalidatesCampaignRouteCacheBeforeBlockedStep()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        fixture.Runtime.AdvanceTick(1f);
        var live = GetLiveCampaignState(fixture.Runtime);
        var cachedDelayedStep = live.RouteCache.Steps
            .Skip(live.RouteCache.NextIndex + 1)
            .Select(step => ((int x, int y)?)step)
            .FirstOrDefault();
        Assert.True(cachedDelayedStep.HasValue);
        var targetColony = GetColony(fixture.World, Faction.Aetheri);
        var topologyBefore = fixture.World.NavigationTopologyVersion;
        Assert.True(fixture.World.TryAddWoodWall(targetColony, cachedDelayedStep.Value));
        Assert.True(fixture.World.NavigationTopologyVersion > topologyBefore);
        var recomputesBefore = fixture.Runtime.Campaigns.Single().RouteCounters.RouteRecomputes;

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.True(campaign.RouteCounters.RouteRecomputes > recomputesBefore);
        Assert.NotEqual(cachedDelayedStep.Value, fixture.Members[0].Pos);
    }

    [Fact]
    public void AdvanceTick_MarchSupplyUsesRationPoolOrCarriedInventoryButNotBoth()
    {
        var carried = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        Assert.True(carried.Members[0].Inventory.TryAdd(ItemType.Food, 2));
        carried.Runtime.AdvanceTick(1f);
        var carriedCampaign = Assert.Single(carried.Runtime.Campaigns);
        Assert.Equal(ArmySupplySourceMode.CarriedInventory, carriedCampaign.Army.Carrier.LastSupplySource);

        var pooled = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        Assert.True(pooled.Members[0].Inventory.TryAdd(ItemType.Food, 2));
        var live = GetLiveCampaignState(pooled.Runtime);
        var origin = GetColony(pooled.World, Faction.Obsidari);
        origin.Stock[Resource.Food] = 100;
        _ = ArmyRationPoolSupplyModel.ReserveRations(origin, homePopulation: 12, armySize: 1, live.Army.RationPoolState);
        Assert.True(live.Army.RationPoolState.RationPoolFood > 0);

        pooled.Runtime.AdvanceTick(1f);

        var pooledCampaign = Assert.Single(pooled.Runtime.Campaigns);
        Assert.Equal(ArmySupplySourceMode.RationPool, pooledCampaign.Army.Carrier.LastSupplySource);
        Assert.True(pooledCampaign.Army.Carrier.LastSupplyTick >= 0);
        Assert.Equal(2, pooled.Members[0].Inventory.GetCount(ItemType.Food));
    }

    [Fact]
    public void AdvanceTick_NoProgressMarchingTickConsumesExactlyOneSupplySource()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        var member = fixture.Members[0];
        Assert.True(member.Inventory.TryAdd(ItemType.Food, 3));
        PrimeOneUnitSupplyDemand(GetLiveCampaignState(fixture.Runtime), member);
        fixture.World.MovementSpeedMultiplier = 0f;
        var beforeFood = member.Inventory.GetCount(ItemType.Food);

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignPhase.Marching, campaign.Phase);
        Assert.Equal(1, campaign.RouteCounters.NoProgressTicks);
        Assert.Equal(ArmySupplySourceMode.CarriedInventory, campaign.Army.Carrier.LastSupplySource);
        Assert.True(campaign.Army.Carrier.LastSupplyTick >= 0);
        Assert.Equal(beforeFood - 1, member.Inventory.GetCount(ItemType.Food));
        Assert.Equal(0, campaign.Army.RationPool.RationPoolFood);
    }

    [Fact]
    public void AdvanceTick_MovementSuccessMarchingTickDoesNotDoubleConsumeSupply()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        var member = fixture.Members[0];
        Assert.True(member.Inventory.TryAdd(ItemType.Food, 3));
        PrimeOneUnitSupplyDemand(GetLiveCampaignState(fixture.Runtime), member);
        var beforeFood = member.Inventory.GetCount(ItemType.Food);

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(1, campaign.RouteCounters.MarchProgressTicks);
        Assert.Equal(ArmySupplySourceMode.CarriedInventory, campaign.Army.Carrier.LastSupplySource);
        Assert.Equal(beforeFood - 1, member.Inventory.GetCount(ItemType.Food));
        Assert.Equal(0, campaign.Army.RationPool.RationPoolFood);
    }

    [Fact]
    public void AdvanceTick_ZeroDtMarchingTickDoesNotConsumeSupply()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        var member = fixture.Members[0];
        Assert.True(member.Inventory.TryAdd(ItemType.Food, 3));

        fixture.Runtime.AdvanceTick(0f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(ArmySupplySourceMode.None, campaign.Army.Carrier.LastSupplySource);
        Assert.Equal(-1, campaign.Army.Carrier.LastSupplyTick);
        Assert.Equal(3, member.Inventory.GetCount(ItemType.Food));
        Assert.Equal(0, campaign.Army.RationPool.RationPoolFood);
    }

    [Fact]
    public void AdvanceTick_UnderstrengthMarchingTickSkipsSupplyAndRouteWork()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        Assert.True(fixture.Members[0].Inventory.TryAdd(ItemType.Food, 3));
        fixture.Members[0].Health = 0f;

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignPhase.Assembling, campaign.Phase);
        Assert.Equal(0, campaign.RouteCounters.PathRequests);
        Assert.Equal(0, campaign.RouteCounters.NoProgressTicks);
        Assert.Equal(ArmySupplySourceMode.None, campaign.Army.Carrier.LastSupplySource);
        Assert.Equal(3, fixture.Members[0].Inventory.GetCount(ItemType.Food));
    }

    [Fact]
    public void AdvanceTick_SupplyInducedRoutingReturnsToAssemblyBeforeMovementOrEncounter()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        var member = fixture.Members[0];
        PrimeSupplyRoutingThreshold(GetLiveCampaignState(fixture.Runtime), member);
        Assert.False(member.IsRouting);
        var beforePos = member.Pos;

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.True(member.IsRouting);
        Assert.Equal(beforePos, member.Pos);
        Assert.Equal(CampaignPhase.Assembling, campaign.Phase);
        Assert.False(campaign.Army.IsAssembled);
        Assert.Equal(-1, campaign.Army.AssemblyCompletedTick);
        Assert.Empty(campaign.Army.MemberActorIds);
        Assert.Equal(ArmySupplySourceMode.CarriedInventory, campaign.Army.Carrier.LastSupplySource);
        Assert.True(campaign.Army.Carrier.LastSupplyTick >= 0);
        Assert.Equal(0, campaign.RouteCounters.PathRequests);
        Assert.Equal(0, campaign.RouteCounters.BlockedMovementChecks);
        Assert.Equal(0, campaign.RouteCounters.RouteRecomputes);
        Assert.Equal(0, campaign.RouteCounters.MarchProgressTicks);
        Assert.Equal(0, campaign.RouteCounters.EncounterTicks);
        Assert.Equal(0, campaign.RouteCounters.NoProgressTicks);
    }

    [Fact]
    public void AdvanceTick_PositiveDtAtEncounterObjectiveTicksSupplyBeforeEncounter()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        var member = fixture.Members[0];
        var owner = GetColony(fixture.World, Faction.Obsidari);
        var target = GetColony(fixture.World, Faction.Aetheri);
        Assert.False(fixture.World.IsMovementBlocked(target.Origin.x, target.Origin.y, owner.Id));
        member.Pos = target.Origin;
        PrimeSupplyRoutingThreshold(GetLiveCampaignState(fixture.Runtime), member);
        Assert.True(Manhattan(member.Pos, target.Origin) <= 1);
        var beforePos = member.Pos;
        var expectedSupplyTick = (int)fixture.Runtime.Tick;

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.True(member.IsRouting);
        Assert.Equal(beforePos, member.Pos);
        Assert.NotEqual(CampaignPhase.Encounter, campaign.Phase);
        Assert.Equal(CampaignPhase.Assembling, campaign.Phase);
        Assert.False(campaign.Army.IsAssembled);
        Assert.Equal(-1, campaign.Army.AssemblyCompletedTick);
        Assert.Empty(campaign.Army.MemberActorIds);
        Assert.Equal(ArmySupplySourceMode.CarriedInventory, campaign.Army.Carrier.LastSupplySource);
        Assert.Equal(expectedSupplyTick, campaign.Army.Carrier.LastSupplyTick);
        Assert.Equal(0, campaign.RouteCounters.PathRequests);
        Assert.Equal(0, campaign.RouteCounters.BlockedMovementChecks);
        Assert.Equal(0, campaign.RouteCounters.RouteRecomputes);
        Assert.Equal(0, campaign.RouteCounters.MarchProgressTicks);
        Assert.Equal(0, campaign.RouteCounters.EncounterTicks);
        Assert.Equal(0, campaign.RouteCounters.NoProgressTicks);
    }

    [Fact]
    public void AdvanceTick_FallbackResolvedObjectiveTriggersEncounterWhenOriginalTargetIsBlocked()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        var target = GetColony(fixture.World, Faction.Aetheri);
        BlockCampaignTargetRadius(fixture.World, target, fixture.World._colonies.First(c => c.Faction == Faction.Obsidari), radius: 1);
        var fallback = FindFirstPassableCampaignTarget(fixture.World, target, fixture.World._colonies.First(c => c.Faction == Faction.Obsidari));
        Assert.True(Manhattan(fallback, target.Origin) > 1);
        Assert.True(Manhattan(fallback, fixture.World._colonies.First(c => c.Faction == Faction.Obsidari).Origin) > 1);
        fixture.Members[0].Pos = fallback;

        fixture.Runtime.AdvanceTick(0f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal(CampaignPhase.Encounter, campaign.Phase);
        Assert.Equal(1, campaign.RouteCounters.EncounterTicks);
        Assert.Equal(0, campaign.RouteCounters.NoProgressTicks);
    }

    [Fact]
    public void AdvanceTick_NaturalMarchReachesEncounterAndRepeatedTicksRemainNonResolving()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        Assert.True(fixture.Members[0].Inventory.TryAdd(ItemType.Food, 3));
        var target = GetColony(fixture.World, Faction.Aetheri);
        var beforeStance = fixture.Runtime.GetSnapshot().FactionStances.First(entry =>
            entry.LeftFactionId == Math.Min((int)Faction.Obsidari, (int)Faction.Aetheri)
            && entry.RightFactionId == Math.Max((int)Faction.Obsidari, (int)Faction.Aetheri)).Stance;
        fixture.World.EnableDiplomacy = false;
        var wallPos = FindBuildableNear(fixture.World, target, minDistanceFromOrigin: 3);
        Assert.True(fixture.World.TryAddWoodWall(target, wallPos));
        var wall = Assert.Single(fixture.World.DefensiveStructures, structure => structure.Pos == wallPos);
        var wallHp = wall.Hp;

        for (var i = 0; i < 96 && fixture.Runtime.Campaigns.Single().Phase == CampaignPhase.Marching; i++)
            fixture.Runtime.AdvanceTick(1f);
        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        var afterStance = fixture.Runtime.GetSnapshot().FactionStances.First(entry =>
            entry.LeftFactionId == Math.Min((int)Faction.Obsidari, (int)Faction.Aetheri)
            && entry.RightFactionId == Math.Max((int)Faction.Obsidari, (int)Faction.Aetheri)).Stance;
        Assert.Equal(CampaignPhase.Encounter, campaign.Phase);
        Assert.True(campaign.RouteCounters.EncounterTicks >= 2);
        Assert.Equal(beforeStance, afterStance);
        Assert.Equal(wallHp, wall.Hp);
    }

    [Fact]
    public void AdvanceTick_EncounterTicksWithoutResolutionDiplomacyOrSiegeSideEffects()
    {
        var fixture = CreateMarchingCampaign(requestedMemberCount: 1, candidateCount: 1);
        var target = GetColony(fixture.World, Faction.Aetheri);
        var beforeStance = fixture.Runtime.GetSnapshot().FactionStances.First(entry =>
            entry.LeftFactionId == Math.Min((int)Faction.Obsidari, (int)Faction.Aetheri)
            && entry.RightFactionId == Math.Max((int)Faction.Obsidari, (int)Faction.Aetheri)).Stance;
        var wallPos = FindBuildableNear(fixture.World, target, minDistanceFromOrigin: 3);
        Assert.True(fixture.World.TryAddWoodWall(target, wallPos));
        var wall = Assert.Single(fixture.World.DefensiveStructures, structure => structure.Pos == wallPos);
        var wallHp = wall.Hp;
        fixture.Members[0].Pos = (target.Origin.x, target.Origin.y);

        fixture.Runtime.AdvanceTick(0f);

        var campaign = Assert.Single(fixture.Runtime.Campaigns);
        var afterStance = fixture.Runtime.GetSnapshot().FactionStances.First(entry =>
            entry.LeftFactionId == Math.Min((int)Faction.Obsidari, (int)Faction.Aetheri)
            && entry.RightFactionId == Math.Max((int)Faction.Obsidari, (int)Faction.Aetheri)).Stance;
        Assert.Equal(CampaignPhase.Encounter, campaign.Phase);
        Assert.Equal(1, campaign.RouteCounters.EncounterTicks);
        Assert.Equal(0, campaign.RouteCounters.MarchProgressTicks);
        Assert.Equal(-1, campaign.Army.Carrier.LastSupplyTick);
        Assert.Equal(beforeStance, afterStance);
        Assert.Equal(wallHp, wall.Hp);
    }

    [Fact]
    public void ExistingCampaignDirectorCommands_AreNotRegressed()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "campaign setup");
        runtime.ProposeTreaty(Faction.Aetheri, Faction.Obsidari, "ceasefire", "pause");

        Assert.Contains("Treaty 'ceasefire'", runtime.LastDirectorActionStatus, StringComparison.Ordinal);
        var stance = runtime.GetSnapshot().FactionStances.First(entry =>
            entry.LeftFactionId == Math.Min((int)Faction.Obsidari, (int)Faction.Aetheri)
            && entry.RightFactionId == Math.Max((int)Faction.Obsidari, (int)Faction.Aetheri));
        Assert.Equal("Hostile", stance.Stance);
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 12, techPath, aiOptions: null, randomSeed: 9601);
    }

    private static MarchingCampaignFixture CreateMarchingCampaign(int requestedMemberCount, int candidateCount)
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        SetAllCampaignCandidatesIneligible(runtime);
        var members = GetPeople(world, Faction.Obsidari).Take(candidateCount).ToArray();
        Assert.True(members.Length >= candidateCount);
        foreach (var member in members)
            member.Profession = Profession.Hunter;

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount);
        Assert.True(result.Success);
        DisableCombatResolutionForCampaignTest(runtime);

        for (var i = 0; i < 80 && runtime.Campaigns.Single().Phase != CampaignPhase.Marching; i++)
            runtime.AdvanceTick(0f);

        var campaign = Assert.Single(runtime.Campaigns);
        Assert.Equal(CampaignPhase.Marching, campaign.Phase);
        Assert.Equal(requestedMemberCount, campaign.Army.AssignedMemberCount);
        Assert.True(campaign.Army.IsAssembled);
        return new MarchingCampaignFixture(runtime, world, members);
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

    private static void EnableDiplomacyAndCombat(SimulationRuntime runtime)
    {
        var world = GetWorld(runtime);
        world.EnableDiplomacy = true;
        world.EnableCombatPrimitives = true;
    }

    private static void DisableCombatResolutionForCampaignTest(SimulationRuntime runtime)
        => GetWorld(runtime).EnableCombatPrimitives = false;

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

    private static void PrimeOneUnitSupplyDemand(CampaignState campaign, Person member)
    {
        var result = ArmySupplyModel.Tick(
            new[] { member },
            campaign.Army.SupplyState,
            dt: 49.5f);
        Assert.Equal(0, result.FoodConsumed);
        Assert.True(campaign.Army.SupplyState.FractionalFoodDemand > 0.98f);
    }

    private static void PrimeSupplyRoutingThreshold(CampaignState campaign, Person member)
    {
        member.ApplyStaminaDelta(-100f);
        SetSupplyState(campaign.Army.SupplyState, fractionalFoodDemand: 0.99f, sustainedOutOfSupplyTicks: 4);
        Assert.Equal(4, campaign.Army.SupplyState.SustainedOutOfSupplyTicks);
        Assert.True(campaign.Army.SupplyState.FractionalFoodDemand > 0.98f);
        Assert.False(member.IsRouting);
    }

    private static void SetSupplyState(ArmySupplyState state, float fractionalFoodDemand, int sustainedOutOfSupplyTicks)
    {
        var fractionalField = typeof(ArmySupplyState).GetField("<FractionalFoodDemand>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        var sustainedField = typeof(ArmySupplyState).GetField("<SustainedOutOfSupplyTicks>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(fractionalField);
        Assert.NotNull(sustainedField);
        fractionalField!.SetValue(state, fractionalFoodDemand);
        sustainedField!.SetValue(state, sustainedOutOfSupplyTicks);
    }

    private static void PlaceActor(World world, Person person, Colony colony, int minDistanceFromOrigin)
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
                    if (world.IsMovementBlocked(x, y, colony.Id) || world.IsActorOccupied(x, y, person))
                        continue;

                    person.Pos = (x, y);
                    return;
                }
            }
        }

        throw new InvalidOperationException("Could not find a passable campaign test tile.");
    }

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

    private static void BlockCampaignTargetRadius(World world, Colony target, Colony mover, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                    continue;

                var pos = (x: target.Origin.x + dx, y: target.Origin.y + dy);
                if (world.IsMovementBlocked(pos.x, pos.y, mover.Id))
                    continue;

                Assert.True(world.TryAddWoodWall(target, pos));
                Assert.True(world.IsMovementBlocked(pos.x, pos.y, mover.Id));
            }
        }
    }

    private static (int x, int y) FindFirstPassableCampaignTarget(World world, Colony target, Colony mover)
    {
        var blockedOrigin = target.Origin;
        int maxRadius = Math.Max(world.Width, world.Height);
        for (var radius = 0; radius <= maxRadius; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > radius)
                        continue;

                    var candidate = (x: blockedOrigin.x + dx, y: blockedOrigin.y + dy);
                    if (candidate != blockedOrigin && Manhattan(candidate, mover.Origin) <= 1)
                        continue;
                    if (world.IsMovementBlocked(candidate.x, candidate.y, mover.Id))
                        continue;

                    return candidate;
                }
            }
        }

        throw new InvalidOperationException("Could not resolve campaign fallback target.");
    }

    private static (int x, int y) FindAdjacentLand(World world, (int x, int y) origin)
    {
        var candidates = new[]
        {
            (x: origin.x + 1, y: origin.y),
            (x: origin.x - 1, y: origin.y),
            (x: origin.x, y: origin.y + 1),
            (x: origin.x, y: origin.y - 1)
        };

        foreach (var candidate in candidates)
        {
            if (candidate.x < 0 || candidate.y < 0 || candidate.x >= world.Width || candidate.y >= world.Height)
                continue;
            if (world.GetTile(candidate.x, candidate.y).Ground == Ground.Water)
                continue;
            return candidate;
        }

        throw new InvalidOperationException("Could not find adjacent land campaign test tile.");
    }

    private static int Manhattan((int x, int y) left, (int x, int y) right)
        => Math.Abs(left.x - right.x) + Math.Abs(left.y - right.y);

    private static void AssertAssemblyDoesNotRunMarchOrSupply(CampaignRuntimeSnapshot campaign)
    {
        Assert.Equal(0, campaign.RouteCounters.PathRequests);
        Assert.Equal(0, campaign.RouteCounters.PathCacheHits);
        Assert.Equal(0, campaign.RouteCounters.BlockedMovementChecks);
        Assert.Equal(0, campaign.RouteCounters.RouteRecomputes);
        Assert.Equal(0, campaign.RouteCounters.MarchProgressTicks);
        Assert.Equal(0, campaign.RouteCounters.EncounterTicks);
        Assert.Equal(0, campaign.RouteCounters.NoProgressTicks);
        Assert.Equal(0, campaign.Army.Supply.FractionalFoodDemand);
        Assert.Equal(0, campaign.Army.Supply.SustainedOutOfSupplyTicks);
        Assert.Equal(0, campaign.Army.RationPool.RationPoolFood);
        Assert.Equal(0, campaign.Army.Foraging.Attempts);
        Assert.Equal(0, campaign.Army.Foraging.Successes);
        Assert.Equal(0, campaign.Army.Foraging.Failures);
        Assert.Equal(0, campaign.Army.Foraging.FoodGained);
    }

    private static World GetWorld(SimulationRuntime runtime)
    {
        var worldField = typeof(SimulationRuntime).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(worldField);
        var world = worldField!.GetValue(runtime) as World;
        Assert.NotNull(world);
        return world!;
    }

    private static CampaignState GetLiveCampaignState(SimulationRuntime runtime)
    {
        var campaignsField = typeof(SimulationRuntime).GetField("_campaigns", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(campaignsField);
        var campaigns = Assert.IsAssignableFrom<IReadOnlyList<CampaignState>>(campaignsField!.GetValue(runtime));
        return Assert.Single(campaigns);
    }

    private static IReadOnlyList<ActorSnapshot> SnapshotActors(SimulationRuntime runtime)
        => GetWorld(runtime)._people
            .OrderBy(person => person.Id)
            .Select(person => new ActorSnapshot(
                person.Id,
                person.Pos.x,
                person.Pos.y,
                person.Roles,
                person.Inventory.GetCount(ItemType.Food),
                person.CombatMorale,
                person.Stamina))
            .ToArray();

    private sealed record ActorSnapshot(
        int ActorId,
        int X,
        int Y,
        PersonRole Roles,
        int CarriedFood,
        float CombatMorale,
        float Stamina);

    private sealed record MarchingCampaignFixture(
        SimulationRuntime Runtime,
        World World,
        IReadOnlyList<Person> Members);
}
