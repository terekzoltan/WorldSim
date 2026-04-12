using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Runtime.Diagnostics;

public sealed record ScenarioAiCountEntry(string Name, int Count);

public sealed record ScenarioAiLatestDecisionSample(
    int ActorId,
    int ColonyId,
    int X,
    int Y,
    string SelectedGoal,
    string NextCommand,
    int PlanLength,
    int PlanCost,
    string ReplanReason,
    string MethodName,
    string DebugDecisionCause,
    string DebugTargetKey,
    string TargetKind);

public sealed record ScenarioAiTimelineSnapshot(
    string? TopGoal,
    int TopGoalCount,
    string? TopCommand,
    int TopCommandCount,
    string? TopReplanReason,
    int TopReplanReasonCount,
    string? TopDebugCause,
    int TopDebugCauseCount)
{
    public static ScenarioAiTimelineSnapshot Empty { get; } = new(
        TopGoal: null,
        TopGoalCount: 0,
        TopCommand: null,
        TopCommandCount: 0,
        TopReplanReason: null,
        TopReplanReasonCount: 0,
        TopDebugCause: null,
        TopDebugCauseCount: 0);
}

public sealed record ScenarioAiTelemetrySnapshot(
    int DecisionCount,
    IReadOnlyList<ScenarioAiCountEntry> GoalCounts,
    IReadOnlyList<ScenarioAiCountEntry> CommandCounts,
    IReadOnlyList<ScenarioAiCountEntry> ReplanReasonCounts,
    IReadOnlyList<ScenarioAiCountEntry> MethodCounts,
    IReadOnlyList<ScenarioAiCountEntry> DebugCauseCounts,
    IReadOnlyList<ScenarioAiCountEntry> TargetKindCounts,
    IReadOnlyList<ScenarioAiCountEntry> TopGoals,
    IReadOnlyList<ScenarioAiCountEntry> TopDebugCauses,
    ScenarioAiLatestDecisionSample? LatestDecision)
{
    public static ScenarioAiTelemetrySnapshot Empty { get; } = new(
        DecisionCount: 0,
        GoalCounts: Array.Empty<ScenarioAiCountEntry>(),
        CommandCounts: Array.Empty<ScenarioAiCountEntry>(),
        ReplanReasonCounts: Array.Empty<ScenarioAiCountEntry>(),
        MethodCounts: Array.Empty<ScenarioAiCountEntry>(),
        DebugCauseCounts: Array.Empty<ScenarioAiCountEntry>(),
        TargetKindCounts: Array.Empty<ScenarioAiCountEntry>(),
        TopGoals: Array.Empty<ScenarioAiCountEntry>(),
        TopDebugCauses: Array.Empty<ScenarioAiCountEntry>(),
        LatestDecision: null);

    public ScenarioAiTimelineSnapshot ToTimelineSnapshot()
    {
        var topGoal = GoalCounts.FirstOrDefault();
        var topCommand = CommandCounts.FirstOrDefault();
        var topReplanReason = ReplanReasonCounts.FirstOrDefault();
        var topDebugCause = DebugCauseCounts.FirstOrDefault();

        return new ScenarioAiTimelineSnapshot(
            TopGoal: topGoal?.Name,
            TopGoalCount: topGoal?.Count ?? 0,
            TopCommand: topCommand?.Name,
            TopCommandCount: topCommand?.Count ?? 0,
            TopReplanReason: topReplanReason?.Name,
            TopReplanReasonCount: topReplanReason?.Count ?? 0,
            TopDebugCause: topDebugCause?.Name,
            TopDebugCauseCount: topDebugCause?.Count ?? 0);
    }
}

public static class ScenarioAiTargetKindClassifier
{
    public static string Normalize(string? debugTargetKey)
    {
        if (string.IsNullOrWhiteSpace(debugTargetKey)
            || string.Equals(debugTargetKey, "none", StringComparison.OrdinalIgnoreCase))
            return "none";

        if (debugTargetKey.StartsWith("build:", StringComparison.OrdinalIgnoreCase))
            return "build";

        if (debugTargetKey.StartsWith("resource:", StringComparison.OrdinalIgnoreCase))
            return "resource";

        if (debugTargetKey.StartsWith("retreat:", StringComparison.OrdinalIgnoreCase))
            return "retreat";

        if (debugTargetKey.StartsWith("move:", StringComparison.OrdinalIgnoreCase))
            return "move";

        return "other";
    }
}
