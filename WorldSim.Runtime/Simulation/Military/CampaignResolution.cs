namespace WorldSim.Simulation.Military;

public enum CampaignResolutionKind
{
    None,
    AttackerVictory,
    DefenderHeld
}

public static class CampaignResolutionReasons
{
    public const string None = "none";
    public const string SiegeBreached = "siege_breached";
    public const string DefenderTimeout = "defender_timeout";
    public const string NoTarget = "no_target";
}

public static class CampaignResolutionPolicy
{
    public const int DefenderHeldEncounterTimeoutTicks = 120;
    public const int AttackerVictoryWarScoreDelta = 60;
    public const int DefenderHeldWarScoreDelta = -30;
    public const int PeaceEligibilityWarScoreThreshold = 60;
    public const int LootFoodCap = 6;
    public const int LootWoodCap = 4;
    public const int LootStoneCap = 3;
    public const int LootGoldCap = 1;
    public const string CeasefireTreatyKind = "ceasefire";
}

internal sealed record CampaignResolutionApplication(
    CampaignResolutionKind Kind,
    string Reason,
    long ResolvedTick,
    Faction AttackerFaction,
    Faction DefenderFaction,
    int OriginColonyId,
    int TargetColonyId,
    int TargetStructureId,
    int LootFood,
    int LootWood,
    int LootStone,
    int LootGold,
    int WarScoreDelta,
    int CumulativeWarScore,
    bool PeaceEligible,
    bool PeaceApplied,
    string TreatyKind);

public sealed class CampaignResolutionState
{
    public bool IsResolved { get; private set; }
    public CampaignResolutionKind Kind { get; private set; } = CampaignResolutionKind.None;
    public string Reason { get; private set; } = CampaignResolutionReasons.None;
    public long ResolvedTick { get; private set; } = -1;
    public Faction AttackerFaction { get; private set; }
    public Faction DefenderFaction { get; private set; }
    public int OriginColonyId { get; private set; } = -1;
    public int TargetColonyId { get; private set; } = -1;
    public int TargetStructureId { get; private set; } = -1;
    public int LootFood { get; private set; }
    public int LootWood { get; private set; }
    public int LootStone { get; private set; }
    public int LootGold { get; private set; }
    public int WarScoreDelta { get; private set; }
    public int CumulativeWarScore { get; private set; }
    public bool PeaceEligible { get; private set; }
    public bool PeaceApplied { get; private set; }
    public string TreatyKind { get; private set; } = CampaignResolutionReasons.None;

    internal bool TryApply(CampaignResolutionApplication application)
    {
        if (IsResolved)
            return false;

        IsResolved = true;
        Kind = application.Kind;
        Reason = string.IsNullOrWhiteSpace(application.Reason)
            ? CampaignResolutionReasons.None
            : application.Reason;
        ResolvedTick = application.ResolvedTick;
        AttackerFaction = application.AttackerFaction;
        DefenderFaction = application.DefenderFaction;
        OriginColonyId = application.OriginColonyId;
        TargetColonyId = application.TargetColonyId;
        TargetStructureId = application.TargetStructureId;
        LootFood = application.LootFood;
        LootWood = application.LootWood;
        LootStone = application.LootStone;
        LootGold = application.LootGold;
        WarScoreDelta = application.WarScoreDelta;
        CumulativeWarScore = application.CumulativeWarScore;
        PeaceEligible = application.PeaceEligible;
        PeaceApplied = application.PeaceApplied;
        TreatyKind = string.IsNullOrWhiteSpace(application.TreatyKind)
            ? CampaignResolutionReasons.None
            : application.TreatyKind;
        return true;
    }
}
