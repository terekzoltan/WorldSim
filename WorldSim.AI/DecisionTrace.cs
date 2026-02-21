namespace WorldSim.AI;

public readonly record struct GoalScoreEntry(string GoalName, float Score, bool IsOnCooldown);

public readonly record struct PlannerDecision(
    NpcCommand Command,
    int PlanLength,
    IReadOnlyList<NpcCommand> PlanPreview);

public sealed record AiDecisionTrace(
    string SelectedGoal,
    string PlannerName,
    string PolicyName,
    int PlanLength,
    IReadOnlyList<NpcCommand> PlanPreview,
    IReadOnlyList<GoalScoreEntry> GoalScores);

public readonly record struct AiDecisionResult(NpcCommand Command, AiDecisionTrace Trace);
