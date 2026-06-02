using System;

namespace WorldSim.Simulation.Military;

public enum ForwardBasePhase
{
    Active,
    Expired,
    Abandoned
}

public static class ForwardBaseCloseReasons
{
    public const string None = "none";
    public const string Expired = "expired";
    public const string CampaignResolved = "campaign_resolved";
    public const string NoLiveMember = "no_live_member";
}

public sealed class ForwardBaseState
{
    public ForwardBaseState(
        int baseId,
        Faction ownerFaction,
        int homeColonyId,
        int campaignId,
        int armyId,
        long createdTick,
        int x,
        int y,
        int radius)
    {
        BaseId = Math.Max(0, baseId);
        OwnerFaction = ownerFaction;
        HomeColonyId = Math.Max(0, homeColonyId);
        CampaignId = Math.Max(0, campaignId);
        ArmyId = Math.Max(0, armyId);
        CreatedTick = Math.Max(0, createdTick);
        X = x;
        Y = y;
        Radius = Math.Max(0, radius);
        LastLiveMemberNearTick = CreatedTick;
    }

    public int BaseId { get; }
    public Faction OwnerFaction { get; }
    public int HomeColonyId { get; }
    public int CampaignId { get; }
    public int ArmyId { get; }
    public long CreatedTick { get; }
    public long EndedTick { get; private set; } = -1;
    public int X { get; }
    public int Y { get; }
    public int Radius { get; }
    public ForwardBasePhase Phase { get; private set; } = ForwardBasePhase.Active;
    public string CloseReason { get; private set; } = ForwardBaseCloseReasons.None;
    public long LastLiveMemberNearTick { get; private set; }
    public int RestTicks { get; private set; }
    public int RestedActorTicks { get; private set; }

    public bool IsActive => Phase == ForwardBasePhase.Active;

    internal void RecordLiveMemberNear(long tick)
    {
        if (!IsActive)
            return;

        LastLiveMemberNearTick = Math.Max(0, tick);
    }

    internal void RecordRest(int actorCount)
    {
        if (!IsActive || actorCount <= 0)
            return;

        RestTicks++;
        RestedActorTicks += actorCount;
    }

    internal void MarkExpired(long tick, string reason = ForwardBaseCloseReasons.Expired)
    {
        if (!IsActive)
            return;

        Phase = ForwardBasePhase.Expired;
        EndedTick = Math.Max(0, tick);
        CloseReason = NormalizeReason(reason);
    }

    internal void MarkAbandoned(long tick, string reason)
    {
        if (!IsActive)
            return;

        Phase = ForwardBasePhase.Abandoned;
        EndedTick = Math.Max(0, tick);
        CloseReason = NormalizeReason(reason);
    }

    private static string NormalizeReason(string? reason)
        => string.IsNullOrWhiteSpace(reason) ? ForwardBaseCloseReasons.None : reason.Trim();
}
