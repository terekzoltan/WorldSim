using System;

namespace WorldSim.Runtime.Diagnostics;

public sealed record ScenarioContactTimelineSnapshot(
    int HostileSensed,
    int PursueStarts,
    int AdjacentContacts,
    int FactionCombatDamageEvents,
    int FactionCombatDeaths,
    int RoutingStarts,
    int BattlePairings,
    int BattleTicksWithDamage,
    int BattleTicksWithDeaths,
    int RoutingBeforeDamage)
{
    public static ScenarioContactTimelineSnapshot Empty { get; } = new(
        HostileSensed: 0,
        PursueStarts: 0,
        AdjacentContacts: 0,
        FactionCombatDamageEvents: 0,
        FactionCombatDeaths: 0,
        RoutingStarts: 0,
        BattlePairings: 0,
        BattleTicksWithDamage: 0,
        BattleTicksWithDeaths: 0,
        RoutingBeforeDamage: 0);
}

public sealed record ScenarioContactTelemetrySnapshot(
    int HostileSensed,
    int PursueStarts,
    int AdjacentContacts,
    int FactionCombatDamageEvents,
    int FactionCombatDeaths,
    int RoutingStarts,
    int BattlePairings,
    int BattleTicksWithDamage,
    int BattleTicksWithDeaths,
    int RoutingBeforeDamage,
    int? FirstHostileSenseTick,
    int? FirstPursueTick,
    int? FirstAdjacentContactTick,
    int? FirstFactionCombatDamageTick,
    int? FirstFactionCombatDeathTick,
    int? FirstBattlePairingTick,
    int? FirstBattleDamageTick,
    int? FirstBattleDeathTick,
    int? FirstRoutingTick,
    int? FirstRoutingBeforeDamageTick)
{
    public static ScenarioContactTelemetrySnapshot Empty { get; } = new(
        HostileSensed: 0,
        PursueStarts: 0,
        AdjacentContacts: 0,
        FactionCombatDamageEvents: 0,
        FactionCombatDeaths: 0,
        RoutingStarts: 0,
        BattlePairings: 0,
        BattleTicksWithDamage: 0,
        BattleTicksWithDeaths: 0,
        RoutingBeforeDamage: 0,
        FirstHostileSenseTick: null,
        FirstPursueTick: null,
        FirstAdjacentContactTick: null,
        FirstFactionCombatDamageTick: null,
        FirstFactionCombatDeathTick: null,
        FirstBattlePairingTick: null,
        FirstBattleDamageTick: null,
        FirstBattleDeathTick: null,
        FirstRoutingTick: null,
        FirstRoutingBeforeDamageTick: null);

    public ScenarioContactTimelineSnapshot ToTimelineSnapshot()
        => new(
            HostileSensed,
            PursueStarts,
            AdjacentContacts,
            FactionCombatDamageEvents,
            FactionCombatDeaths,
            RoutingStarts,
            BattlePairings,
            BattleTicksWithDamage,
            BattleTicksWithDeaths,
            RoutingBeforeDamage);
}
