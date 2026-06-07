using System;

namespace WorldSim.Simulation.Military;

public enum SiegeUnitKind
{
    Ram,
    SiegeTower,
    MobileCatapult
}

public enum SiegeUnitPhase
{
    Active,
    Inactive
}

public static class SiegeUnitInactiveReasons
{
    public const string None = "none";
    public const string CampaignResolved = "campaign_resolved";
    public const string CampaignInvalid = "campaign_invalid";
    public const string SiegeDisabled = "siege_disabled";
}

public sealed class SiegeUnitState
{
    public SiegeUnitState(
        int siegeUnitId,
        int campaignId,
        int armyId,
        Faction ownerFaction,
        SiegeUnitKind kind,
        long createdTick,
        int x,
        int y,
        int targetStructureId,
        int targetX,
        int targetY,
        float maxHealth)
    {
        SiegeUnitId = Math.Max(0, siegeUnitId);
        CampaignId = Math.Max(0, campaignId);
        ArmyId = Math.Max(0, armyId);
        OwnerFaction = ownerFaction;
        Kind = kind;
        CreatedTick = Math.Max(0, createdTick);
        X = x;
        Y = y;
        TargetStructureId = targetStructureId;
        TargetX = targetX;
        TargetY = targetY;
        MaxHealth = Math.Max(1f, maxHealth);
        Health = MaxHealth;
    }

    public int SiegeUnitId { get; }
    public int CampaignId { get; }
    public int ArmyId { get; }
    public Faction OwnerFaction { get; }
    public SiegeUnitKind Kind { get; }
    public long CreatedTick { get; }
    public long EndedTick { get; private set; } = -1;
    public int X { get; private set; }
    public int Y { get; private set; }
    public int TargetStructureId { get; private set; }
    public int TargetX { get; private set; }
    public int TargetY { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; }
    public SiegeUnitPhase Phase { get; private set; } = SiegeUnitPhase.Active;
    public string InactiveReason { get; private set; } = SiegeUnitInactiveReasons.None;
    public string RecentActionEffect { get; private set; } = "ready";
    public long LastActionTick { get; private set; } = -1;

    public bool IsActive => Phase == SiegeUnitPhase.Active;

    internal void RefreshPosition(int x, int y, int targetStructureId, int targetX, int targetY)
    {
        if (!IsActive)
            return;

        X = x;
        Y = y;
        TargetStructureId = targetStructureId;
        TargetX = targetX;
        TargetY = targetY;
    }

    internal void RecordAction(string effect, long tick)
    {
        if (!IsActive)
            return;

        RecentActionEffect = string.IsNullOrWhiteSpace(effect) ? "ready" : effect.Trim();
        LastActionTick = Math.Max(0, tick);
    }

    internal void MarkInactive(long tick, string reason)
    {
        if (!IsActive)
            return;

        Phase = SiegeUnitPhase.Inactive;
        EndedTick = Math.Max(0, tick);
        InactiveReason = string.IsNullOrWhiteSpace(reason) ? SiegeUnitInactiveReasons.None : reason.Trim();
        RecentActionEffect = InactiveReason;
        LastActionTick = Math.Max(0, tick);
    }
}
