using System;

namespace WorldSim.Simulation.Military;

public sealed record CampaignLogisticsOptions(
    int MaxActiveCampaignsPerFaction = 2,
    int MaxUnresolvedCampaignsPerUnorderedPair = 1,
    int MaxActiveConvoysPerFaction = 1,
    int MaxActiveForwardBasesPerFaction = 1,
    int MinimumHomeDefenseWarriors = 1,
    int ConvoySpawnCooldownTicks = 30,
    int RoutePathMaxExpansions = 4096,
    int ConvoyFoodPayload = 6,
    int ForwardBaseMinDistanceFromHome = 8,
    int ForwardBaseRadius = 2,
    int ForwardBaseLifetimeTicks = 240,
    int ForwardBaseNoMemberAbandonTicks = 30,
    float ForwardBaseRestStaminaPerTick = 2f,
    int ScoutIntelBaseRadius = 5,
    int ScoutIntelMaxRadius = 12,
    int ScoutIntelTtlTicks = 60,
    float ScoutIntelConfidence = 0.8f)
{
    public static CampaignLogisticsOptions Default { get; } = new();

    internal CampaignLogisticsOptions Normalized()
        => this with
        {
            MaxActiveCampaignsPerFaction = Math.Max(0, MaxActiveCampaignsPerFaction),
            MaxUnresolvedCampaignsPerUnorderedPair = Math.Max(0, MaxUnresolvedCampaignsPerUnorderedPair),
            MaxActiveConvoysPerFaction = Math.Max(0, MaxActiveConvoysPerFaction),
            MaxActiveForwardBasesPerFaction = Math.Max(0, MaxActiveForwardBasesPerFaction),
            MinimumHomeDefenseWarriors = Math.Max(0, MinimumHomeDefenseWarriors),
            ConvoySpawnCooldownTicks = Math.Max(0, ConvoySpawnCooldownTicks),
            RoutePathMaxExpansions = Math.Max(1, RoutePathMaxExpansions),
            ConvoyFoodPayload = Math.Max(0, ConvoyFoodPayload),
            ForwardBaseMinDistanceFromHome = Math.Max(0, ForwardBaseMinDistanceFromHome),
            ForwardBaseRadius = Math.Max(0, ForwardBaseRadius),
            ForwardBaseLifetimeTicks = Math.Max(1, ForwardBaseLifetimeTicks),
            ForwardBaseNoMemberAbandonTicks = Math.Max(1, ForwardBaseNoMemberAbandonTicks),
            ForwardBaseRestStaminaPerTick = float.IsFinite(ForwardBaseRestStaminaPerTick)
                ? Math.Clamp(ForwardBaseRestStaminaPerTick, 0f, 100f)
                : 0f,
            ScoutIntelBaseRadius = Math.Max(0, ScoutIntelBaseRadius),
            ScoutIntelMaxRadius = Math.Max(0, ScoutIntelMaxRadius),
            ScoutIntelTtlTicks = Math.Max(1, ScoutIntelTtlTicks),
            ScoutIntelConfidence = float.IsFinite(ScoutIntelConfidence)
                ? Math.Clamp(ScoutIntelConfidence, 0f, 1f)
                : 0f
        };
}

public sealed class CampaignLogisticsCounters
{
    public int CampaignLaunchBlockedByCap { get; private set; }
    public int CampaignLaunchBlockedByPairCap { get; private set; }
    public int CampaignLaunchBlockedByHomeDefense { get; private set; }
    public int CampaignLaunchRouteBudgetExhausted { get; private set; }
    public int ConvoySpawnBlockedByThrottle { get; private set; }
    public int ConvoySpawnBlockedByCap { get; private set; }
    public int ConvoySpawnBlockedByHomeDefense { get; private set; }
    public int ConvoySpawnRouteBudgetExhausted { get; private set; }
    public int ConvoyRouteBudgetExhausted { get; private set; }
    public int ConvoysSpawned { get; private set; }
    public int ConvoysDelivered { get; private set; }
    public int ConvoysFailed { get; private set; }
    public int ForwardBasesEstablished { get; private set; }
    public int ForwardBasesExpired { get; private set; }
    public int ForwardBasesAbandoned { get; private set; }
    public int ForwardBaseBuildBlockedByCap { get; private set; }
    public int ForwardBaseBuildBlockedByPlacement { get; private set; }
    public int ForwardBaseBuildBlockedByRouteBudget { get; private set; }
    public int ForwardBaseRestTicks { get; private set; }
    public int ForwardBaseRestedActorTicks { get; private set; }
    public int ScoutIntelObserved { get; private set; }
    public int ScoutIntelRefreshed { get; private set; }
    public int ScoutIntelExpired { get; private set; }

    internal void RecordCampaignLaunchBlockedByCap() => CampaignLaunchBlockedByCap++;
    internal void RecordCampaignLaunchBlockedByPairCap() => CampaignLaunchBlockedByPairCap++;
    internal void RecordCampaignLaunchBlockedByHomeDefense() => CampaignLaunchBlockedByHomeDefense++;
    internal void RecordCampaignLaunchRouteBudgetExhausted() => CampaignLaunchRouteBudgetExhausted++;
    internal void RecordConvoySpawnBlockedByThrottle() => ConvoySpawnBlockedByThrottle++;
    internal void RecordConvoySpawnBlockedByCap() => ConvoySpawnBlockedByCap++;
    internal void RecordConvoySpawnBlockedByHomeDefense() => ConvoySpawnBlockedByHomeDefense++;
    internal void RecordConvoySpawnRouteBudgetExhausted() => ConvoySpawnRouteBudgetExhausted++;
    internal void RecordConvoyRouteBudgetExhausted() => ConvoyRouteBudgetExhausted++;
    internal void RecordConvoySpawned() => ConvoysSpawned++;
    internal void RecordConvoyDelivered() => ConvoysDelivered++;
    internal void RecordConvoyFailed() => ConvoysFailed++;
    internal void RecordForwardBaseEstablished() => ForwardBasesEstablished++;
    internal void RecordForwardBaseExpired() => ForwardBasesExpired++;
    internal void RecordForwardBaseAbandoned() => ForwardBasesAbandoned++;
    internal void RecordForwardBaseBuildBlockedByCap() => ForwardBaseBuildBlockedByCap++;
    internal void RecordForwardBaseBuildBlockedByPlacement() => ForwardBaseBuildBlockedByPlacement++;
    internal void RecordForwardBaseBuildBlockedByRouteBudget() => ForwardBaseBuildBlockedByRouteBudget++;
    internal void RecordForwardBaseRest(int actorCount)
    {
        if (actorCount <= 0)
            return;

        ForwardBaseRestTicks++;
        ForwardBaseRestedActorTicks += actorCount;
    }
    internal void RecordScoutIntelObserved() => ScoutIntelObserved++;
    internal void RecordScoutIntelRefreshed() => ScoutIntelRefreshed++;
    internal void RecordScoutIntelExpired() => ScoutIntelExpired++;
}
