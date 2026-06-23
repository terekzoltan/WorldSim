namespace WorldSim.Contracts.V2;

public static class RefineryVocabulary
{
    public const string OutputModeBoth = "both";
    public const string OutputModeStoryOnly = "story_only";
    public const string OutputModeNudgeOnly = "nudge_only";
    public const string OutputModeOff = "off";

    public const string SeverityMinor = "minor";
    public const string SeverityMajor = "major";
    public const string SeverityEpic = "epic";

    public const string EffectTypeDomainModifier = "domain_modifier";
    public const string BiasTypeGoalBias = "goal_bias";

    public const string TreatyKindCeasefire = "ceasefire";
    public const string TreatyKindPeaceTalks = "peace_talks";

    public static readonly IReadOnlyList<string> SharedOutputModes =
    [
        OutputModeBoth,
        OutputModeStoryOnly,
        OutputModeNudgeOnly,
        OutputModeOff
    ];

    public static readonly IReadOnlyList<string> Severities =
    [
        SeverityMinor,
        SeverityMajor,
        SeverityEpic
    ];

    public static readonly IReadOnlyList<string> Domains =
    [
        "food",
        "morale",
        "economy",
        "military",
        "research"
    ];

    public static readonly IReadOnlyList<string> GoalCategories =
    [
        "farming",
        "gathering",
        "crafting",
        "building",
        "social",
        "military",
        "research",
        "rest"
    ];

    public static readonly IReadOnlyList<string> TreatyKinds =
    [
        TreatyKindCeasefire,
        TreatyKindPeaceTalks
    ];

    public static bool IsSharedOutputMode(string mode) => SharedOutputModes.Contains(mode, StringComparer.Ordinal);

    public static bool IsTreatyKind(string treatyKind) => TreatyKinds.Contains(treatyKind, StringComparer.Ordinal);

    public static bool IsSeverity(string severity) => Severities.Contains(severity, StringComparer.Ordinal);
}
