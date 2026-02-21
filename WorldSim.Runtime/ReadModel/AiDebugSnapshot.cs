namespace WorldSim.Runtime.ReadModel;

public sealed record AiGoalScoreData(string GoalName, float Score, bool IsOnCooldown);

public sealed record AiDebugSnapshot(
    bool HasData,
    string PlannerMode,
    string PolicyMode,
    string TrackingMode,
    int TrackedNpcIndex,
    int TrackedNpcCount,
    long DecisionSequence,
    int TrackedColonyId,
    int TrackedX,
    int TrackedY,
    string SelectedGoal,
    string NextCommand,
    int PlanLength,
    int PlanCost,
    string ReplanReason,
    string MethodName,
    IReadOnlyList<AiGoalScoreData> GoalScores,
    IReadOnlyList<string> RecentDecisions)
{
    public static AiDebugSnapshot Empty(string plannerMode, string policyMode) => new(
        HasData: false,
        PlannerMode: plannerMode,
        PolicyMode: policyMode,
        TrackingMode: "Latest",
        TrackedNpcIndex: 0,
        TrackedNpcCount: 0,
        DecisionSequence: 0,
        TrackedColonyId: -1,
        TrackedX: -1,
        TrackedY: -1,
        SelectedGoal: "None",
        NextCommand: "Idle",
        PlanLength: 0,
        PlanCost: 0,
        ReplanReason: "None",
        MethodName: "None",
        GoalScores: Array.Empty<AiGoalScoreData>(),
        RecentDecisions: Array.Empty<string>());
}
