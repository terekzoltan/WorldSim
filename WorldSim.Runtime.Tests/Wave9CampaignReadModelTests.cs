using System;
using System.IO;
using System.Linq;
using System.Reflection;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Combat;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave9CampaignReadModelTests
{
    [Fact]
    public void WorldSnapshotBuilder_WorldOnlySnapshotExportsEmptyCampaigns()
    {
        var world = new World(width: 24, height: 18, initialPop: 4, randomSeed: 9701);

        var snapshot = WorldSnapshotBuilder.Build(world);

        Assert.NotNull(snapshot.Campaigns);
        Assert.Empty(snapshot.Campaigns);
    }

    [Fact]
    public void ReadModelStringMappings_UseStableContractLiterals()
    {
        Assert.Equal("assembling_pending", InvokePrivateStatic<string>("MapCampaignPhaseForReadModel", CampaignPhase.AssemblingPending));
        Assert.Equal("assembling", InvokePrivateStatic<string>("MapCampaignPhaseForReadModel", CampaignPhase.Assembling));
        Assert.Equal("marching", InvokePrivateStatic<string>("MapCampaignPhaseForReadModel", CampaignPhase.Marching));
        Assert.Equal("encounter", InvokePrivateStatic<string>("MapCampaignPhaseForReadModel", CampaignPhase.Encounter));
        Assert.Equal("resolved", InvokePrivateStatic<string>("MapCampaignPhaseForReadModel", CampaignPhase.Resolved));
        Assert.Equal("unknown", InvokePrivateStatic<string>("MapCampaignPhaseForReadModel", (CampaignPhase)999));

        Assert.Equal("none", InvokePrivateStatic<string>("MapArmySupplySourceForReadModel", ArmySupplySourceMode.None));
        Assert.Equal("carried_inventory", InvokePrivateStatic<string>("MapArmySupplySourceForReadModel", ArmySupplySourceMode.CarriedInventory));
        Assert.Equal("ration_pool", InvokePrivateStatic<string>("MapArmySupplySourceForReadModel", ArmySupplySourceMode.RationPool));
        Assert.Equal("unknown", InvokePrivateStatic<string>("MapArmySupplySourceForReadModel", (ArmySupplySourceMode)999));

        Assert.Equal("succeeded", InvokePrivateStatic<string>("MapArmyForageStatusForReadModel", ArmyForageStatus.Succeeded));
        Assert.Equal("failed", InvokePrivateStatic<string>("MapArmyForageStatusForReadModel", ArmyForageStatus.Failed));
        Assert.Equal("unknown", InvokePrivateStatic<string>("MapArmyForageStatusForReadModel", (ArmyForageStatus)999));

        Assert.Equal("none", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.None));
        Assert.Equal("invalid_consumer_key", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.InvalidConsumerKey));
        Assert.Equal("forager_dead", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.ForagerDead));
        Assert.Equal("source_out_of_bounds", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.SourceOutOfBounds));
        Assert.Equal("source_out_of_range", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.SourceOutOfRange));
        Assert.Equal("water_tile", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.WaterTile));
        Assert.Equal("no_resource_node", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.NoResourceNode));
        Assert.Equal("wrong_resource", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.WrongResource));
        Assert.Equal("depleted_food", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.DepletedFood));
        Assert.Equal("consumer_cap_reached", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.ConsumerCapReached));
        Assert.Equal("no_yield", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.NoYield));
        Assert.Equal("harvest_failed", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", ArmyForageFailureReason.HarvestFailed));
        Assert.Equal("unknown", InvokePrivateStatic<string>("MapArmyForageFailureReasonForReadModel", (ArmyForageFailureReason)999));
    }

    [Fact]
    public void CampaignSnapshot_IsDetachedFromLaterRuntimeMutation()
    {
        var fixture = CreateMarchingCampaign();
        var retained = fixture.Runtime.GetSnapshot();
        var retainedCampaign = Assert.Single(retained.Campaigns);
        var retainedMemberIds = retainedCampaign.Army.MemberActorIds;
        var retainedWaypoints = retainedCampaign.RouteWaypoints;
        var retainedMemberIdValues = retainedMemberIds.ToArray();
        var retainedWaypointValues = retainedWaypoints.Select(point => (point.Index, point.X, point.Y, point.IsNext)).ToArray();

        fixture.Runtime.AdvanceTick(1f);
        var currentCampaign = Assert.Single(fixture.Runtime.GetSnapshot().Campaigns);

        Assert.Equal(retainedCampaign.CampaignId, currentCampaign.CampaignId);
        Assert.Equal(retainedMemberIdValues, retainedCampaign.Army.MemberActorIds.ToArray());
        Assert.Equal(retainedWaypointValues, retainedCampaign.RouteWaypoints.Select(point => (point.Index, point.X, point.Y, point.IsNext)).ToArray());
        Assert.NotSame(retainedCampaign.Army.MemberActorIds, currentCampaign.Army.MemberActorIds);
        Assert.NotSame(retainedCampaign.RouteWaypoints, currentCampaign.RouteWaypoints);
    }

    [Fact]
    public void MarchingCampaignSnapshot_ExportsRouteSupplyAndCounters()
    {
        var fixture = CreateMarchingCampaign();
        fixture.Members[0].Inventory.TryAdd(ItemType.Food, 3);

        fixture.Runtime.AdvanceTick(1f);

        var campaign = Assert.Single(fixture.Runtime.GetSnapshot().Campaigns);
        var queryCampaign = Assert.Single(fixture.Runtime.Campaigns);
        Assert.Equal("marching", campaign.Phase);
        Assert.Equal("marching", campaign.Status);
        Assert.Equal((int)Faction.Obsidari, campaign.OwnerFactionId);
        Assert.Equal((int)Faction.Aetheri, campaign.TargetFactionId);
        Assert.Equal(queryCampaign.RouteIntent.OriginX, campaign.Route.OriginX);
        Assert.Equal(queryCampaign.RouteIntent.OriginY, campaign.Route.OriginY);
        Assert.Equal(queryCampaign.RouteIntent.TargetX, campaign.Route.TargetX);
        Assert.Equal(queryCampaign.RouteIntent.TargetY, campaign.Route.TargetY);
        Assert.True(campaign.Route.HasResolvedObjective);
        Assert.InRange(campaign.Route.ResolvedObjectiveX, 0, fixture.World.Width - 1);
        Assert.InRange(campaign.Route.ResolvedObjectiveY, 0, fixture.World.Height - 1);
        Assert.True(campaign.Route.PathRequests > 0);
        Assert.True(campaign.Route.RouteRecomputes > 0);
        Assert.True(campaign.Route.CachedWaypointCount > 0);
        Assert.Equal(campaign.Route.CachedWaypointCount, campaign.RouteWaypoints.Count);
        var nextWaypoint = Assert.Single(campaign.RouteWaypoints, point => point.IsNext);
        Assert.Equal(campaign.Route.NextWaypointIndex, nextWaypoint.Index);
        Assert.True(campaign.Route.NextWaypointIndex >= 0);
        var lastWaypoint = campaign.RouteWaypoints.OrderBy(point => point.Index).Last();
        Assert.Equal(campaign.Route.ResolvedObjectiveX, lastWaypoint.X);
        Assert.Equal(campaign.Route.ResolvedObjectiveY, lastWaypoint.Y);
        Assert.Equal(1, campaign.Route.MarchProgressTicks);
        Assert.Equal(0, campaign.Route.EncounterTicks);
        Assert.Equal("carried_inventory", campaign.Supply.LastSupplySource);
        Assert.Equal("failed", campaign.Supply.LastForageStatus);
        Assert.Equal("none", campaign.Supply.LastForageFailureReason);
        Assert.True(campaign.Army.IsAssembled);
        Assert.Equal(1, campaign.Army.AssignedMemberCount);
        Assert.Equal(fixture.Members[0].Id, Assert.Single(campaign.Army.MemberActorIds));
        Assert.Equal(fixture.Members[0].Id, campaign.Army.AnchorActorId);
    }

    [Fact]
    public void GetSnapshot_DoesNotMutateCampaignRouteOrMemberState()
    {
        var fixture = CreateMarchingCampaign();
        fixture.Members[0].Inventory.TryAdd(ItemType.Food, 3);
        fixture.Runtime.AdvanceTick(1f);
        var campaignBefore = GetCampaignStates(fixture.Runtime).Single();
        var before = CaptureMutableCampaignState(campaignBefore);

        _ = fixture.Runtime.GetSnapshot();

        var campaignAfter = GetCampaignStates(fixture.Runtime).Single();
        var after = CaptureMutableCampaignState(campaignAfter);
        Assert.Equal(before, after);
    }

    [Fact]
    public void EncounterCampaignSnapshot_ExportsNonResolvingEncounterMarker()
    {
        var fixture = CreateMarchingCampaign();
        var target = GetColony(fixture.World, Faction.Aetheri);
        var beforeStance = fixture.Runtime.GetSnapshot().FactionStances.First(entry =>
            entry.LeftFactionId == Math.Min((int)Faction.Obsidari, (int)Faction.Aetheri)
            && entry.RightFactionId == Math.Max((int)Faction.Obsidari, (int)Faction.Aetheri)).Stance;
        fixture.Members[0].Pos = target.Origin;

        fixture.Runtime.AdvanceTick(0f);

        var snapshot = fixture.Runtime.GetSnapshot();
        var campaign = Assert.Single(snapshot.Campaigns);
        var encounter = Assert.Single(campaign.Encounters);
        var afterStance = snapshot.FactionStances.First(entry =>
            entry.LeftFactionId == Math.Min((int)Faction.Obsidari, (int)Faction.Aetheri)
            && entry.RightFactionId == Math.Max((int)Faction.Obsidari, (int)Faction.Aetheri)).Stance;
        Assert.Equal("encounter", campaign.Phase);
        Assert.Equal("encounter_active", campaign.Status);
        Assert.Equal("active", encounter.Status);
        Assert.Equal("non_resolving", encounter.Outcome);
        Assert.Equal(campaign.CampaignId, encounter.CampaignId);
        Assert.Equal(fixture.Members[0].Pos.x, encounter.SourceX);
        Assert.Equal(fixture.Members[0].Pos.y, encounter.SourceY);
        Assert.True(Math.Abs(encounter.TargetX - target.Origin.x) + Math.Abs(encounter.TargetY - target.Origin.y) <= 1);
        Assert.Equal(campaign.Route.EncounterTicks, encounter.EncounterTicks);
        Assert.Equal(beforeStance, afterStance);
    }

    private static MarchingCampaignFixture CreateMarchingCampaign()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        var world = GetWorld(runtime);
        SetAllCampaignCandidatesIneligible(runtime);
        var member = GetPeople(world, Faction.Obsidari).First();
        member.Profession = Profession.Hunter;

        var result = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        Assert.True(result.Success);
        world.EnableCombatPrimitives = false;

        for (var i = 0; i < 80 && runtime.Campaigns.Single().Phase != CampaignPhase.Marching; i++)
            runtime.AdvanceTick(0f);

        Assert.Equal(CampaignPhase.Marching, runtime.Campaigns.Single().Phase);
        return new MarchingCampaignFixture(runtime, world, new[] { member });
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 12, techPath, aiOptions: null, randomSeed: 9601);
    }

    private static void EnableDiplomacyAndCombat(SimulationRuntime runtime)
    {
        var world = GetWorld(runtime);
        world.EnableDiplomacy = true;
        world.EnableCombatPrimitives = true;
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

    private static World GetWorld(SimulationRuntime runtime)
    {
        var worldField = typeof(SimulationRuntime).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(worldField);
        var world = worldField!.GetValue(runtime) as World;
        Assert.NotNull(world);
        return world!;
    }

    private static IReadOnlyList<CampaignState> GetCampaignStates(SimulationRuntime runtime)
    {
        var campaignsField = typeof(SimulationRuntime).GetField("_campaigns", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(campaignsField);
        var campaigns = campaignsField!.GetValue(runtime) as IReadOnlyList<CampaignState>;
        Assert.NotNull(campaigns);
        return campaigns!;
    }

    private static MutableCampaignState CaptureMutableCampaignState(CampaignState campaign)
        => new(
            campaign.Phase,
            campaign.RouteCounters.PathRequests,
            campaign.RouteCounters.PathCacheHits,
            campaign.RouteCounters.BlockedMovementChecks,
            campaign.RouteCounters.RouteRecomputes,
            campaign.RouteCounters.MarchProgressTicks,
            campaign.RouteCounters.EncounterTicks,
            campaign.RouteCounters.NoProgressTicks,
            campaign.RouteCache.Steps.Count,
            campaign.RouteCache.NextIndex,
            string.Join(",", campaign.Army.MemberActorIds));

    private static T InvokePrivateStatic<T>(string methodName, object argument)
    {
        var method = typeof(SimulationRuntime).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var value = method!.Invoke(null, new[] { argument });
        Assert.IsType<T>(value);
        return (T)value!;
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

    private sealed record MarchingCampaignFixture(
        SimulationRuntime Runtime,
        World World,
        IReadOnlyList<Person> Members);

    private sealed record MutableCampaignState(
        CampaignPhase Phase,
        int PathRequests,
        int PathCacheHits,
        int BlockedMovementChecks,
        int RouteRecomputes,
        int MarchProgressTicks,
        int EncounterTicks,
        int NoProgressTicks,
        int RouteCacheStepCount,
        int RouteCacheNextIndex,
        string MemberActorIdsKey);
}
