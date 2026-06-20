namespace WorldSim.Runtime.Diagnostics;

public static class ScenarioWave10Evidence
{
    public const string ProofTypeOrganic = "organic";
    public const string ProofTypeManualOperator = "manual_operator";
    public const string ProofTypeDeterministicProbe = "deterministic_probe";
    public const string ProofTypeNotConfigured = "not_configured";

    public const string EvidenceStatusPositive = "positive";
    public const string EvidenceStatusProofUnavailable = "proof_unavailable";
    public const string EvidenceStatusNotApplicable = "not_applicable";

    public const string TimelineSemanticsTickSampled = "tick_sampled";
    public const string TimelineSemanticsNotTickSampled = "not_tick_sampled";
    public const string TimelineSemanticsNotSampled = "not_sampled";

    public const string RuntimeSourceMainWorldRun = "main_world_run";
    public const string RuntimeSourceSimulationRuntimeProbe = "simulation_runtime_probe";

    public const string ReasonNone = "none";
    public const string ReasonOrganicLaunchNotReproduced = "organic_launch_not_reproduced_without_policy_tweak";
    public const string ReasonSiegeUnitNotReproduced = "siege_unit_not_reproduced_without_policy_tweak";
    public const string ReasonLaneNotConfigured = "lane_not_configured";
    public const string ReasonMainWorldRunHasNoCampaignRuntime = "main_world_run_has_no_campaign_runtime";
    public const string ReasonCampaignSiegeNotReproduced = "campaign_siege_not_reproduced_without_policy_tweak";
    public const string ReasonSupplyLineConvoyNotReproduced = "supply_line_convoy_not_reproduced_without_policy_tweak";
    public const string ReasonForwardBaseLifecycleNotReproduced = "forward_base_lifecycle_not_reproduced_without_policy_tweak";
    public const string ReasonScoutIntelChoiceNotReproduced = "scout_intel_campaign_choice_not_reproduced_without_policy_tweak";
    public const string ReasonMultiFrontBoundNotReproduced = "multi_front_bound_not_reproduced_without_policy_tweak";
}

public sealed record ScenarioWave10TimelineSnapshot(
    string Wave10Scenario,
    string RuntimeSource,
    string ProofType,
    string EvidenceStatus,
    string TimelineSemantics,
    int ActiveCampaigns,
    int ResolvedCampaigns,
    int ActiveSiegeUnits,
    int CampaignLaunchBlockedByCap,
    int CampaignLaunchBlockedByPairCap,
    long? FirstCampaignLaunchTick,
    long? FirstAssemblyTick,
    long? FirstMarchTick,
    long? FirstEncounterTick,
    long? FirstSiegeTick,
    long? FirstResolutionTick,
    long LongestUnresolvedCampaignAgeTicks)
{
    public static ScenarioWave10TimelineSnapshot Empty { get; } = new(
        Wave10Scenario: "none",
        RuntimeSource: ScenarioWave10Evidence.RuntimeSourceMainWorldRun,
        ProofType: ScenarioWave10Evidence.ProofTypeNotConfigured,
        EvidenceStatus: ScenarioWave10Evidence.EvidenceStatusNotApplicable,
        TimelineSemantics: ScenarioWave10Evidence.TimelineSemanticsNotSampled,
        ActiveCampaigns: 0,
        ResolvedCampaigns: 0,
        ActiveSiegeUnits: 0,
        CampaignLaunchBlockedByCap: 0,
        CampaignLaunchBlockedByPairCap: 0,
        FirstCampaignLaunchTick: null,
        FirstAssemblyTick: null,
        FirstMarchTick: null,
        FirstEncounterTick: null,
        FirstSiegeTick: null,
        FirstResolutionTick: null,
        LongestUnresolvedCampaignAgeTicks: 0);
}

public sealed record ScenarioOrganicLaunchApplyFailureStatus(
    string Status,
    int Count);

public sealed record ScenarioOrganicLaunchDiagnosticsSnapshot(
    int EvaluationTickCount,
    int OwnerEvaluationCount,
    long? LastEvaluationTick,
    int[] EvaluatedFactionIds,
    int LastEvaluatedFactionId,
    int LastEligibleMembers,
    int LastAvailableWarriors,
    int LastActiveCampaignCount,
    int LastTargetOptionCount,
    int LastWarTargetCount,
    int LastHostileTargetCount,
    int LastKnownTargetCount,
    int LastUnknownTargetCount,
    int LastMissingScoutIntelTargetCount,
    bool HasLastBestCandidateScore,
    float LastBestPressureScore,
    float LastBestAdvantageScore,
    float LastBestDistancePenalty,
    float LastBestLaunchScore,
    string LastDecisionKind,
    string LastDecisionReasonCode,
    int LaunchApplyAttempts,
    int LaunchApplySuccesses,
    int LaunchApplyFailures,
    ScenarioOrganicLaunchApplyFailureStatus[] LaunchApplyFailureStatuses,
    string DominantNoLaunchReason)
{
    public const string ReasonNotEvaluated = "not_evaluated";
    public const string ReasonNoEligibleMembers = "no_eligible_members";
    public const string ReasonNoAvailableWarriorsAfterHomeDefense = "no_available_warriors_after_home_defense";
    public const string ReasonNoTargetOptions = "no_target_options";
    public const string ReasonNoKnownTargets = "no_known_targets";
    public const string ReasonMissingScoutIntel = "missing_scout_intel";
    public const string ReasonStrategyHoldNoViableTarget = "strategy_hold_no_viable_target";
    public const string ReasonLaunchApplyFailed = "launch_apply_failed";
    public const string ReasonLaunchApplied = "launch_applied";

    public static ScenarioOrganicLaunchDiagnosticsSnapshot Empty { get; } = new(
        EvaluationTickCount: 0,
        OwnerEvaluationCount: 0,
        LastEvaluationTick: null,
        EvaluatedFactionIds: Array.Empty<int>(),
        LastEvaluatedFactionId: -1,
        LastEligibleMembers: 0,
        LastAvailableWarriors: 0,
        LastActiveCampaignCount: 0,
        LastTargetOptionCount: 0,
        LastWarTargetCount: 0,
        LastHostileTargetCount: 0,
        LastKnownTargetCount: 0,
        LastUnknownTargetCount: 0,
        LastMissingScoutIntelTargetCount: 0,
        HasLastBestCandidateScore: false,
        LastBestPressureScore: 0f,
        LastBestAdvantageScore: 0f,
        LastBestDistancePenalty: 0f,
        LastBestLaunchScore: 0f,
        LastDecisionKind: "none",
        LastDecisionReasonCode: "none",
        LaunchApplyAttempts: 0,
        LaunchApplySuccesses: 0,
        LaunchApplyFailures: 0,
        LaunchApplyFailureStatuses: Array.Empty<ScenarioOrganicLaunchApplyFailureStatus>(),
        DominantNoLaunchReason: ReasonNotEvaluated);
}

public sealed record ScenarioManualDownstreamConvoyDiagnosticsSnapshot(
    int Evaluated,
    int Eligible,
    int Requested,
    string BlockedReason,
    int Spawned,
    int Delivered,
    int Failed)
{
    public static ScenarioManualDownstreamConvoyDiagnosticsSnapshot Empty { get; } = new(
        Evaluated: 0,
        Eligible: 0,
        Requested: 0,
        BlockedReason: "none",
        Spawned: 0,
        Delivered: 0,
        Failed: 0);
}

public sealed record ScenarioManualDownstreamScoutDiagnosticsSnapshot(
    int ObservationPasses,
    int LiveScoutActors,
    int SkippedByRelation,
    int SkippedByRadius,
    int NearestHostileDistance,
    int FreshIntel)
{
    public static ScenarioManualDownstreamScoutDiagnosticsSnapshot Empty { get; } = new(
        ObservationPasses: 0,
        LiveScoutActors: 0,
        SkippedByRelation: 0,
        SkippedByRadius: 0,
        NearestHostileDistance: -1,
        FreshIntel: 0);
}

public sealed record ScenarioManualDownstreamSiegeUnitDiagnosticsSnapshot(
    int EncounterCampaigns,
    int TechLocked,
    int ResolverDisabled,
    int NoTarget,
    int AlreadyPresent,
    int Spawned,
    int ActionTicks)
{
    public static ScenarioManualDownstreamSiegeUnitDiagnosticsSnapshot Empty { get; } = new(
        EncounterCampaigns: 0,
        TechLocked: 0,
        ResolverDisabled: 0,
        NoTarget: 0,
        AlreadyPresent: 0,
        Spawned: 0,
        ActionTicks: 0);
}

public sealed record ScenarioManualDownstreamDiagnosticsSnapshot(
    ScenarioManualDownstreamConvoyDiagnosticsSnapshot Convoy,
    ScenarioManualDownstreamScoutDiagnosticsSnapshot Scout,
    ScenarioManualDownstreamSiegeUnitDiagnosticsSnapshot SiegeUnit)
{
    public static ScenarioManualDownstreamDiagnosticsSnapshot Empty { get; } = new(
        ScenarioManualDownstreamConvoyDiagnosticsSnapshot.Empty,
        ScenarioManualDownstreamScoutDiagnosticsSnapshot.Empty,
        ScenarioManualDownstreamSiegeUnitDiagnosticsSnapshot.Empty);
}

public sealed record ScenarioWave10TelemetrySnapshot(
    string Wave10Scenario,
    string RuntimeSource,
    string ProofType,
    string EvidenceStatus,
    string TimelineSemantics,
    string ReasonCode,
    string[] NonClaims,
    int CampaignLaunches,
    int ActiveCampaigns,
    int ResolvedCampaigns,
    int AttackerVictories,
    int DefenderHeld,
    int CampaignSiegesEntered,
    int CampaignBreaches,
    int SiegePressureTicks,
    int LootFood,
    int LootWood,
    int LootStone,
    int LootGold,
    int WarScoreDeltaTotal,
    int PeaceAppliedCount,
    int ActiveSupplyConvoys,
    int ConvoysSpawned,
    int ConvoysDelivered,
    int ConvoysFailed,
    int ConvoyThrottleBlocks,
    int ConvoyCapBlocks,
    int ConvoyHomeDefenseBlocks,
    int ConvoyRouteBudgetExhausted,
    int ActiveForwardBases,
    int ForwardBasesEstablished,
    int ForwardBasesExpired,
    int ForwardBasesAbandoned,
    int ForwardBaseRestTicks,
    int ScoutIntelObserved,
    int ScoutIntelRefreshed,
    int ScoutIntelExpired,
    int ActiveScoutIntel,
    int FreshScoutIntel,
    int CampaignTargetsWithScoutIntel,
    int SiegeUnitsSpawned,
    int ActiveSiegeUnits,
    int InactiveSiegeUnits,
    int SiegeUnitActionTicks,
    int MaxActiveCampaignsForAnyFaction,
    int MaxUnresolvedPairsForAnyFactionPair,
    int CampaignLaunchBlockedByCap,
    int CampaignLaunchBlockedByPairCap,
    int CampaignLaunchBlockedByHomeDefense,
    int CampaignLaunchRouteBudgetExhausted,
    int WarScorePressureSignals,
    int HomeGarrisonViolationCount,
    long? FirstCampaignLaunchTick,
    long? FirstAssemblyTick,
    long? FirstMarchTick,
    long? FirstEncounterTick,
    long? FirstSiegeTick,
    long? FirstResolutionTick,
    long LongestUnresolvedCampaignAgeTicks,
    long? ManualLaunchAttemptTick,
    bool ManualLaunchAttempted,
    bool ManualLaunchSucceeded,
    string ManualLaunchStatus,
    ScenarioOrganicLaunchDiagnosticsSnapshot OrganicLaunchDiagnostics,
    ScenarioManualDownstreamDiagnosticsSnapshot ManualDownstreamDiagnostics)
{
    public static ScenarioWave10TelemetrySnapshot Empty { get; } = new(
        Wave10Scenario: "none",
        RuntimeSource: ScenarioWave10Evidence.RuntimeSourceMainWorldRun,
        ProofType: ScenarioWave10Evidence.ProofTypeNotConfigured,
        EvidenceStatus: ScenarioWave10Evidence.EvidenceStatusNotApplicable,
        TimelineSemantics: ScenarioWave10Evidence.TimelineSemanticsNotSampled,
        ReasonCode: ScenarioWave10Evidence.ReasonMainWorldRunHasNoCampaignRuntime,
        NonClaims: Array.Empty<string>(),
        CampaignLaunches: 0,
        ActiveCampaigns: 0,
        ResolvedCampaigns: 0,
        AttackerVictories: 0,
        DefenderHeld: 0,
        CampaignSiegesEntered: 0,
        CampaignBreaches: 0,
        SiegePressureTicks: 0,
        LootFood: 0,
        LootWood: 0,
        LootStone: 0,
        LootGold: 0,
        WarScoreDeltaTotal: 0,
        PeaceAppliedCount: 0,
        ActiveSupplyConvoys: 0,
        ConvoysSpawned: 0,
        ConvoysDelivered: 0,
        ConvoysFailed: 0,
        ConvoyThrottleBlocks: 0,
        ConvoyCapBlocks: 0,
        ConvoyHomeDefenseBlocks: 0,
        ConvoyRouteBudgetExhausted: 0,
        ActiveForwardBases: 0,
        ForwardBasesEstablished: 0,
        ForwardBasesExpired: 0,
        ForwardBasesAbandoned: 0,
        ForwardBaseRestTicks: 0,
        ScoutIntelObserved: 0,
        ScoutIntelRefreshed: 0,
        ScoutIntelExpired: 0,
        ActiveScoutIntel: 0,
        FreshScoutIntel: 0,
        CampaignTargetsWithScoutIntel: 0,
        SiegeUnitsSpawned: 0,
        ActiveSiegeUnits: 0,
        InactiveSiegeUnits: 0,
        SiegeUnitActionTicks: 0,
        MaxActiveCampaignsForAnyFaction: 0,
        MaxUnresolvedPairsForAnyFactionPair: 0,
        CampaignLaunchBlockedByCap: 0,
        CampaignLaunchBlockedByPairCap: 0,
        CampaignLaunchBlockedByHomeDefense: 0,
        CampaignLaunchRouteBudgetExhausted: 0,
        WarScorePressureSignals: 0,
        HomeGarrisonViolationCount: 0,
        FirstCampaignLaunchTick: null,
        FirstAssemblyTick: null,
        FirstMarchTick: null,
        FirstEncounterTick: null,
        FirstSiegeTick: null,
        FirstResolutionTick: null,
        LongestUnresolvedCampaignAgeTicks: 0,
        ManualLaunchAttemptTick: null,
        ManualLaunchAttempted: false,
        ManualLaunchSucceeded: false,
        ManualLaunchStatus: "not_attempted",
        OrganicLaunchDiagnostics: ScenarioOrganicLaunchDiagnosticsSnapshot.Empty,
        ManualDownstreamDiagnostics: ScenarioManualDownstreamDiagnosticsSnapshot.Empty);

    public ScenarioWave10TimelineSnapshot ToTimelineSnapshot()
        => new(
            Wave10Scenario,
            RuntimeSource,
            ProofType,
            EvidenceStatus,
            ScenarioWave10Evidence.TimelineSemanticsTickSampled,
            ActiveCampaigns,
            ResolvedCampaigns,
            ActiveSiegeUnits,
            CampaignLaunchBlockedByCap,
            CampaignLaunchBlockedByPairCap,
            FirstCampaignLaunchTick,
            FirstAssemblyTick,
            FirstMarchTick,
            FirstEncounterTick,
            FirstSiegeTick,
            FirstResolutionTick,
            LongestUnresolvedCampaignAgeTicks);
}

public sealed record ScenarioRunTelemetrySnapshot(
    int LivingColonies,
    int People,
    int Food,
    float AverageFoodPerPerson,
    int DeathsOldAge,
    int DeathsStarvation,
    int DeathsPredator,
    int DeathsOther,
    int CombatDeaths,
    int CombatEngagements,
    int PredatorKillsByHumans,
    int BattleTicks,
    int ActiveBattles,
    int ActiveCombatGroups,
    int RoutingPeople,
    float MinCombatMorale,
    int DeathsStarvationRecent60s,
    int DeathsStarvationWithFood,
    int OverlapResolveMoves,
    int CrowdDissipationMoves,
    int BirthFallbackToOccupied,
    int BirthFallbackToParent,
    int BuildSiteResets,
    int NoProgressBackoffResource,
    int NoProgressBackoffBuild,
    int NoProgressBackoffFlee,
    int NoProgressBackoffCombat,
    int AiNoPlanDecisions,
    int AiReplanBackoffDecisions,
    int AiResearchTechDecisions,
    int DenseNeighborhoodTicks,
    int LastTickDenseActors,
    long EntityCount,
    ScenarioContactTelemetrySnapshot Contact,
    ScenarioAiTelemetrySnapshot Ai,
    ScenarioEcologyTelemetrySnapshot Ecology,
    ScenarioEcologyBalanceSnapshot EcologyBalance,
    ScenarioSupplyTelemetrySnapshot Supply,
    bool EnablePredatorHumanAttacks);
