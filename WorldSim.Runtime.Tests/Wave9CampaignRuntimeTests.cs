using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WorldSim.Runtime;
using WorldSim.Simulation;
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
        Assert.Empty(campaign.Army.MemberActorIds);
        Assert.Equal("army:1", campaign.Army.ForageConsumerKey);
        Assert.NotNull(campaign.Army.SupplyState);
        Assert.NotNull(campaign.Army.RationPoolState);
        Assert.NotNull(campaign.Army.CarrierState);
        Assert.NotNull(campaign.Army.ForagingState);
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
    public void AdvanceTick_PositiveDtDoesNotProgressCampaignOrMutateCampaignState()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
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

        Assert.Equal(CampaignPhase.AssemblingPending, campaign.Phase);
        Assert.Empty(army.MemberActorIds);
        Assert.Equal(0, campaign.RouteCounters.PathRequests);
        Assert.Equal(0, campaign.RouteCounters.PathCacheHits);
        Assert.Equal(0, campaign.RouteCounters.BlockedMovementChecks);
        Assert.Equal(0, campaign.RouteCounters.RouteRecomputes);
        Assert.Equal(0, campaign.RouteCounters.MarchProgressTicks);
        Assert.Equal(0, campaign.RouteCounters.EncounterTicks);
        Assert.Equal(0, campaign.RouteCounters.NoProgressTicks);
        Assert.Equal(0, army.SupplyState.FractionalFoodDemand);
        Assert.Equal(0, army.SupplyState.SustainedOutOfSupplyTicks);
        Assert.Equal(0, army.RationPoolState.RationPoolFood);
        Assert.False(army.CarrierState.HasAssignedCarrier);
        Assert.Equal(0, army.ForagingState.Attempts);
        Assert.Equal(0, army.ForagingState.Successes);
        Assert.Equal(0, army.ForagingState.Failures);
        Assert.Equal(0, army.ForagingState.FoodGained);
        Assert.Equal(originStockBefore, GetWorld(runtime)._colonies.First(colony => colony.Id == campaign.OriginColonyId).Stock[Resource.Food]);
        Assert.Equal(targetStockBefore, GetWorld(runtime)._colonies.First(colony => colony.Id == campaign.TargetColonyId).Stock[Resource.Food]);
    }

    [Fact]
    public void AdvanceTick_WithCampaignMatchesControlActorState()
    {
        var campaignRuntime = CreateRuntime();
        var controlRuntime = CreateRuntime();
        EnableDiplomacyAndCombat(campaignRuntime);
        EnableDiplomacyAndCombat(controlRuntime);

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
    public void Campaigns_QueryReturnsSnapshotAndRosterDoesNotExposeMutableList()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        _ = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 4);
        var firstView = runtime.Campaigns;
        var roster = Assert.Single(firstView).Army.MemberActorIds;

        _ = runtime.TryCreateCampaign(Faction.Sylvars, Faction.Chirita, requestedMemberCount: 2);

        Assert.Single(firstView);
        Assert.Equal(2, runtime.Campaigns.Count);
        var mutableCollection = Assert.IsAssignableFrom<ICollection<int>>(roster);
        Assert.True(mutableCollection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableCollection.Add(999));
        Assert.Empty(runtime.Campaigns.First().Army.MemberActorIds);
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

    private static World GetWorld(SimulationRuntime runtime)
    {
        var worldField = typeof(SimulationRuntime).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(worldField);
        var world = worldField!.GetValue(runtime) as World;
        Assert.NotNull(world);
        return world!;
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
}
