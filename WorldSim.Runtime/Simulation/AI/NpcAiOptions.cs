using System;
using System.Collections.Generic;

namespace WorldSim.Simulation;

public enum NpcPlannerMode
{
    Goap,
    Simple,
    Htn
}

public enum NpcPolicyMode
{
    GlobalPlanner,
    FactionMix,
    HtnPilot
}

public sealed class RuntimeAiOptions
{
    public NpcPlannerMode PlannerMode { get; init; } = NpcPlannerMode.Goap;
    public NpcPolicyMode PolicyMode { get; init; } = NpcPolicyMode.GlobalPlanner;
    public IReadOnlyDictionary<Faction, NpcPlannerMode> FactionPlannerTable { get; init; } = DefaultFactionPlannerTable;
    public NpcPlannerMode DefaultFactionPlanner { get; init; } = NpcPlannerMode.Goap;

    private static readonly IReadOnlyDictionary<Faction, NpcPlannerMode> DefaultFactionPlannerTable =
        new Dictionary<Faction, NpcPlannerMode>
        {
            [Faction.Sylvars] = NpcPlannerMode.Goap,
            [Faction.Obsidari] = NpcPlannerMode.Simple,
            [Faction.Aetheri] = NpcPlannerMode.Htn
        };

    public static RuntimeAiOptions FromEnvironment()
    {
        var plannerValue = System.Environment.GetEnvironmentVariable("WORLDSIM_AI_PLANNER");
        var policyValue = System.Environment.GetEnvironmentVariable("WORLDSIM_AI_POLICY");

        var plannerMode = NpcPlannerMode.Goap;
        if (string.Equals(plannerValue, "simple", System.StringComparison.OrdinalIgnoreCase))
            plannerMode = NpcPlannerMode.Simple;
        else if (string.Equals(plannerValue, "htn", System.StringComparison.OrdinalIgnoreCase))
            plannerMode = NpcPlannerMode.Htn;

        var policyMode = NpcPolicyMode.GlobalPlanner;
        if (string.Equals(policyValue, "faction-mix", System.StringComparison.OrdinalIgnoreCase))
            policyMode = NpcPolicyMode.FactionMix;
        else if (string.Equals(policyValue, "htn-pilot", System.StringComparison.OrdinalIgnoreCase))
            policyMode = NpcPolicyMode.HtnPilot;

        if (policyMode == NpcPolicyMode.HtnPilot)
            plannerMode = NpcPlannerMode.Htn;

        var factionTable = ParseFactionPlannerTable(System.Environment.GetEnvironmentVariable("WORLDSIM_AI_POLICY_TABLE"));

        return new RuntimeAiOptions
        {
            PlannerMode = plannerMode,
            PolicyMode = policyMode,
            FactionPlannerTable = factionTable.Table,
            DefaultFactionPlanner = factionTable.DefaultPlanner
        };
    }

    public NpcPlannerMode ResolveFactionPlanner(Faction faction)
    {
        return FactionPlannerTable.TryGetValue(faction, out var planner)
            ? planner
            : DefaultFactionPlanner;
    }

    private static (IReadOnlyDictionary<Faction, NpcPlannerMode> Table, NpcPlannerMode DefaultPlanner) ParseFactionPlannerTable(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (DefaultFactionPlannerTable, NpcPlannerMode.Goap);

        var map = new Dictionary<Faction, NpcPlannerMode>(DefaultFactionPlannerTable);
        var defaultPlanner = NpcPlannerMode.Goap;
        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            var pair = entry.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2)
                continue;

            if (!TryParsePlanner(pair[1], out var planner))
                continue;

            if (string.Equals(pair[0], "default", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair[0], "*", StringComparison.OrdinalIgnoreCase))
            {
                defaultPlanner = planner;
                continue;
            }

            if (!TryParseFaction(pair[0], out var faction))
                continue;

            map[faction] = planner;
        }

        return (map, defaultPlanner);
    }

    private static bool TryParseFaction(string value, out Faction faction)
    {
        if (Enum.TryParse<Faction>(value, ignoreCase: true, out faction))
            return true;

        return false;
    }

    private static bool TryParsePlanner(string value, out NpcPlannerMode planner)
    {
        planner = NpcPlannerMode.Goap;
        return Enum.TryParse(value, ignoreCase: true, out planner);
    }
}
