namespace WorldSim.Runtime.ReadModel;

public enum TileGroundView
{
    Dirt,
    Water,
    Grass
}

public enum ResourceView
{
    None,
    Wood,
    Stone,
    Iron,
    Gold,
    Food,
    Water
}

public enum AnimalKindView
{
    Herbivore,
    Predator
}

public enum SeasonView
{
    Spring,
    Summer,
    Autumn,
    Winter
}

public enum SpecializedBuildingKindView
{
    FarmPlot,
    Workshop,
    Storehouse
}

public enum DefensiveStructureKindView
{
    WoodWall,
    StoneWall,
    ReinforcedWall,
    Gate,
    Watchtower,
    ArrowTower,
    CatapultTower
}

public sealed record WorldRenderSnapshot(
    int Width,
    int Height,
    IReadOnlyList<TileRenderData> Tiles,
    IReadOnlyList<HouseRenderData> Houses,
    IReadOnlyList<SpecializedBuildingRenderData> SpecializedBuildings,
    IReadOnlyList<DefensiveStructureRenderData> DefensiveStructures,
    IReadOnlyList<PersonRenderData> People,
    IReadOnlyList<AnimalRenderData> Animals,
    IReadOnlyList<ColonyHudData> Colonies,
    IReadOnlyList<CombatGroupRenderData> CombatGroups,
    IReadOnlyList<BattleRenderData> Battles,
    IReadOnlyList<SiegeRenderData> Sieges,
    IReadOnlyList<BreachRenderData> Breaches,
    IReadOnlyList<FactionStanceRenderData> FactionStances,
    EcoHudData Ecology,
    SeasonView CurrentSeason,
    bool IsDroughtActive,
    IReadOnlyList<string> RecentEvents,
    DirectorRenderState Director
);

public sealed record DirectorRenderState(
    string StageMarker,
    string OutputMode,
    string OutputModeSource,
    string ApplyStatus,
    int BeatCooldownRemainingTicks,
    int MajorBeatCooldownRemainingTicks,
    int EpicBeatCooldownRemainingTicks,
    double MaxInfluenceBudget,
    double RemainingInfluenceBudget,
    double LastCheckpointBudgetUsed,
    long LastBudgetCheckpointTick,
    bool HasBudgetData,
    IReadOnlyList<DirectorActiveBeatRenderData> ActiveBeats,
    IReadOnlyList<DirectorActiveDirectiveRenderData> ActiveDirectives,
    IReadOnlyList<DirectorPendingChainRenderData> PendingChains,
    IReadOnlyList<DirectorDomainModifierRenderData> ActiveDomainModifiers,
    IReadOnlyList<DirectorGoalBiasRenderData> ActiveGoalBiases,
    string LastActionStatus)
{
    public static DirectorRenderState Empty { get; } = new(
        StageMarker: "not_triggered",
        OutputMode: "unknown",
        OutputModeSource: "unknown",
        ApplyStatus: "not_triggered",
        BeatCooldownRemainingTicks: 0,
        MajorBeatCooldownRemainingTicks: 0,
        EpicBeatCooldownRemainingTicks: 0,
        MaxInfluenceBudget: 5d,
        RemainingInfluenceBudget: 5d,
        LastCheckpointBudgetUsed: 0d,
        LastBudgetCheckpointTick: -1,
        HasBudgetData: false,
        ActiveBeats: Array.Empty<DirectorActiveBeatRenderData>(),
        ActiveDirectives: Array.Empty<DirectorActiveDirectiveRenderData>(),
        PendingChains: Array.Empty<DirectorPendingChainRenderData>(),
        ActiveDomainModifiers: Array.Empty<DirectorDomainModifierRenderData>(),
        ActiveGoalBiases: Array.Empty<DirectorGoalBiasRenderData>(),
        LastActionStatus: "No director action");
}

public sealed record DirectorActiveBeatRenderData(string BeatId, string Text, string Severity, int RemainingTicks, int TotalTicks);
public sealed record DirectorActiveDirectiveRenderData(int ColonyId, string Directive, int RemainingTicks, int TotalTicks);
public sealed record DirectorPendingChainRenderData(
    string ParentBeatId,
    string Status,
    string ConditionSummary,
    string FollowUpBeatId,
    string FollowUpSummary,
    int RemainingWindowTicks,
    int TriggerCount,
    string LastFailureMessage);
public sealed record DirectorDomainModifierRenderData(string SourceId, string Domain, double BaseModifier, double EffectiveModifier, int RemainingTicks, int TotalDurationTicks);
public sealed record DirectorGoalBiasRenderData(int ColonyId, string SourceId, string GoalCategory, double BaseWeight, double EffectiveWeight, int RemainingTicks, int TotalDurationTicks, bool IsBlendActive);

public sealed record TileRenderData(
    int X,
    int Y,
    TileGroundView Ground,
    ResourceView NodeType,
    int NodeAmount,
    int OwnerFactionId,
    bool IsContested,
    float OwnershipStrength,
    float FoodRegrowthProgress);

public sealed record HouseRenderData(int X, int Y, int ColonyId);

public sealed record SpecializedBuildingRenderData(int X, int Y, int ColonyId, SpecializedBuildingKindView Kind);

public sealed record DefensiveStructureRenderData(
    int X,
    int Y,
    int ColonyId,
    DefensiveStructureKindView Kind,
    float Hp,
    float MaxHp,
    bool IsActive);

public sealed record PersonRenderData(
    int X,
    int Y,
    int ActorId,
    int ColonyId,
    float Health,
    bool IsInCombat,
    int LastCombatTick,
    int NoProgressStreak,
    int BackoffTicksRemaining,
    string DebugDecisionCause,
    string DebugTargetKey,
    float CombatMorale = 100f,
    bool IsRouting = false,
    int RoutingTicksRemaining = 0,
    int ActiveCombatGroupId = -1,
    int ActiveBattleId = -1,
    string Formation = "Line",
    bool IsCommander = false,
    int CommanderIntelligence = 0,
    float CommanderMoraleStabilityBonus = 0f);

public sealed record AnimalRenderData(int X, int Y, AnimalKindView Kind);

public sealed record ColonyHudData(
    int Id,
    int FactionId,
    string Name,
    float Morale,
    int Food,
    int Wood,
    int Stone,
    int Iron,
    int Gold,
    int Houses,
    int FarmPlots,
    int Workshops,
    int Storehouses,
    int ToolCharges,
    int People,
    float FoodPerPerson,
    int DeathsOldAge,
    int DeathsStarvation,
    int DeathsPredator,
    int DeathsOther,
    float AverageHunger,
    float AverageStamina,
    string ProfessionSummary,
    string WarState,
    int WarriorCount,
    int WeaponLevel,
    int ArmorLevel,
    float AverageCombatMorale = 100f
);

public sealed record CombatGroupRenderData(
    int GroupId,
    int ColonyId,
    int FactionId,
    string Formation,
    int MemberCount,
    int RoutingMemberCount,
    bool IsRouting,
    float AverageMorale,
    int CommanderActorId,
    int CommanderIntelligence,
    float CommanderMoraleStabilityBonus,
    int AnchorX,
    int AnchorY,
    float StrengthScore,
    float DefenseScore,
    int BattleId);

public sealed record BattleRenderData(
    int BattleId,
    int LeftGroupId,
    int RightGroupId,
    float LeftAverageMorale,
    float RightAverageMorale,
    bool LeftIsRouting,
    bool RightIsRouting,
    int LeftCommanderActorId,
    int RightCommanderActorId,
    int CenterX,
    int CenterY,
    int Radius,
    int Intensity,
    int ElapsedTicks);

public sealed record SiegeRenderData(
    int SiegeId,
    int AttackerColonyId,
    int DefenderColonyId,
    int TargetStructureId,
    DefensiveStructureKindView TargetKind,
    int CenterX,
    int CenterY,
    int ActiveAttackerCount,
    int StartedTick,
    int LastActiveTick,
    int BreachCount,
    string Status);

public sealed record BreachRenderData(
    int StructureId,
    int DefenderColonyId,
    int AttackerColonyId,
    int X,
    int Y,
    int CreatedTick,
    DefensiveStructureKindView StructureKind);

public sealed record FactionStanceRenderData(int LeftFactionId, int RightFactionId, string Stance);

public sealed record EcoHudData(
    int Herbivores,
    int Predators,
    int ActiveFoodNodes,
    int DepletedFoodNodes,
    int CriticalHungry,
    int AnimalStuckRecoveries,
    int PredatorDeaths,
    int PredatorHumanHits,
    int DeathsOldAge,
    int DeathsStarvation,
    int DeathsPredator,
    int DeathsOther,
    int DeathsStarvationRecent60s,
    int DeathsStarvationWithFood,
    bool PredatorHumanAttacksEnabled,
    float AverageFoodPerPerson,
    int ColoniesInFoodEmergency,
    float FoodPerPersonSpread,
    int SoftReservationCount,
    int OverlapResolveMoves,
    int CrowdDissipationMoves,
    int BirthFallbackToOccupied,
    int BirthFallbackToParent,
    int BuildSiteResetCount,
    int NoProgressBackoffResource,
    int NoProgressBackoffBuild,
    int NoProgressBackoffFlee,
    int NoProgressBackoffCombat,
    int DenseNeighborhoodTicks,
    int LastTickDenseActors
);
