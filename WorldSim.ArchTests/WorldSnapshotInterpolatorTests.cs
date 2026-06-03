using WorldSim.Graphics.Rendering;
using WorldSim.Runtime.ReadModel;
using Xunit;

namespace WorldSim.ArchTests;

public class WorldSnapshotInterpolatorTests
{
    [Fact]
    public void Interpolate_PreservesCurrentCampaigns()
    {
        var previous = CreateSnapshot(Array.Empty<CampaignRenderData>(), personX: 0);
        var campaign = CreateCampaign();
        var current = CreateSnapshot(new[] { campaign }, personX: 10);

        var interpolated = WorldSnapshotInterpolator.Interpolate(previous, current, alpha: 0.5f);

        var result = Assert.Single(interpolated.Campaigns);
        Assert.Equal(campaign.CampaignId, result.CampaignId);
        Assert.Equal(campaign.Phase, result.Phase);
        Assert.Equal(campaign.Status, result.Status);
        Assert.Equal(campaign.Route.TargetX, result.Route.TargetX);
        Assert.Equal(campaign.Route.TargetY, result.Route.TargetY);
        Assert.Equal(campaign.Army.ArmyId, result.Army.ArmyId);
        Assert.Equal(campaign.Army.AssignedMemberCount, result.Army.AssignedMemberCount);
        Assert.Equal(5, Assert.Single(interpolated.People).X);
    }

    [Fact]
    public void Interpolate_PreservesSupplyConvoys()
    {
        var previous = CreateSnapshot(Array.Empty<CampaignRenderData>(), Array.Empty<SupplyConvoyRenderData>(), personX: 0);
        var convoy = CreateSupplyConvoy();
        var current = CreateSnapshot(Array.Empty<CampaignRenderData>(), new[] { convoy }, personX: 10);

        var interpolated = WorldSnapshotInterpolator.Interpolate(previous, current, alpha: 0.5f);

        var result = Assert.Single(interpolated.SupplyConvoys);
        Assert.Equal(convoy.ConvoyId, result.ConvoyId);
        Assert.Equal(convoy.TargetCampaignId, result.TargetCampaignId);
        Assert.Equal(convoy.Phase, result.Phase);
        Assert.Equal(convoy.PayloadFood, result.PayloadFood);
        Assert.Equal(5, Assert.Single(interpolated.People).X);
    }

    [Fact]
    public void Interpolate_PreservesForwardBases()
    {
        var previous = CreateSnapshot(Array.Empty<CampaignRenderData>(), Array.Empty<SupplyConvoyRenderData>(), Array.Empty<ForwardBaseRenderData>(), personX: 0);
        var forwardBase = CreateForwardBase();
        var current = CreateSnapshot(Array.Empty<CampaignRenderData>(), Array.Empty<SupplyConvoyRenderData>(), new[] { forwardBase }, personX: 10);

        var interpolated = WorldSnapshotInterpolator.Interpolate(previous, current, alpha: 0.5f);

        var result = Assert.Single(interpolated.ForwardBases);
        Assert.Equal(forwardBase.BaseId, result.BaseId);
        Assert.Equal(forwardBase.CampaignId, result.CampaignId);
        Assert.Equal(forwardBase.Phase, result.Phase);
        Assert.Equal(forwardBase.CloseReason, result.CloseReason);
        Assert.Equal(5, Assert.Single(interpolated.People).X);
    }

    [Fact]
    public void Interpolate_PreservesScoutIntel()
    {
        var previous = CreateSnapshot(Array.Empty<CampaignRenderData>(), Array.Empty<SupplyConvoyRenderData>(), Array.Empty<ForwardBaseRenderData>(), Array.Empty<ScoutIntelRenderData>(), personX: 0);
        var intel = CreateScoutIntel();
        var current = CreateSnapshot(Array.Empty<CampaignRenderData>(), Array.Empty<SupplyConvoyRenderData>(), Array.Empty<ForwardBaseRenderData>(), new[] { intel }, personX: 10);

        var interpolated = WorldSnapshotInterpolator.Interpolate(previous, current, alpha: 0.5f);

        var result = Assert.Single(interpolated.ScoutIntel);
        Assert.Equal(intel.IntelId, result.IntelId);
        Assert.Equal(intel.OwnerFactionId, result.OwnerFactionId);
        Assert.Equal(intel.ObservedColonyId, result.ObservedColonyId);
        Assert.Equal(intel.ObservationKind, result.ObservationKind);
        Assert.Equal(5, Assert.Single(interpolated.People).X);
    }

    private static WorldRenderSnapshot CreateSnapshot(IReadOnlyList<CampaignRenderData> campaigns, int personX)
        => CreateSnapshot(campaigns, Array.Empty<SupplyConvoyRenderData>(), personX);

    private static WorldRenderSnapshot CreateSnapshot(
        IReadOnlyList<CampaignRenderData> campaigns,
        IReadOnlyList<SupplyConvoyRenderData> supplyConvoys,
        int personX)
        => CreateSnapshot(campaigns, supplyConvoys, Array.Empty<ForwardBaseRenderData>(), personX);

    private static WorldRenderSnapshot CreateSnapshot(
        IReadOnlyList<CampaignRenderData> campaigns,
        IReadOnlyList<SupplyConvoyRenderData> supplyConvoys,
        IReadOnlyList<ForwardBaseRenderData> forwardBases,
        int personX)
        => CreateSnapshot(campaigns, supplyConvoys, forwardBases, Array.Empty<ScoutIntelRenderData>(), personX);

    private static WorldRenderSnapshot CreateSnapshot(
        IReadOnlyList<CampaignRenderData> campaigns,
        IReadOnlyList<SupplyConvoyRenderData> supplyConvoys,
        IReadOnlyList<ForwardBaseRenderData> forwardBases,
        IReadOnlyList<ScoutIntelRenderData> scoutIntel,
        int personX)
        => new(
            Width: 16,
            Height: 16,
            Tiles: Array.Empty<TileRenderData>(),
            Houses: Array.Empty<HouseRenderData>(),
            SpecializedBuildings: Array.Empty<SpecializedBuildingRenderData>(),
            DefensiveStructures: Array.Empty<DefensiveStructureRenderData>(),
            People: new[] { CreatePerson(personX) },
            Animals: Array.Empty<AnimalRenderData>(),
            Colonies: Array.Empty<ColonyHudData>(),
            CombatGroups: Array.Empty<CombatGroupRenderData>(),
            Battles: Array.Empty<BattleRenderData>(),
            Sieges: Array.Empty<SiegeRenderData>(),
            Breaches: Array.Empty<BreachRenderData>(),
            Campaigns: campaigns,
            SupplyConvoys: supplyConvoys,
            ForwardBases: forwardBases,
            ScoutIntel: scoutIntel,
            FactionStances: Array.Empty<FactionStanceRenderData>(),
            Ecology: CreateEcoHudData(),
            CurrentSeason: SeasonView.Spring,
            IsDroughtActive: false,
            RecentEvents: Array.Empty<string>(),
            Director: DirectorRenderState.Empty);

    private static PersonRenderData CreatePerson(int x)
        => new(
            X: x,
            Y: 4,
            ActorId: 7,
            ColonyId: 2,
            Health: 100f,
            IsInCombat: false,
            LastCombatTick: -1,
            NoProgressStreak: 0,
            BackoffTicksRemaining: 0,
            DebugDecisionCause: string.Empty,
            DebugTargetKey: string.Empty);

    private static CampaignRenderData CreateCampaign()
        => new(
            CampaignId: 42,
            ArmyId: 77,
            OwnerFactionId: 1,
            TargetFactionId: 2,
            OriginColonyId: 3,
            TargetColonyId: 4,
            Phase: "marching",
            Status: "marching",
            CreatedTick: 11,
            Route: new CampaignRouteRenderData(
                OriginX: 1,
                OriginY: 2,
                TargetX: 13,
                TargetY: 14,
                HasResolvedObjective: true,
                ResolvedObjectiveX: 12,
                ResolvedObjectiveY: 14,
                UsesFallbackObjective: true,
                PathRequests: 2,
                PathCacheHits: 1,
                BlockedMovementChecks: 3,
                RouteRecomputes: 1,
                MarchProgressTicks: 5,
                EncounterTicks: 0,
                NoProgressTicks: 0,
                CachedWaypointCount: 2,
                NextWaypointIndex: 1),
            Army: new ArmyRenderData(
                ArmyId: 77,
                HomeColonyId: 3,
                OriginX: 1,
                OriginY: 2,
                TargetX: 13,
                TargetY: 14,
                RequestedMemberCount: 2,
                AssignedMemberCount: 2,
                MemberActorIds: new[] { 7, 8 },
                HasRallyPoint: true,
                RallyX: 2,
                RallyY: 3,
                IsAssembled: true,
                AssemblyStartedTick: 11,
                AssemblyCompletedTick: 12,
                AnchorActorId: 7,
                AnchorX: 4,
                AnchorY: 5),
            Supply: new ArmySupplyRenderData(
                FractionalFoodDemand: 0.25f,
                SustainedOutOfSupplyTicks: 0,
                RationPoolFood: 3,
                AssignedCarrierActorId: 9,
                HasAssignedCarrier: true,
                LastSupplyTick: 20,
                LastSupplySource: "carried_inventory",
                ForageAttempts: 1,
                ForageSuccesses: 1,
                ForageFailures: 0,
                ForageFoodGained: 2,
                LastForageSourceX: 6,
                LastForageSourceY: 7,
                LastForageConsumerKey: "army:77",
                LastForageStatus: "succeeded",
                LastForageFailureReason: "none"),
            RouteWaypoints: new[]
            {
                new CampaignRouteWaypointRenderData(0, 1, 2, IsNext: false),
                new CampaignRouteWaypointRenderData(1, 12, 14, IsNext: true)
            },
            Resolution: CampaignResolutionRenderData.Empty,
            Encounters: Array.Empty<CampaignEncounterRenderData>());

    private static SupplyConvoyRenderData CreateSupplyConvoy()
        => new(
            ConvoyId: 8,
            OwnerFactionId: 1,
            HomeColonyId: 3,
            TargetCampaignId: 42,
            TargetArmyId: 77,
            Phase: "marching",
            CreatedTick: 30,
            CompletedTick: -1,
            CurrentX: 5,
            CurrentY: 6,
            TargetX: 12,
            TargetY: 14,
            PayloadFood: 6,
            PathRequests: 1,
            PathCacheHits: 0,
            RouteRecomputes: 1,
            ProgressTicks: 2,
            NoProgressTicks: 0);

    private static ForwardBaseRenderData CreateForwardBase()
        => new(
            BaseId: 3,
            OwnerFactionId: 1,
            HomeColonyId: 3,
            CampaignId: 42,
            ArmyId: 77,
            Phase: "active",
            CreatedTick: 40,
            EndedTick: -1,
            X: 8,
            Y: 9,
            Radius: 2,
            RestTicks: 1,
            RestedActorTicks: 2,
            CloseReason: "none");

    private static ScoutIntelRenderData CreateScoutIntel()
        => new(
            IntelId: 12,
            OwnerFactionId: 1,
            ObservedFactionId: 2,
            ObservedColonyId: 3,
            ObservationKind: "colony",
            X: 6,
            Y: 7,
            SourceActorId: 9,
            CreatedTick: 40,
            LastRefreshTick: 41,
            ExpirationTick: 100,
            TicksSinceRefresh: 0,
            Confidence: 0.8f);

    private static EcoHudData CreateEcoHudData()
        => new(
            Herbivores: 0,
            Predators: 0,
            ActiveFoodNodes: 0,
            DepletedFoodNodes: 0,
            CriticalHungry: 0,
            AnimalStuckRecoveries: 0,
            PredatorDeaths: 0,
            PredatorHumanHits: 0,
            DeathsOldAge: 0,
            DeathsStarvation: 0,
            DeathsPredator: 0,
            DeathsOther: 0,
            DeathsStarvationRecent60s: 0,
            DeathsStarvationWithFood: 0,
            PredatorHumanAttacksEnabled: false,
            AverageFoodPerPerson: 0f,
            ColoniesInFoodEmergency: 0,
            FoodPerPersonSpread: 0f,
            SoftReservationCount: 0,
            OverlapResolveMoves: 0,
            CrowdDissipationMoves: 0,
            BirthFallbackToOccupied: 0,
            BirthFallbackToParent: 0,
            BuildSiteResetCount: 0,
            NoProgressBackoffResource: 0,
            NoProgressBackoffBuild: 0,
            NoProgressBackoffFlee: 0,
            NoProgressBackoffCombat: 0,
            DenseNeighborhoodTicks: 0,
            LastTickDenseActors: 0);
}
