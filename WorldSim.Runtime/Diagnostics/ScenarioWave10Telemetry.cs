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
    string ManualLaunchStatus)
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
        ManualLaunchStatus: "not_attempted");

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
