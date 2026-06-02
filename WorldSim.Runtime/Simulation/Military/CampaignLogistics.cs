using System;

namespace WorldSim.Simulation.Military;

public sealed record CampaignLogisticsOptions(
    int MaxActiveCampaignsPerFaction = 2,
    int MaxActiveConvoysPerFaction = 1,
    int MinimumHomeDefenseWarriors = 1,
    int ConvoySpawnCooldownTicks = 30,
    int RoutePathMaxExpansions = 4096,
    int ConvoyFoodPayload = 6)
{
    public static CampaignLogisticsOptions Default { get; } = new();

    internal CampaignLogisticsOptions Normalized()
        => this with
        {
            MaxActiveCampaignsPerFaction = Math.Max(0, MaxActiveCampaignsPerFaction),
            MaxActiveConvoysPerFaction = Math.Max(0, MaxActiveConvoysPerFaction),
            MinimumHomeDefenseWarriors = Math.Max(0, MinimumHomeDefenseWarriors),
            ConvoySpawnCooldownTicks = Math.Max(0, ConvoySpawnCooldownTicks),
            RoutePathMaxExpansions = Math.Max(1, RoutePathMaxExpansions),
            ConvoyFoodPayload = Math.Max(0, ConvoyFoodPayload)
        };
}

public sealed class CampaignLogisticsCounters
{
    public int CampaignLaunchBlockedByCap { get; private set; }
    public int CampaignLaunchBlockedByHomeDefense { get; private set; }
    public int ConvoySpawnBlockedByThrottle { get; private set; }
    public int ConvoySpawnBlockedByCap { get; private set; }
    public int ConvoySpawnBlockedByHomeDefense { get; private set; }
    public int ConvoySpawnRouteBudgetExhausted { get; private set; }
    public int ConvoyRouteBudgetExhausted { get; private set; }
    public int ConvoysSpawned { get; private set; }
    public int ConvoysDelivered { get; private set; }
    public int ConvoysFailed { get; private set; }

    internal void RecordCampaignLaunchBlockedByCap() => CampaignLaunchBlockedByCap++;
    internal void RecordCampaignLaunchBlockedByHomeDefense() => CampaignLaunchBlockedByHomeDefense++;
    internal void RecordConvoySpawnBlockedByThrottle() => ConvoySpawnBlockedByThrottle++;
    internal void RecordConvoySpawnBlockedByCap() => ConvoySpawnBlockedByCap++;
    internal void RecordConvoySpawnBlockedByHomeDefense() => ConvoySpawnBlockedByHomeDefense++;
    internal void RecordConvoySpawnRouteBudgetExhausted() => ConvoySpawnRouteBudgetExhausted++;
    internal void RecordConvoyRouteBudgetExhausted() => ConvoyRouteBudgetExhausted++;
    internal void RecordConvoySpawned() => ConvoysSpawned++;
    internal void RecordConvoyDelivered() => ConvoysDelivered++;
    internal void RecordConvoyFailed() => ConvoysFailed++;
}
