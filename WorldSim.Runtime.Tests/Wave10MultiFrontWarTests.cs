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

public sealed class Wave10MultiFrontWarTests
{
    private const int OrganicCadenceTicks = 20;

    [Fact]
    public void OrganicMultiFront_TargetFilteringExcludesCappedPairAndAllowsDifferentFront()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        PrepareEligibleWarriors(runtime, Faction.Obsidari, count: 5);
        DeclareWarWithIntel(runtime, Faction.Obsidari, Faction.Sylvars);
        DeclareWarWithIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        Assert.True(ApplyOrganicLaunch(runtime, Faction.Obsidari, Faction.Sylvars, requestedWarriors: 1).Success);
        var targetOptions = BuildOrganicCampaignTargetOptions(runtime, Faction.Obsidari, availableForLaunch: 3);
        Assert.DoesNotContain(targetOptions, target => target.TargetFactionId == (int)Faction.Sylvars);
        Assert.Contains(targetOptions, target => target.TargetFactionId == (int)Faction.Aetheri);
        Assert.True(ApplyOrganicLaunch(runtime, Faction.Obsidari, Faction.Aetheri, requestedWarriors: 1).Success);

        var campaigns = runtime.Campaigns
            .Where(campaign => campaign.OwnerFaction == Faction.Obsidari && campaign.Phase != CampaignPhase.Resolved)
            .ToArray();
        Assert.Equal(2, campaigns.Length);
        Assert.Equal(2, campaigns.Select(campaign => campaign.TargetFaction).Distinct().Count());
        Assert.Contains(campaigns, campaign => campaign.TargetFaction == Faction.Sylvars);
        Assert.Contains(campaigns, campaign => campaign.TargetFaction == Faction.Aetheri);
        Assert.Equal(0, runtime.CampaignLogisticsCounters.CampaignLaunchBlockedByPairCap);
    }

    [Fact]
    public void OrganicMultiFront_ThirdUnresolvedCampaignIsBlockedByOwnerCap()
    {
        var targetColonyId = GetColony(GetWorld(CreateRuntime()), Faction.Chirita).Id;
        var runtime = CreateRuntime(FixedLaunchStrategist.For(Faction.Chirita, targetColonyId, requestedWarriors: 1));
        EnableDiplomacyAndCombat(runtime);
        PrepareEligibleWarriors(runtime, Faction.Obsidari, count: 6);
        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Sylvars, requestedMemberCount: 1).Success);
        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1).Success);
        DeclareWarWithIntel(runtime, Faction.Obsidari, Faction.Chirita);

        AdvanceToNextCadence(runtime);

        Assert.Equal(2, runtime.Campaigns.Count(campaign => campaign.OwnerFaction == Faction.Obsidari && campaign.Phase != CampaignPhase.Resolved));
        Assert.True(runtime.CampaignLogisticsCounters.CampaignLaunchBlockedByCap > 0);
    }

    [Fact]
    public void OrganicMultiFront_InjectedDuplicateSamePairIsBlockedAtApplyBoundary()
    {
        var targetColonyId = GetColony(GetWorld(CreateRuntime()), Faction.Aetheri).Id;
        var runtime = CreateRuntime(FixedLaunchStrategist.For(Faction.Aetheri, targetColonyId, requestedWarriors: 1));
        EnableDiplomacyAndCombat(runtime);
        PrepareEligibleWarriors(runtime, Faction.Obsidari, count: 4);
        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1).Success);
        DeclareWarWithIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        AdvanceToNextCadence(runtime);

        Assert.Single(runtime.Campaigns, campaign => campaign.OwnerFaction == Faction.Obsidari && campaign.TargetFaction == Faction.Aetheri);
        Assert.True(runtime.CampaignLogisticsCounters.CampaignLaunchBlockedByPairCap > 0);
    }

    [Fact]
    public void OrganicMultiFront_HomeGarrisonReservePreventsEmptyingColonyAcrossFronts()
    {
        var runtime = CreateRuntime(new QueuedLaunchStrategist(
            Faction.Obsidari,
            ((int)Faction.Sylvars, GetColony(GetWorld(CreateRuntime()), Faction.Sylvars).Id, 2),
            ((int)Faction.Aetheri, GetColony(GetWorld(CreateRuntime()), Faction.Aetheri).Id, 1)));
        EnableDiplomacyAndCombat(runtime);
        PrepareEligibleWarriors(runtime, Faction.Obsidari, count: 3);
        DeclareWarWithIntel(runtime, Faction.Obsidari, Faction.Sylvars);
        DeclareWarWithIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        AdvanceToNextCadence(runtime);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);
        AdvanceToNextCadence(runtime);

        Assert.Single(runtime.Campaigns, campaign => campaign.OwnerFaction == Faction.Obsidari && campaign.Phase != CampaignPhase.Resolved);
        Assert.True(runtime.CampaignLogisticsCounters.CampaignLaunchBlockedByHomeDefense > 0);
    }

    [Fact]
    public void OrganicMultiFront_RouteBudgetFailureBlocksLaunchBeforeCreation()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        PrepareEligibleWarriors(runtime, Faction.Obsidari, count: 3);
        var world = GetWorld(runtime);
        var target = GetColony(world, Faction.Aetheri);
        MoveTargetOriginToBlockableTile(world, target);
        Assert.True(world.TryAddWoodWall(target, target.Origin));
        DeclareWarWithIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        AdvanceToNextCadence(runtime);

        Assert.DoesNotContain(runtime.Campaigns, campaign => campaign.OwnerFaction == Faction.Obsidari);
        Assert.True(runtime.CampaignLogisticsCounters.CampaignLaunchRouteBudgetExhausted > 0);
    }

    [Fact]
    public void OrganicMultiFront_ResolvedHistoricalCampaignsDoNotCountAgainstOwnerOrPairCaps()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        PrepareEligibleWarriors(runtime, Faction.Obsidari, count: 4);
        DeclareWarWithIntel(runtime, Faction.Obsidari, Faction.Aetheri);
        Assert.True(ApplyOrganicLaunch(runtime, Faction.Obsidari, Faction.Aetheri, requestedWarriors: 1).Success);
        ResolveCampaign(Assert.Single(GetCampaignStates(runtime)));
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);

        Assert.True(ApplyOrganicLaunch(runtime, Faction.Obsidari, Faction.Aetheri, requestedWarriors: 1).Success);

        Assert.Equal(2, runtime.Campaigns.Count(campaign => campaign.OwnerFaction == Faction.Obsidari));
        Assert.Single(runtime.Campaigns, campaign => campaign.OwnerFaction == Faction.Obsidari && campaign.Phase != CampaignPhase.Resolved);
        Assert.Equal(0, runtime.CampaignLogisticsCounters.CampaignLaunchBlockedByCap);
        Assert.Equal(0, runtime.CampaignLogisticsCounters.CampaignLaunchBlockedByPairCap);
    }

    [Fact]
    public void OrganicMultiFront_WarScoreModifierDeterministicallyShapesTargetChoiceWithoutBypassingGates()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);
        PrepareEligibleWarriors(runtime, Faction.Obsidari, count: 3);
        GetWorld(runtime).SetFactionStance(Faction.Obsidari, Faction.Sylvars, Stance.Hostile);
        GetWorld(runtime).SetFactionStance(Faction.Obsidari, Faction.Aetheri, Stance.Hostile);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Sylvars);
        AddActionableScoutIntel(runtime, Faction.Obsidari, Faction.Aetheri);
        RecordCampaignWarScore(runtime, Faction.Obsidari, Faction.Sylvars, CampaignResolutionPolicy.AttackerVictoryWarScoreDelta);
        RecordCampaignWarScore(runtime, Faction.Aetheri, Faction.Obsidari, CampaignResolutionPolicy.AttackerVictoryWarScoreDelta);

        AdvanceToNextCadence(runtime);

        var campaign = Assert.Single(runtime.Campaigns, campaign => campaign.OwnerFaction == Faction.Obsidari);
        Assert.Equal(Faction.Aetheri, campaign.TargetFaction);
    }

    [Fact]
    public void PublicManualCampaignCreation_RemainsUncappedCompatibilityPath()
    {
        var runtime = CreateRuntime();
        EnableDiplomacyAndCombat(runtime);

        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Sylvars, requestedMemberCount: 1).Success);
        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1).Success);
        Assert.True(runtime.TryCreateCampaign(Faction.Obsidari, Faction.Chirita, requestedMemberCount: 1).Success);

        Assert.Equal(3, runtime.Campaigns.Count(campaign => campaign.OwnerFaction == Faction.Obsidari));
        Assert.Equal(0, runtime.CampaignLogisticsCounters.CampaignLaunchBlockedByCap);
    }

    private static SimulationRuntime CreateRuntime(ICampaignStrategist? strategist = null)
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 32, techPath, aiOptions: null, randomSeed: 9601, campaignStrategist: strategist);
    }

    private static void EnableDiplomacyAndCombat(SimulationRuntime runtime)
    {
        var world = GetWorld(runtime);
        world.EnableDiplomacy = true;
        world.EnableCombatPrimitives = true;
    }

    private static void PrepareEligibleWarriors(SimulationRuntime runtime, Faction faction, int count)
    {
        SetAllCampaignCandidatesIneligible(GetWorld(runtime));
        foreach (var member in SelectCampaignMemberCandidates(GetWorld(runtime), faction, count))
            member.AssignRole(PersonRole.Warrior);
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

    private static void DeclareWarWithIntel(SimulationRuntime runtime, Faction ownerFaction, Faction targetFaction)
    {
        runtime.DeclareWar(ownerFaction, targetFaction, "p7-g multi-front test");
        AddActionableScoutIntel(runtime, ownerFaction, targetFaction);
    }

    private static void AddActionableScoutIntel(SimulationRuntime runtime, Faction ownerFaction, Faction targetFaction)
    {
        var world = GetWorld(runtime);
        var owner = GetColony(world, ownerFaction);
        var target = GetColony(world, targetFaction);
        var sourceActor = SelectCampaignMemberCandidates(world, ownerFaction, 1)[0];
        sourceActor.AssignRole(PersonRole.Scout);
        GetScoutIntelStates(runtime).Add(new ScoutIntelState(
            intelId: GetScoutIntelStates(runtime).Count + 1,
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
        var field = typeof(SimulationRuntime).GetField("_scoutIntel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<List<ScoutIntelState>>(field!.GetValue(runtime));
    }

    private static List<CampaignState> GetCampaignStates(SimulationRuntime runtime)
    {
        var field = typeof(SimulationRuntime).GetField("_campaigns", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<List<CampaignState>>(field!.GetValue(runtime));
    }

    private static void AdvanceToNextCadence(SimulationRuntime runtime)
    {
        if (runtime.Tick < OrganicCadenceTicks)
            SetRuntimeTick(runtime, OrganicCadenceTicks);
        else if (runtime.Tick % OrganicCadenceTicks != 0)
            SetRuntimeTick(runtime, runtime.Tick + (OrganicCadenceTicks - (runtime.Tick % OrganicCadenceTicks)));

        runtime.AdvanceTick(0f);
    }

    private static void SetRuntimeTick(SimulationRuntime runtime, long tick)
    {
        var field = typeof(SimulationRuntime).GetField("<Tick>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(runtime, tick);
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

    private static void RecordCampaignWarScore(SimulationRuntime runtime, Faction attacker, Faction defender, int delta)
    {
        var method = typeof(SimulationRuntime).GetMethod("RecordCampaignWarScore", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(runtime, new object[] { attacker, defender, delta });
    }

    private static CampaignCreationResult ApplyOrganicLaunch(SimulationRuntime runtime, Faction ownerFaction, Faction targetFaction, int requestedWarriors)
    {
        var world = GetWorld(runtime);
        var owner = GetColony(world, ownerFaction);
        var target = GetColony(world, targetFaction);
        var method = typeof(SimulationRuntime).GetMethod("TryApplyOrganicCampaignLaunch", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<CampaignCreationResult>(method!.Invoke(runtime, new object[]
        {
            owner,
            new CampaignStrategyDecision(
                CampaignStrategyDecisionKind.LaunchCampaign,
                CampaignStrategyReasonCode.TargetPressureAndAdvantage,
                TargetFactionId: (int)targetFaction,
                TargetColonyId: target.Id,
                RequestedWarriors: requestedWarriors,
                Score: 1.0f),
            new HashSet<int>(),
            new HashSet<int>()
        }));
    }

    private static IReadOnlyList<CampaignTargetOption> BuildOrganicCampaignTargetOptions(
        SimulationRuntime runtime,
        Faction ownerFaction,
        int availableForLaunch)
    {
        var owner = GetColony(GetWorld(runtime), ownerFaction);
        var method = typeof(SimulationRuntime).GetMethod("BuildOrganicCampaignTargetOptions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IReadOnlyList<CampaignTargetOption>>(method!.Invoke(runtime, new object[]
        {
            owner,
            availableForLaunch,
            new HashSet<int>(),
            new HashSet<int>()
        }));
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

        throw new InvalidOperationException("Could not find a buildable multi-front test tile.");
    }

    private static Colony GetColony(World world, Faction faction)
    {
        var colony = world._colonies.FirstOrDefault(candidate => candidate.Faction == faction);
        Assert.NotNull(colony);
        return colony!;
    }

    private static World GetWorld(SimulationRuntime runtime)
    {
        var field = typeof(SimulationRuntime).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var world = field!.GetValue(runtime) as World;
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

    private sealed class FirstAvailableTargetStrategist : ICampaignStrategist
    {
        private readonly Faction _ownerFaction;
        private readonly int _requestedWarriors;

        public FirstAvailableTargetStrategist(Faction ownerFaction, int requestedWarriors)
        {
            _ownerFaction = ownerFaction;
            _requestedWarriors = requestedWarriors;
        }

        public CampaignStrategyDecision Decide(in CampaignStrategyContext context)
        {
            if (context.FactionId != (int)_ownerFaction)
            {
                return new CampaignStrategyDecision(
                    CampaignStrategyDecisionKind.HoldDefensivePosture,
                    CampaignStrategyReasonCode.NoViableTarget);
            }

            var target = (context.Targets ?? Array.Empty<CampaignTargetOption>()).FirstOrDefault(candidate => candidate.IsKnown);
            if (target.TargetFactionId < 0)
            {
                return new CampaignStrategyDecision(
                    CampaignStrategyDecisionKind.HoldDefensivePosture,
                    CampaignStrategyReasonCode.NoViableTarget);
            }

            return new CampaignStrategyDecision(
                CampaignStrategyDecisionKind.LaunchCampaign,
                CampaignStrategyReasonCode.TargetPressureAndAdvantage,
                TargetFactionId: target.TargetFactionId,
                TargetColonyId: target.TargetColonyId,
                RequestedWarriors: _requestedWarriors,
                Score: 1.0f);
        }
    }

    private sealed class QueuedLaunchStrategist : ICampaignStrategist
    {
        private readonly Faction _ownerFaction;
        private readonly Queue<(int TargetFactionId, int TargetColonyId, int RequestedWarriors)> _launches;

        public QueuedLaunchStrategist(Faction ownerFaction, params (int TargetFactionId, int TargetColonyId, int RequestedWarriors)[] launches)
        {
            _ownerFaction = ownerFaction;
            _launches = new Queue<(int TargetFactionId, int TargetColonyId, int RequestedWarriors)>(launches);
        }

        public CampaignStrategyDecision Decide(in CampaignStrategyContext context)
        {
            if (context.FactionId != (int)_ownerFaction || _launches.Count == 0)
            {
                return new CampaignStrategyDecision(
                    CampaignStrategyDecisionKind.HoldDefensivePosture,
                    CampaignStrategyReasonCode.NoViableTarget);
            }

            var launch = _launches.Dequeue();
            return new CampaignStrategyDecision(
                CampaignStrategyDecisionKind.LaunchCampaign,
                CampaignStrategyReasonCode.TargetPressureAndAdvantage,
                TargetFactionId: launch.TargetFactionId,
                TargetColonyId: launch.TargetColonyId,
                RequestedWarriors: launch.RequestedWarriors,
                Score: 1.0f);
        }
    }
}
