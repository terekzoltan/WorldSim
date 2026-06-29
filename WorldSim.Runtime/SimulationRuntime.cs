using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using WorldSim.AI;
using WorldSim.Runtime.Diagnostics;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Combat;
using WorldSim.Simulation.Defense;
using WorldSim.Simulation.Diplomacy;
using WorldSim.Simulation.Ecology;
using WorldSim.Simulation.Effects;
using WorldSim.Simulation.Military;
using WorldSim.Simulation.Navigation;

namespace WorldSim.Runtime;

public sealed class SimulationRuntime
{
    private static readonly HashSet<string> KnownDirectorDirectives = new(StringComparer.Ordinal)
    {
        "PrioritizeFood",
        "StabilizeMorale",
        "BoostIndustry"
    };

    private static readonly HashSet<string> KnownTreatyKinds = new(StringComparer.Ordinal)
    {
        "ceasefire",
        "peace_talks"
    };

    private static readonly HashSet<string> KnownCausalConditionMetrics = new(StringComparer.Ordinal)
    {
        "food_reserves_pct",
        "morale_avg",
        "population",
        "economy_output"
    };

    private static readonly HashSet<string> KnownCausalConditionOperators = new(StringComparer.Ordinal)
    {
        "lt",
        "gt",
        "eq"
    };

    private const int MinCausalWindowTicks = 10;
    private const int MaxCausalWindowTicks = 100;
    private const int CampaignPathMaxExpansions = 4096;
    private const int OperatorSmokeCampaignPathMaxExpansions = 32768;
    private const int OrganicCampaignLaunchCadenceTicks = 20;
    private const int OrganicCampaignWarScorePressureThreshold = 30;
    private const float OrganicCampaignWarScorePressureModifier = 0.08f;
    private const float RamWallGatePressureDamage = 18f;
    private const float RamStructurePressureDamage = 8f;
    private const float MobileCatapultPressureDamage = 10f;
    private const double FloatingCausalEqTolerance = 0.0001d;

    private readonly record struct CampaignMarchObjective((int x, int y) MovementTarget, bool UsesFallback);

    private readonly record struct CampaignSiegePressureCandidate(
        CampaignState Campaign,
        Colony Attacker,
        DefensiveStructure Target);

    private readonly record struct CampaignWarScoreKey(Faction First, Faction Second)
    {
        public static CampaignWarScoreKey From(Faction left, Faction right)
            => (int)left <= (int)right
                ? new CampaignWarScoreKey(left, right)
                : new CampaignWarScoreKey(right, left);

        public int SignFor(Faction faction)
            => faction == First ? 1 : -1;
    }

    private sealed class OrganicLaunchDiagnosticsAccumulator
    {
        private readonly Dictionary<string, int> _failureStatuses = new(StringComparer.Ordinal);
        private readonly SortedSet<int> _evaluatedFactionIds = new();

        public int EvaluationTickCount { get; private set; }
        public int OwnerEvaluationCount { get; private set; }
        public long? LastEvaluationTick { get; private set; }
        public int LastEvaluatedFactionId { get; private set; } = -1;
        public int LastEligibleMembers { get; private set; }
        public int LastAvailableWarriors { get; private set; }
        public int LastActiveCampaignCount { get; private set; }
        public int LastTargetOptionCount { get; private set; }
        public int LastWarTargetCount { get; private set; }
        public int LastHostileTargetCount { get; private set; }
        public int LastKnownTargetCount { get; private set; }
        public int LastUnknownTargetCount { get; private set; }
        public int LastMissingScoutIntelTargetCount { get; private set; }
        public bool HasLastBestCandidateScore { get; private set; }
        public float LastBestPressureScore { get; private set; }
        public float LastBestAdvantageScore { get; private set; }
        public float LastBestDistancePenalty { get; private set; }
        public float LastBestLaunchScore { get; private set; }
        public string LastDecisionKind { get; private set; } = "none";
        public string LastDecisionReasonCode { get; private set; } = "none";
        public int LaunchApplyAttempts { get; private set; }
        public int LaunchApplySuccesses { get; private set; }
        public int LaunchApplyFailures { get; private set; }
        private string? _dominantNoLaunchReason;

        public void RecordEvaluationTick(long tick)
        {
            EvaluationTickCount++;
            LastEvaluationTick = tick;
        }

        public void RecordEvaluation(
            Faction ownerFaction,
            int eligibleMembers,
            CampaignStrategyContext context,
            IReadOnlyList<CampaignTargetOption> targetOptions,
            int warTargetCount,
            int hostileTargetCount,
            CampaignStrategyDecision decision)
        {
            OwnerEvaluationCount++;
            LastEvaluatedFactionId = (int)ownerFaction;
            _evaluatedFactionIds.Add((int)ownerFaction);
            LastEligibleMembers = eligibleMembers;
            LastAvailableWarriors = context.AvailableWarriors;
            LastActiveCampaignCount = context.ActiveCampaignCount;
            LastTargetOptionCount = targetOptions.Count;
            LastWarTargetCount = warTargetCount;
            LastHostileTargetCount = hostileTargetCount;
            LastKnownTargetCount = targetOptions.Count(target => target.IsKnown);
            LastUnknownTargetCount = targetOptions.Count(target => !target.IsKnown);
            LastMissingScoutIntelTargetCount = targetOptions.Count(target => !target.IsKnown && !target.HasScoutIntel);
            LastDecisionKind = decision.Kind.ToString();
            LastDecisionReasonCode = decision.ReasonCode.ToString();
            RecordBestCandidateScore(context, targetOptions);
            PromoteDominantReason(DetermineNoLaunchReasonForCurrentEvaluation());
        }

        public void RecordLaunchApplyResult(CampaignCreationResult result)
        {
            LaunchApplyAttempts++;
            if (result.Success)
            {
                LaunchApplySuccesses++;
                PromoteDominantReason(ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonLaunchApplied);
                return;
            }

            LaunchApplyFailures++;
            var status = result.Status.ToString();
            _failureStatuses[status] = _failureStatuses.TryGetValue(status, out var count) ? count + 1 : 1;
            PromoteDominantReason(ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonLaunchApplyFailed);
        }

        public ScenarioOrganicLaunchDiagnosticsSnapshot ToSnapshot()
            => new(
                EvaluationTickCount,
                OwnerEvaluationCount,
                LastEvaluationTick,
                _evaluatedFactionIds.ToArray(),
                LastEvaluatedFactionId,
                LastEligibleMembers,
                LastAvailableWarriors,
                LastActiveCampaignCount,
                LastTargetOptionCount,
                LastWarTargetCount,
                LastHostileTargetCount,
                LastKnownTargetCount,
                LastUnknownTargetCount,
                LastMissingScoutIntelTargetCount,
                HasLastBestCandidateScore,
                LastBestPressureScore,
                LastBestAdvantageScore,
                LastBestDistancePenalty,
                LastBestLaunchScore,
                LastDecisionKind,
                LastDecisionReasonCode,
                LaunchApplyAttempts,
                LaunchApplySuccesses,
                LaunchApplyFailures,
                _failureStatuses
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new ScenarioOrganicLaunchApplyFailureStatus(pair.Key, pair.Value))
                    .ToArray(),
                _dominantNoLaunchReason ?? ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNotEvaluated);

        private void RecordBestCandidateScore(
            CampaignStrategyContext context,
            IReadOnlyList<CampaignTargetOption> targetOptions)
        {
            if (targetOptions.Count <= 0)
            {
                HasLastBestCandidateScore = false;
                LastBestPressureScore = 0f;
                LastBestAdvantageScore = 0f;
                LastBestDistancePenalty = 0f;
                LastBestLaunchScore = 0f;
                return;
            }

            var selected = targetOptions
                .Select(target => (Target: target, Score: CalculateOrganicLaunchDiagnosticScore(context, target)))
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Target.TargetColonyId)
                .ThenBy(candidate => candidate.Target.TargetFactionId)
                .First();
            HasLastBestCandidateScore = true;
            LastBestPressureScore = selected.Target.PressureScore;
            LastBestAdvantageScore = selected.Target.AdvantageScore;
            LastBestDistancePenalty = selected.Target.DistancePenalty;
            LastBestLaunchScore = selected.Score;
        }

        private static float CalculateOrganicLaunchDiagnosticScore(
            CampaignStrategyContext context,
            CampaignTargetOption target)
        {
            var score = (target.PressureScore * 0.55f)
                + (target.AdvantageScore * 0.45f)
                + (context.VisibleEnemyPressure * 0.15f)
                - target.DistancePenalty;
            return Math.Clamp(score, 0f, 1f);
        }

        private string DetermineNoLaunchReasonForCurrentEvaluation()
        {
            if (LastEligibleMembers <= 0)
                return ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNoEligibleMembers;

            if (LastAvailableWarriors <= 0)
                return ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNoAvailableWarriorsAfterHomeDefense;

            if (LastTargetOptionCount <= 0)
                return ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNoTargetOptions;

            if (LastKnownTargetCount <= 0 && LastMissingScoutIntelTargetCount > 0)
                return ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonMissingScoutIntel;

            if (LastKnownTargetCount <= 0)
                return ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNoKnownTargets;

            return ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonStrategyHoldNoViableTarget;
        }

        private void PromoteDominantReason(string reason)
        {
            if (_dominantNoLaunchReason == null || ReasonPriority(reason) > ReasonPriority(_dominantNoLaunchReason))
                _dominantNoLaunchReason = reason;
        }

        private static int ReasonPriority(string reason)
            => reason switch
            {
                ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonLaunchApplied => 100,
                ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonLaunchApplyFailed => 90,
                ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonMissingScoutIntel => 80,
                ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNoKnownTargets => 70,
                ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonStrategyHoldNoViableTarget => 60,
                ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNoTargetOptions => 50,
                ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNoAvailableWarriorsAfterHomeDefense => 40,
                ScenarioOrganicLaunchDiagnosticsSnapshot.ReasonNoEligibleMembers => 30,
                _ => 0
            };
    }

    private readonly World _world;
    private readonly double _directorDampeningFactor;
    private readonly Queue<string> _recentAiDecisions = new();
    private readonly DirectorState _directorState = new();
    private readonly ICampaignStrategist _campaignStrategist;
    private readonly List<CampaignState> _campaigns = new();
    private readonly List<SupplyConvoyState> _supplyConvoys = new();
    private readonly List<ForwardBaseState> _forwardBases = new();
    private readonly List<ScoutIntelState> _scoutIntel = new();
    private readonly List<SiegeUnitState> _siegeUnits = new();
    private readonly Dictionary<CampaignWarScoreKey, int> _campaignWarScores = new();
    private readonly CampaignLogisticsOptions _campaignLogisticsOptions = CampaignLogisticsOptions.Default.Normalized();
    private readonly CampaignLogisticsCounters _campaignLogisticsCounters = new();
    private readonly OrganicLaunchDiagnosticsAccumulator _organicLaunchDiagnostics = new();
    private DirectorExecutionState _directorExecutionState = DirectorExecutionState.NotTriggered;
    private int _lastObservedDecisionTick = -1;
    private int _nextCampaignId = 1;
    private int _nextArmyId = 1;
    private int _nextSupplyConvoyId = 1;
    private int _nextForwardBaseId = 1;
    private int _nextScoutIntelId = 1;
    private int _nextSiegeUnitId = 1;
    private AiDebugSnapshot _latestAiDebugSnapshot;
    private int _trackedNpcCursor;
    private int _trackedActorId = -1;
    private bool _manualTracking;
    public NpcPlannerMode PlannerMode { get; }
    public NpcPolicyMode PolicyMode { get; }

    public long Tick { get; private set; }
    public string LastTechActionStatus { get; private set; } = "No tech action";
    public string LastDirectorActionStatus { get; private set; } = "No director action";
    public int LoadedTechCount => TechTree.Techs.Count;

    public int Width => _world.Width;
    public int Height => _world.Height;
    public int ColonyCount => _world._colonies.Count;
    public IReadOnlyList<CampaignRuntimeSnapshot> Campaigns
        => _campaigns.Select(CampaignRuntimeSnapshot.From).ToArray();
    public IReadOnlyList<SupplyConvoyRuntimeSnapshot> SupplyConvoys
        => _supplyConvoys.Select(SupplyConvoyRuntimeSnapshot.From).ToArray();
    public IReadOnlyList<ForwardBaseRuntimeSnapshot> ForwardBases
        => _forwardBases.Select(ForwardBaseRuntimeSnapshot.From).ToArray();
    public IReadOnlyList<ScoutIntelRuntimeSnapshot> ScoutIntel
        => _scoutIntel
            .Where(intel => intel.IsActive(Tick))
            .OrderBy(intel => intel.IntelId)
            .Select(intel => ScoutIntelRuntimeSnapshot.From(intel, CurrentCompletedTick))
            .ToArray();
    public IReadOnlyList<SiegeUnitRuntimeSnapshot> SiegeUnits
        => _siegeUnits
            .OrderBy(unit => unit.SiegeUnitId)
            .Select(SiegeUnitRuntimeSnapshot.From)
            .ToArray();
    public CampaignLogisticsCounters CampaignLogisticsCounters => _campaignLogisticsCounters;

    public SimulationRuntime(int width, int height, int initialPopulation, string technologyFilePath)
        : this(width, height, initialPopulation, technologyFilePath, null)
    {
    }

    public SimulationRuntime(int width, int height, int initialPopulation, string technologyFilePath, RuntimeAiOptions? aiOptions = null)
        : this(width, height, initialPopulation, technologyFilePath, aiOptions, null)
    {
    }

    public SimulationRuntime(int width, int height, int initialPopulation, string technologyFilePath, RuntimeAiOptions? aiOptions, int? randomSeed)
        : this(width, height, initialPopulation, technologyFilePath, aiOptions, randomSeed, null)
    {
    }

    internal SimulationRuntime(
        int width,
        int height,
        int initialPopulation,
        string technologyFilePath,
        RuntimeAiOptions? aiOptions,
        int? randomSeed,
        ICampaignStrategist? campaignStrategist)
    {
        var resolvedOptions = aiOptions ?? RuntimeAiOptions.FromEnvironment();
        PlannerMode = resolvedOptions.PlannerMode;
        PolicyMode = resolvedOptions.PolicyMode;
        _campaignStrategist = campaignStrategist ?? new DefaultCampaignStrategist();
        _world = new World(width, height, initialPopulation, colony => CreateBrain(colony, resolvedOptions), randomSeed);
        _latestAiDebugSnapshot = AiDebugSnapshot.Empty(PlannerMode.ToString(), PolicyMode.ToString());
        TechTree.Load(technologyFilePath);

        _world.EnableDiplomacy = ReadBoolEnv("WORLDSIM_ENABLE_DIPLOMACY", fallback: false);
        _world.EnableCombatPrimitives = ReadBoolEnv("WORLDSIM_ENABLE_COMBAT_PRIMITIVES", fallback: false);
        _world.EnableSiege = ReadBoolEnv("WORLDSIM_ENABLE_SIEGE", fallback: true);
        _world.EnablePredatorHumanAttacks = ReadBoolEnv("WORLDSIM_ENABLE_PREDATOR_ATTACKS", fallback: false);
        _world.RequireFortificationTechUnlock = true;

        _directorDampeningFactor = ReadClampedDoubleEnv("REFINERY_DIRECTOR_DAMPENING", fallback: 1.0);

        if (LoadedTechCount == 0)
        {
            throw new InvalidOperationException("SimulationRuntime started with zero loaded technologies.");
        }
    }

    private static double ReadClampedDoubleEnv(string key, double fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
            return Math.Clamp(fallback, 0d, 1d);

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return Math.Clamp(fallback, 0d, 1d);

        return Math.Clamp(parsed, 0d, 1d);
    }

    private static bool ReadBoolEnv(string key, bool fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        if (bool.TryParse(raw, out var parsed))
            return parsed;

        raw = raw.Trim();
        if (string.Equals(raw, "1", StringComparison.Ordinal))
            return true;
        if (string.Equals(raw, "0", StringComparison.Ordinal))
            return false;

        return fallback;
    }

    public void AdvanceTick(float dt)
    {
        var blockedCampaignActorIds = CaptureCampaignBlockedActorIds();
        PruneInvalidCampaignMembers(blockedCampaignActorIds);
        EvaluateOrganicCampaignLaunches(blockedCampaignActorIds);
        QueueCampaignSiegePressureForActiveEncounters(dt, blockedCampaignActorIds);
        _world.Update(dt);
        var postWorldBlockedCampaignActorIds = CaptureCampaignBlockedActorIds();
        blockedCampaignActorIds.UnionWith(postWorldBlockedCampaignActorIds);
        SyncCampaignSiegeStates(blockedCampaignActorIds);
        ResolveCampaignEncounters();
        DeactivateInvalidSiegeUnits();
        PruneInvalidCampaignMembers(blockedCampaignActorIds);
        var marchEligibleCampaignIds = _campaigns
            .Where(campaign => campaign.Phase == CampaignPhase.Marching)
            .Select(campaign => campaign.CampaignId)
            .ToHashSet();
        AdvanceCampaignAssemblies(blockedCampaignActorIds);
        AdvanceCampaignMarches(marchEligibleCampaignIds, dt, blockedCampaignActorIds);
        AdvanceForwardBases(blockedCampaignActorIds);
        AdvanceScoutIntel();
        AdvanceSupplyConvoys();
        AdvanceCampaignEncounters();
        RecordCampaignWave9PhaseTicks();
        EvaluatePendingDirectorCausalChains();
        _directorState.Tick();
        RefreshAiDebugSnapshot();
        Tick++;
    }

    public WorldRenderSnapshot GetSnapshot()
    {
        var snapshot = WorldSnapshotBuilder.Build(_world);
        return snapshot with
        {
            Campaigns = BuildCampaignRenderData(),
            SupplyConvoys = BuildSupplyConvoyRenderData(),
            ForwardBases = BuildForwardBaseRenderData(),
            ScoutIntel = BuildScoutIntelRenderData(),
            SiegeUnits = BuildSiegeUnitRenderData(),
            Director = BuildDirectorRenderState()
        };
    }

    public void ConfigureScenarioRunnerWorldOptions(
        bool enableCombatPrimitives,
        bool enableDiplomacy,
        bool enableSiege,
        bool enablePredatorHumanAttacks,
        bool stoneBuildingsEnabled,
        float birthRateMultiplier,
        float movementSpeedMultiplier,
        float? animalReplenishmentChancePerSecond,
        float? predatorReplenishmentChance,
        float? foodRegrowthMinSeconds,
        float? foodRegrowthJitterSeconds,
        EmergencyRescuePolicy emergencyRescuePolicy)
    {
        _world.EnableCombatPrimitives = enableCombatPrimitives;
        _world.EnableDiplomacy = enableDiplomacy;
        _world.EnableSiege = enableSiege;
        _world.EnablePredatorHumanAttacks = enablePredatorHumanAttacks;
        _world.StoneBuildingsEnabled = stoneBuildingsEnabled;
        _world.BirthRateMultiplier = birthRateMultiplier;
        _world.MovementSpeedMultiplier = movementSpeedMultiplier;
        _world.EmergencyRescuePolicy = emergencyRescuePolicy;

        if (animalReplenishmentChancePerSecond.HasValue)
            _world.AnimalReplenishmentChancePerSecond = animalReplenishmentChancePerSecond.Value;
        if (predatorReplenishmentChance.HasValue)
            _world.PredatorReplenishmentChance = predatorReplenishmentChance.Value;
        if (foodRegrowthMinSeconds.HasValue)
            _world.FoodRegrowthMinSeconds = foodRegrowthMinSeconds.Value;
        if (foodRegrowthJitterSeconds.HasValue)
            _world.FoodRegrowthJitterSeconds = foodRegrowthJitterSeconds.Value;
    }

    public ScenarioRunTelemetrySnapshot BuildScenarioRunTelemetrySnapshot()
    {
        var totalFood = _world._colonies.Sum(colony => colony.Stock[Resource.Food]);
        var livingPeople = _world._people.Where(person => person.Health > 0f).ToArray();
        var totalPeople = livingPeople.Length;
        var averageFoodPerPerson = totalPeople > 0 ? totalFood / (float)totalPeople : 0f;
        var livingColonies = _world._colonies.Count(colony => livingPeople.Any(person => person.Home == colony));
        var routingPeople = livingPeople.Count(person => person.IsRouting);
        var minCombatMorale = livingPeople.Select(person => person.CombatMorale).DefaultIfEmpty(100f).Min();
        var entityCount = totalPeople
            + _world._animals.Count(animal => animal.IsAlive)
            + _world._colonies.Sum(colony => colony.HouseCount)
            + _world.DefensiveStructures.Count;

        return new ScenarioRunTelemetrySnapshot(
            LivingColonies: livingColonies,
            People: totalPeople,
            Food: totalFood,
            AverageFoodPerPerson: averageFoodPerPerson,
            DeathsOldAge: _world.TotalDeathsOldAge,
            DeathsStarvation: _world.TotalDeathsStarvation,
            DeathsPredator: _world.TotalDeathsPredator,
            DeathsOther: _world.TotalDeathsOther,
            CombatDeaths: _world.TotalCombatDeaths,
            CombatEngagements: _world.TotalCombatEngagements,
            PredatorKillsByHumans: _world.TotalPredatorKillsByHumans,
            BattleTicks: _world.TotalBattleTicks,
            ActiveBattles: _world.ActiveBattleCount,
            ActiveCombatGroups: _world.ActiveCombatGroupCount,
            RoutingPeople: routingPeople,
            MinCombatMorale: minCombatMorale,
            DeathsStarvationRecent60s: _world.RecentDeathsStarvation60s,
            DeathsStarvationWithFood: _world.TotalStarvationDeathsWithFood,
            OverlapResolveMoves: _world.TotalOverlapResolveMoves,
            CrowdDissipationMoves: _world.TotalCrowdDissipationMoves,
            BirthFallbackToOccupied: _world.TotalBirthFallbackToOccupiedCount,
            BirthFallbackToParent: _world.TotalBirthFallbackToParentCount,
            BuildSiteResets: _world.TotalBuildSiteResetCount,
            NoProgressBackoffResource: _world.TotalNoProgressBackoffResource,
            NoProgressBackoffBuild: _world.TotalNoProgressBackoffBuild,
            NoProgressBackoffFlee: _world.TotalNoProgressBackoffFlee,
            NoProgressBackoffCombat: _world.TotalNoProgressBackoffCombat,
            AiNoPlanDecisions: _world.TotalAiNoPlanDecisions,
            AiReplanBackoffDecisions: _world.TotalAiReplanBackoffDecisions,
            AiResearchTechDecisions: _world.TotalAiResearchTechDecisions,
            DenseNeighborhoodTicks: _world.DenseNeighborhoodTicks,
            LastTickDenseActors: _world.LastTickDenseActors,
            EntityCount: entityCount,
            Contact: _world.BuildScenarioContactTelemetrySnapshot(),
            Ai: _world.BuildScenarioAiTelemetrySnapshot(),
            Ecology: _world.BuildScenarioEcologyTelemetrySnapshot(),
            EcologyBalance: _world.BuildScenarioEcologyBalanceSnapshot(),
            Supply: _world.BuildScenarioSupplyTelemetrySnapshot(),
            EnablePredatorHumanAttacks: _world.EnablePredatorHumanAttacks);
    }

    private IReadOnlyList<CampaignRenderData> BuildCampaignRenderData()
        => _campaigns
            .Select(BuildCampaignRenderData)
            .ToArray();

    private IReadOnlyList<SupplyConvoyRenderData> BuildSupplyConvoyRenderData()
        => _supplyConvoys
            .OrderBy(convoy => convoy.ConvoyId)
            .Select(convoy => new SupplyConvoyRenderData(
                convoy.ConvoyId,
                (int)convoy.OwnerFaction,
                convoy.HomeColonyId,
                convoy.TargetCampaignId,
                convoy.TargetArmyId,
                MapSupplyConvoyPhaseForReadModel(convoy.Phase),
                convoy.CreatedTick,
                convoy.CompletedTick,
                convoy.CurrentX,
                convoy.CurrentY,
                convoy.TargetX,
                convoy.TargetY,
                convoy.PayloadFood,
                convoy.RouteCounters.PathRequests,
                convoy.RouteCounters.PathCacheHits,
                convoy.RouteCounters.RouteRecomputes,
                convoy.RouteCounters.ProgressTicks,
                convoy.RouteCounters.NoProgressTicks))
            .ToArray();

    private IReadOnlyList<ForwardBaseRenderData> BuildForwardBaseRenderData()
        => _forwardBases
            .OrderBy(forwardBase => forwardBase.BaseId)
            .Select(forwardBase => new ForwardBaseRenderData(
                forwardBase.BaseId,
                (int)forwardBase.OwnerFaction,
                forwardBase.HomeColonyId,
                forwardBase.CampaignId,
                forwardBase.ArmyId,
                MapForwardBasePhaseForReadModel(forwardBase.Phase),
                forwardBase.CreatedTick,
                forwardBase.EndedTick,
                forwardBase.X,
                forwardBase.Y,
                forwardBase.Radius,
                forwardBase.RestTicks,
                forwardBase.RestedActorTicks,
                forwardBase.CloseReason))
            .ToArray();

    private IReadOnlyList<ScoutIntelRenderData> BuildScoutIntelRenderData()
        => _scoutIntel
            .Where(intel => intel.IsActive(Tick))
            .OrderBy(intel => intel.IntelId)
            .Select(intel => new ScoutIntelRenderData(
                intel.IntelId,
                (int)intel.OwnerFaction,
                (int)intel.ObservedFaction,
                intel.ObservedColonyId,
                MapScoutIntelObservationKindForReadModel(intel.ObservationKind),
                intel.X,
                intel.Y,
                intel.SourceActorId,
                intel.CreatedTick,
                intel.LastRefreshTick,
                intel.ExpirationTick,
                CalculateScoutIntelTicksSinceRefresh(intel),
                intel.Confidence))
            .ToArray();

    private IReadOnlyList<SiegeUnitRenderData> BuildSiegeUnitRenderData()
        => _siegeUnits
            .OrderBy(unit => unit.SiegeUnitId)
            .Select(unit => new SiegeUnitRenderData(
                unit.SiegeUnitId,
                unit.CampaignId,
                unit.ArmyId,
                (int)unit.OwnerFaction,
                MapSiegeUnitKindForReadModel(unit.Kind),
                MapSiegeUnitPhaseForReadModel(unit.Phase),
                unit.InactiveReason,
                unit.X,
                unit.Y,
                unit.TargetStructureId,
                unit.TargetX,
                unit.TargetY,
                unit.Health,
                unit.MaxHealth,
                unit.RecentActionEffect,
                unit.LastActionTick))
            .ToArray();

    private int CalculateScoutIntelTicksSinceRefresh(ScoutIntelState intel)
        => (int)Math.Min(int.MaxValue, Math.Max(0, CurrentCompletedTick - intel.LastRefreshTick));

    private long CurrentCompletedTick => Math.Max(0, Tick - 1);

    public ScenarioWave9TelemetrySnapshot BuildScenarioWave9TelemetrySnapshot(string? wave9Scenario = null)
    {
        if (_campaigns.Count == 0)
        {
            return string.IsNullOrWhiteSpace(wave9Scenario)
                ? ScenarioWave9TelemetrySnapshot.Empty
                : (ScenarioWave9TelemetrySnapshot.Empty with { Wave9Scenario = NormalizeWave9ScenarioName(wave9Scenario) }).AsDeterministicProbe();
        }

        var latestSupply = _campaigns
            .Select(campaign => campaign.Army.CarrierState)
            .OrderByDescending(state => state.LastSupplyTick)
            .FirstOrDefault();
        var latestCampaign = _campaigns
            .OrderByDescending(campaign => campaign.CampaignId)
            .First();

        return new ScenarioWave9TelemetrySnapshot(
            Wave9Scenario: NormalizeWave9ScenarioName(wave9Scenario),
            EvidenceKind: "deterministic_probe",
            TimelineSemantics: "not_tick_sampled",
            ActiveCampaigns: _campaigns.Count(campaign => campaign.Phase != CampaignPhase.Resolved),
            ActiveArmies: _campaigns.Count(campaign => campaign.Phase != CampaignPhase.Resolved && campaign.Army.MemberCount > 0),
            TotalArmyMembers: _campaigns.Sum(campaign => campaign.Army.MemberCount),
            TotalRationPoolFood: _campaigns.Sum(campaign => campaign.Army.RationPoolState.RationPoolFood),
            CampaignLaunches: _campaigns.Count,
            AssemblyStartedCount: _campaigns.Sum(campaign => campaign.Wave9Evidence.AssemblyStartedCount),
            AssemblyCompletedCount: _campaigns.Sum(campaign => campaign.Wave9Evidence.AssemblyCompletedCount),
            MarchStartedCount: _campaigns.Sum(campaign => campaign.Wave9Evidence.MarchStartedCount),
            MarchCompletedCount: _campaigns.Sum(campaign => campaign.Wave9Evidence.MarchCompletedCount),
            CampaignsReturnedOrAborted: _campaigns.Sum(campaign => campaign.Wave9Evidence.CampaignsReturnedOrAborted),
            SupplySourceMode: latestSupply == null ? "none" : MapArmySupplySourceForReadModel(latestSupply.LastSupplySource),
            MemberInventoryConsumed: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.MemberInventoryConsumed),
            RationPoolConsumed: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.RationPoolConsumed),
            CarriedInventorySupplyTicks: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.CarriedInventorySupplyTicks),
            RationPoolSupplyTicks: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.RationPoolSupplyTicks),
            LowSupplyTicks: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.LowSupplyTicks),
            OutOfSupplyTicks: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.OutOfSupplyTicks),
            SupplyAttritionEvents: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.SupplyAttritionEvents),
            SupplyRoutingEvents: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.SupplyRoutingEvents),
            CarrierAssignments: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.CarrierAssignments),
            CarrierDeliveries: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.CarrierDeliveries),
            CarrierSupplyApplications: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.CarrierDeliveries),
            ResupplyDelivered: _campaigns.Sum(campaign => campaign.Army.Wave9Evidence.ResupplyDelivered),
            CampaignForageAttempts: _campaigns.Sum(campaign => campaign.Army.ForagingState.Attempts),
            CampaignForageSuccesses: _campaigns.Sum(campaign => campaign.Army.ForagingState.Successes),
            CampaignForageFoodGained: _campaigns.Sum(campaign => campaign.Army.ForagingState.FoodGained),
            CampaignForageCapReached: _campaigns.Sum(campaign => campaign.Army.ForagingState.LastFailureReason == ArmyForageFailureReason.ConsumerCapReached ? 1 : 0),
            CampaignPhase: MapCampaignPhaseForReadModel(latestCampaign.Phase),
            CampaignAssemblingTicks: _campaigns.Sum(campaign => campaign.Wave9Evidence.AssemblingTicks),
            CampaignMarchingTicks: _campaigns.Sum(campaign => campaign.Wave9Evidence.MarchingTicks),
            CampaignEncounterTicks: _campaigns.Sum(campaign => campaign.Wave9Evidence.EncounterTicks),
            CampaignRouteProgress: _campaigns.Sum(campaign => campaign.RouteCounters.MarchProgressTicks),
            CampaignEncounterCount: _campaigns.Sum(campaign => campaign.Wave9Evidence.EncounterCount),
            PeakMarchDistance: _campaigns.Max(campaign => campaign.RouteCounters.MarchProgressTicks));
    }

    public ScenarioWave10TelemetrySnapshot BuildScenarioWave10TelemetrySnapshot(
        string? wave10Scenario,
        string proofType,
        string evidenceStatus,
        string timelineSemantics,
        string reasonCode,
        IReadOnlyList<string>? nonClaims = null,
        string runtimeSource = ScenarioWave10Evidence.RuntimeSourceSimulationRuntimeProbe,
        long? manualLaunchAttemptTick = null,
        bool manualLaunchSucceeded = false,
        string? manualLaunchStatus = null)
    {
        var allCampaigns = _campaigns.ToArray();
        var unresolvedCampaigns = _campaigns
            .Where(campaign => campaign.Phase != CampaignPhase.Resolved)
            .ToArray();
        var resolvedCampaigns = _campaigns
            .Where(campaign => campaign.Resolution.IsResolved)
            .ToArray();
        var activeScoutIntel = _scoutIntel
            .Where(intel => intel.IsActive(Tick))
            .ToArray();
        var freshScoutIntel = activeScoutIntel
            .Count(intel => CalculateScoutIntelTicksSinceRefresh(intel) <= GetOrganicCampaignScoutIntelFreshnessThresholdTicks());
        var activeSiegeUnits = _siegeUnits.Count(unit => unit.IsActive);
        var inactiveSiegeUnits = _siegeUnits.Count - activeSiegeUnits;
        var manualDownstreamDiagnostics = BuildManualDownstreamDiagnosticsSnapshot(unresolvedCampaigns, freshScoutIntel);

        return new ScenarioWave10TelemetrySnapshot(
            Wave10Scenario: string.IsNullOrWhiteSpace(wave10Scenario) ? "none" : wave10Scenario.Trim(),
            RuntimeSource: string.IsNullOrWhiteSpace(runtimeSource) ? ScenarioWave10Evidence.RuntimeSourceSimulationRuntimeProbe : runtimeSource.Trim(),
            ProofType: string.IsNullOrWhiteSpace(proofType) ? ScenarioWave10Evidence.ProofTypeNotConfigured : proofType.Trim(),
            EvidenceStatus: string.IsNullOrWhiteSpace(evidenceStatus) ? ScenarioWave10Evidence.EvidenceStatusNotApplicable : evidenceStatus.Trim(),
            TimelineSemantics: string.IsNullOrWhiteSpace(timelineSemantics) ? ScenarioWave10Evidence.TimelineSemanticsNotSampled : timelineSemantics.Trim(),
            ReasonCode: string.IsNullOrWhiteSpace(reasonCode) ? ScenarioWave10Evidence.ReasonNone : reasonCode.Trim(),
            NonClaims: nonClaims?.Where(claim => !string.IsNullOrWhiteSpace(claim)).Select(claim => claim.Trim()).ToArray() ?? Array.Empty<string>(),
            CampaignLaunches: _campaigns.Count,
            ActiveCampaigns: unresolvedCampaigns.Length,
            ResolvedCampaigns: resolvedCampaigns.Length,
            AttackerVictories: resolvedCampaigns.Count(campaign => campaign.Resolution.Kind == CampaignResolutionKind.AttackerVictory),
            DefenderHeld: resolvedCampaigns.Count(campaign => campaign.Resolution.Kind == CampaignResolutionKind.DefenderHeld),
            CampaignSiegesEntered: _campaigns.Sum(campaign => campaign.Siege.SiegesEntered),
            CampaignBreaches: _campaigns.Sum(campaign => campaign.Siege.BreachesObserved),
            SiegePressureTicks: _campaigns.Sum(campaign => campaign.Siege.SiegePressureTicks),
            LootFood: resolvedCampaigns.Sum(campaign => campaign.Resolution.LootFood),
            LootWood: resolvedCampaigns.Sum(campaign => campaign.Resolution.LootWood),
            LootStone: resolvedCampaigns.Sum(campaign => campaign.Resolution.LootStone),
            LootGold: resolvedCampaigns.Sum(campaign => campaign.Resolution.LootGold),
            WarScoreDeltaTotal: resolvedCampaigns.Sum(campaign => campaign.Resolution.WarScoreDelta),
            PeaceAppliedCount: resolvedCampaigns.Count(campaign => campaign.Resolution.PeaceApplied),
            ActiveSupplyConvoys: _supplyConvoys.Count(convoy => convoy.Phase is SupplyConvoyPhase.Pending or SupplyConvoyPhase.Marching),
            ConvoysSpawned: _campaignLogisticsCounters.ConvoysSpawned,
            ConvoysDelivered: _campaignLogisticsCounters.ConvoysDelivered,
            ConvoysFailed: _campaignLogisticsCounters.ConvoysFailed,
            ConvoyThrottleBlocks: _campaignLogisticsCounters.ConvoySpawnBlockedByThrottle,
            ConvoyCapBlocks: _campaignLogisticsCounters.ConvoySpawnBlockedByCap,
            ConvoyHomeDefenseBlocks: _campaignLogisticsCounters.ConvoySpawnBlockedByHomeDefense,
            ConvoyRouteBudgetExhausted: _campaignLogisticsCounters.ConvoySpawnRouteBudgetExhausted + _campaignLogisticsCounters.ConvoyRouteBudgetExhausted,
            ActiveForwardBases: _forwardBases.Count(forwardBase => forwardBase.Phase == ForwardBasePhase.Active),
            ForwardBasesEstablished: _campaignLogisticsCounters.ForwardBasesEstablished,
            ForwardBasesExpired: _campaignLogisticsCounters.ForwardBasesExpired,
            ForwardBasesAbandoned: _campaignLogisticsCounters.ForwardBasesAbandoned,
            ForwardBaseRestTicks: _campaignLogisticsCounters.ForwardBaseRestTicks,
            ScoutIntelObserved: _campaignLogisticsCounters.ScoutIntelObserved,
            ScoutIntelRefreshed: _campaignLogisticsCounters.ScoutIntelRefreshed,
            ScoutIntelExpired: _campaignLogisticsCounters.ScoutIntelExpired,
            ActiveScoutIntel: activeScoutIntel.Length,
            FreshScoutIntel: freshScoutIntel,
            CampaignTargetsWithScoutIntel: CountCampaignTargetsWithScoutIntel(unresolvedCampaigns),
            SiegeUnitsSpawned: _siegeUnits.Count,
            ActiveSiegeUnits: activeSiegeUnits,
            InactiveSiegeUnits: inactiveSiegeUnits,
            SiegeUnitActionTicks: _siegeUnits.Count(unit => unit.LastActionTick >= 0),
            MaxActiveCampaignsForAnyFaction: MaxUnresolvedCampaignsByOwner(unresolvedCampaigns),
            MaxUnresolvedPairsForAnyFactionPair: MaxUnresolvedCampaignsByUnorderedPair(unresolvedCampaigns),
            CampaignLaunchBlockedByCap: _campaignLogisticsCounters.CampaignLaunchBlockedByCap,
            CampaignLaunchBlockedByPairCap: _campaignLogisticsCounters.CampaignLaunchBlockedByPairCap,
            CampaignLaunchBlockedByHomeDefense: _campaignLogisticsCounters.CampaignLaunchBlockedByHomeDefense,
            CampaignLaunchRouteBudgetExhausted: _campaignLogisticsCounters.CampaignLaunchRouteBudgetExhausted,
            WarScorePressureSignals: _campaignWarScores.Values.Count(score => Math.Abs(score) >= OrganicCampaignWarScorePressureThreshold),
            HomeGarrisonViolationCount: CountHomeGarrisonViolations(unresolvedCampaigns),
            FirstCampaignLaunchTick: MinNonNegative(allCampaigns.Select(campaign => campaign.CreatedTick)),
            FirstAssemblyTick: MinNonNegative(allCampaigns.Select(campaign => campaign.Army.AssemblyStartedTick)),
            FirstMarchTick: MinNonNegative(allCampaigns.Select(campaign => campaign.Army.AssemblyCompletedTick)),
            FirstEncounterTick: MinNonNegative(allCampaigns.Select(campaign => campaign.EncounterStartedTick)),
            FirstSiegeTick: MinNonNegative(allCampaigns.Select(campaign => campaign.Siege.SiegeEnteredTick)),
            FirstResolutionTick: MinNonNegative(resolvedCampaigns.Select(campaign => campaign.ResolvedTick)),
            LongestUnresolvedCampaignAgeTicks: unresolvedCampaigns.Length == 0 ? 0 : unresolvedCampaigns.Max(campaign => Math.Max(0, Tick - campaign.CreatedTick)),
            ManualLaunchAttemptTick: manualLaunchAttemptTick,
            ManualLaunchAttempted: manualLaunchAttemptTick.HasValue,
            ManualLaunchSucceeded: manualLaunchSucceeded,
            ManualLaunchStatus: string.IsNullOrWhiteSpace(manualLaunchStatus) ? "not_attempted" : manualLaunchStatus.Trim(),
            OrganicLaunchDiagnostics: _organicLaunchDiagnostics.ToSnapshot(),
            ManualDownstreamDiagnostics: manualDownstreamDiagnostics);
    }

    private ScenarioManualDownstreamDiagnosticsSnapshot BuildManualDownstreamDiagnosticsSnapshot(
        IReadOnlyCollection<CampaignState> unresolvedCampaigns,
        int freshScoutIntel)
        => new(
            BuildManualDownstreamConvoyDiagnosticsSnapshot(unresolvedCampaigns),
            BuildManualDownstreamScoutDiagnosticsSnapshot(freshScoutIntel),
            BuildManualDownstreamSiegeUnitDiagnosticsSnapshot());

    private ScenarioManualDownstreamConvoyDiagnosticsSnapshot BuildManualDownstreamConvoyDiagnosticsSnapshot(
        IReadOnlyCollection<CampaignState> unresolvedCampaigns)
    {
        return new ScenarioManualDownstreamConvoyDiagnosticsSnapshot(
            Evaluated: _campaignLogisticsCounters.ConvoyEvaluations > 0 ? _campaignLogisticsCounters.ConvoyEvaluations : unresolvedCampaigns.Count,
            Eligible: _campaignLogisticsCounters.ConvoyEligibleCampaigns,
            Requested: _campaignLogisticsCounters.ConvoyRequests,
            BlockedReason: ResolveManualDownstreamConvoyBlockedReason(),
            Spawned: _campaignLogisticsCounters.ConvoysSpawned,
            Delivered: _campaignLogisticsCounters.ConvoysDelivered,
            Failed: _campaignLogisticsCounters.ConvoysFailed);
    }

    private string ResolveManualDownstreamConvoyBlockedReason()
    {
        if (_campaignLogisticsCounters.ConvoysSpawned > 0)
            return "spawned";
        if (_campaignLogisticsCounters.ConvoySpawnRouteBudgetExhausted + _campaignLogisticsCounters.ConvoyRouteBudgetExhausted > 0)
            return "route_budget";
        if (_campaignLogisticsCounters.ConvoySpawnBlockedByHomeDefense > 0)
            return "home_defense";
        if (_campaignLogisticsCounters.ConvoySpawnBlockedByThrottle > 0)
            return "throttle";
        if (_campaignLogisticsCounters.ConvoySpawnBlockedByCap > 0)
            return "cap";
        if (_campaignLogisticsCounters.ConvoyRequests > 0)
            return "requested_no_spawn";
        return "none";
    }

    private ScenarioManualDownstreamScoutDiagnosticsSnapshot BuildManualDownstreamScoutDiagnosticsSnapshot(int freshScoutIntel)
        => new(
            ObservationPasses: _campaignLogisticsCounters.ScoutObservationPasses,
            LiveScoutActors: CountLiveScoutActors(),
            SkippedByRelation: _campaignLogisticsCounters.ScoutObservationSkippedByRelation,
            SkippedByRadius: _campaignLogisticsCounters.ScoutObservationSkippedByRadius,
            NearestHostileDistance: CalculateNearestScoutHostileDistance(),
            FreshIntel: freshScoutIntel);

    private int CountLiveScoutActors()
        => _world._people.Count(IsLiveScout);

    private int CalculateNearestScoutHostileDistance()
    {
        var scouts = _world._people.Where(IsLiveScout).ToArray();
        if (scouts.Length == 0)
            return -1;

        var nearest = int.MaxValue;
        foreach (var scout in scouts)
        {
            var ownerFaction = scout.Home.Faction;
            foreach (var target in _world._colonies)
            {
                if (target.Faction == ownerFaction || !IsScoutIntelTargetRelation(ownerFaction, target.Faction))
                    continue;
                nearest = Math.Min(nearest, ManhattanDistance(scout.Pos, target.Origin));
            }
        }

        return nearest == int.MaxValue ? -1 : nearest;
    }

    private ScenarioManualDownstreamSiegeUnitDiagnosticsSnapshot BuildManualDownstreamSiegeUnitDiagnosticsSnapshot()
        => new(
            EncounterCampaigns: _campaignLogisticsCounters.SiegeUnitEncounterCampaigns,
            TechLocked: _campaignLogisticsCounters.SiegeUnitTechLocked,
            ResolverDisabled: _campaignLogisticsCounters.SiegeUnitResolverDisabled,
            NoTarget: _campaignLogisticsCounters.SiegeUnitNoTarget,
            AlreadyPresent: _campaignLogisticsCounters.SiegeUnitAlreadyPresent,
            Spawned: _campaignLogisticsCounters.SiegeUnitsSpawned,
            ActionTicks: _campaignLogisticsCounters.SiegeUnitActionTicks);

    private static long? MinNonNegative(IEnumerable<long> values)
    {
        var candidates = values.Where(value => value >= 0).ToArray();
        return candidates.Length == 0 ? null : candidates.Min();
    }

    private int CountCampaignTargetsWithScoutIntel(IEnumerable<CampaignState> campaigns)
        => campaigns.Count(campaign =>
        {
            var targetColony = _world._colonies.FirstOrDefault(colony => colony.Id == campaign.TargetColonyId);
            return targetColony != null && HasActionableScoutIntelForOrganicTarget(campaign.OwnerFaction, targetColony);
        });

    private static int MaxUnresolvedCampaignsByOwner(IReadOnlyCollection<CampaignState> campaigns)
        => campaigns.Count == 0
            ? 0
            : campaigns.GroupBy(campaign => campaign.OwnerFaction).Max(group => group.Count());

    private static int MaxUnresolvedCampaignsByUnorderedPair(IReadOnlyCollection<CampaignState> campaigns)
        => campaigns.Count == 0
            ? 0
            : campaigns.GroupBy(campaign => (Math.Min((int)campaign.OwnerFaction, (int)campaign.TargetFaction), Math.Max((int)campaign.OwnerFaction, (int)campaign.TargetFaction))).Max(group => group.Count());

    private int CountHomeGarrisonViolations(IEnumerable<CampaignState> campaigns)
        => campaigns
            .Select(campaign => _world._colonies.FirstOrDefault(colony => colony.Id == campaign.OriginColonyId))
            .Where(colony => colony != null)
            .Select(colony => colony!)
            .Distinct()
            .Count(colony => _world._people.Count(person =>
                person.Health > 0f
                && ReferenceEquals(person.Home, colony)
                && person.HasRole(PersonRole.Warrior)) < _campaignLogisticsOptions.MinimumHomeDefenseWarriors);

    private CampaignRenderData BuildCampaignRenderData(CampaignState campaign)
    {
        bool hasObjective = TryResolveCampaignMarchObjective(campaign, out var objective);
        var routeCache = campaign.RouteCache;
        var waypoints = routeCache.Steps
            .Select((step, index) => new CampaignRouteWaypointRenderData(
                index,
                step.x,
                step.y,
                index == routeCache.NextIndex))
            .ToArray();
        var memberActorIds = campaign.Army.MemberActorIds.ToArray();
        var anchor = memberActorIds
            .Select(actorId => _world._people.FirstOrDefault(person => person.Id == actorId))
            .Where(person => person != null)
            .Select(person => person!)
            .OrderBy(person => person.Id)
            .FirstOrDefault();
        var source = anchor?.Pos ?? (x: campaign.RouteIntent.OriginX, y: campaign.RouteIntent.OriginY);
        var target = hasObjective
            ? objective.MovementTarget
            : (x: campaign.RouteIntent.TargetX, y: campaign.RouteIntent.TargetY);
        var encounters = campaign.Phase == CampaignPhase.Encounter || campaign.Resolution.IsResolved
            ? new[]
            {
                new CampaignEncounterRenderData(
                    campaign.CampaignId,
                    source.x,
                    source.y,
                    target.x,
                    target.y,
                    campaign.Resolution.IsResolved ? "resolved" : "active",
                    MapCampaignEncounterOutcomeForReadModel(campaign),
                    campaign.RouteCounters.EncounterTicks)
            }
            : Array.Empty<CampaignEncounterRenderData>();

        return new CampaignRenderData(
            campaign.CampaignId,
            campaign.ArmyId,
            (int)campaign.OwnerFaction,
            (int)campaign.TargetFaction,
            campaign.OriginColonyId,
            campaign.TargetColonyId,
            MapCampaignPhaseForReadModel(campaign.Phase),
            MapCampaignStatusForReadModel(campaign),
            campaign.CreatedTick,
            new CampaignRouteRenderData(
                campaign.RouteIntent.OriginX,
                campaign.RouteIntent.OriginY,
                campaign.RouteIntent.TargetX,
                campaign.RouteIntent.TargetY,
                hasObjective,
                hasObjective ? objective.MovementTarget.x : -1,
                hasObjective ? objective.MovementTarget.y : -1,
                hasObjective && objective.UsesFallback,
                campaign.RouteCounters.PathRequests,
                campaign.RouteCounters.PathCacheHits,
                campaign.RouteCounters.BlockedMovementChecks,
                campaign.RouteCounters.RouteRecomputes,
                campaign.RouteCounters.MarchProgressTicks,
                campaign.RouteCounters.EncounterTicks,
                campaign.RouteCounters.NoProgressTicks,
                waypoints.Length,
                routeCache.HasPath ? routeCache.NextIndex : -1),
            new ArmyRenderData(
                campaign.Army.ArmyId,
                campaign.Army.HomeColonyId,
                campaign.Army.OriginX,
                campaign.Army.OriginY,
                campaign.Army.TargetX,
                campaign.Army.TargetY,
                campaign.Army.RequestedMemberCount,
                campaign.Army.MemberCount,
                memberActorIds,
                campaign.Army.HasRallyPoint,
                campaign.Army.RallyX,
                campaign.Army.RallyY,
                campaign.Army.IsAssembled,
                campaign.Army.AssemblyStartedTick,
                campaign.Army.AssemblyCompletedTick,
                anchor?.Id ?? -1,
                anchor?.Pos.x ?? -1,
                anchor?.Pos.y ?? -1),
            new ArmySupplyRenderData(
                campaign.Army.SupplyState.FractionalFoodDemand,
                campaign.Army.SupplyState.SustainedOutOfSupplyTicks,
                campaign.Army.RationPoolState.RationPoolFood,
                campaign.Army.CarrierState.AssignedCarrierActorId,
                campaign.Army.CarrierState.HasAssignedCarrier,
                campaign.Army.CarrierState.LastSupplyTick,
                MapArmySupplySourceForReadModel(campaign.Army.CarrierState.LastSupplySource),
                campaign.Army.ForagingState.Attempts,
                campaign.Army.ForagingState.Successes,
                campaign.Army.ForagingState.Failures,
                campaign.Army.ForagingState.FoodGained,
                campaign.Army.ForagingState.LastSourceX,
                campaign.Army.ForagingState.LastSourceY,
                campaign.Army.ForagingState.LastConsumerKey,
                MapArmyForageStatusForReadModel(campaign.Army.ForagingState.LastStatus),
                MapArmyForageFailureReasonForReadModel(campaign.Army.ForagingState.LastFailureReason)),
            waypoints,
            BuildCampaignResolutionRenderData(campaign.Resolution),
            encounters);
    }

    private static CampaignResolutionRenderData BuildCampaignResolutionRenderData(CampaignResolutionState resolution)
    {
        if (!resolution.IsResolved)
            return CampaignResolutionRenderData.Empty;

        return new CampaignResolutionRenderData(
            resolution.IsResolved,
            MapCampaignResolutionKindForReadModel(resolution.Kind),
            resolution.Reason,
            resolution.ResolvedTick,
            resolution.TargetStructureId,
            resolution.LootFood,
            resolution.LootWood,
            resolution.LootStone,
            resolution.LootGold,
            resolution.WarScoreDelta,
            resolution.CumulativeWarScore,
            resolution.PeaceEligible,
            resolution.PeaceApplied,
            resolution.TreatyKind);
    }

    private static string MapCampaignResolutionKindForReadModel(CampaignResolutionKind kind)
        => kind switch
        {
            CampaignResolutionKind.AttackerVictory => "attacker_victory",
            CampaignResolutionKind.DefenderHeld => "defender_held",
            _ => "none"
        };

    private static string MapCampaignPhaseForReadModel(CampaignPhase phase)
        => phase switch
        {
            CampaignPhase.AssemblingPending => "assembling_pending",
            CampaignPhase.Assembling => "assembling",
            CampaignPhase.Marching => "marching",
            CampaignPhase.Encounter => "encounter",
            CampaignPhase.Resolved => "resolved",
            _ => "unknown"
        };

    private static string MapCampaignStatusForReadModel(CampaignState campaign)
        => campaign.Phase switch
        {
            CampaignPhase.AssemblingPending => "pending_assembly",
            CampaignPhase.Assembling => "assembling",
            CampaignPhase.Marching => "marching",
            CampaignPhase.Encounter => "encounter_active",
            CampaignPhase.Resolved => "resolved",
            _ => "unknown"
        };

    private static string MapCampaignEncounterOutcomeForReadModel(CampaignState campaign)
        => campaign.Siege.Status switch
        {
            CampaignSiegeStatus.SeekingTarget => "siege_seeking_target",
            CampaignSiegeStatus.Active => "siege_active",
            CampaignSiegeStatus.Breached => "siege_breached",
            CampaignSiegeStatus.NoTarget => "no_siege_target",
            _ => "non_resolving"
        };

    private static string MapArmySupplySourceForReadModel(ArmySupplySourceMode source)
        => source switch
        {
            ArmySupplySourceMode.None => "none",
            ArmySupplySourceMode.CarriedInventory => "carried_inventory",
            ArmySupplySourceMode.RationPool => "ration_pool",
            _ => "unknown"
        };

    private static string MapArmyForageStatusForReadModel(ArmyForageStatus status)
        => status switch
        {
            ArmyForageStatus.Succeeded => "succeeded",
            ArmyForageStatus.Failed => "failed",
            _ => "unknown"
        };

    private static string MapArmyForageFailureReasonForReadModel(ArmyForageFailureReason reason)
        => reason switch
        {
            ArmyForageFailureReason.None => "none",
            ArmyForageFailureReason.InvalidConsumerKey => "invalid_consumer_key",
            ArmyForageFailureReason.ForagerDead => "forager_dead",
            ArmyForageFailureReason.SourceOutOfBounds => "source_out_of_bounds",
            ArmyForageFailureReason.SourceOutOfRange => "source_out_of_range",
            ArmyForageFailureReason.WaterTile => "water_tile",
            ArmyForageFailureReason.NoResourceNode => "no_resource_node",
            ArmyForageFailureReason.WrongResource => "wrong_resource",
            ArmyForageFailureReason.DepletedFood => "depleted_food",
            ArmyForageFailureReason.ConsumerCapReached => "consumer_cap_reached",
            ArmyForageFailureReason.NoYield => "no_yield",
            ArmyForageFailureReason.HarvestFailed => "harvest_failed",
            _ => "unknown"
        };

    private static string MapSupplyConvoyPhaseForReadModel(SupplyConvoyPhase phase)
        => phase switch
        {
            SupplyConvoyPhase.Pending => "pending",
            SupplyConvoyPhase.Marching => "marching",
            SupplyConvoyPhase.Delivered => "delivered",
            SupplyConvoyPhase.Failed => "failed",
            _ => "unknown"
        };

    private static string MapForwardBasePhaseForReadModel(ForwardBasePhase phase)
        => phase switch
        {
            ForwardBasePhase.Active => "active",
            ForwardBasePhase.Expired => "expired",
            ForwardBasePhase.Abandoned => "abandoned",
            _ => "unknown"
        };

    private static string MapScoutIntelObservationKindForReadModel(ScoutIntelObservationKind kind)
        => kind switch
        {
            ScoutIntelObservationKind.Colony => "colony",
            _ => "unknown"
        };

    private static string MapSiegeUnitKindForReadModel(SiegeUnitKind kind)
        => kind switch
        {
            SiegeUnitKind.Ram => "ram",
            SiegeUnitKind.SiegeTower => "siege_tower",
            SiegeUnitKind.MobileCatapult => "mobile_catapult",
            _ => "unknown"
        };

    private static string MapSiegeUnitPhaseForReadModel(SiegeUnitPhase phase)
        => phase switch
        {
            SiegeUnitPhase.Active => "active",
            SiegeUnitPhase.Inactive => "inactive",
            _ => "unknown"
        };

    private static string NormalizeWave9ScenarioName(string? wave9Scenario)
        => string.IsNullOrWhiteSpace(wave9Scenario) ? "none" : wave9Scenario.Trim();

    public CampaignCreationResult TryCreateCampaign(Faction ownerFaction, Faction targetFaction, int requestedMemberCount)
    {
        var validation = ValidateCampaignCreation(ownerFaction, targetFaction, requestedMemberCount);
        if (!validation.Success)
            return validation.Result;

        return CreateCampaignFromResolvedColonies(validation.OwnerColony!, validation.TargetColony!, requestedMemberCount);
    }

    private CampaignCreationResult CreateCampaignFromResolvedColonies(Colony ownerColony, Colony targetColony, int requestedMemberCount)
    {
        var campaignId = _nextCampaignId++;
        var armyId = _nextArmyId++;
        _campaigns.Add(CreateCampaignState(ownerColony, targetColony, requestedMemberCount, campaignId, armyId, Tick));

        return CampaignCreationResult.Created(campaignId, armyId);
    }

    public CampaignCreationResult TryCreateManualCampaign(ManualCampaignLaunchCommand command)
    {
        var commandValidation = ValidateCampaignCommandConfiguration(command.OwnerFaction, command.TargetFaction, command.RequestedMemberCount);
        if (!commandValidation.Success)
            return commandValidation.Result;

        var blockedCampaignActorIds = CaptureCampaignBlockedActorIds();

        if (command.AllowFallback)
            return TryCreateDefaultOperatorSmokeCampaign(command.RequestedMemberCount, blockedCampaignActorIds);

        var validation = ValidateCampaignCreation(command.OwnerFaction, command.TargetFaction, command.RequestedMemberCount);
        if (!validation.Success)
            return validation.Result;

        return TryCreateManualCampaignForPair(
            validation.OwnerColony!,
            validation.TargetColony!,
            command.RequestedMemberCount,
            blockedCampaignActorIds,
            allowRoleFallback: false,
            out _,
            out var explicitResult)
            ? explicitResult
            : explicitResult;
    }

    private CampaignCreationResult TryCreateDefaultOperatorSmokeCampaign(int requestedMemberCount, HashSet<int> blockedCampaignActorIds)
    {
        var diagnostics = new List<string>();
        foreach (var (ownerColony, targetColony) in EnumerateDefaultOperatorSmokePairs())
        {
            if (TryCreateManualCampaignForPair(
                    ownerColony,
                    targetColony,
                    requestedMemberCount,
                    blockedCampaignActorIds,
                    allowRoleFallback: true,
                    out var successDiagnostic,
                    out var successResult))
            {
                return successResult;
            }

            diagnostics.Add(successDiagnostic);
        }

        if (diagnostics.Count == 0)
        {
            return CampaignCreationResult.Failed(
                CampaignCreationStatus.CampaignRuntimeUnavailable,
                "Manual smoke launch could not find any distinct faction colony pair.");
        }

        return CampaignCreationResult.Failed(
            CampaignCreationStatus.CampaignRuntimeUnavailable,
            $"Manual smoke launch could not find a viable campaign pair. Tried: {string.Join("; ", diagnostics.Take(4))}");
    }

    private IEnumerable<(Colony OwnerColony, Colony TargetColony)> EnumerateDefaultOperatorSmokePairs()
    {
        var seenPairs = new HashSet<(int OwnerColonyId, int TargetColonyId)>();

        if (TryGetColonyByFaction(ManualCampaignLaunchCommand.DefaultOperatorSmoke.OwnerFaction, out var defaultOwner)
            && TryGetColonyByFaction(ManualCampaignLaunchCommand.DefaultOperatorSmoke.TargetFaction, out var defaultTarget)
            && defaultOwner.Id != defaultTarget.Id)
        {
            seenPairs.Add((defaultOwner.Id, defaultTarget.Id));
            yield return (defaultOwner, defaultTarget);
        }

        var orderedColonies = _world._colonies
            .OrderBy(colony => (int)colony.Faction)
            .ThenBy(colony => colony.Id)
            .ToArray();

        foreach (var ownerColony in orderedColonies)
        {
            foreach (var targetColony in orderedColonies)
            {
                if (ownerColony.Faction == targetColony.Faction)
                    continue;

                if (!seenPairs.Add((ownerColony.Id, targetColony.Id)))
                    continue;

                yield return (ownerColony, targetColony);
            }
        }
    }

    private bool TryCreateManualCampaignForPair(
        Colony ownerColony,
        Colony targetColony,
        int requestedMemberCount,
        HashSet<int> blockedCampaignActorIds,
        bool allowRoleFallback,
        out string diagnostic,
        out CampaignCreationResult result)
    {
        var preflightCampaign = CreateCampaignState(ownerColony, targetColony, requestedMemberCount, campaignId: 0, armyId: 0, createdTick: Tick);

        if (!TryFindCampaignRallyPoint(preflightCampaign, out var rallyPoint))
        {
            diagnostic = $"{ownerColony.Faction}->{targetColony.Faction} rally unresolved origin=({ownerColony.Origin.x},{ownerColony.Origin.y})";
            result = CampaignCreationResult.Failed(
                CampaignCreationStatus.CampaignRuntimeUnavailable,
                $"Manual smoke launch rally unresolved for {ownerColony.Faction}->{targetColony.Faction} from origin ({ownerColony.Origin.x},{ownerColony.Origin.y}).");
            return false;
        }

        if (!TryResolveCampaignMarchObjective(preflightCampaign, out var objective))
        {
            diagnostic = $"{ownerColony.Faction}->{targetColony.Faction} objective unresolved target=({targetColony.Origin.x},{targetColony.Origin.y})";
            result = CampaignCreationResult.Failed(
                CampaignCreationStatus.CampaignRuntimeUnavailable,
                $"Manual smoke launch objective unresolved for {ownerColony.Faction}->{targetColony.Faction} toward target ({targetColony.Origin.x},{targetColony.Origin.y}).");
            return false;
        }

        if (!HasCampaignPathPreflight(preflightCampaign, rallyPoint, objective.MovementTarget, GetOperatorSmokePathMaxExpansions()))
        {
            diagnostic = $"{ownerColony.Faction}->{targetColony.Faction} route/path failed start=({rallyPoint.x},{rallyPoint.y}) objective=({objective.MovementTarget.x},{objective.MovementTarget.y})";
            result = CampaignCreationResult.Failed(
                CampaignCreationStatus.CampaignRuntimeUnavailable,
                $"Manual smoke launch route/path preflight failed for {ownerColony.Faction}->{targetColony.Faction} from ({rallyPoint.x},{rallyPoint.y}) to ({objective.MovementTarget.x},{objective.MovementTarget.y}).");
            return false;
        }

        var candidate = FindNextCampaignAssemblyMember(preflightCampaign, blockedCampaignActorIds, allowRoleFallback);
        if (candidate == null)
        {
            diagnostic = $"{ownerColony.Faction}->{targetColony.Faction} no eligible member ownerColony={ownerColony.Id}";
            result = CampaignCreationResult.Failed(
                CampaignCreationStatus.CampaignRuntimeUnavailable,
                $"Manual smoke launch requires at least one eligible member for {ownerColony.Faction}->{targetColony.Faction} (owner colony {ownerColony.Id}).");
            return false;
        }

        var createdResult = CreateCampaignFromResolvedColonies(ownerColony, targetColony, requestedMemberCount);
        var campaign = _campaigns.First(candidateCampaign => candidateCampaign.CampaignId == createdResult.CampaignId && candidateCampaign.ArmyId == createdResult.ArmyId);
        PrimeManualCampaignForSmoke(campaign, candidate, blockedCampaignActorIds, rallyPoint, objective);
        result = createdResult;
        diagnostic = $"{ownerColony.Faction}->{targetColony.Faction} created";
        return true;
    }

    private bool TryGetColonyByFaction(Faction faction, out Colony colony)
    {
        colony = _world._colonies.FirstOrDefault(candidate => candidate.Faction == faction)!;
        return colony != null;
    }

    private (bool Success, CampaignCreationResult Result, Colony? OwnerColony, Colony? TargetColony) ValidateCampaignCreation(
        Faction ownerFaction,
        Faction targetFaction,
        int requestedMemberCount)
    {
        var validation = ValidateCampaignCommandConfiguration(ownerFaction, targetFaction, requestedMemberCount);
        if (!validation.Success)
            return (false, validation.Result, null, null);

        var ownerColony = _world._colonies.FirstOrDefault(colony => colony.Faction == ownerFaction);
        if (ownerColony == null)
        {
            return (false,
                CampaignCreationResult.Failed(
                    CampaignCreationStatus.OwnerColonyNotFound,
                    $"No owner colony found for faction {ownerFaction}."),
                null,
                null);
        }

        var targetColony = _world._colonies.FirstOrDefault(colony => colony.Faction == targetFaction);
        if (targetColony == null)
        {
            return (false,
                CampaignCreationResult.Failed(
                    CampaignCreationStatus.TargetColonyNotFound,
                    $"No target colony found for faction {targetFaction}."),
                ownerColony,
                null);
        }

        return (true, CampaignCreationResult.Created(0, 0), ownerColony, targetColony);
    }

    private (bool Success, CampaignCreationResult Result) ValidateCampaignCommandConfiguration(
        Faction ownerFaction,
        Faction targetFaction,
        int requestedMemberCount)
    {
        if (!IsCampaignRuntimeAvailable())
        {
            return (false,
                CampaignCreationResult.Failed(
                    CampaignCreationStatus.CampaignRuntimeUnavailable,
                    "Campaign creation requires WORLDSIM_ENABLE_DIPLOMACY=true and WORLDSIM_ENABLE_COMBAT_PRIMITIVES=true."));
        }

        if (!Enum.IsDefined(typeof(Faction), ownerFaction))
        {
            return (false,
                CampaignCreationResult.Failed(
                    CampaignCreationStatus.InvalidOwnerFaction,
                    $"Invalid owner faction value: {(int)ownerFaction}."));
        }

        if (!Enum.IsDefined(typeof(Faction), targetFaction))
        {
            return (false,
                CampaignCreationResult.Failed(
                    CampaignCreationStatus.InvalidTargetFaction,
                    $"Invalid target faction value: {(int)targetFaction}."));
        }

        if (ownerFaction == targetFaction)
        {
            return (false,
                CampaignCreationResult.Failed(
                    CampaignCreationStatus.SameFaction,
                    "Campaign creation requires ownerFaction != targetFaction."));
        }

        if (requestedMemberCount <= 0)
        {
            return (false,
                CampaignCreationResult.Failed(
                    CampaignCreationStatus.InvalidRequestedMemberCount,
                    "Campaign creation requires requestedMemberCount > 0."));
        }

        return (true, CampaignCreationResult.Created(0, 0));
    }

    private CampaignState CreateCampaignState(
        Colony ownerColony,
        Colony targetColony,
        int requestedMemberCount,
        int campaignId,
        int armyId,
        long createdTick)
    {
        var routeIntent = new CampaignRouteIntent(
            ownerColony.Id,
            targetColony.Id,
            ownerColony.Origin.x,
            ownerColony.Origin.y,
            targetColony.Origin.x,
            targetColony.Origin.y);
        var army = new ArmyState(
            armyId,
            ownerColony.Faction,
            ownerColony.Id,
            ownerColony.Origin.x,
            ownerColony.Origin.y,
            targetColony.Origin.x,
            targetColony.Origin.y,
            requestedMemberCount);
        return new CampaignState(
            campaignId,
            ownerColony.Faction,
            targetColony.Faction,
            ownerColony.Id,
            targetColony.Id,
            createdTick,
            routeIntent,
            army);
    }

    private void PrimeManualCampaignForSmoke(
        CampaignState campaign,
        Person candidate,
        HashSet<int> blockedCampaignActorIds,
        (int x, int y) rallyPoint,
        CampaignMarchObjective objective)
    {
        campaign.Army.SetRallyPoint(rallyPoint.x, rallyPoint.y);
        campaign.BeginAssembly(Tick);
        if (campaign.Army.TryAddMemberActorId(candidate.Id) && candidate.HasRole(PersonRole.SupplyCarrier))
        {
            ArmySupplyCarrierModel.AssignCarrier(candidate, campaign.Army.CarrierState);
            campaign.Army.Wave9Evidence.RecordCarrierAssignment();
        }

        MoveCampaignMembersToRally(campaign, blockedCampaignActorIds);
        WarmCampaignRouteCacheForSmoke(campaign, objective.MovementTarget);
    }

    private void WarmCampaignRouteCacheForSmoke(CampaignState campaign, (int x, int y) target)
    {
        var anchor = campaign.Army.MemberActorIds
            .Select(actorId => _world._people.FirstOrDefault(person => person.Id == actorId))
            .Where(person => person != null)
            .Select(person => person!)
            .OrderBy(person => person.Id)
            .FirstOrDefault();
        var start = anchor?.Pos ?? (x: campaign.RouteIntent.OriginX, y: campaign.RouteIntent.OriginY);
        BuildCampaignRouteCache(campaign, new NavigationGrid(_world), start, target, _world.NavigationTopologyVersion, GetOperatorSmokePathMaxExpansions());
    }

    private bool HasCampaignPathPreflight(CampaignState campaign, (int x, int y) start, (int x, int y) target, int maxExpansions = CampaignPathMaxExpansions)
    {
        var path = NavigationPathfinder.FindPath(
            new NavigationGrid(_world),
            start,
            target,
            campaign.OriginColonyId,
            maxExpansions,
            out var budgetExceeded);

        return !budgetExceeded && path.Count > 1;
    }

    private int GetOperatorSmokePathMaxExpansions()
        => Math.Max(OperatorSmokeCampaignPathMaxExpansions, _world.Width * _world.Height);

    private void EvaluateOrganicCampaignLaunches(HashSet<int> blockedCampaignActorIds)
    {
        if (!IsCampaignRuntimeAvailable() || Tick < OrganicCampaignLaunchCadenceTicks)
            return;

        if (Tick % OrganicCampaignLaunchCadenceTicks != 0)
            return;

        var assignedActorIds = GetActiveCampaignActorIds();
        _organicLaunchDiagnostics.RecordEvaluationTick(Tick);
        foreach (var ownerColony in _world._colonies.OrderBy(colony => (int)colony.Faction).ThenBy(colony => colony.Id))
        {
            SynchronizeOrganicCampaignMobilizationRoles(ownerColony, assignedActorIds, blockedCampaignActorIds);
            var context = BuildOrganicCampaignStrategyContext(ownerColony, assignedActorIds, blockedCampaignActorIds, out var eligibleMemberCount);
            var decision = _campaignStrategist.Decide(context);
            var targets = context.Targets ?? Array.Empty<CampaignTargetOption>();
            _organicLaunchDiagnostics.RecordEvaluation(
                ownerColony.Faction,
                eligibleMemberCount,
                context,
                targets,
                CountTargetOptionsByStance(ownerColony.Faction, targets, Stance.War),
                CountTargetOptionsByStance(ownerColony.Faction, targets, Stance.Hostile),
                decision);
            if (decision.Kind == CampaignStrategyDecisionKind.LaunchCampaign)
            {
                var result = TryApplyOrganicCampaignLaunch(ownerColony, decision, assignedActorIds, blockedCampaignActorIds);
                _organicLaunchDiagnostics.RecordLaunchApplyResult(result);
                if (result.Success && result.CampaignId.HasValue)
                    assignedActorIds = GetActiveCampaignActorIds();
                continue;
            }

            if (decision.Kind == CampaignStrategyDecisionKind.RequestConvoy)
                TryApplySupplyConvoyRequest(ownerColony, decision, assignedActorIds, blockedCampaignActorIds);

            if (decision.Kind == CampaignStrategyDecisionKind.ReinforceCampaign
                && TryApplySiegeUnitProtectionReinforcement(ownerColony, decision, assignedActorIds, blockedCampaignActorIds))
                continue;
        }
    }

    private void SynchronizeOrganicCampaignMobilizationRoles(
        Colony ownerColony,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
    {
        var warState = _world.GetColonyWarState(ownerColony.Id);
        if (warState is not (ColonyWarState.Tense or ColonyWarState.War))
            return;

        var mobilizedWarriorCount = _world.GetColonyWarriorCount(ownerColony.Id);
        if (mobilizedWarriorCount <= 0)
            return;

        var candidates = _world._people
            .Where(person => IsOrganicCampaignMobilizationCandidate(person, ownerColony, assignedActorIds, blockedCampaignActorIds))
            .ToArray();
        if (candidates.Length <= 0)
            return;

        var desiredWarriorRoles = Math.Min(
            candidates.Length,
            Math.Max(mobilizedWarriorCount, _campaignLogisticsOptions.MinimumHomeDefenseWarriors + 1));
        var existingWarriorRoles = candidates.Count(person => person.HasRole(PersonRole.Warrior));
        if (existingWarriorRoles >= desiredWarriorRoles)
            return;

        var toAssign = desiredWarriorRoles - existingWarriorRoles;
        var assigned = 0;
        foreach (var candidate in candidates
                     .Where(person => !person.HasRole(PersonRole.Warrior) && !HasSpecialCampaignRole(person))
                     .OrderByDescending(person => person.Strength + person.Defense)
                     .ThenBy(person => person.Id)
                     .Take(toAssign))
        {
            candidate.AssignRole(PersonRole.Warrior);
            assigned++;
        }

        if (assigned >= toAssign)
            return;

        foreach (var candidate in candidates
                     .Where(person => !person.HasRole(PersonRole.Warrior))
                     .OrderByDescending(person => person.Strength + person.Defense)
                     .ThenBy(person => person.Id)
                     .Take(toAssign - assigned))
        {
            candidate.AssignRole(PersonRole.Warrior);
        }
    }

    private static bool IsOrganicCampaignMobilizationCandidate(
        Person person,
        Colony ownerColony,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
    {
        if (person.Health <= 0f || person.Home.Id != ownerColony.Id)
            return false;
        if (assignedActorIds.Contains(person.Id))
            return false;
        if (blockedCampaignActorIds.Contains(person.Id) || IsCampaignAssemblyBlockedByTransientOwnership(person))
            return false;

        return true;
    }

    private static bool HasSpecialCampaignRole(Person person)
        => person.HasRole(PersonRole.Scout)
           || person.HasRole(PersonRole.SupplyCarrier)
           || person.HasRole(PersonRole.Commander);

    private CampaignStrategyContext BuildOrganicCampaignStrategyContext(
        Colony ownerColony,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds,
        out int eligibleMemberCount)
    {
        var eligibleMembers = GetOrganicCampaignEligibleMembers(ownerColony, assignedActorIds, blockedCampaignActorIds);
        eligibleMemberCount = eligibleMembers.Count;
        var availableWarriors = eligibleMembers.Count(person => person.HasRole(PersonRole.Warrior));
        var availableForLaunch = Math.Max(0, availableWarriors - _campaignLogisticsOptions.MinimumHomeDefenseWarriors);
        var activeCampaignCount = CountUnresolvedOwnerCampaigns(ownerColony.Faction);
        return new CampaignStrategyContext(
            FactionId: (int)ownerColony.Faction,
            AvailableWarriors: availableForLaunch,
            AvailableCarriers: eligibleMembers.Count(person => person.HasRole(PersonRole.SupplyCarrier)),
            ActiveCampaignCount: activeCampaignCount,
            MaxActiveCampaigns: _campaignLogisticsOptions.MaxActiveCampaignsPerFaction,
            HomeDefenseScore: availableForLaunch,
            MinimumHomeDefenseScore: _campaignLogisticsOptions.MinimumHomeDefenseWarriors,
            SupplyReadiness: 1.0f,
            VisibleEnemyPressure: CalculateVisibleEnemyPressure(ownerColony.Faction),
            CanLaunchCampaign: true,
            CanAbortCampaign: true,
            CanRequestConvoy: true,
            CanReinforceCampaign: true,
            Targets: BuildOrganicCampaignTargetOptions(ownerColony, availableForLaunch, assignedActorIds, blockedCampaignActorIds),
            ActiveCampaigns: BuildActiveCampaignStrategyFacts(ownerColony.Faction));
    }

    private int CountTargetOptionsByStance(
        Faction ownerFaction,
        IReadOnlyList<CampaignTargetOption> targetOptions,
        Stance stance)
        => targetOptions.Count(target =>
            Enum.IsDefined(typeof(Faction), (Faction)target.TargetFactionId)
            && _world.GetFactionStance(ownerFaction, (Faction)target.TargetFactionId) == stance);

    private IReadOnlyList<CampaignTargetOption> BuildOrganicCampaignTargetOptions(
        Colony ownerColony,
        int availableForLaunch,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
    {
        if (availableForLaunch <= 0)
            return Array.Empty<CampaignTargetOption>();

        var targetColonies = _world._colonies
            .Where(colony => colony.Faction != ownerColony.Faction)
            .Select(colony => (Colony: colony, Stance: _world.GetFactionStance(ownerColony.Faction, colony.Faction)))
            .Where(candidate => candidate.Stance >= Stance.Hostile)
            .ToArray();
        if (targetColonies.Any(candidate => candidate.Stance == Stance.War))
            targetColonies = targetColonies.Where(candidate => candidate.Stance == Stance.War).ToArray();

        return targetColonies
            .Where(candidate => !IsOrganicCampaignPairCapReached(ownerColony.Faction, candidate.Colony.Faction))
            .OrderBy(candidate => (int)candidate.Colony.Faction)
            .ThenBy(candidate => candidate.Colony.Id)
            .Select(candidate => BuildOrganicCampaignTargetOption(
                ownerColony,
                candidate.Colony,
                candidate.Stance,
                availableForLaunch,
                assignedActorIds,
                blockedCampaignActorIds))
            .ToArray();
    }

    private CampaignTargetOption BuildOrganicCampaignTargetOption(
        Colony ownerColony,
        Colony targetColony,
        Stance stance,
        int availableForLaunch,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
    {
        var defenderCount = GetOrganicCampaignEligibleMembers(targetColony, assignedActorIds, blockedCampaignActorIds).Count;
        var advantage = CalculateOrganicCampaignAdvantage(availableForLaunch, defenderCount);
        var pressure = ApplyOrganicCampaignWarScorePressureModifier(
            ownerColony.Faction,
            targetColony.Faction,
            stance == Stance.War ? 1.0f : 0.75f);
        var distancePenalty = CalculateOrganicCampaignDistancePenalty(ownerColony, targetColony)
            + CalculateOrganicCampaignTieBreakPenalty(targetColony);
        var isKnown = TryResolveOrganicCampaignTargetKnowledge(
            ownerColony.Faction,
            targetColony,
            stance,
            out var scoutIntel,
            out var scoutIntelTicksSinceRefresh);
        return new CampaignTargetOption(
            TargetFactionId: (int)targetColony.Faction,
            TargetColonyId: targetColony.Id,
            PressureScore: pressure,
            AdvantageScore: advantage,
            MinimumWarriors: 1,
            RequestedWarriors: Math.Min(2, availableForLaunch),
            RequestedCarriers: 0,
            DistancePenalty: distancePenalty,
            IsKnown: isKnown,
            HasScoutIntel: scoutIntel != null,
            ScoutIntelTicksSinceRefresh: scoutIntelTicksSinceRefresh,
            ScoutIntelConfidence: scoutIntel?.Confidence ?? 0f);
    }

    private bool TryResolveOrganicCampaignTargetKnowledge(
        Faction ownerFaction,
        Colony targetColony,
        Stance stance,
        out ScoutIntelState? scoutIntel,
        out int scoutIntelTicksSinceRefresh)
    {
        if (TryGetActionableScoutIntelForOrganicTarget(ownerFaction, targetColony, out scoutIntel, out scoutIntelTicksSinceRefresh))
            return true;

        return stance == Stance.War;
    }

    private bool HasActionableScoutIntelForOrganicTarget(Faction ownerFaction, Colony targetColony)
        => TryGetActionableScoutIntelForOrganicTarget(ownerFaction, targetColony, out _, out _);

    private bool TryGetActionableScoutIntelForOrganicTarget(
        Faction ownerFaction,
        Colony targetColony,
        out ScoutIntelState? scoutIntel,
        out int scoutIntelTicksSinceRefresh)
    {
        scoutIntel = null;
        scoutIntelTicksSinceRefresh = int.MaxValue;
        if (!IsScoutIntelTargetRelation(ownerFaction, targetColony.Faction))
            return false;

        scoutIntel = _scoutIntel
            .Where(intel =>
                intel.IsActive(Tick)
                && intel.OwnerFaction == ownerFaction
                && intel.ObservedFaction == targetColony.Faction
                && intel.ObservedColonyId == targetColony.Id
                && intel.ObservationKind == ScoutIntelObservationKind.Colony)
            .OrderBy(intel => CalculateScoutIntelTicksSinceRefresh(intel))
            .ThenByDescending(intel => intel.Confidence)
            .ThenBy(intel => intel.IntelId)
            .FirstOrDefault();
        if (scoutIntel == null)
            return false;

        scoutIntelTicksSinceRefresh = CalculateScoutIntelTicksSinceRefresh(scoutIntel);
        return scoutIntelTicksSinceRefresh <= GetOrganicCampaignScoutIntelFreshnessThresholdTicks();
    }

    private int GetOrganicCampaignScoutIntelFreshnessThresholdTicks()
        => Math.Max(1, _campaignLogisticsOptions.ScoutIntelTtlTicks / 2);

    private CampaignCreationResult TryApplyOrganicCampaignLaunch(
        Colony ownerColony,
        CampaignStrategyDecision decision,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
    {
        if (!Enum.IsDefined(typeof(Faction), (Faction)decision.TargetFactionId))
        {
            return CampaignCreationResult.Failed(
                CampaignCreationStatus.InvalidTargetFaction,
                $"Invalid organic target faction value: {decision.TargetFactionId}.");
        }

        var targetFaction = (Faction)decision.TargetFactionId;
        if (ownerColony.Faction == targetFaction)
        {
            return CampaignCreationResult.Failed(
                CampaignCreationStatus.SameFaction,
                "Organic campaign creation requires owner faction and target faction to differ.");
        }

        var targetColony = _world._colonies.FirstOrDefault(colony => colony.Id == decision.TargetColonyId && colony.Faction == targetFaction);
        if (targetColony == null)
        {
            return CampaignCreationResult.Failed(
                CampaignCreationStatus.TargetColonyNotFound,
                $"No organic target colony found for faction {targetFaction} and colony {decision.TargetColonyId}.");
        }

        var stance = _world.GetFactionStance(ownerColony.Faction, targetFaction);
        if (stance < Stance.Hostile)
        {
            return CampaignCreationResult.Failed(
                CampaignCreationStatus.TargetColonyNotFound,
                $"Organic campaign target {targetFaction} is not hostile to {ownerColony.Faction}.");
        }

        if (!TryResolveOrganicCampaignTargetKnowledge(ownerColony.Faction, targetColony, stance, out _, out _))
        {
            return CampaignCreationResult.Failed(
                CampaignCreationStatus.CampaignRuntimeUnavailable,
                $"Organic campaign target {targetFaction}/{targetColony.Id} is missing target knowledge for {ownerColony.Faction}.");
        }

        if (IsOrganicCampaignOwnerCapReached(ownerColony.Faction))
        {
            _campaignLogisticsCounters.RecordCampaignLaunchBlockedByCap();
            return CampaignCreationResult.Failed(CampaignCreationStatus.CampaignRuntimeUnavailable, "Organic campaign owner cap reached.");
        }

        if (IsOrganicCampaignPairCapReached(ownerColony.Faction, targetFaction))
        {
            _campaignLogisticsCounters.RecordCampaignLaunchBlockedByPairCap();
            return CampaignCreationResult.Failed(CampaignCreationStatus.CampaignRuntimeUnavailable, "Organic campaign unordered faction-pair cap reached.");
        }

        if (!HasOrganicCampaignRoutePreflight(ownerColony, targetColony))
        {
            _campaignLogisticsCounters.RecordCampaignLaunchRouteBudgetExhausted();
            return CampaignCreationResult.Failed(CampaignCreationStatus.CampaignRuntimeUnavailable, "Organic campaign route/path budget preflight failed.");
        }

        var eligibleMembers = GetOrganicCampaignEligibleMembers(ownerColony, assignedActorIds, blockedCampaignActorIds);
        var availableForLaunch = Math.Max(0, eligibleMembers.Count(person => person.HasRole(PersonRole.Warrior)) - _campaignLogisticsOptions.MinimumHomeDefenseWarriors);
        if (availableForLaunch <= 0)
        {
            _campaignLogisticsCounters.RecordCampaignLaunchBlockedByHomeDefense();
            return CampaignCreationResult.Failed(CampaignCreationStatus.InvalidRequestedMemberCount, "Organic campaign launch would violate home defense reserve.");
        }

        var requestedMemberCount = Math.Max(1, decision.RequestedWarriors + decision.RequestedCarriers);
        requestedMemberCount = Math.Min(requestedMemberCount, availableForLaunch);
        return CreateCampaignFromResolvedColonies(ownerColony, targetColony, requestedMemberCount);
    }

    private bool HasOrganicCampaignRoutePreflight(Colony ownerColony, Colony targetColony)
    {
        var grid = new NavigationGrid(_world);
        var path = NavigationPathfinder.FindPath(
            grid,
            ownerColony.Origin,
            targetColony.Origin,
            ownerColony.Id,
            _campaignLogisticsOptions.RoutePathMaxExpansions,
            out var budgetExceeded);

        return !budgetExceeded && path.Count > 1;
    }

    private IReadOnlyList<ActiveCampaignStrategyFact> BuildActiveCampaignStrategyFacts(Faction ownerFaction)
        => _campaigns
            .Where(campaign => campaign.OwnerFaction == ownerFaction && campaign.Phase != CampaignPhase.Resolved)
            .OrderBy(campaign => campaign.CampaignId)
            .Select(campaign =>
            {
                var supplyReadiness = CalculateCampaignSupplyReadiness(campaign);
                _campaignLogisticsCounters.RecordConvoyEvaluation(supplyReadiness < 1.0f);
                var siegeFacts = BuildCampaignSiegeUnitStrategyFacts(campaign);
                return new ActiveCampaignStrategyFact(
                    campaign.CampaignId,
                    (int)campaign.TargetFaction,
                    campaign.TargetColonyId,
                    SupplyReadiness: supplyReadiness,
                    AdvantageScore: 0f,
                    StalledTicks: campaign.RouteCounters.NoProgressTicks,
                    IsRecoverable: true,
                    ActiveSiegeUnitCount: siegeFacts.activeCount,
                    DamagedActiveSiegeUnitCount: siegeFacts.damagedCount,
                    HasActiveSiegeUnits: siegeFacts.activeCount > 0,
                    HasDamagedActiveSiegeUnits: siegeFacts.damagedCount > 0);
            })
            .ToArray();

    private (int activeCount, int damagedCount) BuildCampaignSiegeUnitStrategyFacts(CampaignState campaign)
    {
        var activeCount = 0;
        var damagedCount = 0;
        foreach (var unit in _siegeUnits)
        {
            if (unit.CampaignId != campaign.CampaignId
                || unit.ArmyId != campaign.Army.ArmyId
                || unit.Phase != SiegeUnitPhase.Active)
                continue;

            activeCount++;
            if (unit.Health < unit.MaxHealth)
                damagedCount++;
        }

        return (activeCount, damagedCount);
    }

    private bool TryApplySiegeUnitProtectionReinforcement(
        Colony ownerColony,
        CampaignStrategyDecision decision,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
    {
        if (decision.ReasonCode != CampaignStrategyReasonCode.CampaignSiegeUnitProtectionNeeded
            || decision.RequestedWarriors <= 0)
            return false;

        var targetCampaign = _campaigns.FirstOrDefault(campaign =>
            campaign.CampaignId == decision.CampaignId
            && campaign.OwnerFaction == ownerColony.Faction
            && campaign.Phase != CampaignPhase.Resolved);
        if (targetCampaign == null || BuildCampaignSiegeUnitStrategyFacts(targetCampaign).damagedCount <= 0)
            return false;

        var reserve = CountAvailableHomeDefenseWarriors(ownerColony, assignedActorIds, blockedCampaignActorIds)
            - _campaignLogisticsOptions.MinimumHomeDefenseWarriors;
        var toAssign = Math.Min(decision.RequestedWarriors, Math.Max(0, reserve));
        if (toAssign <= 0)
            return false;

        var assigned = 0;
        foreach (var candidate in GetOrganicCampaignEligibleMembers(ownerColony, assignedActorIds, blockedCampaignActorIds)
                     .Where(person => person.HasRole(PersonRole.Warrior)))
        {
            if (!targetCampaign.Army.TryAddProtectionReinforcementMemberActorId(candidate.Id))
                continue;

            assignedActorIds.Add(candidate.Id);
            assigned++;
            if (assigned >= toAssign)
                break;
        }

        return assigned > 0;
    }

    private bool TryApplySupplyConvoyRequest(
        Colony ownerColony,
        CampaignStrategyDecision decision,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
    {
        var targetCampaign = _campaigns.FirstOrDefault(campaign =>
            campaign.CampaignId == decision.CampaignId
            && campaign.OwnerFaction == ownerColony.Faction
            && campaign.Phase != CampaignPhase.Resolved);
        if (targetCampaign == null)
            return false;

        _campaignLogisticsCounters.RecordConvoyRequest();

        if (CountActiveSupplyConvoys(ownerColony.Faction) >= _campaignLogisticsOptions.MaxActiveConvoysPerFaction)
        {
            _campaignLogisticsCounters.RecordConvoySpawnBlockedByCap();
            return false;
        }

        if (IsSupplyConvoySpawnThrottled(ownerColony.Faction))
        {
            _campaignLogisticsCounters.RecordConvoySpawnBlockedByThrottle();
            return false;
        }

        if (CountAvailableHomeDefenseWarriors(ownerColony, assignedActorIds, blockedCampaignActorIds) < _campaignLogisticsOptions.MinimumHomeDefenseWarriors)
        {
            _campaignLogisticsCounters.RecordConvoySpawnBlockedByHomeDefense();
            return false;
        }

        var target = ResolveSupplyConvoyTarget(targetCampaign);
        if (!HasSupplyConvoyRoutePreflight(ownerColony, target))
        {
            _campaignLogisticsCounters.RecordConvoySpawnRouteBudgetExhausted();
            return false;
        }

        var convoyId = _nextSupplyConvoyId++;
        _supplyConvoys.Add(new SupplyConvoyState(
            convoyId,
            ownerColony.Faction,
            ownerColony.Id,
            targetCampaign.CampaignId,
            targetCampaign.ArmyId,
            Tick,
            ownerColony.Origin.x,
            ownerColony.Origin.y,
            target.x,
            target.y,
            _campaignLogisticsOptions.ConvoyFoodPayload));
        _campaignLogisticsCounters.RecordConvoySpawned();
        return true;
    }

    private int CountActiveSupplyConvoys(Faction ownerFaction)
        => _supplyConvoys.Count(convoy => convoy.OwnerFaction == ownerFaction && convoy.IsActive);

    private bool IsSupplyConvoySpawnThrottled(Faction ownerFaction)
    {
        if (_campaignLogisticsOptions.ConvoySpawnCooldownTicks <= 0)
            return false;

        var lastSpawnTick = _supplyConvoys
            .Where(convoy => convoy.OwnerFaction == ownerFaction)
            .Select(convoy => convoy.CreatedTick)
            .DefaultIfEmpty(-1)
            .Max();
        return lastSpawnTick >= 0 && Tick - lastSpawnTick < _campaignLogisticsOptions.ConvoySpawnCooldownTicks;
    }

    private int CountAvailableHomeDefenseWarriors(
        Colony ownerColony,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
        => _world._people.Count(person =>
            person.Health > 0f
            && person.Home.Id == ownerColony.Id
            && person.HasRole(PersonRole.Warrior)
            && !assignedActorIds.Contains(person.Id)
            && !blockedCampaignActorIds.Contains(person.Id)
            && !IsCampaignAssemblyBlockedByTransientOwnership(person));

    private (int x, int y) ResolveSupplyConvoyTarget(CampaignState campaign)
    {
        if (campaign.Phase == CampaignPhase.Marching && TryResolveCampaignMarchObjective(campaign, out var objective))
            return objective.MovementTarget;

        var memberActorId = campaign.Army.MemberActorIds
            .OrderBy(actorId => actorId)
            .FirstOrDefault();
        if (memberActorId > 0)
        {
            var member = _world._people.FirstOrDefault(person => person.Id == memberActorId && person.Health > 0f);
            if (member != null)
                return member.Pos;
        }

        return (campaign.RouteIntent.TargetX, campaign.RouteIntent.TargetY);
    }

    private bool HasSupplyConvoyRoutePreflight(Colony ownerColony, (int x, int y) target)
    {
        var grid = new NavigationGrid(_world);
        var path = NavigationPathfinder.FindPath(
            grid,
            ownerColony.Origin,
            target,
            ownerColony.Id,
            _campaignLogisticsOptions.RoutePathMaxExpansions,
            out var budgetExceeded);

        return !budgetExceeded && path.Count > 1;
    }

    private static float CalculateCampaignSupplyReadiness(CampaignState campaign)
    {
        if (campaign.Phase is CampaignPhase.Resolved or CampaignPhase.AssemblingPending or CampaignPhase.Assembling)
            return 1.0f;
        if (campaign.Army.RationPoolState.RationPoolFood > 0)
            return 1.0f;
        if (campaign.Army.SupplyState.SustainedOutOfSupplyTicks >= 5)
            return 0.1f;
        if (campaign.Army.SupplyState.SustainedOutOfSupplyTicks > 0)
            return 0.35f;

        return 1.0f;
    }

    private List<Person> GetOrganicCampaignEligibleMembers(
        Colony ownerColony,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
        => _world._people
            .Where(person => IsEligibleOrganicCampaignMember(person, ownerColony, assignedActorIds, blockedCampaignActorIds))
            .OrderBy(person => GetCampaignAssemblyCandidatePriority(person))
            .ThenByDescending(person => person.Strength + person.Defense)
            .ThenBy(person => person.Id)
            .ToList();

    private static bool IsEligibleOrganicCampaignMember(
        Person person,
        Colony ownerColony,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
    {
        if (person.Health <= 0f || person.Home.Id != ownerColony.Id)
            return false;
        if (assignedActorIds.Contains(person.Id))
            return false;
        if (blockedCampaignActorIds.Contains(person.Id) || IsCampaignAssemblyBlockedByTransientOwnership(person))
            return false;

        return IsCampaignAssemblyRoleCandidate(person);
    }

    private HashSet<int> GetActiveCampaignActorIds()
    {
        var assignedActorIds = new HashSet<int>();
        foreach (var campaign in _campaigns)
        {
            if (campaign.Phase == CampaignPhase.Resolved)
                continue;

            foreach (var actorId in campaign.Army.MemberActorIds)
                assignedActorIds.Add(actorId);
        }

        return assignedActorIds;
    }

    private int CountUnresolvedOwnerCampaigns(Faction ownerFaction)
        => _campaigns.Count(campaign => campaign.OwnerFaction == ownerFaction && campaign.Phase != CampaignPhase.Resolved);

    private int CountUnresolvedUnorderedPairCampaigns(Faction firstFaction, Faction secondFaction)
        => _campaigns.Count(campaign => campaign.Phase != CampaignPhase.Resolved
            && IsSameUnorderedFactionPair(campaign.OwnerFaction, campaign.TargetFaction, firstFaction, secondFaction));

    private bool IsOrganicCampaignOwnerCapReached(Faction ownerFaction)
        => CountUnresolvedOwnerCampaigns(ownerFaction) >= _campaignLogisticsOptions.MaxActiveCampaignsPerFaction;

    private bool IsOrganicCampaignPairCapReached(Faction firstFaction, Faction secondFaction)
        => CountUnresolvedUnorderedPairCampaigns(firstFaction, secondFaction)
            >= _campaignLogisticsOptions.MaxUnresolvedCampaignsPerUnorderedPair;

    private float ApplyOrganicCampaignWarScorePressureModifier(Faction ownerFaction, Faction targetFaction, float pressure)
    {
        var warScore = GetCampaignWarScoreFor(ownerFaction, targetFaction);
        if (warScore <= -OrganicCampaignWarScorePressureThreshold)
            pressure += OrganicCampaignWarScorePressureModifier;
        else if (warScore >= OrganicCampaignWarScorePressureThreshold)
            pressure -= OrganicCampaignWarScorePressureModifier;

        return Math.Clamp(pressure, 0f, 1f);
    }

    private int GetCampaignWarScoreFor(Faction ownerFaction, Faction targetFaction)
    {
        var key = CampaignWarScoreKey.From(ownerFaction, targetFaction);
        return _campaignWarScores.GetValueOrDefault(key, 0) * key.SignFor(ownerFaction);
    }

    private static bool IsSameUnorderedFactionPair(Faction leftOwner, Faction leftTarget, Faction rightOwner, Faction rightTarget)
        => Math.Min((int)leftOwner, (int)leftTarget) == Math.Min((int)rightOwner, (int)rightTarget)
           && Math.Max((int)leftOwner, (int)leftTarget) == Math.Max((int)rightOwner, (int)rightTarget);

    private float CalculateVisibleEnemyPressure(Faction ownerFaction)
        => _world._colonies
            .Where(colony => colony.Faction != ownerFaction)
            .Select(colony => _world.GetFactionStance(ownerFaction, colony.Faction) switch
            {
                Stance.War => 1.0f,
                Stance.Hostile => 0.75f,
                _ => 0f
            })
            .DefaultIfEmpty(0f)
            .Max();

    private static float CalculateOrganicCampaignAdvantage(int attackerCount, int defenderCount)
    {
        var scale = Math.Max(1, Math.Max(attackerCount, defenderCount));
        return Math.Clamp(0.5f + ((attackerCount - defenderCount) / (float)scale * 0.5f), 0f, 1f);
    }

    private float CalculateOrganicCampaignDistancePenalty(Colony ownerColony, Colony targetColony)
    {
        var distance = Math.Abs(ownerColony.Origin.x - targetColony.Origin.x)
            + Math.Abs(ownerColony.Origin.y - targetColony.Origin.y);
        var scale = Math.Max(1, _world.Width + _world.Height);
        return Math.Clamp(distance / (float)scale, 0f, 1f) * 0.2f;
    }

    private static float CalculateOrganicCampaignTieBreakPenalty(Colony targetColony)
        => (((int)targetColony.Faction * 1000) + targetColony.Id) * 0.000001f;

    public int PrepareWave9CampaignScenario(Faction ownerFaction, int candidateCount, int carriedFoodPerCandidate)
    {
        if (!Enum.IsDefined(typeof(Faction), ownerFaction))
            return 0;

        var prepared = 0;
        foreach (var person in _world._people
                     .Where(person => person.Health > 0f && person.Home.Faction == ownerFaction)
                     .OrderBy(person => person.Id))
        {
            person.Profession = Profession.Hunter;
            for (var i = 0; i < Math.Max(0, carriedFoodPerCandidate); i++)
            {
                if (!person.Inventory.TryAdd(ItemType.Food, 1))
                    break;
            }

            prepared++;
            if (prepared >= Math.Max(0, candidateCount))
                break;
        }

        return prepared;
    }

    public void DisableWave9CampaignScenarioCombatInvalidation()
        => _world.EnableCombatPrimitives = false;

    public bool TryPrepareWave10EvidenceScenario(string? wave10Scenario)
    {
        if (!IsCampaignRuntimeAvailable() || string.IsNullOrWhiteSpace(wave10Scenario))
            return false;

        return wave10Scenario.Trim().ToLowerInvariant() switch
        {
            "organic_campaign_launch" => TryPrepareWave10OrganicCampaignLaunchEvidence(),
            "campaign_siege_resolution" => TryPrepareWave10CampaignSiegeEvidence(unlockSiegeCraft: false),
            "multi_front_bounded" => TryPrepareWave10MultiFrontEvidence(),
            "supply_line_convoy" => TryPrepareWave10SupplyLineEvidence(),
            "forward_base_long_campaign" => TryPrepareWave10ForwardBaseEvidence(),
            "scout_intel_campaign_choice" => TryPrepareWave10ScoutIntelEvidence(),
            "siege_unit_breach" => TryPrepareWave10CampaignSiegeEvidence(unlockSiegeCraft: true),
            _ => false
        };
    }

    private bool TryPrepareWave10OrganicCampaignLaunchEvidence()
    {
        if (!TryPrepareWave10ScoutIntelPreconditions(Faction.Obsidari, Faction.Aetheri, warriorCount: 3))
            return false;

        DeclareWar(Faction.Obsidari, Faction.Aetheri, "wave10 organic evidence setup");
        Tick = Math.Max(Tick, OrganicCampaignLaunchCadenceTicks);
        return true;
    }

    private bool TryPrepareWave10ScoutIntelEvidence()
    {
        if (!TryPrepareWave10ScoutIntelPreconditions(Faction.Obsidari, Faction.Aetheri, warriorCount: 2))
            return false;

        DeclareWar(Faction.Obsidari, Faction.Aetheri, "wave10 scout-intel evidence setup");
        var result = TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
        return result.Success;
    }

    private bool TryPrepareWave10MultiFrontEvidence()
    {
        const Faction ownerFaction = Faction.Obsidari;
        if (!TryPrepareWave10ScoutIntelPreconditions(ownerFaction, Faction.Aetheri, warriorCount: 6)
            || !TryPrepareWave10ScoutIntelPreconditions(ownerFaction, Faction.Sylvars, warriorCount: 6)
            || !TryPrepareWave10ScoutIntelPreconditions(ownerFaction, Faction.Chirita, warriorCount: 6))
        {
            return false;
        }

        var first = TryCreateCampaign(ownerFaction, Faction.Aetheri, requestedMemberCount: 1);
        var second = TryCreateCampaign(ownerFaction, Faction.Sylvars, requestedMemberCount: 1);
        if (!first.Success || !second.Success)
            return false;

        var ownerColony = _world._colonies.FirstOrDefault(colony => colony.Faction == ownerFaction);
        var thirdTarget = _world._colonies.FirstOrDefault(colony => colony.Faction == Faction.Chirita);
        if (ownerColony == null || thirdTarget == null)
            return false;

        var assignedActorIds = GetActiveCampaignActorIds();
        var blockedCampaignActorIds = CaptureCampaignBlockedActorIds();
        var decision = new CampaignStrategyDecision(
            CampaignStrategyDecisionKind.LaunchCampaign,
            CampaignStrategyReasonCode.TargetPressureAndAdvantage,
            TargetFactionId: (int)Faction.Chirita,
            TargetColonyId: thirdTarget.Id,
            RequestedWarriors: 1,
            RequestedCarriers: 0,
            Score: 1f);

        _ = TryApplyOrganicCampaignLaunch(ownerColony, decision, assignedActorIds, blockedCampaignActorIds);

        return CountUnresolvedOwnerCampaigns(ownerFaction) >= _campaignLogisticsOptions.MaxActiveCampaignsPerFaction
            && _campaignLogisticsCounters.CampaignLaunchBlockedByCap > 0;
    }

    private bool TryPrepareWave10SupplyLineEvidence()
    {
        var campaign = TryCreateWave10PreparedMarchingCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, preparedWarriors: 3);
        if (campaign == null)
            return false;

        campaign.Army.SupplyState.RecordSupplyPressure(isOutOfSupply: true);
        campaign.Army.SupplyState.RecordSupplyPressure(isOutOfSupply: true);
        var owner = _world._colonies.FirstOrDefault(colony => colony.Id == campaign.OriginColonyId);
        if (owner == null)
            return false;

        var assignedActorIds = GetActiveCampaignActorIds();
        var blockedCampaignActorIds = CaptureCampaignBlockedActorIds();
        var decision = new CampaignStrategyDecision(
            CampaignStrategyDecisionKind.RequestConvoy,
            CampaignStrategyReasonCode.CampaignSupplyLow,
            TargetFactionId: (int)campaign.TargetFaction,
            TargetColonyId: campaign.TargetColonyId,
            CampaignId: campaign.CampaignId,
            Score: 1f);
        if (!TryApplySupplyConvoyRequest(owner, decision, assignedActorIds, blockedCampaignActorIds))
            return false;

        _ = TryApplySupplyConvoyRequest(owner, decision, assignedActorIds, blockedCampaignActorIds);
        return true;
    }

    private bool TryPrepareWave10ForwardBaseEvidence()
    {
        var campaign = TryCreateWave10PreparedMarchingCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, preparedWarriors: 2);
        if (campaign == null)
            return false;

        var owner = _world._colonies.FirstOrDefault(colony => colony.Id == campaign.OriginColonyId);
        if (owner == null || !TryFindWave10ForwardBaseAnchor(owner, out var anchor))
            return false;

        var member = GetWave10CampaignMembers(campaign).FirstOrDefault();
        if (member == null)
            return false;

        member.Pos = anchor;
        member.ApplyStaminaDelta(-50f);
        if (!TryEstablishForwardBase(campaign, new HashSet<int>()))
            return false;

        AdvanceForwardBases(new HashSet<int>());
        return true;
    }

    private bool TryPrepareWave10CampaignSiegeEvidence(bool unlockSiegeCraft)
    {
        var campaign = TryCreateWave10PreparedMarchingCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1, preparedWarriors: 2);
        if (campaign == null)
            return false;

        var attacker = _world._colonies.FirstOrDefault(colony => colony.Id == campaign.OriginColonyId);
        var defender = _world._colonies.FirstOrDefault(colony => colony.Id == campaign.TargetColonyId);
        if (attacker == null || defender == null)
            return false;

        if (unlockSiegeCraft)
            UnlockWave10SiegeCraftForEvidence(attacker);

        if (!TryEnsureWave10EvidenceWall(defender, out var wall))
            return false;

        foreach (var member in GetWave10CampaignMembers(campaign))
        {
            member.Pos = FindWave10EncounterPosition(defender, wall.Pos);
            ResetWave10EvidenceActor(member);
        }

        campaign.BeginEncounter(Tick);
        return campaign.Phase == CampaignPhase.Encounter;
    }

    private bool TryPrepareWave10ScoutIntelPreconditions(Faction ownerFaction, Faction targetFaction, int warriorCount)
    {
        var owner = _world._colonies.FirstOrDefault(colony => colony.Faction == ownerFaction);
        var target = _world._colonies.FirstOrDefault(colony => colony.Faction == targetFaction);
        if (owner == null || target == null)
            return false;

        var prepared = PrepareWave10EvidenceWarriors(owner, warriorCount);
        if (prepared.Length < warriorCount)
            return false;

        var scout = _world._people
            .Where(person => person.Health > 0f && person.Home.Id == owner.Id && !prepared.Contains(person))
            .OrderBy(person => person.Id)
            .FirstOrDefault();
        if (scout == null)
            return false;

        ResetWave10EvidenceActor(scout);
        scout.AssignRole(PersonRole.Scout);
        scout.Pos = target.Origin;
        _world.SetFactionStance(owner.Faction, target.Faction, Stance.War);
        AdvanceScoutIntel();
        return _campaignLogisticsCounters.ScoutIntelObserved > 0;
    }

    private CampaignState? TryCreateWave10PreparedMarchingCampaign(
        Faction ownerFaction,
        Faction targetFaction,
        int requestedMemberCount,
        int preparedWarriors)
    {
        var owner = _world._colonies.FirstOrDefault(colony => colony.Faction == ownerFaction);
        if (owner == null)
            return null;

        var members = PrepareWave10EvidenceWarriors(owner, Math.Max(preparedWarriors, requestedMemberCount));
        if (members.Length < requestedMemberCount)
            return null;

        var result = TryCreateCampaign(ownerFaction, targetFaction, requestedMemberCount);
        if (!result.Success || !result.CampaignId.HasValue)
            return null;

        var campaign = _campaigns.First(candidate => candidate.CampaignId == result.CampaignId.Value);
        for (var i = 0; i < requestedMemberCount; i++)
            campaign.Army.TryAddMemberActorId(members[i].Id);

        var rally = owner.Origin;
        campaign.Army.SetRallyPoint(rally.x, rally.y);
        campaign.BeginAssembly(Tick);
        campaign.MarkAssemblyComplete(Tick);
        foreach (var member in GetWave10CampaignMembers(campaign))
            member.Pos = rally;
        return campaign.Phase == CampaignPhase.Marching ? campaign : null;
    }

    private Person[] PrepareWave10EvidenceWarriors(Colony owner, int count)
    {
        var people = _world._people
            .Where(person => person.Health > 0f && person.Home.Id == owner.Id)
            .OrderBy(person => person.Id)
            .ToArray();
        foreach (var person in people)
            ResetWave10EvidenceActor(person);

        var selected = people.Take(Math.Max(0, count)).ToArray();
        foreach (var person in selected)
            person.AssignRole(PersonRole.Warrior);
        return selected;
    }

    private static void ResetWave10EvidenceActor(Person person)
    {
        person.ClearRole(PersonRole.Warrior | PersonRole.SupplyCarrier | PersonRole.Scout | PersonRole.Commander);
        person.Profession = Profession.Generalist;
        person.Current = Job.Idle;
        person.SetCombatAssignment(null, null, Formation.Line, isCommander: false);
        person.ApplyStaminaDelta(100f);
        person.ApplyMoraleDelta(100f);
    }

    private IReadOnlyList<Person> GetWave10CampaignMembers(CampaignState campaign)
        => campaign.Army.MemberActorIds
            .Select(actorId => _world._people.FirstOrDefault(person => person.Id == actorId && person.Health > 0f))
            .Where(person => person != null)
            .Select(person => person!)
            .OrderBy(person => person.Id)
            .ToArray();

    private void UnlockWave10SiegeCraftForEvidence(Colony colony)
    {
        foreach (var techId in new[] { "woodcutting", "construction", "mining", "tools", "fortification", "siege_craft" })
            colony.UnlockedTechs.Add(techId);
    }

    private bool TryEnsureWave10EvidenceWall(Colony defender, out DefensiveStructure wall)
    {
        wall = _world.DefensiveStructures
            .Where(structure => !structure.IsDestroyed && structure.Owner.Id == defender.Id)
            .OrderBy(structure => Math.Abs(structure.Pos.x - defender.Origin.x) + Math.Abs(structure.Pos.y - defender.Origin.y))
            .ThenBy(structure => structure.Id)
            .FirstOrDefault()!;
        if (wall != null)
            return true;

        if (!TryFindWave10BuildableNear(defender.Origin, minDistance: 3, out var pos))
            return false;

        if (!_world.TryAddWoodWall(defender, pos))
            return false;

        wall = _world.DefensiveStructures.Single(structure => structure.Owner.Id == defender.Id && structure.Pos == pos);
        return true;
    }

    private (int x, int y) FindWave10EncounterPosition(Colony defender, (int x, int y) wall)
    {
        foreach (var candidate in EnumerateWave10ManhattanRing(wall, maxRadius: 3))
        {
            if (_world.IsMovementBlocked(candidate.x, candidate.y, defender.Id))
                continue;
            return candidate;
        }

        return defender.Origin;
    }

    private bool TryFindWave10ForwardBaseAnchor(Colony owner, out (int x, int y) anchor)
    {
        for (var y = 0; y < _world.Height; y++)
        {
            for (var x = 0; x < _world.Width; x++)
            {
                var candidate = (x, y);
                if (Math.Abs(candidate.x - owner.Origin.x) + Math.Abs(candidate.y - owner.Origin.y) < _campaignLogisticsOptions.ForwardBaseMinDistanceFromHome)
                    continue;
                if (_world.IsMovementBlocked(candidate.x, candidate.y, owner.Id))
                    continue;
                if (!HasForwardBaseRoutePreflight(owner, candidate))
                    continue;

                anchor = candidate;
                return true;
            }
        }

        anchor = default;
        return false;
    }

    private bool TryFindWave10BuildableNear((int x, int y) origin, int minDistance, out (int x, int y) position)
    {
        for (var radius = Math.Max(0, minDistance); radius <= Math.Max(_world.Width, _world.Height); radius++)
        {
            foreach (var candidate in EnumerateWave10ManhattanRing(origin, radius))
            {
                if (Math.Abs(candidate.x - origin.x) + Math.Abs(candidate.y - origin.y) < minDistance)
                    continue;
                if (!_world.CanPlaceStructureAt(candidate.x, candidate.y))
                    continue;

                position = candidate;
                return true;
            }
        }

        position = default;
        return false;
    }

    private IEnumerable<(int x, int y)> EnumerateWave10ManhattanRing((int x, int y) origin, int maxRadius)
    {
        for (var radius = 0; radius <= Math.Max(0, maxRadius); radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) != radius)
                        continue;
                    int x = origin.x + dx;
                    int y = origin.y + dy;
                    if (x < 0 || y < 0 || x >= _world.Width || y >= _world.Height)
                        continue;
                    yield return (x, y);
                }
            }
        }
    }

    private void AdvanceCampaignAssemblies(HashSet<int> blockedCampaignActorIds)
    {
        foreach (var campaign in _campaigns)
        {
            if (campaign.Phase is not CampaignPhase.AssemblingPending and not CampaignPhase.Assembling)
                continue;

            PruneInvalidCampaignMembers(campaign, blockedCampaignActorIds);

            if (!campaign.Army.HasRallyPoint && TryFindCampaignRallyPoint(campaign, out var rallyPoint))
                campaign.Army.SetRallyPoint(rallyPoint.x, rallyPoint.y);

            if (!campaign.Army.HasRallyPoint)
                continue;

            var candidate = FindNextCampaignAssemblyMember(campaign, blockedCampaignActorIds);
            if (candidate != null)
            {
                campaign.BeginAssembly(Tick);
                if (campaign.Army.TryAddMemberActorId(candidate.Id) && candidate.HasRole(PersonRole.SupplyCarrier))
                {
                    ArmySupplyCarrierModel.AssignCarrier(candidate, campaign.Army.CarrierState);
                    campaign.Army.Wave9Evidence.RecordCarrierAssignment();
                }
            }
            else if (campaign.Army.MemberCount == 0)
            {
                continue;
            }
            else
            {
                campaign.BeginAssembly(Tick);
            }

            MoveCampaignMembersToRally(campaign, blockedCampaignActorIds);

            if (IsCampaignAssemblyComplete(campaign, blockedCampaignActorIds))
                campaign.MarkAssemblyComplete(Tick);
        }
    }

    private HashSet<int> CaptureCampaignBlockedActorIds()
        => _world._people
            .Where(IsCampaignAssemblyBlockedByTransientOwnership)
            .Select(person => person.Id)
            .ToHashSet();

    private void PruneInvalidCampaignMembers(HashSet<int> blockedCampaignActorIds)
    {
        foreach (var campaign in _campaigns)
        {
            if (campaign.Phase is not CampaignPhase.AssemblingPending and not CampaignPhase.Assembling and not CampaignPhase.Marching)
                continue;

            PruneInvalidCampaignMembers(campaign, blockedCampaignActorIds);
        }
    }

    private void PruneInvalidCampaignMembers(CampaignState campaign, HashSet<int> blockedCampaignActorIds)
    {
        var wasMarching = campaign.Phase == CampaignPhase.Marching;
        foreach (var actorId in campaign.Army.MemberActorIds)
        {
            var person = _world._people.FirstOrDefault(candidate => candidate.Id == actorId);
            if (IsValidAssignedCampaignMember(person, blockedCampaignActorIds))
                continue;
            if (IsLiveAssignedCampaignMemberNearActiveForwardBase(campaign, person))
                continue;

            campaign.Army.RemoveMemberActorId(actorId);
            ClearPrunedCampaignCarrier(campaign.Army, actorId, person);
        }

        if (wasMarching && campaign.Army.MemberCount < campaign.Army.RequestedMemberCount)
            campaign.ReturnToAssemblyAfterRosterInvalidation(Tick);
    }

    private static bool IsValidAssignedCampaignMember(Person? person, HashSet<int> blockedCampaignActorIds)
        => person != null
           && person.Health > 0f
           && !blockedCampaignActorIds.Contains(person.Id)
           && !IsCampaignAssemblyBlockedByTransientOwnership(person);

    private static bool IsCampaignAssemblyBlockedByTransientOwnership(Person person)
        => person.IsRouting
           || person.IsInCombat
           || person.ActiveBattleId >= 0
           || person.ActiveCombatGroupId >= 0
           || person.Current is Job.Fight or Job.Flee or Job.RaidBorder or Job.AttackStructure;

    private bool IsLiveAssignedCampaignMemberNearActiveForwardBase(CampaignState campaign, Person? person)
    {
        if (person == null || person.Health <= 0f || !campaign.Army.HasMemberActorId(person.Id))
            return false;

        return _forwardBases.Any(forwardBase =>
            forwardBase.IsActive
            && forwardBase.OwnerFaction == campaign.OwnerFaction
            && forwardBase.CampaignId == campaign.CampaignId
            && forwardBase.ArmyId == campaign.ArmyId
            && IsWithinManhattanDistance(person.Pos, (forwardBase.X, forwardBase.Y), forwardBase.Radius));
    }

    private void ClearPrunedCampaignCarrier(ArmyState army, int actorId, Person? person)
    {
        if (army.CarrierState.AssignedCarrierActorId != actorId)
            return;

        bool liveAssignedCarrier = person != null
            && person.Health > 0f
            && _world._people.Any(candidate => ReferenceEquals(candidate, person));
        if (liveAssignedCarrier)
        {
            ArmySupplyCarrierModel.ClearCarrier(person!, army.CarrierState);
            return;
        }

        army.CarrierState.ClearCarrier();
    }

    private bool TryFindCampaignRallyPoint(CampaignState campaign, out (int x, int y) rallyPoint)
    {
        var origin = (x: campaign.RouteIntent.OriginX, y: campaign.RouteIntent.OriginY);
        if (TryFindCampaignRallyPoint(campaign, origin, requireActorFree: true, out rallyPoint))
            return true;

        return TryFindCampaignRallyPoint(campaign, origin, requireActorFree: false, out rallyPoint);
    }

    private bool TryFindCampaignRallyPoint(
        CampaignState campaign,
        (int x, int y) origin,
        bool requireActorFree,
        out (int x, int y) rallyPoint)
    {
        int maxRadius = Math.Max(_world.Width, _world.Height);
        for (var radius = 0; radius <= maxRadius; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > radius)
                        continue;

                    int x = origin.x + dx;
                    int y = origin.y + dy;
                    if (_world.IsMovementBlocked(x, y, campaign.OriginColonyId))
                        continue;
                    if (requireActorFree && _world.IsActorOccupied(x, y))
                        continue;

                    rallyPoint = (x, y);
                    return true;
                }
            }
        }

        rallyPoint = default;
        return false;
    }

    private Person? FindNextCampaignAssemblyMember(CampaignState campaign, HashSet<int> blockedCampaignActorIds, bool allowRoleFallback = false)
    {
        var assignedActorIds = GetActiveCampaignActorIds(exclude: campaign);
        var candidates = _world._people
            .Where(person => IsEligibleCampaignAssemblyMember(person, campaign, assignedActorIds, blockedCampaignActorIds))
            .OrderBy(GetCampaignAssemblyCandidatePriority)
            .ThenByDescending(person => person.Strength + person.Defense)
            .ThenBy(person => person.Id)
            .FirstOrDefault();
        if (candidates != null || !allowRoleFallback)
            return candidates;

        return _world._people
            .Where(person => IsValidOperatorSmokeFallbackMember(person, campaign, assignedActorIds, blockedCampaignActorIds))
            .OrderByDescending(person => person.Strength + person.Defense)
            .ThenBy(person => person.Id)
            .FirstOrDefault();
    }

    private HashSet<int> GetActiveCampaignActorIds(CampaignState exclude)
    {
        var assignedActorIds = new HashSet<int>();
        foreach (var campaign in _campaigns)
        {
            if (ReferenceEquals(campaign, exclude) || campaign.Phase == CampaignPhase.Resolved)
                continue;

            foreach (var actorId in campaign.Army.MemberActorIds)
                assignedActorIds.Add(actorId);
        }

        return assignedActorIds;
    }

    private static bool IsEligibleCampaignAssemblyMember(
        Person person,
        CampaignState campaign,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
    {
        if (person.Health <= 0f || person.Home.Id != campaign.OriginColonyId)
            return false;
        if (campaign.Army.HasMemberActorId(person.Id) || assignedActorIds.Contains(person.Id))
            return false;
        if (blockedCampaignActorIds.Contains(person.Id) || IsCampaignAssemblyBlockedByTransientOwnership(person))
            return false;

        return IsCampaignAssemblyRoleCandidate(person);
    }

    private static bool IsCampaignAssemblyRoleCandidate(Person person)
        => person.HasRole(PersonRole.Warrior)
           || person.HasRole(PersonRole.SupplyCarrier)
           || person.Profession == Profession.Hunter;

    private static bool IsValidOperatorSmokeFallbackMember(
        Person person,
        CampaignState campaign,
        HashSet<int> assignedActorIds,
        HashSet<int> blockedCampaignActorIds)
        => person.Health > 0f
           && person.Home.Id == campaign.OriginColonyId
           && !campaign.Army.HasMemberActorId(person.Id)
           && !assignedActorIds.Contains(person.Id)
           && !blockedCampaignActorIds.Contains(person.Id)
           && !IsCampaignAssemblyBlockedByTransientOwnership(person);

    private static int GetCampaignAssemblyCandidatePriority(Person person)
    {
        if (person.HasRole(PersonRole.Warrior))
            return 0;
        if (person.HasRole(PersonRole.SupplyCarrier))
            return 1;
        return 2;
    }

    private void MoveCampaignMembersToRally(CampaignState campaign, HashSet<int> blockedCampaignActorIds)
    {
        var rally = (x: campaign.Army.RallyX, y: campaign.Army.RallyY);
        foreach (var actorId in campaign.Army.MemberActorIds)
        {
            var person = _world._people.FirstOrDefault(candidate => candidate.Id == actorId);
            if (!IsValidAssignedCampaignMember(person, blockedCampaignActorIds))
                continue;

            person?.MoveTowardCampaignRally(_world, rally);
        }
    }

    private bool IsCampaignAssemblyComplete(CampaignState campaign, HashSet<int> blockedCampaignActorIds)
    {
        if (campaign.Army.MemberCount == 0)
            return false;

        if (campaign.Army.MemberCount < campaign.Army.RequestedMemberCount)
            return false;

        var rally = (x: campaign.Army.RallyX, y: campaign.Army.RallyY);
        var hasLivingMember = false;
        foreach (var actorId in campaign.Army.MemberActorIds)
        {
            var person = _world._people.FirstOrDefault(candidate => candidate.Id == actorId);
            if (!IsValidAssignedCampaignMember(person, blockedCampaignActorIds))
                return false;

            var validPerson = person!;
            hasLivingMember = true;

            if (Math.Abs(validPerson.Pos.x - rally.x) + Math.Abs(validPerson.Pos.y - rally.y) > 1)
                return false;
        }

        return hasLivingMember;
    }

    private void AdvanceCampaignMarches(HashSet<int> marchEligibleCampaignIds, float dt, HashSet<int> blockedCampaignActorIds)
    {
        foreach (var campaign in _campaigns)
        {
            if (campaign.Phase != CampaignPhase.Marching || !marchEligibleCampaignIds.Contains(campaign.CampaignId))
                continue;

            PruneInvalidCampaignMembers(campaign, blockedCampaignActorIds);
            if (campaign.Phase != CampaignPhase.Marching)
                continue;

            var members = GetValidCampaignMembers(campaign, blockedCampaignActorIds);
            if (members.Count < campaign.Army.RequestedMemberCount)
            {
                campaign.ReturnToAssemblyAfterRosterInvalidation(Tick);
                continue;
            }

            if (dt <= 0f)
            {
                var zeroDtAnchor = members[0];
                if (TryResolveCampaignMarchObjective(campaign, out var zeroDtObjective)
                    && IsCampaignAtEncounterObjective(zeroDtAnchor.Pos, zeroDtObjective))
                {
                    campaign.BeginEncounter(Tick);
                    continue;
                }
            }

            TickCampaignMarchSupply(campaign, members, dt, Tick);

            PruneInvalidCampaignMembers(campaign, blockedCampaignActorIds);
            if (campaign.Phase != CampaignPhase.Marching)
                continue;

            members = GetValidCampaignMembers(campaign, blockedCampaignActorIds);
            if (members.Count < campaign.Army.RequestedMemberCount)
            {
                campaign.ReturnToAssemblyAfterRosterInvalidation(Tick);
                continue;
            }

            var anchor = members[0];
            var hasObjective = TryResolveCampaignMarchObjective(campaign, out var objective);

            if (hasObjective && IsCampaignAtEncounterObjective(anchor.Pos, objective))
            {
                campaign.BeginEncounter(Tick);
                continue;
            }

            if (!hasObjective)
            {
                campaign.RouteCounters.RecordNoProgress();
                continue;
            }

            var nextStep = GetNextCampaignMarchStep(campaign, anchor.Pos, objective.MovementTarget);
            if (!nextStep.HasValue)
            {
                campaign.RouteCounters.RecordNoProgress();
                continue;
            }

            bool moved = false;
            foreach (var member in members)
            {
                if (member.MoveTowardCampaignMarch(_world, nextStep.Value))
                    moved = true;
            }

            if (anchor.Pos == nextStep.Value)
                campaign.RouteCache.Advance();

            if (!moved)
            {
                campaign.RouteCounters.RecordNoProgress();
                continue;
            }

            campaign.RouteCounters.RecordMarchProgress();

            if (members.Any(member => IsCampaignAtEncounterObjective(member.Pos, objective)))
                campaign.BeginEncounter(Tick);
        }
    }

    private void AdvanceCampaignEncounters()
    {
        foreach (var campaign in _campaigns)
        {
            if (campaign.Phase == CampaignPhase.Encounter)
                campaign.RouteCounters.RecordEncounterTick();
        }
    }

    private void AdvanceForwardBases(HashSet<int> blockedCampaignActorIds)
    {
        var closedCampaignIds = new HashSet<int>();
        foreach (var forwardBase in _forwardBases.Where(forwardBase => forwardBase.IsActive).OrderBy(forwardBase => forwardBase.BaseId).ToArray())
        {
            var campaign = _campaigns.FirstOrDefault(candidate =>
                candidate.CampaignId == forwardBase.CampaignId
                && candidate.ArmyId == forwardBase.ArmyId
                && candidate.OwnerFaction == forwardBase.OwnerFaction);
            if (campaign == null || campaign.Phase == CampaignPhase.Resolved)
            {
                MarkForwardBaseAbandoned(forwardBase, ForwardBaseCloseReasons.CampaignResolved);
                closedCampaignIds.Add(forwardBase.CampaignId);
                continue;
            }

            if (Tick - forwardBase.CreatedTick >= _campaignLogisticsOptions.ForwardBaseLifetimeTicks)
            {
                forwardBase.MarkExpired(Tick);
                _campaignLogisticsCounters.RecordForwardBaseExpired();
                closedCampaignIds.Add(forwardBase.CampaignId);
                continue;
            }

            var liveNearbyMembers = GetLiveAssignedCampaignMembersNearForwardBase(campaign, forwardBase);
            if (liveNearbyMembers.Length > 0)
                forwardBase.RecordLiveMemberNear(Tick);

            var restEligibleMembers = GetValidCampaignMembers(campaign, blockedCampaignActorIds)
                .Where(member => IsWithinManhattanDistance(member.Pos, (forwardBase.X, forwardBase.Y), forwardBase.Radius))
                .ToArray();
            if (restEligibleMembers.Length > 0)
            {
                foreach (var member in restEligibleMembers)
                    member.ApplyStaminaDelta(_campaignLogisticsOptions.ForwardBaseRestStaminaPerTick);
                forwardBase.RecordRest(restEligibleMembers.Length);
                _campaignLogisticsCounters.RecordForwardBaseRest(restEligibleMembers.Length);
                continue;
            }

            if (liveNearbyMembers.Length == 0
                && Tick - forwardBase.LastLiveMemberNearTick >= _campaignLogisticsOptions.ForwardBaseNoMemberAbandonTicks)
            {
                MarkForwardBaseAbandoned(forwardBase, ForwardBaseCloseReasons.NoLiveMember);
                closedCampaignIds.Add(forwardBase.CampaignId);
            }
        }

        foreach (var campaign in _campaigns
                     .Where(campaign => campaign.Phase is CampaignPhase.Marching or CampaignPhase.Encounter)
                     .OrderBy(campaign => campaign.CampaignId))
        {
            if (closedCampaignIds.Contains(campaign.CampaignId))
                continue;
            if (HasActiveForwardBaseForCampaign(campaign))
                continue;

            TryEstablishForwardBase(campaign, blockedCampaignActorIds);
        }
    }

    private void MarkForwardBaseAbandoned(ForwardBaseState forwardBase, string reason)
    {
        forwardBase.MarkAbandoned(Tick, reason);
        _campaignLogisticsCounters.RecordForwardBaseAbandoned();
    }

    private bool HasActiveForwardBaseForCampaign(CampaignState campaign)
        => _forwardBases.Any(forwardBase =>
            forwardBase.IsActive
            && forwardBase.CampaignId == campaign.CampaignId
            && forwardBase.ArmyId == campaign.ArmyId);

    private int CountActiveForwardBases(Faction ownerFaction)
        => _forwardBases.Count(forwardBase => forwardBase.OwnerFaction == ownerFaction && forwardBase.IsActive);

    private Person[] GetLiveAssignedCampaignMembersNearForwardBase(CampaignState campaign, ForwardBaseState forwardBase)
        => campaign.Army.MemberActorIds
            .Select(actorId => _world._people.FirstOrDefault(person => person.Id == actorId))
            .Where(person => person != null
                && person.Health > 0f
                && IsWithinManhattanDistance(person.Pos, (forwardBase.X, forwardBase.Y), forwardBase.Radius))
            .Select(person => person!)
            .OrderBy(person => person.Id)
            .ToArray();

    private bool TryEstablishForwardBase(CampaignState campaign, HashSet<int> blockedCampaignActorIds)
    {
        if (CountActiveForwardBases(campaign.OwnerFaction) >= _campaignLogisticsOptions.MaxActiveForwardBasesPerFaction)
        {
            _campaignLogisticsCounters.RecordForwardBaseBuildBlockedByCap();
            return false;
        }

        var members = GetValidCampaignMembers(campaign, blockedCampaignActorIds);
        var anchor = members.OrderBy(member => member.Id).FirstOrDefault();
        if (anchor == null)
            return false;

        var home = _world._colonies.FirstOrDefault(colony => colony.Id == campaign.OriginColonyId);
        if (home == null)
            return false;

        if (ManhattanDistance(home.Origin, anchor.Pos) < _campaignLogisticsOptions.ForwardBaseMinDistanceFromHome)
            return false;

        if (!TryResolveForwardBasePlacement(campaign, anchor.Pos, out var placement))
        {
            _campaignLogisticsCounters.RecordForwardBaseBuildBlockedByPlacement();
            return false;
        }

        if (!HasForwardBaseRoutePreflight(home, placement))
        {
            _campaignLogisticsCounters.RecordForwardBaseBuildBlockedByRouteBudget();
            return false;
        }

        var forwardBase = new ForwardBaseState(
            _nextForwardBaseId++,
            campaign.OwnerFaction,
            campaign.OriginColonyId,
            campaign.CampaignId,
            campaign.ArmyId,
            Tick,
            placement.x,
            placement.y,
            _campaignLogisticsOptions.ForwardBaseRadius);
        _forwardBases.Add(forwardBase);
        campaign.Army.SetRallyPoint(placement.x, placement.y);
        _campaignLogisticsCounters.RecordForwardBaseEstablished();
        return true;
    }

    private bool TryResolveForwardBasePlacement(CampaignState campaign, (int x, int y) anchor, out (int x, int y) placement)
    {
        var maxRadius = Math.Max(0, _campaignLogisticsOptions.ForwardBaseRadius);
        for (var radius = 0; radius <= maxRadius; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > radius)
                        continue;

                    int x = anchor.x + dx;
                    int y = anchor.y + dy;
                    if (_world.IsMovementBlocked(x, y, campaign.OriginColonyId))
                        continue;

                    placement = (x, y);
                    return true;
                }
            }
        }

        placement = default;
        return false;
    }

    private bool HasForwardBaseRoutePreflight(Colony ownerColony, (int x, int y) placement)
    {
        var grid = new NavigationGrid(_world);
        var path = NavigationPathfinder.FindPath(
            grid,
            ownerColony.Origin,
            placement,
            ownerColony.Id,
            _campaignLogisticsOptions.RoutePathMaxExpansions,
            out var budgetExceeded);

        return !budgetExceeded && path.Count > 1;
    }

    private static bool IsWithinManhattanDistance((int x, int y) left, (int x, int y) right, int maxDistance)
        => ManhattanDistance(left, right) <= Math.Max(0, maxDistance);

    private static int ManhattanDistance((int x, int y) left, (int x, int y) right)
        => Math.Abs(left.x - right.x) + Math.Abs(left.y - right.y);

    private void AdvanceScoutIntel()
    {
        ExpireScoutIntel();

        var scouts = _world._people
            .Where(IsLiveScout)
            .OrderBy(person => person.Id)
            .ToArray();
        if (scouts.Length == 0)
            return;

        var targetColonies = _world._colonies
            .OrderBy(colony => (int)colony.Faction)
            .ThenBy(colony => colony.Id)
            .ToArray();

        foreach (var scout in scouts)
        {
            var ownerFaction = scout.Home.Faction;
            var radius = GetScoutIntelRadius(scout.Home);
            foreach (var target in targetColonies)
            {
                if (target.Faction == ownerFaction)
                    continue;

                _campaignLogisticsCounters.RecordScoutObservationPass();
                if (!IsScoutIntelTargetRelation(ownerFaction, target.Faction))
                {
                    _campaignLogisticsCounters.RecordScoutObservationSkippedByRelation();
                    continue;
                }
                if (!IsWithinManhattanDistance(scout.Pos, target.Origin, radius))
                {
                    _campaignLogisticsCounters.RecordScoutObservationSkippedByRadius();
                    continue;
                }

                RecordScoutColonyIntel(ownerFaction, target, scout.Id);
            }
        }
    }

    private void ExpireScoutIntel()
    {
        foreach (var intel in _scoutIntel.OrderBy(intel => intel.IntelId))
        {
            if (intel.TryMarkExpired(Tick))
                _campaignLogisticsCounters.RecordScoutIntelExpired();
        }
    }

    private static bool IsLiveScout(Person person)
        => person.Health > 0f && person.HasRole(PersonRole.Scout);

    private int GetScoutIntelRadius(Colony ownerColony)
        => Math.Min(
            _campaignLogisticsOptions.ScoutIntelMaxRadius,
            _campaignLogisticsOptions.ScoutIntelBaseRadius + Math.Max(0, ownerColony.ScoutRadiusBonus));

    private bool IsScoutIntelTargetRelation(Faction ownerFaction, Faction observedFaction)
        => _world.GetFactionStance(ownerFaction, observedFaction) is Stance.Hostile or Stance.War;

    private void RecordScoutColonyIntel(Faction ownerFaction, Colony target, int sourceActorId)
    {
        const ScoutIntelObservationKind observationKind = ScoutIntelObservationKind.Colony;
        var existing = _scoutIntel.FirstOrDefault(intel =>
            intel.IsActive(Tick)
            && intel.OwnerFaction == ownerFaction
            && intel.ObservedColonyId == target.Id
            && intel.ObservationKind == observationKind);
        if (existing != null)
        {
            existing.Refresh(
                target.Origin.x,
                target.Origin.y,
                sourceActorId,
                Tick,
                _campaignLogisticsOptions.ScoutIntelTtlTicks,
                _campaignLogisticsOptions.ScoutIntelConfidence);
            _campaignLogisticsCounters.RecordScoutIntelRefreshed();
            return;
        }

        _scoutIntel.Add(new ScoutIntelState(
            _nextScoutIntelId++,
            ownerFaction,
            target.Faction,
            target.Id,
            observationKind,
            target.Origin.x,
            target.Origin.y,
            sourceActorId,
            Tick,
            _campaignLogisticsOptions.ScoutIntelTtlTicks,
            _campaignLogisticsOptions.ScoutIntelConfidence));
        _campaignLogisticsCounters.RecordScoutIntelObserved();
    }

    private void AdvanceSupplyConvoys()
    {
        foreach (var convoy in _supplyConvoys.Where(convoy => convoy.IsActive).OrderBy(convoy => convoy.ConvoyId).ToArray())
        {
            var targetCampaign = _campaigns.FirstOrDefault(campaign =>
                campaign.CampaignId == convoy.TargetCampaignId
                && campaign.ArmyId == convoy.TargetArmyId
                && campaign.OwnerFaction == convoy.OwnerFaction);
            if (targetCampaign == null || targetCampaign.Phase == CampaignPhase.Resolved)
            {
                convoy.MarkFailed(Tick);
                _campaignLogisticsCounters.RecordConvoyFailed();
                continue;
            }

            if (IsSupplyConvoyAtTarget(convoy))
            {
                if (HasLiveSupplyConvoyRecipient(convoy, targetCampaign))
                    DeliverSupplyConvoy(convoy, targetCampaign);
                else
                    convoy.RouteCounters.RecordNoProgress();
                continue;
            }

            var nextStep = GetNextSupplyConvoyStep(convoy);
            if (!nextStep.HasValue)
            {
                convoy.RouteCounters.RecordNoProgress();
                convoy.MarkFailed(Tick);
                _campaignLogisticsCounters.RecordConvoyRouteBudgetExhausted();
                _campaignLogisticsCounters.RecordConvoyFailed();
                continue;
            }

            convoy.BeginMarch();
            convoy.MoveTo(nextStep.Value.x, nextStep.Value.y);
            convoy.RouteCounters.RecordProgress();
            if (convoy.RouteCache.PeekNext() == nextStep.Value)
                convoy.RouteCache.Advance();

            if (IsSupplyConvoyAtTarget(convoy) && HasLiveSupplyConvoyRecipient(convoy, targetCampaign))
                DeliverSupplyConvoy(convoy, targetCampaign);
        }
    }

    private bool IsSupplyConvoyAtTarget(SupplyConvoyState convoy)
        => Math.Abs(convoy.CurrentX - convoy.TargetX) + Math.Abs(convoy.CurrentY - convoy.TargetY) <= 1;

    private void DeliverSupplyConvoy(SupplyConvoyState convoy, CampaignState targetCampaign)
    {
        if (!convoy.IsActive || targetCampaign.Phase == CampaignPhase.Resolved || !HasLiveSupplyConvoyRecipient(convoy, targetCampaign))
            return;

        targetCampaign.Army.RationPoolState.AddRations(convoy.PayloadFood);
        convoy.MarkDelivered(Tick);
        _campaignLogisticsCounters.RecordConvoyDelivered();
    }

    private bool HasLiveSupplyConvoyRecipient(SupplyConvoyState convoy, CampaignState targetCampaign)
    {
        foreach (var actorId in targetCampaign.Army.MemberActorIds)
        {
            var member = _world._people.FirstOrDefault(person => person.Id == actorId && person.Health > 0f);
            if (member == null)
                continue;

            if (Math.Abs(member.Pos.x - convoy.CurrentX) + Math.Abs(member.Pos.y - convoy.CurrentY) <= 1)
                return true;
        }

        return false;
    }

    private (int x, int y)? GetNextSupplyConvoyStep(SupplyConvoyState convoy)
    {
        var target = (x: convoy.TargetX, y: convoy.TargetY);
        var topologyVersion = _world.NavigationTopologyVersion;
        if (convoy.RouteCache.IsValid(target, topologyVersion))
        {
            convoy.RouteCounters.RecordPathCacheHit();
        }
        else
        {
            BuildSupplyConvoyRouteCache(convoy, target, topologyVersion);
        }

        var next = convoy.RouteCache.PeekNext();
        if (!next.HasValue)
            return null;

        if (!_world.IsMovementBlocked(next.Value.x, next.Value.y, convoy.HomeColonyId))
            return next;

        convoy.RouteCache.Invalidate();
        BuildSupplyConvoyRouteCache(convoy, target, _world.NavigationTopologyVersion);
        next = convoy.RouteCache.PeekNext();
        if (!next.HasValue)
            return null;

        return _world.IsMovementBlocked(next.Value.x, next.Value.y, convoy.HomeColonyId)
            ? null
            : next;
    }

    private void BuildSupplyConvoyRouteCache(SupplyConvoyState convoy, (int x, int y) target, int topologyVersion)
    {
        convoy.RouteCounters.RecordPathRequest();
        convoy.RouteCounters.RecordRouteRecompute();
        var grid = new NavigationGrid(_world);
        var path = NavigationPathfinder.FindPath(
            grid,
            (convoy.CurrentX, convoy.CurrentY),
            target,
            convoy.HomeColonyId,
            _campaignLogisticsOptions.RoutePathMaxExpansions,
            out var budgetExceeded);

        if (budgetExceeded || path.Count <= 1)
        {
            convoy.RouteCache.Invalidate();
            return;
        }

        convoy.RouteCache.Set(target, topologyVersion, path);
    }

    private void QueueCampaignSiegePressureForActiveEncounters(float dt, HashSet<int> blockedCampaignActorIds)
    {
        if (dt <= 0f)
            return;

        var encounterCampaigns = _campaigns
            .Where(campaign => campaign.Phase == CampaignPhase.Encounter)
            .OrderBy(campaign => campaign.CreatedTick)
            .ThenBy(campaign => campaign.CampaignId)
            .ToArray();
        if (encounterCampaigns.Length == 0)
            return;

        foreach (var campaign in encounterCampaigns)
            _campaignLogisticsCounters.RecordSiegeUnitEncounterCampaign();

        if (!IsCampaignSiegeResolverEnabled())
        {
            _campaignLogisticsCounters.RecordSiegeUnitResolverDisabled(encounterCampaigns.Length);
            foreach (var campaign in encounterCampaigns)
            {
                campaign.Siege.SuppressActivePressure(Tick);
                MarkCampaignSiegeUnitsInactive(campaign, SiegeUnitInactiveReasons.SiegeDisabled);
            }
            return;
        }

        var pressureCandidates = new List<CampaignSiegePressureCandidate>();
        var noTargetCandidates = new List<CampaignState>();
        foreach (var campaign in encounterCampaigns)
        {
            if (campaign.Siege.Status == CampaignSiegeStatus.Breached)
                continue;

            if (!HasCompletePressureCapableEncounterRoster(campaign, blockedCampaignActorIds))
            {
                campaign.Siege.SuppressActivePressure(Tick);
                MarkCampaignSiegeUnitsInactive(campaign, SiegeUnitInactiveReasons.CampaignInvalid);
                continue;
            }

            var attacker = _world._colonies.FirstOrDefault(colony => colony.Id == campaign.OriginColonyId);
            if (attacker == null)
            {
                campaign.Siege.SuppressActivePressure(Tick);
                MarkCampaignSiegeUnitsInactive(campaign, SiegeUnitInactiveReasons.CampaignInvalid);
                continue;
            }

            if (!TrySelectCampaignSiegeTarget(campaign, out var target))
            {
                if (TryObserveRecentCampaignBreach(campaign))
                    continue;

                noTargetCandidates.Add(campaign);
                continue;
            }

            pressureCandidates.Add(new CampaignSiegePressureCandidate(campaign, attacker, target));
        }

        var pressurePairs = new HashSet<(int attackerColonyId, int defenderColonyId)>();
        foreach (var group in pressureCandidates.GroupBy(candidate => GetCampaignSiegePair(candidate.Campaign)))
        {
            var driver = group
                .OrderBy(candidate => candidate.Campaign.CreatedTick)
                .ThenBy(candidate => candidate.Campaign.CampaignId)
                .First();
            pressurePairs.Add(group.Key);

            driver.Campaign.Siege.RecordPressure(driver.Target.Id, driver.Target.Owner.Id, Tick);
            ApplyCampaignSiegeUnitEffects(driver.Campaign, driver.Attacker, driver.Target);
            _world.QueueExternalSiegePressure(driver.Attacker, driver.Target);

            foreach (var nonDriver in group.Where(candidate => !ReferenceEquals(candidate.Campaign, driver.Campaign)))
            {
                nonDriver.Campaign.Siege.SuppressActivePressure(Tick);
                MarkCampaignSiegeUnitsInactive(nonDriver.Campaign, SiegeUnitInactiveReasons.CampaignInvalid);
            }
        }

        foreach (var group in noTargetCandidates.GroupBy(GetCampaignSiegePair))
        {
            if (pressurePairs.Contains(group.Key))
            {
                foreach (var campaign in group)
                {
                    campaign.Siege.SuppressActivePressure(Tick);
                    MarkCampaignSiegeUnitsInactive(campaign, SiegeUnitInactiveReasons.CampaignInvalid);
                }
                continue;
            }

            var reporter = group
                .OrderBy(campaign => campaign.CreatedTick)
                .ThenBy(campaign => campaign.CampaignId)
                .First();
            reporter.Siege.MarkNoTarget(Tick);
            _campaignLogisticsCounters.RecordSiegeUnitNoTarget();
            MarkCampaignSiegeUnitsInactive(reporter, SiegeUnitInactiveReasons.CampaignInvalid);

            foreach (var nonReporter in group.Where(campaign => !ReferenceEquals(campaign, reporter)))
            {
                nonReporter.Siege.SuppressActivePressure(Tick);
                MarkCampaignSiegeUnitsInactive(nonReporter, SiegeUnitInactiveReasons.CampaignInvalid);
            }
        }
    }

    private void ApplyCampaignSiegeUnitEffects(CampaignState campaign, Colony attacker, DefensiveStructure target)
    {
        if (!CanCampaignUseDedicatedSiegeUnits(attacker))
        {
            _campaignLogisticsCounters.RecordSiegeUnitTechLocked();
            return;
        }

        var existingActiveCount = _siegeUnits.Count(unit =>
            unit.IsActive
            && unit.CampaignId == campaign.CampaignId
            && unit.ArmyId == campaign.ArmyId);
        var units = EnsureCampaignSiegeUnits(campaign, attacker, target);
        var activeCount = units.Count(unit => unit.IsActive);
        _campaignLogisticsCounters.RecordSiegeUnitAlreadyPresent(existingActiveCount);
        _campaignLogisticsCounters.RecordSiegeUnitsSpawned(Math.Max(0, activeCount - existingActiveCount));
        var anchor = GetSiegeUnitAnchor(campaign);
        foreach (var unit in units.Where(unit => unit.IsActive).OrderBy(unit => unit.Kind))
        {
            unit.RefreshPosition(anchor.x, anchor.y, target.Id, target.Pos.x, target.Pos.y);
            switch (unit.Kind)
            {
                case SiegeUnitKind.Ram:
                    if (!target.IsDestroyed)
                    {
                        var damage = IsWallOrGate(target.Kind) ? RamWallGatePressureDamage : RamStructurePressureDamage;
                        _world.TryDamageDefensiveStructure(target.Pos, damage, attacker);
                    }
                    unit.RecordAction(IsWallOrGate(target.Kind) ? "ram_wall_gate_pressure" : "ram_structure_pressure", Tick);
                    break;

                case SiegeUnitKind.SiegeTower:
                    if (!target.IsDestroyed && IsWallOrGate(target.Kind))
                        _world.TryDamageDefensiveStructure(target.Pos, 4f, attacker);
                    unit.RecordAction(IsWallOrGate(target.Kind) ? "siege_tower_access_pressure" : "siege_tower_support", Tick);
                    break;

                case SiegeUnitKind.MobileCatapult:
                    if (!target.IsDestroyed)
                        _world.TryDamageDefensiveStructure(target.Pos, MobileCatapultPressureDamage, attacker);
                    unit.RecordAction("mobile_catapult_ranged_pressure", Tick);
                    break;
            }
        }

        _campaignLogisticsCounters.RecordSiegeUnitActionTicks(activeCount);
    }

    private IReadOnlyList<SiegeUnitState> EnsureCampaignSiegeUnits(CampaignState campaign, Colony attacker, DefensiveStructure target)
    {
        var anchor = GetSiegeUnitAnchor(campaign);
        var units = new List<SiegeUnitState>();
        foreach (var kind in new[] { SiegeUnitKind.Ram, SiegeUnitKind.SiegeTower, SiegeUnitKind.MobileCatapult })
        {
            var existing = _siegeUnits.FirstOrDefault(unit =>
                unit.IsActive
                && unit.CampaignId == campaign.CampaignId
                && unit.ArmyId == campaign.ArmyId
                && unit.Kind == kind);
            if (existing == null)
            {
                existing = new SiegeUnitState(
                    _nextSiegeUnitId++,
                    campaign.CampaignId,
                    campaign.ArmyId,
                    attacker.Faction,
                    kind,
                    Tick,
                    anchor.x,
                    anchor.y,
                    target.Id,
                    target.Pos.x,
                    target.Pos.y,
                    GetSiegeUnitMaxHealth(kind));
                _siegeUnits.Add(existing);
            }

            units.Add(existing);
        }

        return units;
    }

    private (int x, int y) GetSiegeUnitAnchor(CampaignState campaign)
    {
        var anchor = campaign.Army.MemberActorIds
            .Select(actorId => _world._people.FirstOrDefault(person => person.Id == actorId && person.Health > 0f))
            .Where(person => person != null)
            .Select(person => ((int x, int y)?)person!.Pos)
            .FirstOrDefault();
        return anchor.HasValue
            ? anchor.Value
            : (campaign.RouteIntent.TargetX, campaign.RouteIntent.TargetY);
    }

    private static bool CanCampaignUseDedicatedSiegeUnits(Colony attacker)
        => attacker.UnlockedTechs.Contains("siege_craft");

    private static float GetSiegeUnitMaxHealth(SiegeUnitKind kind)
        => kind switch
        {
            SiegeUnitKind.Ram => 160f,
            SiegeUnitKind.SiegeTower => 140f,
            SiegeUnitKind.MobileCatapult => 120f,
            _ => 100f
        };

    private static bool IsWallOrGate(DefensiveStructureKind kind)
        => kind is DefensiveStructureKind.WoodWall
            or DefensiveStructureKind.StoneWall
            or DefensiveStructureKind.ReinforcedWall
            or DefensiveStructureKind.Gate;

    private void MarkCampaignSiegeUnitsInactive(CampaignState campaign, string reason)
    {
        foreach (var unit in _siegeUnits.Where(unit =>
                     unit.IsActive
                     && unit.CampaignId == campaign.CampaignId
                     && unit.ArmyId == campaign.ArmyId))
        {
            unit.MarkInactive(Tick, reason);
        }
    }

    private bool HasActiveCampaignSiegeUnits(CampaignState campaign)
        => _siegeUnits.Any(unit =>
            unit.IsActive
            && unit.CampaignId == campaign.CampaignId
            && unit.ArmyId == campaign.ArmyId);

    private void DeactivateInvalidSiegeUnits()
    {
        foreach (var unit in _siegeUnits.Where(unit => unit.IsActive).ToArray())
        {
            var campaign = _campaigns.FirstOrDefault(candidate =>
                candidate.CampaignId == unit.CampaignId
                && candidate.ArmyId == unit.ArmyId);
            if (campaign == null)
            {
                unit.MarkInactive(Tick, SiegeUnitInactiveReasons.CampaignInvalid);
                continue;
            }

            if (campaign.Phase == CampaignPhase.Resolved)
                unit.MarkInactive(Tick, SiegeUnitInactiveReasons.CampaignResolved);
        }
    }

    private bool IsCampaignSiegeResolverEnabled()
        => _world.EnableSiege && _world.EnableCombatPrimitives;

    private bool HasCompletePressureCapableEncounterRoster(CampaignState campaign, HashSet<int> blockedCampaignActorIds)
        => campaign.Army.MemberActorIds
               .Select(actorId => _world._people.FirstOrDefault(person => person.Id == actorId))
               .Count(person => IsValidAssignedCampaignMember(person, blockedCampaignActorIds)) >= campaign.Army.RequestedMemberCount;

    private bool TrySelectCampaignSiegeTarget(CampaignState campaign, out DefensiveStructure target)
    {
        var reference = (x: campaign.RouteIntent.TargetX, y: campaign.RouteIntent.TargetY);
        var selected = _world.DefensiveStructures
            .Where(structure => !structure.IsDestroyed && structure.Owner.Id == campaign.TargetColonyId)
            .OrderBy(structure => Math.Abs(structure.Pos.x - reference.x) + Math.Abs(structure.Pos.y - reference.y))
            .ThenBy(structure => structure.Id)
            .FirstOrDefault();

        target = selected!;
        return selected != null;
    }

    private void SyncCampaignSiegeStates(HashSet<int> blockedCampaignActorIds)
    {
        if (!IsCampaignSiegeResolverEnabled())
        {
            foreach (var campaign in _campaigns.Where(campaign => campaign.Phase == CampaignPhase.Encounter))
            {
                campaign.Siege.SuppressActivePressure(Tick);
                MarkCampaignSiegeUnitsInactive(campaign, SiegeUnitInactiveReasons.SiegeDisabled);
            }
            return;
        }

        var activeSieges = _world.GetActiveSieges();
        var recentBreaches = _world.GetRecentBreaches();

        foreach (var campaign in _campaigns)
        {
            if (campaign.Phase != CampaignPhase.Encounter)
                continue;

            if (campaign.Siege.LastPressureTick == Tick)
            {
                var matchingSiege = activeSieges
                    .Where(siege => IsMatchingCampaignActiveSiege(campaign, siege))
                    .OrderBy(siege => siege.TargetStructureId == campaign.Siege.TargetStructureId ? 0 : 1)
                    .ThenBy(siege => siege.SiegeId)
                    .FirstOrDefault();
                if (matchingSiege != null)
                    campaign.Siege.ObserveActiveSiege(matchingSiege.SiegeId, matchingSiege.BreachCount, Tick);
            }

            if (campaign.Siege.Status == CampaignSiegeStatus.Breached)
                continue;

            if (campaign.Siege.Status == CampaignSiegeStatus.NoTarget)
                continue;

            var hasCompletePressureCapableRoster = HasCompletePressureCapableEncounterRoster(campaign, blockedCampaignActorIds);
            if (!hasCompletePressureCapableRoster && HasActiveCampaignSiegeUnits(campaign))
            {
                campaign.Siege.SuppressActivePressure(Tick);
                MarkCampaignSiegeUnitsInactive(campaign, SiegeUnitInactiveReasons.CampaignInvalid);
                continue;
            }

            if (campaign.Siege.LastPressureTick == Tick)
                continue;

            if (!hasCompletePressureCapableRoster)
            {
                campaign.Siege.SuppressActivePressure(Tick);
                MarkCampaignSiegeUnitsInactive(campaign, SiegeUnitInactiveReasons.CampaignInvalid);
                continue;
            }

            TryObserveRecentCampaignBreach(campaign, recentBreaches);
        }
    }

    private void ResolveCampaignEncounters()
    {
        var currentBreachedPairs = _campaigns
            .Where(campaign => campaign.Phase == CampaignPhase.Encounter
                && campaign.Siege.Status == CampaignSiegeStatus.Breached)
            .Select(campaign => (campaign.OwnerFaction, campaign.TargetFaction))
            .ToHashSet();

        foreach (var campaign in _campaigns
                     .Where(campaign => campaign.Phase == CampaignPhase.Encounter)
                     .OrderBy(campaign => campaign.CreatedTick)
                     .ThenBy(campaign => campaign.CampaignId))
        {
            if (!TryBuildCampaignResolution(campaign, currentBreachedPairs, out var resolution))
                continue;

            campaign.Resolve(resolution);
        }
    }

    private bool TryBuildCampaignResolution(
        CampaignState campaign,
        HashSet<(Faction OwnerFaction, Faction TargetFaction)> currentBreachedPairs,
        out CampaignResolutionApplication resolution)
    {
        resolution = null!;
        if (campaign.Resolution.IsResolved)
            return false;

        var attacker = _world._colonies.FirstOrDefault(colony => colony.Id == campaign.OriginColonyId);
        var defender = _world._colonies.FirstOrDefault(colony => colony.Id == campaign.TargetColonyId);
        if (attacker == null || defender == null)
            return false;

        CampaignResolutionKind kind;
        string reason;
        if (campaign.Siege.Status == CampaignSiegeStatus.Breached)
        {
            kind = CampaignResolutionKind.AttackerVictory;
            reason = CampaignResolutionReasons.SiegeBreached;
        }
        else if (campaign.Siege.Status == CampaignSiegeStatus.NoTarget)
        {
            if (HasCurrentSameOrderedPairBreach(campaign, currentBreachedPairs))
                return false;

            kind = CampaignResolutionKind.DefenderHeld;
            reason = CampaignResolutionReasons.NoTarget;
        }
        else if (campaign.RouteCounters.EncounterTicks >= CampaignResolutionPolicy.DefenderHeldEncounterTimeoutTicks)
        {
            if (HasCurrentSameOrderedPairBreach(campaign, currentBreachedPairs))
                return false;

            kind = CampaignResolutionKind.DefenderHeld;
            reason = CampaignResolutionReasons.DefenderTimeout;
        }
        else
        {
            return false;
        }

        int lootFood = 0;
        int lootWood = 0;
        int lootStone = 0;
        int lootGold = 0;
        int warScoreDelta;
        if (kind == CampaignResolutionKind.AttackerVictory)
        {
            lootFood = TransferCampaignLoot(defender, attacker, Resource.Food, CampaignResolutionPolicy.LootFoodCap);
            lootWood = TransferCampaignLoot(defender, attacker, Resource.Wood, CampaignResolutionPolicy.LootWoodCap);
            lootStone = TransferCampaignLoot(defender, attacker, Resource.Stone, CampaignResolutionPolicy.LootStoneCap);
            lootGold = TransferCampaignLoot(defender, attacker, Resource.Gold, CampaignResolutionPolicy.LootGoldCap);
            warScoreDelta = CampaignResolutionPolicy.AttackerVictoryWarScoreDelta;
        }
        else
        {
            warScoreDelta = CampaignResolutionPolicy.DefenderHeldWarScoreDelta;
        }

        int cumulativeWarScore = RecordCampaignWarScore(campaign.OwnerFaction, campaign.TargetFaction, warScoreDelta);
        bool peaceEligible = _world.GetFactionStance(campaign.OwnerFaction, campaign.TargetFaction) == Stance.War
            && cumulativeWarScore >= CampaignResolutionPolicy.PeaceEligibilityWarScoreThreshold;
        bool peaceApplied = false;
        var treatyKind = CampaignResolutionReasons.None;
        if (peaceEligible)
        {
            treatyKind = CampaignResolutionPolicy.CeasefireTreatyKind;
            peaceApplied = TryApplyCampaignResolutionCeasefire(campaign.OwnerFaction, campaign.TargetFaction, out _, out _);
        }

        resolution = new CampaignResolutionApplication(
            kind,
            reason,
            Tick,
            campaign.OwnerFaction,
            campaign.TargetFaction,
            campaign.OriginColonyId,
            campaign.TargetColonyId,
            campaign.Siege.TargetStructureId,
            lootFood,
            lootWood,
            lootStone,
            lootGold,
            warScoreDelta,
            cumulativeWarScore,
            peaceEligible,
            peaceApplied,
            treatyKind);
        return true;
    }

    private static bool HasCurrentSameOrderedPairBreach(
        CampaignState campaign,
        HashSet<(Faction OwnerFaction, Faction TargetFaction)> currentBreachedPairs)
        => currentBreachedPairs.Contains((campaign.OwnerFaction, campaign.TargetFaction));

    private int RecordCampaignWarScore(Faction attacker, Faction defender, int delta)
    {
        var key = CampaignWarScoreKey.From(attacker, defender);
        int next = _campaignWarScores.GetValueOrDefault(key, 0) + key.SignFor(attacker) * delta;
        _campaignWarScores[key] = next;
        return next * key.SignFor(attacker);
    }

    private bool TryApplyCampaignResolutionCeasefire(Faction proposer, Faction receiver, out Stance previous, out Stance current)
    {
        previous = _world.GetFactionStance(proposer, receiver);
        current = previous;
        if (previous != Stance.War)
            return false;

        var changed = _world.ProposeTreaty(
            proposer,
            receiver,
            CampaignResolutionPolicy.CeasefireTreatyKind,
            out previous,
            out current);
        if (changed)
            _world.AddExternalEvent($"[Campaign] Ceasefire after decisive campaign resolution: {proposer} -> {receiver}");

        return changed;
    }

    private static int TransferCampaignLoot(Colony source, Colony destination, Resource resource, int cap)
    {
        int available = Math.Max(0, source.Stock.GetValueOrDefault(resource, 0));
        int amount = Math.Min(Math.Max(0, cap), available);
        if (amount <= 0)
            return 0;

        source.Stock[resource] = available - amount;
        destination.Stock[resource] = destination.Stock.GetValueOrDefault(resource, 0) + amount;
        return amount;
    }

    private bool TryObserveRecentCampaignBreach(CampaignState campaign)
        => TryObserveRecentCampaignBreach(campaign, _world.GetRecentBreaches());

    private bool TryObserveRecentCampaignBreach(CampaignState campaign, IReadOnlyList<BreachState> recentBreaches)
    {
        var observed = false;
        foreach (var breach in recentBreaches
                     .Where(breach => IsMatchingCampaignBreach(campaign, breach))
                     .OrderBy(breach => breach.CreatedTick)
                     .ThenBy(breach => breach.StructureId))
        {
            campaign.Siege.ObserveBreach(breach.StructureId, breach.CreatedTick, Tick);
            observed = true;
        }

        return observed;
    }

    private static (int attackerColonyId, int defenderColonyId) GetCampaignSiegePair(CampaignState campaign)
        => (campaign.OriginColonyId, campaign.TargetColonyId);

    private static bool IsMatchingCampaignActiveSiege(CampaignState campaign, SiegeState siege)
        => siege.AttackerColonyId == campaign.OriginColonyId
           && siege.DefenderColonyId == campaign.TargetColonyId
           && siege.TargetStructureId == campaign.Siege.TargetStructureId;

    private static bool IsMatchingCampaignBreach(CampaignState campaign, BreachState breach)
        => breach.AttackerColonyId == campaign.OriginColonyId
           && breach.DefenderColonyId == campaign.TargetColonyId
           && campaign.Siege.TargetStructureId >= 0
           && breach.StructureId == campaign.Siege.TargetStructureId;

    private IReadOnlyList<Person> GetValidCampaignMembers(CampaignState campaign, HashSet<int> blockedCampaignActorIds)
        => campaign.Army.MemberActorIds
            .Select(actorId => _world._people.FirstOrDefault(person => person.Id == actorId))
            .Where(person => IsValidAssignedCampaignMember(person, blockedCampaignActorIds))
            .Select(person => person!)
            .OrderBy(person => person.Id)
            .ToArray();

    private (int x, int y)? GetNextCampaignMarchStep(CampaignState campaign, (int x, int y) start, (int x, int y) target)
    {
        var grid = new NavigationGrid(_world);
        int topologyVersion = _world.NavigationTopologyVersion;
        campaign.RouteCounters.RecordPathRequest();

        if (campaign.RouteCache.IsValid(target, topologyVersion))
        {
            campaign.RouteCounters.RecordPathCacheHit();
        }
        else
        {
            BuildCampaignRouteCache(campaign, grid, start, target, topologyVersion);
        }

        var next = campaign.RouteCache.PeekNext();
        if (!next.HasValue)
            return null;

        campaign.RouteCounters.RecordBlockedMovementCheck();
        if (!_world.IsMovementBlocked(next.Value.x, next.Value.y, campaign.OriginColonyId))
            return next;

        campaign.RouteCache.Invalidate();
        BuildCampaignRouteCache(campaign, grid, start, target, _world.NavigationTopologyVersion);
        next = campaign.RouteCache.PeekNext();
        if (!next.HasValue)
            return null;

        campaign.RouteCounters.RecordBlockedMovementCheck();
        return _world.IsMovementBlocked(next.Value.x, next.Value.y, campaign.OriginColonyId)
            ? null
            : next;
    }

    private void BuildCampaignRouteCache(
        CampaignState campaign,
        NavigationGrid grid,
        (int x, int y) start,
        (int x, int y) target,
        int topologyVersion,
        int maxExpansions = CampaignPathMaxExpansions)
    {
        campaign.RouteCounters.RecordRouteRecompute();
        var path = NavigationPathfinder.FindPath(
            grid,
            start,
            target,
            campaign.OriginColonyId,
            maxExpansions,
            out var budgetExceeded);

        if (budgetExceeded || path.Count <= 1)
        {
            campaign.RouteCache.Invalidate();
            return;
        }

        campaign.RouteCache.Set(target, topologyVersion, path);
    }

    private bool TryResolveCampaignMarchObjective(CampaignState campaign, out CampaignMarchObjective objective)
    {
        var origin = (x: campaign.RouteIntent.TargetX, y: campaign.RouteIntent.TargetY);
        int maxRadius = Math.Max(_world.Width, _world.Height);
        for (var radius = 0; radius <= maxRadius; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > radius)
                        continue;

                    int x = origin.x + dx;
                    int y = origin.y + dy;
                    var candidate = (x, y);
                    if (candidate != origin && !IsCampaignFallbackObjectiveAllowed(campaign, candidate))
                        continue;
                    if (_world.IsMovementBlocked(x, y, campaign.OriginColonyId))
                        continue;

                    objective = new CampaignMarchObjective(candidate, UsesFallback: candidate != origin);
                    return true;
                }
            }
        }

        objective = default;
        return false;
    }

    private static bool IsCampaignFallbackObjectiveAllowed(CampaignState campaign, (int x, int y) candidate)
        => Math.Abs(candidate.x - campaign.RouteIntent.OriginX) + Math.Abs(candidate.y - campaign.RouteIntent.OriginY) > 1;

    private static bool IsCampaignAtEncounterObjective((int x, int y) position, CampaignMarchObjective objective)
        => Math.Abs(position.x - objective.MovementTarget.x) + Math.Abs(position.y - objective.MovementTarget.y) <= 1;

    private static void TickCampaignMarchSupply(CampaignState campaign, IReadOnlyList<Person> members, float dt, long tick)
    {
        if (dt <= 0f || members.Count == 0)
            return;

        int supplyTick = tick > int.MaxValue ? int.MaxValue : (int)Math.Max(0, tick);

        if (campaign.Army.RationPoolState.RationPoolFood > 0)
        {
            var result = ArmySupplyCarrierModel.TickRationPool(
                members,
                campaign.Army.SupplyState,
                campaign.Army.CarrierState,
                campaign.Army.RationPoolState,
                supplyTick,
                dt);
            campaign.Army.Wave9Evidence.RecordSupplyTick(result);
            return;
        }

        var carriedResult = ArmySupplyCarrierModel.TickCarriedInventory(
            members,
            campaign.Army.SupplyState,
            campaign.Army.CarrierState,
            supplyTick,
            dt);
        campaign.Army.Wave9Evidence.RecordSupplyTick(carriedResult);
    }

    private void RecordCampaignWave9PhaseTicks()
    {
        foreach (var campaign in _campaigns)
            campaign.RecordWave9PhaseTick();
    }

    private DirectorRenderState BuildDirectorRenderState()
    {
        var activeBeats = _directorState.ActiveBeats
            .Select(beat => new DirectorActiveBeatRenderData(
                beat.BeatId,
                beat.Text,
                beat.Severity.ToString(),
                beat.RemainingTicks,
                beat.TotalTicks))
            .ToList();

        var activeDirectives = _directorState.ActiveDirectives
            .Select(directive => new DirectorActiveDirectiveRenderData(
                directive.ColonyId,
                directive.Directive,
                directive.RemainingTicks,
                directive.TotalTicks))
            .ToList();

        var activeModifiers = _world.GetActiveDomainModifiers()
            .Select(modifier => new DirectorDomainModifierRenderData(
                modifier.SourceId,
                modifier.Domain.ToString(),
                modifier.BaseModifier,
                modifier.EffectiveModifier,
                modifier.RemainingTicks,
                modifier.TotalDurationTicks))
            .ToList();

        var activeBiases = _world._colonies
            .SelectMany(colony => _world.GetActiveGoalBiases(colony.Id)
                .Select(bias => new DirectorGoalBiasRenderData(
                    bias.ColonyId,
                    bias.SourceId,
                    bias.GoalCategory,
                    bias.BaseWeight,
                    bias.EffectiveWeight,
                    bias.RemainingTicks,
                    bias.TotalDurationTicks,
                    bias.IsBlendActive)))
            .OrderBy(bias => bias.ColonyId)
            .ThenBy(bias => bias.GoalCategory)
            .ToList();

        var pendingChains = _directorState.PendingCausalChains
            .Select(chain => new DirectorPendingChainRenderData(
                chain.ParentBeatId,
                chain.Status,
                chain.ConditionSummary,
                chain.FollowUpBeatId,
                chain.FollowUpSummary,
                chain.RemainingWindowTicks,
                chain.TriggerCount,
                chain.LastFailureMessage))
            .ToList();

        var stageMarker = _directorExecutionState.Stage;
        var outputMode = _directorExecutionState.EffectiveOutputMode;
        var outputModeSource = _directorExecutionState.EffectiveOutputModeSource;

        return new DirectorRenderState(
            StageMarker: stageMarker,
            OutputMode: outputMode,
            OutputModeSource: outputModeSource,
            ApplyStatus: _directorExecutionState.ApplyStatus,
            BeatCooldownRemainingTicks: _directorState.BeatCooldownRemainingTicks,
            MajorBeatCooldownRemainingTicks: _directorState.MajorBeatCooldownRemainingTicks,
            EpicBeatCooldownRemainingTicks: _directorState.EpicBeatCooldownRemainingTicks,
            MaxInfluenceBudget: _directorState.MaxInfluenceBudget,
            RemainingInfluenceBudget: _directorState.RemainingInfluenceBudget,
            LastCheckpointBudgetUsed: _directorState.LastCheckpointBudgetUsed,
            LastBudgetCheckpointTick: _directorState.LastBudgetCheckpointTick,
            HasBudgetData: _directorState.HasBudgetData,
            ActiveBeats: activeBeats,
            ActiveDirectives: activeDirectives,
            PendingChains: pendingChains,
            ActiveDomainModifiers: activeModifiers,
            ActiveGoalBiases: activeBiases,
            LastActionStatus: LastDirectorActionStatus);
    }

    public void PrepareDirectorCheckpointBudget(double maxBudget, long tick)
    {
        _directorState.BeginCheckpointBudget(tick, maxBudget);
    }

    public void RecordDirectorCheckpointBudgetUsed(double budgetUsed, long tick)
    {
        _directorState.ApplyCheckpointBudgetUsed(tick, budgetUsed);
    }

    public void SetDirectorExecutionState(
        string effectiveOutputMode,
        string effectiveOutputModeSource,
        string stage,
        long tick,
        bool isDirectorGoal,
        string applyStatus = "applied",
        string? actionStatus = null)
    {
        _directorExecutionState = new DirectorExecutionState(
            EffectiveOutputMode: NormalizeOutputMode(effectiveOutputMode),
            EffectiveOutputModeSource: string.IsNullOrWhiteSpace(effectiveOutputModeSource) ? "unknown" : effectiveOutputModeSource.Trim().ToLowerInvariant(),
            Stage: string.IsNullOrWhiteSpace(stage) ? "not_triggered" : stage.Trim(),
            Tick: tick,
            IsDirectorGoal: isDirectorGoal,
            ApplyStatus: NormalizeApplyStatus(applyStatus));

        if (!string.IsNullOrWhiteSpace(actionStatus))
            LastDirectorActionStatus = actionStatus.Trim();
    }

    private static string NormalizeOutputMode(string? outputMode)
    {
        var normalized = string.IsNullOrWhiteSpace(outputMode) ? "unknown" : outputMode.Trim().ToLowerInvariant();
        return normalized is "unknown" or "both" or "story_only" or "nudge_only" or "off"
            ? normalized
            : "unknown";
    }

    private static string NormalizeApplyStatus(string? applyStatus)
    {
        var normalized = string.IsNullOrWhiteSpace(applyStatus) ? "applied" : applyStatus.Trim().ToLowerInvariant();
        return normalized is "not_triggered" or "applied" or "apply_failed" or "request_failed"
            ? normalized
            : "applied";
    }

    public AiDebugSnapshot GetAiDebugSnapshot() => _latestAiDebugSnapshot;

    public void CycleTrackedNpc(int delta)
    {
        var tracked = GetTrackedDecisions();
        if (tracked.Count == 0)
            return;

        var current = ResolveSelectedDecision(tracked);
        var currentIndex = current == null
            ? 0
            : tracked.FindIndex(decision => decision.ActorId == current.ActorId);
        if (currentIndex < 0)
            currentIndex = 0;

        _manualTracking = true;
        _trackedNpcCursor = NormalizeTrackedIndex(currentIndex + Math.Sign(delta), tracked.Count);
        _trackedActorId = tracked[_trackedNpcCursor].ActorId;
        RefreshAiDebugSnapshot();
    }

    public void ResetTrackedNpc()
    {
        _manualTracking = false;
        _trackedNpcCursor = 0;
        _trackedActorId = -1;
        RefreshAiDebugSnapshot();
    }

    public int NormalizeColonyIndex(int index)
    {
        if (ColonyCount == 0)
            return 0;

        var normalized = index % ColonyCount;
        if (normalized < 0)
            normalized += ColonyCount;
        return normalized;
    }

    public int GetColonyId(int index)
    {
        if (ColonyCount == 0)
            return -1;

        var colony = _world._colonies[NormalizeColonyIndex(index)];
        return colony.Id;
    }

    public IReadOnlyList<string> GetLockedTechNames(int colonyIndex)
    {
        if (ColonyCount == 0)
            return Array.Empty<string>();

        var colony = _world._colonies[NormalizeColonyIndex(colonyIndex)];
        return TechTree.Techs
            .Where(t => !colony.UnlockedTechs.Contains(t.Id))
            .Select(t => t.Name)
            .ToList();
    }

    public void UnlockLockedTechBySlot(int colonyIndex, int slot)
    {
        if (ColonyCount == 0)
        {
            LastTechActionStatus = "No colonies available";
            return;
        }

        var colony = _world._colonies[NormalizeColonyIndex(colonyIndex)];
        var locked = TechTree.Techs
            .Where(t => !colony.UnlockedTechs.Contains(t.Id))
            .ToList();

        if (slot < 0 || slot >= locked.Count)
        {
            LastTechActionStatus = "Invalid tech slot";
            return;
        }

        var selected = locked[slot];
        var result = TechTree.TryUnlock(selected.Id, _world, colony);
        LastTechActionStatus = result.Success
            ? $"Unlocked: {selected.Name}"
            : $"Tech blocked: {selected.Name} ({result.Reason})";
    }

    public JsonObject BuildRefinerySnapshot()
    {
        var colonies = new JsonArray();
        foreach (var colony in _world._colonies)
        {
            colonies.Add(new JsonObject
            {
                ["id"] = colony.Id,
                ["unlockedTechCount"] = colony.UnlockedTechs.Count,
                ["houseCount"] = colony.HouseCount
            });
        }

        return new JsonObject
        {
            ["world"] = new JsonObject
            {
                ["width"] = _world.Width,
                ["height"] = _world.Height,
                ["peopleCount"] = _world._people.Count,
                ["colonyCount"] = _world._colonies.Count,
                ["foodYield"] = _world.FoodYield,
                ["woodYield"] = _world.WoodYield,
                ["stoneYield"] = _world.StoneYield,
                ["ironYield"] = _world.IronYield,
                ["goldYield"] = _world.GoldYield
            },
            ["colonies"] = colonies,
            ["director"] = BuildDirectorSnapshotJson()
        };
    }

    public bool IsKnownTech(string techId) => TechTree.Techs.Any(t => t.Id == techId);

    public bool IsKnownDirectorDirective(string directive)
    {
        return !string.IsNullOrWhiteSpace(directive) && KnownDirectorDirectives.Contains(directive);
    }

    public void UnlockTechForPrimaryColony(string techId)
    {
        if (ColonyCount == 0)
            throw new InvalidOperationException("Cannot unlock tech: world has no colonies.");

        var colony = _world._colonies[0];
        var result = TechTree.TryUnlock(techId, _world, colony);
        if (!result.Success)
            throw new InvalidOperationException($"Cannot unlock tech '{techId}': {result.Reason}");
    }

    public void ApplyStoryBeat(
        string beatId,
        string text,
        long durationTicks,
        IReadOnlyList<DirectorDomainModifierSpec>? effects = null,
        DirectorCausalChainSpec? causalChain = null)
    {
        var (alreadyActive, validatedEffects, severity) = PrepareStoryBeatApplication(beatId, text, durationTicks, effects);
        if (alreadyActive)
        {
            LastDirectorActionStatus = $"Story beat '{beatId}' already active (idempotent)";
            return;
        }

        var result = _directorState.ApplyStoryBeat(beatId, text, (int)durationTicks, severity);
        if (!result.Success)
            throw new InvalidOperationException($"Cannot apply story beat '{beatId}': {result.Message}");

        if (severity != DirectorBeatSeverity.Minor)
        {
            foreach (var (domain, modifier) in validatedEffects)
            {
                _world.RegisterDomainModifier(
                    sourceId: "beat:" + beatId,
                    domain: domain,
                    modifier: modifier,
                    durationTicks: (int)durationTicks,
                    dampeningFactor: _directorDampeningFactor);
            }
        }

        _world.AddExternalEvent($"[Director:{severity.ToString().ToUpperInvariant()}] {text}");

        if (causalChain.HasValue)
        {
            var validatedChain = ValidateCausalChainSpec(beatId, causalChain.Value);
            _directorState.RegisterCausalChain(beatId, validatedChain, Tick);
        }

        LastDirectorActionStatus = result.Message;
    }

    public void ValidateStoryBeat(
        string beatId,
        string text,
        long durationTicks,
        IReadOnlyList<DirectorDomainModifierSpec>? effects = null,
        DirectorCausalChainSpec? causalChain = null)
    {
        _ = PrepareStoryBeatApplication(beatId, text, durationTicks, effects);
        if (causalChain.HasValue)
            _ = ValidateCausalChainSpec(beatId, causalChain.Value);
    }

    private static DirectorBeatSeverity InferBeatSeverity(int effectCount)
    {
        if (effectCount < 0)
            throw new InvalidOperationException($"Cannot infer beat severity: invalid effect count {effectCount}.");
        if (effectCount > 3)
            throw new InvalidOperationException($"Cannot apply story beat: effect count {effectCount} exceeds S3-A cap (max 3).");

        return effectCount switch
        {
            0 => DirectorBeatSeverity.Minor,
            <= 2 => DirectorBeatSeverity.Major,
            _ => DirectorBeatSeverity.Epic
        };
    }

    public void ApplyColonyDirective(
        int colonyId,
        string directive,
        long durationTicks,
        IReadOnlyList<DirectorGoalBiasSpec>? biases = null)
    {
        var biasSpecs = PrepareColonyDirectiveApplication(colonyId, directive, durationTicks, biases);

        _world.ReplaceGoalBiases(
            sourceId: $"directive:{colonyId}:{directive}",
            colonyId: colonyId,
            biases: biasSpecs,
            durationTicks: (int)durationTicks,
            dampeningFactor: _directorDampeningFactor);

        var result = _directorState.ApplyDirective(colonyId, directive, (int)durationTicks);
        _world.AddExternalEvent($"[Director] Directive: {directive} (C{colonyId}, {durationTicks} ticks)");
        LastDirectorActionStatus = result.Message;
    }

    public void ValidateColonyDirective(
        int colonyId,
        string directive,
        long durationTicks,
        IReadOnlyList<DirectorGoalBiasSpec>? biases = null)
    {
        _ = PrepareColonyDirectiveApplication(colonyId, directive, durationTicks, biases);
    }

    public void DeclareWar(Faction attacker, Faction defender, string? reason = null)
    {
        ValidateDeclareWar(attacker, defender);

        var changed = _world.DeclareWar(attacker, defender, out var previous, out var current);
        var reasonSuffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" ({reason.Trim()})";
        _world.AddExternalEvent($"[Director] DeclareWar: {attacker} -> {defender}{reasonSuffix}");
        LastDirectorActionStatus = changed
            ? $"Declared war: {attacker} -> {defender} ({previous} -> {current})"
            : $"DeclareWar no-op: {attacker} and {defender} already in {current}";
    }

    public void ValidateDeclareWar(Faction attacker, Faction defender)
    {
        ValidateCampaignRuntimeAvailability();
        ValidateFactionValue(attacker, nameof(attacker));
        ValidateFactionValue(defender, nameof(defender));

        if (attacker == defender)
            throw new InvalidOperationException("declareWar requires attackerFactionId != defenderFactionId.");
    }

    public void ProposeTreaty(Faction proposer, Faction receiver, string treatyKind, string? note = null)
    {
        var normalizedKind = ValidateProposeTreaty(proposer, receiver, treatyKind);

        var changed = _world.ProposeTreaty(proposer, receiver, normalizedKind, out var previous, out var current);
        var noteSuffix = string.IsNullOrWhiteSpace(note) ? string.Empty : $" ({note.Trim()})";
        _world.AddExternalEvent($"[Director] ProposeTreaty: {normalizedKind} {proposer} -> {receiver}{noteSuffix}");
        LastDirectorActionStatus = changed
            ? $"Treaty '{normalizedKind}' applied: {proposer} -> {receiver} ({previous} -> {current})"
            : $"Treaty '{normalizedKind}' no-op: {proposer} -> {receiver} remains {current}";
    }

    public string ValidateProposeTreaty(Faction proposer, Faction receiver, string treatyKind)
    {
        ValidateCampaignRuntimeAvailability();
        ValidateFactionValue(proposer, nameof(proposer));
        ValidateFactionValue(receiver, nameof(receiver));

        if (proposer == receiver)
            throw new InvalidOperationException("proposeTreaty requires proposerFactionId != receiverFactionId.");

        if (string.IsNullOrWhiteSpace(treatyKind))
        {
            throw new InvalidOperationException(
                "proposeTreaty.treatyKind is required. Expected one of: ceasefire, peace_talks.");
        }

        var normalized = treatyKind.Trim().ToLowerInvariant();
        if (!KnownTreatyKinds.Contains(normalized))
        {
            throw new InvalidOperationException(
                $"Unsupported proposeTreaty.treatyKind '{treatyKind}'. Expected one of: ceasefire, peace_talks.");
        }

        return normalized;
    }

    private void ValidateCampaignRuntimeAvailability()
    {
        if (!IsCampaignRuntimeAvailable())
        {
            throw new InvalidOperationException(
                "Campaign commands require WORLDSIM_ENABLE_DIPLOMACY=true and WORLDSIM_ENABLE_COMBAT_PRIMITIVES=true.");
        }
    }

    private bool IsCampaignRuntimeAvailable()
        => _world.EnableDiplomacy && _world.EnableCombatPrimitives;

    private static void ValidateFactionValue(Faction faction, string paramName)
    {
        if (!Enum.IsDefined(typeof(Faction), faction))
        {
            throw new InvalidOperationException($"Invalid faction value for {paramName}: {(int)faction}.");
        }
    }

    private (bool alreadyActive, List<(RuntimeDomain domain, double modifier)> effects, DirectorBeatSeverity severity) PrepareStoryBeatApplication(
        string beatId,
        string text,
        long durationTicks,
        IReadOnlyList<DirectorDomainModifierSpec>? effects)
    {
        if (string.IsNullOrWhiteSpace(beatId))
            throw new InvalidOperationException("Cannot apply story beat: beatId is required.");

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Cannot apply story beat: text is required.");

        if (durationTicks <= 0)
            throw new InvalidOperationException($"Cannot apply story beat '{beatId}': durationTicks must be > 0.");

        var alreadyActive = _directorState.ActiveBeats.Any(beat => string.Equals(beat.BeatId, beatId, StringComparison.Ordinal));

        var validatedEffects = new List<(RuntimeDomain domain, double modifier)>();
        if (effects != null)
        {
            foreach (var effect in effects)
            {
                if (effect.DurationTicks != (int)durationTicks)
                {
                    throw new InvalidOperationException(
                        $"Cannot apply story beat '{beatId}': effect durationTicks {effect.DurationTicks} must match beat durationTicks {(int)durationTicks}."
                    );
                }

                var domain = ParseRuntimeDomain(effect.Domain);
                if (effect.Modifier < -0.30d || effect.Modifier > 0.30d)
                {
                    throw new InvalidOperationException(
                        $"Cannot apply story beat '{beatId}': modifier {effect.Modifier} out of bounds [-0.30, +0.30] for domain '{effect.Domain}'."
                    );
                }

                validatedEffects.Add((domain, effect.Modifier));
            }
        }

        var severity = InferBeatSeverity(validatedEffects.Count);
        if (!alreadyActive)
        {
            if (severity == DirectorBeatSeverity.Major && _directorState.MajorBeatCooldownRemainingTicks > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot apply story beat '{beatId}': Major beat cooldown active ({_directorState.MajorBeatCooldownRemainingTicks} ticks)"
                );
            }

            if (severity == DirectorBeatSeverity.Epic && _directorState.EpicBeatCooldownRemainingTicks > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot apply story beat '{beatId}': Epic beat cooldown active ({_directorState.EpicBeatCooldownRemainingTicks} ticks)"
                );
            }
        }

        return (alreadyActive, validatedEffects, severity);
    }

    private List<GoalBiasSpec> PrepareColonyDirectiveApplication(
        int colonyId,
        string directive,
        long durationTicks,
        IReadOnlyList<DirectorGoalBiasSpec>? biases)
    {
        if (colonyId < 0 || colonyId >= ColonyCount)
        {
            throw new InvalidOperationException(
                $"Cannot apply colony directive: unknown colonyId '{colonyId}'. colonyCount={ColonyCount}"
            );
        }

        if (string.IsNullOrWhiteSpace(directive))
            throw new InvalidOperationException("Cannot apply colony directive: directive is required.");

        if (durationTicks <= 0)
        {
            throw new InvalidOperationException(
                $"Cannot apply colony directive '{directive}': durationTicks must be > 0."
            );
        }

        var effectiveBiases = (biases != null && biases.Count > 0)
            ? biases
            : BuildDefaultDirectiveBiases(directive);

        return effectiveBiases
            .Select(bias =>
            {
                if (string.IsNullOrWhiteSpace(bias.GoalCategory))
                    throw new InvalidOperationException("Cannot apply colony directive: goalCategory is required in biases.");
                if (!IsKnownGoalBiasCategory(bias.GoalCategory))
                    throw new InvalidOperationException($"Cannot apply colony directive: unknown goalCategory '{bias.GoalCategory}'.");
                if (bias.Weight < 0d || bias.Weight > 0.50d)
                    throw new InvalidOperationException($"Cannot apply colony directive: bias weight {bias.Weight} out of bounds [0.0, 0.50].");
                if (bias.DurationTicks.HasValue && bias.DurationTicks.Value != (int)durationTicks)
                {
                    throw new InvalidOperationException(
                        $"Cannot apply colony directive '{directive}': bias durationTicks {bias.DurationTicks.Value} must match directive durationTicks {(int)durationTicks}."
                    );
                }
                return new GoalBiasSpec(bias.GoalCategory, bias.Weight);
            })
            .ToList();
    }

    private static RuntimeDomain ParseRuntimeDomain(string domainRaw)
    {
        if (string.IsNullOrWhiteSpace(domainRaw))
            throw new InvalidOperationException("Domain is required.");

        return domainRaw.Trim().ToLowerInvariant() switch
        {
            "food" => RuntimeDomain.Food,
            "morale" => RuntimeDomain.Morale,
            "economy" => RuntimeDomain.Economy,
            "military" => RuntimeDomain.Military,
            "research" => RuntimeDomain.Research,
            _ => throw new InvalidOperationException($"Unknown domain '{domainRaw}'.")
        };
    }

    private static bool IsKnownGoalBiasCategory(string category)
    {
        var trimmed = category.Trim();
        return string.Equals(trimmed, GoalBiasCategories.Farming, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Gathering, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Building, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Crafting, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Rest, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Social, StringComparison.OrdinalIgnoreCase)
               || string.Equals(trimmed, GoalBiasCategories.Military, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<DirectorGoalBiasSpec> BuildDefaultDirectiveBiases(string directive)
    {
        if (!IsKnownDirectorDirective(directive))
            throw new InvalidOperationException($"Cannot apply colony directive: unknown directive '{directive}'.");

        // Transitional mapping: known directive IDs map to bias compositions.
        return directive switch
        {
            "PrioritizeFood" => new[]
            {
                new DirectorGoalBiasSpec(GoalBiasCategories.Farming, 0.25, null),
                new DirectorGoalBiasSpec(GoalBiasCategories.Gathering, 0.15, null)
            },
            "StabilizeMorale" => new[]
            {
                new DirectorGoalBiasSpec(GoalBiasCategories.Building, 0.20, null),
                new DirectorGoalBiasSpec(GoalBiasCategories.Farming, 0.15, null)
            },
            "BoostIndustry" => new[]
            {
                new DirectorGoalBiasSpec(GoalBiasCategories.Crafting, 0.20, null),
                new DirectorGoalBiasSpec(GoalBiasCategories.Building, 0.12, null),
                new DirectorGoalBiasSpec(GoalBiasCategories.Gathering, 0.08, null)
            },
            _ => Array.Empty<DirectorGoalBiasSpec>()
        };
    }

    private JsonObject BuildDirectorSnapshotJson()
    {
        var metrics = BuildDirectorConditionMetrics();

        var activeBeats = new JsonArray();
        foreach (var beat in _directorState.ActiveBeats)
        {
            activeBeats.Add(new JsonObject
            {
                ["beatId"] = beat.BeatId,
                ["severity"] = beat.Severity.ToString(),
                ["remainingTicks"] = beat.RemainingTicks
            });
        }

        var activeDirectives = new JsonArray();
        foreach (var directive in _directorState.ActiveDirectives)
        {
            activeDirectives.Add(new JsonObject
            {
                ["colonyId"] = directive.ColonyId,
                ["directive"] = directive.Directive,
                ["remainingTicks"] = directive.RemainingTicks
            });
        }

        var activeDomainModifiers = new JsonArray();
        foreach (var modifier in _world.GetActiveDomainModifiers())
        {
            activeDomainModifiers.Add(new JsonObject
            {
                ["sourceId"] = modifier.SourceId,
                ["domain"] = modifier.Domain.ToString().ToLowerInvariant(),
                ["baseModifier"] = modifier.BaseModifier,
                ["effectiveModifier"] = modifier.EffectiveModifier,
                ["remainingTicks"] = modifier.RemainingTicks,
                ["totalDurationTicks"] = modifier.TotalDurationTicks
            });
        }

        var activeGoalBiases = new JsonArray();
        foreach (var colony in _world._colonies)
        {
            foreach (var bias in _world.GetActiveGoalBiases(colony.Id))
            {
                activeGoalBiases.Add(new JsonObject
                {
                    ["colonyId"] = bias.ColonyId,
                    ["sourceId"] = bias.SourceId,
                    ["goalCategory"] = bias.GoalCategory,
                    ["baseWeight"] = bias.BaseWeight,
                    ["effectiveWeight"] = bias.EffectiveWeight,
                    ["remainingTicks"] = bias.RemainingTicks,
                    ["totalDurationTicks"] = bias.TotalDurationTicks,
                    ["isBlendActive"] = bias.IsBlendActive
                });
            }
        }

        var pendingCausalChains = new JsonArray();
        foreach (var chain in _directorState.PendingCausalChains)
        {
            pendingCausalChains.Add(new JsonObject
            {
                ["parentBeatId"] = chain.ParentBeatId,
                ["status"] = chain.Status,
                ["conditionSummary"] = chain.ConditionSummary,
                ["followUpBeatId"] = chain.FollowUpBeatId,
                ["followUpSummary"] = chain.FollowUpSummary,
                ["remainingWindowTicks"] = chain.RemainingWindowTicks,
                ["triggerCount"] = chain.TriggerCount,
                ["lastFailureMessage"] = chain.LastFailureMessage
            });
        }

        return new JsonObject
        {
            ["currentTick"] = Tick,
            ["currentSeason"] = _world.CurrentSeason.ToString(),
            ["effectiveOutputMode"] = _directorExecutionState.EffectiveOutputMode,
            ["effectiveOutputModeSource"] = _directorExecutionState.EffectiveOutputModeSource,
            ["stage"] = _directorExecutionState.Stage,
            ["colonyPopulation"] = metrics.LivingPopulation,
            ["foodReservesPct"] = metrics.FoodReservesPct,
            ["moraleAvg"] = metrics.MoraleAvg,
            ["economyOutput"] = metrics.EconomyOutput,
            ["activeBeats"] = activeBeats,
            ["activeDirectives"] = activeDirectives,
            ["pendingCausalChains"] = pendingCausalChains,
            ["beatCooldownRemainingTicks"] = _directorState.BeatCooldownRemainingTicks,
            ["maxInfluenceBudget"] = _directorState.MaxInfluenceBudget,
            ["remainingInfluenceBudget"] = _directorState.RemainingInfluenceBudget,
            ["lastCheckpointBudgetUsed"] = _directorState.LastCheckpointBudgetUsed,
            ["lastBudgetCheckpointTick"] = _directorState.LastBudgetCheckpointTick,
            ["dampeningFactor"] = _directorDampeningFactor,
            ["activeDomainModifiers"] = activeDomainModifiers,
            ["activeGoalBiases"] = activeGoalBiases
        };
    }

    private void EvaluatePendingDirectorCausalChains()
    {
        var evaluationTick = Tick + 1;
        var metrics = BuildDirectorConditionMetrics();
        var followUps = _directorState.EvaluatePendingCausalChains(
            evaluationTick,
            condition => EvaluateDirectorCondition(condition, metrics));

        foreach (var trigger in followUps)
        {
            try
            {
                ApplyStoryBeat(
                    trigger.FollowUpBeat.BeatId,
                    trigger.FollowUpBeat.Text,
                    trigger.FollowUpBeat.DurationTicks,
                    trigger.FollowUpBeat.Effects,
                    causalChain: null);
            }
            catch (Exception ex)
            {
                _directorState.MarkCausalChainTriggerFailed(trigger.ParentBeatId, ex.Message);
                LastDirectorActionStatus = $"Causal chain trigger failed for '{trigger.ParentBeatId}': {ex.Message}";
            }
        }
    }

    private static bool EvaluateDirectorCondition(DirectorCausalConditionSpec condition, DirectorConditionMetrics metrics)
    {
        var observed = condition.Metric switch
        {
            "food_reserves_pct" => metrics.FoodReservesPct,
            "morale_avg" => metrics.MoraleAvg,
            "population" => metrics.LivingPopulation,
            "economy_output" => metrics.EconomyOutput,
            _ => throw new InvalidOperationException($"Unknown causal condition metric '{condition.Metric}'.")
        };

        return condition.Operator switch
        {
            "lt" => observed < condition.Threshold,
            "gt" => observed > condition.Threshold,
            "eq" when string.Equals(condition.Metric, "population", StringComparison.Ordinal)
                => observed == condition.Threshold,
            "eq" => Math.Abs(observed - condition.Threshold) <= FloatingCausalEqTolerance,
            _ => throw new InvalidOperationException($"Unknown causal condition operator '{condition.Operator}'.")
        };
    }

    private DirectorCausalChainSpec ValidateCausalChainSpec(string parentBeatId, DirectorCausalChainSpec chain)
    {
        var metric = NormalizeCausalConditionMetric(chain.Condition.Metric);
        var op = NormalizeCausalConditionOperator(chain.Condition.Operator);

        if (chain.WindowTicks < MinCausalWindowTicks || chain.WindowTicks > MaxCausalWindowTicks)
        {
            throw new InvalidOperationException(
                $"Cannot apply causal chain for beat '{parentBeatId}': windowTicks must be in [{MinCausalWindowTicks}, {MaxCausalWindowTicks}]."
            );
        }

        if (chain.MaxTriggers != 1)
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': maxTriggers must be 1 in S7-A.");

        if (double.IsNaN(chain.Condition.Threshold) || double.IsInfinity(chain.Condition.Threshold))
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': condition threshold must be finite.");

        if (string.Equals(metric, "population", StringComparison.Ordinal)
            && string.Equals(op, "eq", StringComparison.Ordinal)
            && Math.Abs(chain.Condition.Threshold - Math.Round(chain.Condition.Threshold)) > FloatingCausalEqTolerance)
        {
            throw new InvalidOperationException(
                $"Cannot apply causal chain for beat '{parentBeatId}': population eq threshold must be an integer value."
            );
        }

        var followUpBeat = ValidateFollowUpBeatSpec(parentBeatId, chain.FollowUpBeat);
        return new DirectorCausalChainSpec(
            new DirectorCausalConditionSpec(metric, op, chain.Condition.Threshold),
            followUpBeat,
            chain.WindowTicks,
            1);
    }

    private DirectorFollowUpBeatSpec ValidateFollowUpBeatSpec(string parentBeatId, DirectorFollowUpBeatSpec followUpBeat)
    {
        if (string.IsNullOrWhiteSpace(followUpBeat.BeatId))
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': follow-up beatId is required.");
        if (string.Equals(followUpBeat.BeatId, parentBeatId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': follow-up beatId must differ from parent beatId.");
        if (string.IsNullOrWhiteSpace(followUpBeat.Text))
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': follow-up text is required.");
        if (followUpBeat.DurationTicks <= 0)
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': follow-up durationTicks must be > 0.");

        var effects = followUpBeat.Effects ?? Array.Empty<DirectorDomainModifierSpec>();
        if (effects.Count > 3)
            throw new InvalidOperationException($"Cannot apply causal chain for beat '{parentBeatId}': follow-up effect count {effects.Count} exceeds max 3.");

        foreach (var effect in effects)
        {
            if (effect.DurationTicks != (int)followUpBeat.DurationTicks)
            {
                throw new InvalidOperationException(
                    $"Cannot apply causal chain for beat '{parentBeatId}': follow-up effect durationTicks {effect.DurationTicks} must match follow-up durationTicks {(int)followUpBeat.DurationTicks}."
                );
            }

            _ = ParseRuntimeDomain(effect.Domain);
            if (effect.Modifier < -0.30d || effect.Modifier > 0.30d)
            {
                throw new InvalidOperationException(
                    $"Cannot apply causal chain for beat '{parentBeatId}': follow-up modifier {effect.Modifier} out of bounds [-0.30, +0.30] for domain '{effect.Domain}'."
                );
            }
        }

        return new DirectorFollowUpBeatSpec(
            followUpBeat.BeatId,
            followUpBeat.Text,
            followUpBeat.DurationTicks,
            effects.ToList());
    }

    private static string NormalizeCausalConditionMetric(string metric)
    {
        if (string.IsNullOrWhiteSpace(metric))
            throw new InvalidOperationException("Causal condition metric is required.");

        var normalized = metric.Trim().ToLowerInvariant();
        if (!KnownCausalConditionMetrics.Contains(normalized))
            throw new InvalidOperationException($"Unknown causal condition metric '{metric}'.");

        return normalized;
    }

    private static string NormalizeCausalConditionOperator(string op)
    {
        if (string.IsNullOrWhiteSpace(op))
            throw new InvalidOperationException("Causal condition operator is required.");

        var normalized = op.Trim().ToLowerInvariant();
        if (!KnownCausalConditionOperators.Contains(normalized))
            throw new InvalidOperationException($"Unknown causal condition operator '{op}'.");

        return normalized;
    }

    private DirectorConditionMetrics BuildDirectorConditionMetrics()
    {
        int livingPopulation = _world._people.Count(person => person.Health > 0f);
        int totalFood = _world._colonies.Sum(colony => colony.Stock.GetValueOrDefault(Resource.Food, 0));
        double foodReservesPctNormalized = livingPopulation <= 0
            ? 0d
            : Math.Clamp(totalFood / (double)(livingPopulation * 6), 0d, 1d);
        double moraleAvg = _world._colonies.Count == 0
            ? 0d
            : _world._colonies.Average(colony => colony.Morale);
        double economyOutput = _world._colonies.Count == 0
            ? 1d
            : _world._colonies.Average(colony => colony.ColonyWorkMultiplier);

        return new DirectorConditionMetrics(
            LivingPopulation: livingPopulation,
            FoodReservesPctNormalized: foodReservesPctNormalized,
            FoodReservesPct: foodReservesPctNormalized * 100d,
            MoraleAvg: moraleAvg,
            EconomyOutput: economyOutput);
    }

    private readonly record struct DirectorConditionMetrics(
        int LivingPopulation,
        double FoodReservesPctNormalized,
        double FoodReservesPct,
        double MoraleAvg,
        double EconomyOutput);

    private RuntimeNpcBrain CreateBrain(Colony colony, RuntimeAiOptions options)
    {
        return options.PolicyMode switch
        {
            NpcPolicyMode.FactionMix => CreateFactionPolicyBrain(colony, options),
            NpcPolicyMode.HtnPilot => new RuntimeNpcBrain(NpcPlannerMode.Htn, "HtnPilot"),
            _ => new RuntimeNpcBrain(options.PlannerMode, $"Global:{options.PlannerMode}")
        };
    }

    private static RuntimeNpcBrain CreateFactionPolicyBrain(Colony colony, RuntimeAiOptions options)
    {
        var planner = options.ResolveFactionPlanner(colony.Faction);
        return new RuntimeNpcBrain(planner, $"FactionMix:{colony.Faction}->{planner}");
    }

    private void RefreshAiDebugSnapshot()
    {
        var tracked = GetTrackedDecisions();
        var latest = ResolveLatestDecision(tracked);

        if (latest == null)
        {
            _latestAiDebugSnapshot = AiDebugSnapshot.Empty(PlannerMode.ToString(), PolicyMode.ToString());
            return;
        }

        var selectedDecision = ResolveSelectedDecision(tracked) ?? latest;
        var selectedIndex = tracked.FindIndex(decision => decision.ActorId == selectedDecision.ActorId);
        if (selectedIndex < 0)
            selectedIndex = 0;

        _trackedNpcCursor = selectedIndex;
        if (_manualTracking)
            _trackedActorId = tracked[selectedIndex].ActorId;

        var selected = tracked[selectedIndex];

        if (latest.WorldTick > _lastObservedDecisionTick)
        {
            _lastObservedDecisionTick = latest.WorldTick;
            var summary = $"{latest.Trace.PolicyName} | Goal {latest.Trace.SelectedGoal} -> {latest.Job}";
            _recentAiDecisions.Enqueue(summary);
            while (_recentAiDecisions.Count > 24)
                _recentAiDecisions.Dequeue();
        }

        _latestAiDebugSnapshot = new AiDebugSnapshot(
            HasData: true,
            PlannerMode: selected.Trace.PlannerName,
            PolicyMode: selected.Trace.PolicyName,
            TrackingMode: _manualTracking ? "Manual" : "Latest",
            TrackedNpcIndex: selectedIndex + 1,
            TrackedNpcCount: tracked.Count,
            DecisionSequence: selected.Sequence,
            TrackedActorId: selected.ActorId,
            TrackedColonyId: selected.ColonyId,
            TrackedX: selected.X,
            TrackedY: selected.Y,
            SelectedGoal: selected.Trace.SelectedGoal,
            NextCommand: selected.Job.ToString(),
            PlanLength: selected.Trace.PlanLength,
            PlanCost: selected.Trace.PlanCost,
            ReplanReason: selected.Trace.ReplanReason,
            MethodName: selected.Trace.MethodName,
            GoalScores: selected.Trace.GoalScores
                .OrderByDescending(score => score.Score)
                .Select(score => new AiGoalScoreData(score.GoalName, score.Score, score.IsOnCooldown))
                .ToList(),
            RecentDecisions: _recentAiDecisions.ToList());
    }

    private List<RuntimeAiDecision> GetTrackedDecisions()
    {
        return _world._people
            .Select(person => person.LastAiDecision)
            .Where(decision => decision != null)
            .Select(decision => decision!)
            .OrderBy(decision => decision.ActorId)
            .ToList();
    }

    private RuntimeAiDecision? ResolveSelectedDecision(IReadOnlyList<RuntimeAiDecision> tracked)
    {
        var latest = ResolveLatestDecision(tracked);
        if (!_manualTracking)
            return latest;

        if (_trackedActorId >= 0)
        {
            var byActor = tracked.FirstOrDefault(decision => decision.ActorId == _trackedActorId);
            if (byActor != null)
                return byActor;
        }

        _manualTracking = false;
        _trackedActorId = -1;
        return latest;
    }

    private static RuntimeAiDecision? ResolveLatestDecision(IReadOnlyList<RuntimeAiDecision> tracked)
    {
        return tracked
            .OrderByDescending(decision => decision.WorldTick)
            .ThenByDescending(decision => decision.Sequence)
            .ThenBy(decision => decision.ActorId)
            .FirstOrDefault();
    }

    private static int NormalizeTrackedIndex(int index, int count)
    {
        if (count <= 0)
            return 0;

        var normalized = index % count;
        if (normalized < 0)
            normalized += count;
        return normalized;
    }
}
