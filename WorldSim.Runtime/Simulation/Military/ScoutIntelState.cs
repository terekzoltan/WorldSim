using System;

namespace WorldSim.Simulation.Military;

public enum ScoutIntelObservationKind
{
    Colony
}

public sealed class ScoutIntelState
{
    public ScoutIntelState(
        int intelId,
        Faction ownerFaction,
        Faction observedFaction,
        int observedColonyId,
        ScoutIntelObservationKind observationKind,
        int x,
        int y,
        int sourceActorId,
        long createdTick,
        int ttlTicks,
        float confidence)
    {
        IntelId = Math.Max(0, intelId);
        OwnerFaction = ownerFaction;
        ObservedFaction = observedFaction;
        ObservedColonyId = Math.Max(0, observedColonyId);
        ObservationKind = observationKind;
        X = x;
        Y = y;
        SourceActorId = Math.Max(0, sourceActorId);
        CreatedTick = Math.Max(0, createdTick);
        LastRefreshTick = CreatedTick;
        ExpirationTick = CreatedTick + Math.Max(1, ttlTicks);
        Confidence = NormalizeConfidence(confidence);
    }

    public int IntelId { get; }
    public Faction OwnerFaction { get; }
    public Faction ObservedFaction { get; }
    public int ObservedColonyId { get; }
    public ScoutIntelObservationKind ObservationKind { get; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public int SourceActorId { get; private set; }
    public long CreatedTick { get; }
    public long LastRefreshTick { get; private set; }
    public long ExpirationTick { get; private set; }
    public float Confidence { get; private set; }
    public bool IsExpired { get; private set; }

    public bool IsActive(long tick) => !IsExpired && Math.Max(0, tick) <= ExpirationTick;

    internal void Refresh(int x, int y, int sourceActorId, long tick, int ttlTicks, float confidence)
    {
        if (IsExpired)
            return;

        X = x;
        Y = y;
        SourceActorId = Math.Max(0, sourceActorId);
        LastRefreshTick = Math.Max(0, tick);
        ExpirationTick = LastRefreshTick + Math.Max(1, ttlTicks);
        Confidence = NormalizeConfidence(confidence);
    }

    internal bool TryMarkExpired(long tick)
    {
        if (IsExpired || Math.Max(0, tick) <= ExpirationTick)
            return false;

        IsExpired = true;
        return true;
    }

    private static float NormalizeConfidence(float confidence)
        => float.IsFinite(confidence) ? Math.Clamp(confidence, 0f, 1f) : 0f;
}
