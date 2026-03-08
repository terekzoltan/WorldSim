namespace WorldSim.RefineryAdapter.Integration;

public sealed record DirectorExecutionStatus(
    string EffectiveOutputMode,
    string EffectiveOutputModeSource,
    string Stage,
    long Tick,
    bool IsDirectorGoal
)
{
    public static DirectorExecutionStatus NotTriggered { get; } = new(
        EffectiveOutputMode: "unknown",
        EffectiveOutputModeSource: "unknown",
        Stage: "not_triggered",
        Tick: -1,
        IsDirectorGoal: false
    );
}
