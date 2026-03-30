namespace WorldSim.Runtime;

public sealed record DirectorExecutionState(
    string EffectiveOutputMode,
    string EffectiveOutputModeSource,
    string Stage,
    long Tick,
    bool IsDirectorGoal,
    string ApplyStatus)
{
    public static DirectorExecutionState NotTriggered { get; } = new(
        EffectiveOutputMode: "unknown",
        EffectiveOutputModeSource: "unknown",
        Stage: "not_triggered",
        Tick: -1,
        IsDirectorGoal: false,
        ApplyStatus: "not_triggered");
}
