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
using WorldSim.Simulation.Navigation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave10CampaignLogisticsTests
{
    private const int OrganicCadenceTicks = 20;

    [Fact]
    public void PublicManualCampaignCreation_IsNotCappedByP7AOrganicCampaignCap()
    {
        var runtime = CreateRuntime(FixedLaunchStrategist.For(Faction.Aetheri, targetColonyId: GetColony(GetWorld(CreateRuntime()), Faction.Aetheri).Id));
        EnableDiplomacyAndCombat(runtime);

        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1).Success);
        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Chirita, requestedMemberCount: 1).Success);
        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Sylvars, requestedMemberCount: 1).Success);

        Assert.Equal(3, runtime.Campaigns.Count(campaign => campaign.OwnerFaction == Faction.Obsidari));
        Assert.Empty(runtime.SupplyConvoys);
    }

    [Fact]
    public void OrganicLaunchCap_IsOrganicPathOnlyAndDoesNotConsumeManualCompatibility()
    {
        var targetColonyId = GetColony(GetWorld(CreateRuntime()), Faction.Aetheri).Id;
        var runtime = CreateRuntime(FixedLaunchStrategist.For(Faction.Aetheri, targetColonyId));
        EnableDiplomacyAndCombat(runtime);
        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1).Success);
        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Chirita, requestedMemberCount: 1).Success);
        AdvanceTicks(runtime, OrganicCadenceTicks);
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "p7a organic cap first");
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        runtime.AdvanceTick(0f);

        Assert.Equal(2, runtime.Campaigns.Count(campaign => campaign.OwnerFaction == Faction.Obsidari && campaign.Phase != CampaignPhase.Resolved));
        Assert.True(runtime.CampaignLogisticsCounters.CampaignLaunchBlockedByCap > 0);
        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Sylvars, requestedMemberCount: 1).Success);
    }

    [Fact]
    public void LowSupplyReadiness_RequestConvoyThroughExistingStrategistAndRuntimeAppliesIt()
    {
        var runtime = CreateRuntime(FixedConvoyStrategist.For(campaignId: 1));
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        SetSustainedOutOfSupplyTicks(campaign.Army.SupplyState, 2);

        AdvanceToNextCadence(runtime);

        var convoy = Assert.Single(runtime.SupplyConvoys);
        Assert.Equal(Faction.Obsidari, convoy.OwnerFaction);
        Assert.Equal(campaign.CampaignId, convoy.TargetCampaignId);
        Assert.Equal(campaign.ArmyId, convoy.TargetArmyId);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ConvoysSpawned);
    }

    [Fact]
    public void ConvoySpawnFailures_DoNotConsumeConvoyIdOrMutateConvoyList()
    {
        var runtime = CreateRuntime(FixedConvoyStrategist.For(campaignId: 1));
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateAssemblyPendingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        BlockCampaignTargetRadius(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Aetheri), GetColony(GetWorld(runtime), Faction.Obsidari), radius: 2);
        PrepareAdditionalWarriors(runtime, Faction.Obsidari, count: 1);

        AdvanceToNextCadence(runtime);

        Assert.Empty(runtime.SupplyConvoys);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ConvoySpawnRouteBudgetExhausted);

        var successRuntime = CreateRuntime(FixedConvoyStrategist.For(campaignId: 1));
        EnableDiplomacyAndCombat(successRuntime);
        CreateMarchingCampaign(successRuntime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        AdvanceToNextCadence(successRuntime);

        Assert.Equal(1, Assert.Single(successRuntime.SupplyConvoys).ConvoyId);
    }

    [Fact]
    public void ConvoyCapAndThrottle_AreOwnerScopedAndIgnoreDeliveredConvoysForActiveCap()
    {
        var runtime = CreateRuntime(FixedConvoyStrategist.For(campaignId: 1));
        EnableDiplomacyAndCombat(runtime);
        var first = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 2);
        SetSustainedOutOfSupplyTicks(first.Army.SupplyState, 2);
        AddFakeConvoy(runtime, first, phase: SupplyConvoyPhase.Pending);

        AdvanceToNextCadence(runtime);

        Assert.Single(runtime.SupplyConvoys);
        Assert.Equal(Faction.Obsidari, runtime.SupplyConvoys.Single().OwnerFaction);
        Assert.True(runtime.CampaignLogisticsCounters.ConvoySpawnBlockedByCap > 0);

        foreach (var convoy in GetLiveConvoys(runtime))
            MarkConvoyDelivered(convoy, runtime.Tick);
        AddFakeConvoy(runtime, first, phase: SupplyConvoyPhase.Delivered);

        AdvanceToNextCadence(runtime);

        Assert.Equal(2, runtime.SupplyConvoys.Count);
        Assert.True(runtime.CampaignLogisticsCounters.ConvoySpawnBlockedByThrottle > 0);
    }

    [Fact]
    public void ConvoyHomeDefense_UsesActualUnassignedWarriors()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 0);
        SetSustainedOutOfSupplyTicks(campaign.Army.SupplyState, 2);

        AdvanceToNextCadence(runtime);

        Assert.Empty(runtime.SupplyConvoys);
        Assert.Equal(0, runtime.CampaignLogisticsCounters.CampaignLaunchBlockedByHomeDefense);
        Assert.True(runtime.CampaignLogisticsCounters.ConvoySpawnBlockedByHomeDefense > 0);
    }

    [Fact]
    public void ConvoyDelivery_RequiresLiveAssignedArmyMemberNearConvoy()
    {
        var runtime = CreateRuntime(FixedConvoyStrategist.For(campaignId: 1));
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        var member = GetAssignedMember(runtime, campaign);
        var before = campaign.Army.RationPoolState.RationPoolFood;

        AdvanceToNextCadence(runtime);
        AdvanceUntil(runtime, () => IsConvoyAtStaticTarget(runtime.SupplyConvoys.Single()), maxTicks: 160);
        var arrived = Assert.Single(runtime.SupplyConvoys);
        var liveCampaign = GetLiveCampaigns(runtime).Single(candidate => candidate.CampaignId == campaign.CampaignId);
        EnsureArmyMember(liveCampaign.Army, member.Id);
        member.Pos = (arrived.CurrentX, arrived.CurrentY);
        InvokeRuntimeMethod(runtime, "AdvanceSupplyConvoys");
        var delivered = Assert.Single(runtime.SupplyConvoys);

        Assert.Equal(SupplyConvoyPhase.Delivered, delivered.Phase);
        Assert.Equal(before + delivered.PayloadFood, Assert.Single(runtime.Campaigns).Army.RationPool.RationPoolFood);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ConvoysDelivered);
        Assert.Equal(0, runtime.SupplyConvoys.Count(convoy => convoy.Phase is SupplyConvoyPhase.Pending or SupplyConvoyPhase.Marching));
    }

    [Fact]
    public void ConvoyAtStaticTargetWithoutArmyRecipient_DoesNotDeliver()
    {
        var runtime = CreateRuntime(FixedConvoyStrategist.For(campaignId: 1));
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        var member = GetAssignedMember(runtime, campaign);
        var owner = GetColony(GetWorld(runtime), Faction.Obsidari);
        member.Pos = owner.Origin;

        AdvanceToNextCadence(runtime);
        AdvanceUntil(runtime, () => IsConvoyAtStaticTarget(runtime.SupplyConvoys.Single()), maxTicks: 160);
        var noProgressBefore = Assert.Single(runtime.SupplyConvoys).RouteCounters.NoProgressTicks;
        runtime.AdvanceTick(0f);
        var convoy = Assert.Single(runtime.SupplyConvoys);

        Assert.True(IsConvoyAtStaticTarget(convoy));
        Assert.Equal(SupplyConvoyPhase.Marching, convoy.Phase);
        Assert.Equal(0, campaign.Army.RationPoolState.RationPoolFood);
        Assert.Equal(0, runtime.CampaignLogisticsCounters.ConvoysDelivered);
        Assert.True(convoy.RouteCounters.NoProgressTicks > noProgressBefore);
    }

    [Fact]
    public void ConvoyDelivery_DeadOrMissingAssignedMemberDoesNotDeliver()
    {
        var runtime = CreateRuntime(FixedConvoyStrategist.For(campaignId: 1));
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        GetAssignedMember(runtime, campaign).Health = 0f;

        AdvanceToNextCadence(runtime);
        AdvanceUntil(runtime, () => IsConvoyAtStaticTarget(runtime.SupplyConvoys.Single()), maxTicks: 160);
        runtime.AdvanceTick(0f);

        var convoy = Assert.Single(runtime.SupplyConvoys);
        Assert.Equal(SupplyConvoyPhase.Marching, convoy.Phase);
        Assert.Equal(0, campaign.Army.RationPoolState.RationPoolFood);
        Assert.Equal(0, runtime.CampaignLogisticsCounters.ConvoysDelivered);
    }

    [Fact]
    public void ConvoyTargetResolvedBeforeDelivery_FailsWithoutDelivery()
    {
        var runtime = CreateRuntime(FixedConvoyStrategist.For(campaignId: 1));
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);

        AdvanceToNextCadence(runtime);
        ResolveCampaign(GetLiveCampaigns(runtime).Single());
        runtime.AdvanceTick(0f);

        var convoy = Assert.Single(runtime.SupplyConvoys);
        Assert.Equal(SupplyConvoyPhase.Failed, convoy.Phase);
        Assert.Equal(0, campaign.Army.RationPoolState.RationPoolFood);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ConvoysFailed);
    }

    [Fact]
    public void SnapshotExportsSupplyConvoyReadModelWithoutGraphicsInference()
    {
        var runtime = CreateRuntime(FixedConvoyStrategist.For(campaignId: 1));
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);

        AdvanceToNextCadence(runtime);

        var convoy = Assert.Single(runtime.GetSnapshot().SupplyConvoys);
        Assert.Equal(1, convoy.ConvoyId);
        Assert.Equal((int)Faction.Obsidari, convoy.OwnerFactionId);
        Assert.Equal(campaign.CampaignId, convoy.TargetCampaignId);
        Assert.Equal(campaign.ArmyId, convoy.TargetArmyId);
        Assert.True(convoy.PayloadFood > 0);
        Assert.NotEqual("unknown", convoy.Phase);
    }

    [Fact]
    public void ForwardBaseEstablishesForDistantMarchingCampaignAndSetsRallyPoint()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        var member = GetAssignedMember(runtime, campaign);
        var anchor = FindForwardBaseAnchor(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Obsidari));
        member.Pos = anchor;

        AdvanceForwardBases(runtime);

        var forwardBase = Assert.Single(runtime.ForwardBases);
        Assert.Equal(ForwardBasePhase.Active, forwardBase.Phase);
        Assert.Equal(anchor, (forwardBase.X, forwardBase.Y));
        Assert.True(campaign.Army.HasRallyPoint);
        Assert.Equal(anchor, (campaign.Army.RallyX, campaign.Army.RallyY));
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ForwardBasesEstablished);
    }

    [Fact]
    public void ForwardBaseDoesNotEstablishNearHome()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        GetAssignedMember(runtime, campaign).Pos = GetColony(GetWorld(runtime), Faction.Obsidari).Origin;

        AdvanceForwardBases(runtime);

        Assert.Empty(runtime.ForwardBases);
        Assert.Equal(0, runtime.CampaignLogisticsCounters.ForwardBasesEstablished);
    }

    [Fact]
    public void ForwardBaseDoesNotEstablishWithoutLiveAssignedArmyMember()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        var member = GetAssignedMember(runtime, campaign);
        member.Pos = FindForwardBaseAnchor(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Obsidari));
        member.Health = 0f;

        AdvanceForwardBases(runtime);

        Assert.Empty(runtime.ForwardBases);
        Assert.Equal(0, runtime.CampaignLogisticsCounters.ForwardBasesEstablished);
    }

    [Fact]
    public void ForwardBasePlacementFallsBackDeterministicallyFromBlockedAnchor()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var blocker = GetColony(world, Faction.Aetheri);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        var anchor = FindForwardBaseAnchorWithPassableNeighbor(world, owner);
        GetAssignedMember(runtime, campaign).Pos = anchor;
        Assert.True(world.TryAddWoodWall(blocker, anchor));

        AdvanceForwardBases(runtime);

        var forwardBase = Assert.Single(runtime.ForwardBases);
        Assert.NotEqual(anchor, (forwardBase.X, forwardBase.Y));
        Assert.False(world.IsMovementBlocked(forwardBase.X, forwardBase.Y, owner.Id));
        Assert.Equal(1, Math.Abs(forwardBase.X - anchor.x) + Math.Abs(forwardBase.Y - anchor.y));
    }

    [Fact]
    public void ForwardBasePlacementFailureDoesNotConsumeBaseId()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var blocker = GetColony(world, Faction.Aetheri);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        var member = GetAssignedMember(runtime, campaign);
        var blockedAnchor = FindForwardBaseAnchor(world, owner);
        member.Pos = blockedAnchor;
        BlockForwardBasePlacementRadius(world, blocker, blockedAnchor, radius: 2);

        AdvanceForwardBases(runtime);

        Assert.Empty(runtime.ForwardBases);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ForwardBaseBuildBlockedByPlacement);

        member.Pos = FindForwardBaseAnchor(world, owner, excluded: blockedAnchor);
        AdvanceForwardBases(runtime);

        Assert.Equal(1, Assert.Single(runtime.ForwardBases).BaseId);
    }

    [Fact]
    public void ForwardBaseCapBlocksSecondActiveBasePerFaction()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var first = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 2);
        var second = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Chirita, requestedMemberCount: 1, reserveWarriors: 2);
        GetAssignedMember(runtime, first).Pos = FindForwardBaseAnchor(world, owner);
        GetAssignedMember(runtime, second).Pos = FindForwardBaseAnchor(world, owner, excluded: GetAssignedMember(runtime, first).Pos);

        AdvanceForwardBases(runtime);

        Assert.Single(runtime.ForwardBases, forwardBase => forwardBase.Phase == ForwardBasePhase.Active);
        Assert.True(runtime.CampaignLogisticsCounters.ForwardBaseBuildBlockedByCap > 0);
    }

    [Fact]
    public void ExpiredOrAbandonedForwardBaseDoesNotCountAgainstCap()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        var owner = GetColony(world, Faction.Obsidari);
        var first = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 2);
        var second = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Chirita, requestedMemberCount: 1, reserveWarriors: 2);
        GetAssignedMember(runtime, first).Pos = FindForwardBaseAnchor(world, owner);
        GetAssignedMember(runtime, second).Pos = FindForwardBaseAnchor(world, owner, excluded: GetAssignedMember(runtime, first).Pos);
        AdvanceForwardBases(runtime);
        MarkForwardBaseExpired(GetLiveForwardBases(runtime).Single(), runtime.Tick);

        AdvanceForwardBases(runtime);

        Assert.Equal(2, runtime.ForwardBases.Count);
        Assert.Equal(1, runtime.ForwardBases.Count(forwardBase => forwardBase.Phase == ForwardBasePhase.Active));
    }

    [Fact]
    public void ForwardBaseExpiresAfterLifetime()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        GetAssignedMember(runtime, campaign).Pos = FindForwardBaseAnchor(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Obsidari));
        AdvanceForwardBases(runtime);
        SetRuntimeTick(runtime, runtime.Tick + 240);

        AdvanceForwardBases(runtime);

        var forwardBase = Assert.Single(runtime.ForwardBases);
        Assert.Equal(ForwardBasePhase.Expired, forwardBase.Phase);
        Assert.Equal("expired", forwardBase.CloseReason);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ForwardBasesExpired);
    }

    [Fact]
    public void ForwardBaseAbandonsWhenCampaignResolves()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        GetAssignedMember(runtime, campaign).Pos = FindForwardBaseAnchor(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Obsidari));
        AdvanceForwardBases(runtime);
        ResolveCampaign(campaign);

        AdvanceForwardBases(runtime);

        var forwardBase = Assert.Single(runtime.ForwardBases);
        Assert.Equal(ForwardBasePhase.Abandoned, forwardBase.Phase);
        Assert.Equal("campaign_resolved", forwardBase.CloseReason);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ForwardBasesAbandoned);
    }

    [Fact]
    public void ForwardBaseAbandonsAfterNoLiveMemberWindow()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        var member = GetAssignedMember(runtime, campaign);
        member.Pos = FindForwardBaseAnchor(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Obsidari));
        AdvanceForwardBases(runtime);
        member.Health = 0f;
        SetRuntimeTick(runtime, runtime.Tick + 30);

        AdvanceForwardBases(runtime);

        var forwardBase = Assert.Single(runtime.ForwardBases);
        Assert.Equal(ForwardBasePhase.Abandoned, forwardBase.Phase);
        Assert.Equal("no_live_member", forwardBase.CloseReason);
    }

    [Fact]
    public void ForwardBaseLiveTransientAssignedMemberKeepsBaseActiveButDoesNotRest()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        var member = GetAssignedMember(runtime, campaign);
        member.Pos = FindForwardBaseAnchor(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Obsidari));
        AdvanceForwardBases(runtime);
        var baseBeforeTransient = Assert.Single(runtime.ForwardBases);
        SetStamina(member, 50f);
        var baselineRestTicks = baseBeforeTransient.RestTicks;
        var baselineRestedActorTicks = baseBeforeTransient.RestedActorTicks;
        var baselineGlobalRestTicks = runtime.CampaignLogisticsCounters.ForwardBaseRestTicks;
        var baselineGlobalRestedActorTicks = runtime.CampaignLogisticsCounters.ForwardBaseRestedActorTicks;
        member.Current = Job.Fight;
        SetRuntimeTick(runtime, baseBeforeTransient.CreatedTick + 30);

        runtime.AdvanceTick(0f);

        var forwardBase = Assert.Single(runtime.ForwardBases);
        Assert.Contains(member.Id, campaign.Army.MemberActorIds);
        Assert.Equal(ForwardBasePhase.Active, forwardBase.Phase);
        Assert.Equal("none", forwardBase.CloseReason);
        Assert.Equal(runtime.Tick - 1, forwardBase.LastLiveMemberNearTick);
        Assert.Equal(50f, member.Stamina);
        Assert.Equal(baselineRestTicks, forwardBase.RestTicks);
        Assert.Equal(baselineRestedActorTicks, forwardBase.RestedActorTicks);
        Assert.Equal(baselineGlobalRestTicks, runtime.CampaignLogisticsCounters.ForwardBaseRestTicks);
        Assert.Equal(baselineGlobalRestedActorTicks, runtime.CampaignLogisticsCounters.ForwardBaseRestedActorTicks);
    }

    [Fact]
    public void ForwardBaseRestAppliesOnlyToLiveAssignedMembersNearBaseAndClampsStamina()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 2);
        var assigned = GetAssignedMember(runtime, campaign);
        var unassigned = SelectPeople(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Obsidari), 2).Last();
        var anchor = FindForwardBaseAnchor(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Obsidari));
        assigned.Pos = anchor;
        unassigned.Pos = anchor;
        SetStamina(assigned, 99f);
        SetStamina(unassigned, 50f);
        AdvanceForwardBases(runtime);

        AdvanceForwardBases(runtime);

        Assert.Equal(100f, assigned.Stamina);
        Assert.Equal(50f, unassigned.Stamina);
        Assert.Equal(1, Assert.Single(runtime.ForwardBases).RestTicks);
        Assert.Equal(1, runtime.CampaignLogisticsCounters.ForwardBaseRestedActorTicks);
    }

    [Fact]
    public void ForwardBaseUsesCurrentArmyPositionNotStaticTarget()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        var currentAnchor = FindForwardBaseAnchor(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Obsidari));
        GetAssignedMember(runtime, campaign).Pos = currentAnchor;

        AdvanceForwardBases(runtime);

        var forwardBase = Assert.Single(runtime.ForwardBases);
        Assert.Equal(currentAnchor, (forwardBase.X, forwardBase.Y));
        Assert.NotEqual((campaign.RouteIntent.TargetX, campaign.RouteIntent.TargetY), (forwardBase.X, forwardBase.Y));
    }

    [Fact]
    public void SnapshotExportsForwardBaseReadModelWithoutGraphicsInference()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var campaign = CreateMarchingCampaign(runtime, Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, reserveWarriors: 1);
        GetAssignedMember(runtime, campaign).Pos = FindForwardBaseAnchor(GetWorld(runtime), GetColony(GetWorld(runtime), Faction.Obsidari));

        AdvanceForwardBases(runtime);

        var forwardBase = Assert.Single(runtime.GetSnapshot().ForwardBases);
        Assert.Equal(1, forwardBase.BaseId);
        Assert.Equal((int)Faction.Obsidari, forwardBase.OwnerFactionId);
        Assert.Equal(campaign.CampaignId, forwardBase.CampaignId);
        Assert.Equal(campaign.ArmyId, forwardBase.ArmyId);
        Assert.Equal("active", forwardBase.Phase);
        Assert.Equal("none", forwardBase.CloseReason);
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

    private static CampaignState CreateMarchingCampaign(SimulationRuntime runtime, Faction owner, Faction target, int requestedMemberCount, int reserveWarriors)
    {
        SetAllCampaignCandidatesIneligible(runtime);
        var world = GetWorld(runtime);
        var ownerColony = GetColony(world, owner);
        var members = SelectPeople(world, ownerColony, requestedMemberCount + reserveWarriors);
        foreach (var member in members.Take(requestedMemberCount))
            member.Profession = Profession.Hunter;
        foreach (var member in members.Skip(requestedMemberCount))
            member.AssignRole(PersonRole.Warrior);

        Assert.True(runtime.TryCreateCampaign(owner, target, requestedMemberCount).Success);
        var campaign = GetLiveCampaigns(runtime).Single(candidate => candidate.OwnerFaction == owner && candidate.TargetFaction == target && candidate.Phase != CampaignPhase.Resolved);
        for (var i = 0; i < requestedMemberCount; i++)
            AddArmyMember(campaign.Army, members[i].Id);
        InvokeCampaignMethod(campaign, "BeginAssembly", runtime.Tick);
        InvokeCampaignMethod(campaign, "MarkAssemblyComplete", runtime.Tick);
        Assert.Equal(CampaignPhase.Marching, campaign.Phase);
        return campaign;
    }

    private static void AddArmyMember(ArmyState army, int actorId)
    {
        var method = typeof(ArmyState).GetMethod("TryAddMemberActorId", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.True((bool)method!.Invoke(army, new object[] { actorId })!);
    }

    private static void EnsureArmyMember(ArmyState army, int actorId)
    {
        if (army.MemberActorIds.Contains(actorId))
            return;

        AddArmyMember(army, actorId);
    }

    private static void InvokeCampaignMethod(CampaignState campaign, string methodName, long tick)
    {
        var method = typeof(CampaignState).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(campaign, new object[] { tick });
    }

    private static void InvokeRuntimeMethod(SimulationRuntime runtime, string methodName)
    {
        var method = typeof(SimulationRuntime).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(runtime, Array.Empty<object>());
    }

    private static CampaignState CreateAssemblyPendingCampaign(SimulationRuntime runtime, Faction owner, Faction target, int requestedMemberCount)
    {
        SetAllCampaignCandidatesIneligible(runtime);
        Assert.True(runtime.TryCreateCampaign(owner, target, requestedMemberCount).Success);
        return GetLiveCampaigns(runtime).Single();
    }

    private static void PrepareEligibleWarriors(SimulationRuntime runtime, Faction faction, int count)
    {
        SetAllCampaignCandidatesIneligible(runtime);
        PrepareAdditionalWarriors(runtime, faction, count);
    }

    private static void PrepareAdditionalWarriors(SimulationRuntime runtime, Faction faction, int count)
    {
        var world = GetWorld(runtime);
        foreach (var person in SelectPeople(world, GetColony(world, faction), count))
            person.AssignRole(PersonRole.Warrior);
    }

    private static void AddActionableScoutIntel(SimulationRuntime runtime, Faction ownerFaction, Faction targetFaction)
    {
        var world = GetWorld(runtime);
        var owner = GetColony(world, ownerFaction);
        var target = GetColony(world, targetFaction);
        var sourceActor = SelectPeople(world, owner, 1)[0];
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
            createdTick: runtime.Tick,
            ttlTicks: 60,
            confidence: 0.8f));
    }

    private static List<ScoutIntelState> GetScoutIntelStates(SimulationRuntime runtime)
    {
        var scoutIntelField = typeof(SimulationRuntime).GetField("_scoutIntel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(scoutIntelField);
        return Assert.IsAssignableFrom<List<ScoutIntelState>>(scoutIntelField!.GetValue(runtime));
    }

    private static Person[] SelectPeople(World world, Colony colony, int count)
    {
        var people = world._people
            .Where(person => person.Home.Id == colony.Id && person.Health > 0f)
            .OrderBy(person => person.Id)
            .Take(count)
            .ToArray();
        Assert.True(people.Length >= count);
        return people;
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

    private static void AdvanceToNextCadence(SimulationRuntime runtime)
    {
        if (runtime.Tick < OrganicCadenceTicks)
        {
            SetRuntimeTick(runtime, OrganicCadenceTicks);
        }
        else if (runtime.Tick % OrganicCadenceTicks != 0)
        {
            SetRuntimeTick(runtime, runtime.Tick + (OrganicCadenceTicks - (runtime.Tick % OrganicCadenceTicks)));
        }

        runtime.AdvanceTick(0f);
    }

    private static void SetRuntimeTick(SimulationRuntime runtime, long tick)
    {
        var field = typeof(SimulationRuntime).GetField("<Tick>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(runtime, tick);
    }

    private static void AdvanceTicks(SimulationRuntime runtime, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            runtime.AdvanceTick(0f);
    }

    private static void AdvanceUntil(SimulationRuntime runtime, Func<bool> predicate, int maxTicks)
    {
        for (var i = 0; i < maxTicks && !predicate(); i++)
            runtime.AdvanceTick(0f);

        Assert.True(predicate());
    }

    private static void AdvanceForwardBases(SimulationRuntime runtime)
    {
        var method = typeof(SimulationRuntime).GetMethod("AdvanceForwardBases", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(runtime, new object[] { new HashSet<int>() });
    }

    private static bool IsConvoyAtStaticTarget(SupplyConvoyRuntimeSnapshot convoy)
        => Math.Abs(convoy.CurrentX - convoy.TargetX) + Math.Abs(convoy.CurrentY - convoy.TargetY) <= 1;

    private static Person GetAssignedMember(SimulationRuntime runtime, CampaignState campaign)
    {
        var actorId = Assert.Single(campaign.Army.MemberActorIds);
        var member = GetWorld(runtime)._people.FirstOrDefault(person => person.Id == actorId);
        Assert.NotNull(member);
        return member!;
    }

    private static void SetSustainedOutOfSupplyTicks(ArmySupplyState state, int ticks)
    {
        var field = typeof(ArmySupplyState).GetField("<SustainedOutOfSupplyTicks>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(state, ticks);
    }

    private static void MarkConvoyDelivered(SupplyConvoyState convoy, long tick)
    {
        var method = typeof(SupplyConvoyState).GetMethod("MarkDelivered", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(convoy, new object[] { tick });
    }

    private static void MarkForwardBaseExpired(ForwardBaseState forwardBase, long tick)
    {
        var method = typeof(ForwardBaseState).GetMethod("MarkExpired", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(forwardBase, new object[] { tick, "expired" });
    }

    private static void SetStamina(Person person, float value)
    {
        var property = typeof(Person).GetProperty(nameof(Person.Stamina));
        Assert.NotNull(property);
        property!.SetValue(person, value);
    }

    private static void AddFakeConvoy(SimulationRuntime runtime, CampaignState campaign, SupplyConvoyPhase phase)
    {
        var convoy = new SupplyConvoyState(
            convoyId: 99,
            campaign.OwnerFaction,
            campaign.OriginColonyId,
            campaign.CampaignId,
            campaign.ArmyId,
            runtime.Tick,
            campaign.RouteIntent.OriginX,
            campaign.RouteIntent.OriginY,
            campaign.RouteIntent.TargetX,
            campaign.RouteIntent.TargetY,
            payloadFood: 1);
        if (phase == SupplyConvoyPhase.Delivered)
            MarkConvoyDelivered(convoy, runtime.Tick);
        GetLiveConvoys(runtime).Add(convoy);
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
            }
        }
    }

    private static void BlockForwardBasePlacementRadius(World world, Colony blocker, (int x, int y) anchor, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                    continue;

                var pos = (x: anchor.x + dx, y: anchor.y + dy);
                if (world.IsMovementBlocked(pos.x, pos.y, blocker.Id))
                    continue;

                Assert.True(world.TryAddWoodWall(blocker, pos));
            }
        }
    }

    private static (int x, int y) FindForwardBaseAnchor(World world, Colony owner, (int x, int y)? excluded = null)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var candidate = (x, y);
                if (excluded.HasValue && excluded.Value == candidate)
                    continue;
                if (Math.Abs(candidate.x - owner.Origin.x) + Math.Abs(candidate.y - owner.Origin.y) < 8)
                    continue;
                if (world.IsMovementBlocked(candidate.x, candidate.y, owner.Id))
                    continue;
                if (!HasRoute(world, owner, candidate))
                    continue;

                return candidate;
            }
        }

        throw new InvalidOperationException("Could not find a forward-base anchor for the test world.");
    }

    private static (int x, int y) FindForwardBaseAnchorWithPassableNeighbor(World world, Colony owner)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var candidate = (x, y);
                if (Math.Abs(candidate.x - owner.Origin.x) + Math.Abs(candidate.y - owner.Origin.y) < 8)
                    continue;
                if (world.IsMovementBlocked(candidate.x, candidate.y, owner.Id))
                    continue;
                if (!HasRoute(world, owner, candidate))
                    continue;
                if (GetNeighborCandidates(candidate).Any(neighbor =>
                        !world.IsMovementBlocked(neighbor.x, neighbor.y, owner.Id)
                        && HasRoute(world, owner, neighbor)))
                    return candidate;
            }
        }

        throw new InvalidOperationException("Could not find a forward-base anchor with a passable neighbor for the test world.");
    }

    private static IEnumerable<(int x, int y)> GetNeighborCandidates((int x, int y) anchor)
    {
        yield return (anchor.x, anchor.y - 1);
        yield return (anchor.x - 1, anchor.y);
        yield return (anchor.x + 1, anchor.y);
        yield return (anchor.x, anchor.y + 1);
    }

    private static bool HasRoute(World world, Colony owner, (int x, int y) target)
    {
        var path = NavigationPathfinder.FindPath(
            new NavigationGrid(world),
            owner.Origin,
            target,
            owner.Id,
            maxExpansions: 4096,
            out var budgetExceeded);

        return !budgetExceeded && path.Count > 1;
    }

    private static Colony GetColony(World world, Faction faction)
    {
        var colony = world._colonies.FirstOrDefault(candidate => candidate.Faction == faction);
        Assert.NotNull(colony);
        return colony!;
    }

    private static List<CampaignState> GetLiveCampaigns(SimulationRuntime runtime)
    {
        var campaignsField = typeof(SimulationRuntime).GetField("_campaigns", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(campaignsField);
        return Assert.IsAssignableFrom<List<CampaignState>>(campaignsField!.GetValue(runtime));
    }

    private static List<SupplyConvoyState> GetLiveConvoys(SimulationRuntime runtime)
    {
        var convoysField = typeof(SimulationRuntime).GetField("_supplyConvoys", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(convoysField);
        return Assert.IsAssignableFrom<List<SupplyConvoyState>>(convoysField!.GetValue(runtime));
    }

    private static List<ForwardBaseState> GetLiveForwardBases(SimulationRuntime runtime)
    {
        var basesField = typeof(SimulationRuntime).GetField("_forwardBases", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(basesField);
        return Assert.IsAssignableFrom<List<ForwardBaseState>>(basesField!.GetValue(runtime));
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

    private sealed class FixedConvoyStrategist : ICampaignStrategist
    {
        private readonly int _campaignId;

        private FixedConvoyStrategist(int campaignId)
        {
            _campaignId = campaignId;
        }

        public static FixedConvoyStrategist For(int campaignId) => new(campaignId);

        public CampaignStrategyDecision Decide(in CampaignStrategyContext context)
            => new(
                CampaignStrategyDecisionKind.RequestConvoy,
                CampaignStrategyReasonCode.CampaignSupplyLow,
                CampaignId: _campaignId,
                Score: 1.0f);
    }

    private sealed class FixedLaunchStrategist : ICampaignStrategist
    {
        private readonly Faction _targetFaction;
        private readonly int _targetColonyId;

        private FixedLaunchStrategist(Faction targetFaction, int targetColonyId)
        {
            _targetFaction = targetFaction;
            _targetColonyId = targetColonyId;
        }

        public static FixedLaunchStrategist For(Faction targetFaction, int targetColonyId) => new(targetFaction, targetColonyId);

        public CampaignStrategyDecision Decide(in CampaignStrategyContext context)
            => new(
                CampaignStrategyDecisionKind.LaunchCampaign,
                CampaignStrategyReasonCode.TargetPressureAndAdvantage,
                TargetFactionId: (int)_targetFaction,
                TargetColonyId: _targetColonyId,
                RequestedWarriors: 1,
                Score: 1.0f);
    }
}
