namespace WorldSim.Runtime;

public sealed record DirectorExecutionState(
    string EffectiveOutputMode,
    string EffectiveOutputModeSource,
    string Stage,
    long Tick,
    bool IsDirectorGoal)
{
    public static DirectorExecutionState NotTriggered { get; } = new(
        EffectiveOutputMode: "both",
        EffectiveOutputModeSource: "unknown",
        Stage: "idle",
        Tick: -1,
        IsDirectorGoal: false);
}
