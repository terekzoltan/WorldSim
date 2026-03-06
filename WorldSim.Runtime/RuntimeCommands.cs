namespace WorldSim.Runtime;

public readonly record struct RuntimeFeatureFlags(
    bool EnableCombatPrimitives,
    bool EnableDiplomacy,
    bool EnableFortifications,
    bool EnableSiege,
    bool EnableSupply,
    bool EnableCampaigns,
    bool EnablePredatorHumanAttacks
);

public readonly record struct RuntimeCommandResult(bool Success, string Message)
{
    public static RuntimeCommandResult Ok(string message) => new(true, message);

    public static RuntimeCommandResult Fail(string message) => new(false, message);
}

public abstract record RuntimePatchCommand;

public sealed record UnlockTechRuntimeCommand(string TechId) : RuntimePatchCommand;

public readonly record struct DirectorDomainModifierSpec(string Domain, double Modifier, int DurationTicks);

public readonly record struct DirectorGoalBiasSpec(string GoalCategory, double Weight, int? DurationTicks);

public sealed record ApplyStoryBeatRuntimeCommand(
    string BeatId,
    string Text,
    long DurationTicks,
    IReadOnlyList<DirectorDomainModifierSpec> Effects) : RuntimePatchCommand;

public sealed record ApplyColonyDirectiveRuntimeCommand(
    int ColonyId,
    string Directive,
    long DurationTicks,
    IReadOnlyList<DirectorGoalBiasSpec> Biases) : RuntimePatchCommand;
