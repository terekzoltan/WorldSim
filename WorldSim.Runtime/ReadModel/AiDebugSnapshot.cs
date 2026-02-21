namespace WorldSim.Runtime.ReadModel;

public sealed record AiGoalScoreData(string GoalName, float Score, bool IsOnCooldown);

public sealed record AiDebugSnapshot(
    bool HasData,
    string PlannerMode,
    string PolicyMode,
    int TrackedColonyId,
    int TrackedX,
    int TrackedY,
    string SelectedGoal,
    string NextCommand,
    int PlanLength,
    IReadOnlyList<AiGoalScoreData> GoalScores,
    IReadOnlyList<string> RecentDecisions)
{
    public static AiDebugSnapshot Empty(string plannerMode, string policyMode) => new(
        HasData: false,
        PlannerMode: plannerMode,
        PolicyMode: policyMode,
        TrackedColonyId: -1,
        TrackedX: -1,
        TrackedY: -1,
        SelectedGoal: "None",
        NextCommand: "Idle",
        PlanLength: 0,
        GoalScores: Array.Empty<AiGoalScoreData>(),
        RecentDecisions: Array.Empty<string>());
}
