using System.Globalization;
using WorldSim.Contracts.V2;

namespace WorldSim.RefineryAdapter.Integration;

public sealed record RefineryRuntimeOptions(
    RefineryIntegrationMode Mode,
    string Goal,
    string DirectorOutputMode,
    string FixtureResponsePath,
    string ServiceBaseUrl,
    bool StrictMode,
    long RequestSeed,
    int LiveTimeoutMs,
    int LiveRetryCount,
    int CircuitBreakerSeconds,
    bool ApplyToWorld,
    int MinTriggerIntervalMs,
    double DirectorMaxBudget = 5d,
    string OperatorProfileName = "auto"
)
{
    public const string ProfileFixtureSmoke = "fixture_smoke";
    public const string ProfileLiveMock = "live_mock";
    public const string ProfileLiveDirector = "live_director";

    private static readonly string[] OperatorPresetCycle =
    {
        ProfileFixtureSmoke,
        ProfileLiveMock,
        ProfileLiveDirector
    };

    public static RefineryRuntimeOptions FromEnvironment(string baseDirectory, bool applyOperatorPreset = true)
    {
        var modeRaw = System.Environment.GetEnvironmentVariable("REFINERY_INTEGRATION_MODE") ?? "off";
        var mode = modeRaw.Trim().ToLowerInvariant() switch
        {
            "fixture" => RefineryIntegrationMode.Fixture,
            "live" => RefineryIntegrationMode.Live,
            _ => RefineryIntegrationMode.Off
        };

        var goal = System.Environment.GetEnvironmentVariable("REFINERY_GOAL") ?? "TECH_TREE_PATCH";
        var directorOutputMode = NormalizeDirectorOutputMode(
            System.Environment.GetEnvironmentVariable("REFINERY_DIRECTOR_OUTPUT_MODE")
        );
        var operatorPresetName = NormalizeOperatorPresetName(System.Environment.GetEnvironmentVariable("REFINERY_OPERATOR_PRESET"));
        var operatorProfileName = ResolveOperatorProfileName(mode, goal, applyToWorld: string.Equals(System.Environment.GetEnvironmentVariable("REFINERY_APPLY_TO_WORLD"), "true", System.StringComparison.OrdinalIgnoreCase));
        var strictMode = !string.Equals(System.Environment.GetEnvironmentVariable("REFINERY_LENIENT"), "true", System.StringComparison.OrdinalIgnoreCase);
        var serviceBaseUrl = System.Environment.GetEnvironmentVariable("REFINERY_BASE_URL") ?? "http://localhost:8091";
        var requestSeed = ParseLongEnv("REFINERY_REQUEST_SEED", 123L);
        var timeoutMs = ParseIntEnv("REFINERY_TIMEOUT_MS", 1200);
        var retryCount = ParseIntEnv("REFINERY_RETRY_COUNT", 1);
        var breakerSeconds = ParseIntEnv("REFINERY_BREAKER_SECONDS", 10);
        var applyToWorld = string.Equals(System.Environment.GetEnvironmentVariable("REFINERY_APPLY_TO_WORLD"), "true", System.StringComparison.OrdinalIgnoreCase);
        var minTriggerIntervalMs = ParseIntEnv("REFINERY_MIN_TRIGGER_MS", 500);
        var directorMaxBudget = ParseDoubleEnv("REFINERY_DIRECTOR_MAX_BUDGET", 5d);

        var defaultFixtureFile = string.Equals(goal, DirectorGoals.SeasonDirectorCheckpoint, System.StringComparison.Ordinal)
            ? "patch-season-director-v1.expected.json"
            : "patch-tech-tree-v1.expected.json";

        var defaultFixture = System.IO.Path.Combine(
            FindRepoRoot(baseDirectory),
            "refinery-service-java",
            "examples",
            "responses",
            defaultFixtureFile
        );
        var fixtureResponsePath = System.Environment.GetEnvironmentVariable("REFINERY_FIXTURE_RESPONSE") ?? defaultFixture;

        var options = new RefineryRuntimeOptions(
            mode,
            goal,
            directorOutputMode,
            fixtureResponsePath,
            serviceBaseUrl,
            strictMode,
            requestSeed,
            timeoutMs,
            retryCount,
            breakerSeconds,
            applyToWorld,
            minTriggerIntervalMs,
            directorMaxBudget,
            operatorProfileName
        );

        if (applyOperatorPreset && operatorPresetName is not null)
            return ApplyOperatorPreset(options, operatorPresetName);

        return options;
    }

    public static string? ReadOperatorPresetFromEnvironment()
    {
        return NormalizeOperatorPresetName(System.Environment.GetEnvironmentVariable("REFINERY_OPERATOR_PRESET"));
    }

    public static string NextOperatorPresetName(string currentProfileName)
    {
        var current = NormalizeOperatorPresetName(currentProfileName) ?? ProfileFixtureSmoke;
        var index = Array.FindIndex(OperatorPresetCycle, name => string.Equals(name, current, StringComparison.Ordinal));
        if (index < 0)
            return ProfileFixtureSmoke;

        return OperatorPresetCycle[(index + 1) % OperatorPresetCycle.Length];
    }

    public static string? NormalizeOperatorPresetName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Trim().ToLowerInvariant().Replace('-', '_');
        return normalized switch
        {
            ProfileFixtureSmoke => ProfileFixtureSmoke,
            ProfileLiveMock => ProfileLiveMock,
            ProfileLiveDirector => ProfileLiveDirector,
            _ => null
        };
    }

    public static RefineryRuntimeOptions ApplyOperatorPreset(RefineryRuntimeOptions baseline, string presetName)
    {
        return presetName switch
        {
            ProfileFixtureSmoke => baseline with
            {
                Mode = RefineryIntegrationMode.Fixture,
                Goal = DirectorGoals.SeasonDirectorCheckpoint,
                DirectorOutputMode = "auto",
                ApplyToWorld = true,
                OperatorProfileName = ProfileFixtureSmoke
            },
            ProfileLiveMock => baseline with
            {
                Mode = RefineryIntegrationMode.Live,
                Goal = DirectorGoals.SeasonDirectorCheckpoint,
                DirectorOutputMode = "auto",
                ApplyToWorld = true,
                OperatorProfileName = ProfileLiveMock
            },
            ProfileLiveDirector => baseline with
            {
                Mode = RefineryIntegrationMode.Live,
                Goal = DirectorGoals.SeasonDirectorCheckpoint,
                DirectorOutputMode = "auto",
                ApplyToWorld = true,
                LiveTimeoutMs = Math.Max(12000, baseline.LiveTimeoutMs),
                LiveRetryCount = 0,
                OperatorProfileName = ProfileLiveDirector
            },
            _ => baseline
        };
    }

    private static int ParseIntEnv(string key, int fallback)
    {
        var raw = System.Environment.GetEnvironmentVariable(key);
        return int.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    private static long ParseLongEnv(string key, long fallback)
    {
        var raw = System.Environment.GetEnvironmentVariable(key);
        return long.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    private static double ParseDoubleEnv(string key, double fallback)
    {
        var raw = System.Environment.GetEnvironmentVariable(key);
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return fallback;

        if (double.IsNaN(parsed) || double.IsInfinity(parsed))
            return fallback;

        return Math.Max(0d, parsed);
    }

    private static string FindRepoRoot(string baseDirectory)
    {
        var current = new System.IO.DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            var marker = System.IO.Path.Combine(current.FullName, "refinery-service-java");
            if (System.IO.Directory.Exists(marker))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        return baseDirectory;
    }

    private static string NormalizeDirectorOutputMode(string? raw)
    {
        var normalized = string.IsNullOrWhiteSpace(raw) ? "auto" : raw.Trim().ToLowerInvariant();
        return normalized switch
        {
            "auto" or "both" or "story_only" or "nudge_only" or "off" => normalized,
            _ => "auto"
        };
    }

    private static string ResolveOperatorProfileName(RefineryIntegrationMode mode, string goal, bool applyToWorld)
    {
        var explicitProfile = System.Environment.GetEnvironmentVariable("REFINERY_OPERATOR_PROFILE");
        if (!string.IsNullOrWhiteSpace(explicitProfile))
            return explicitProfile.Trim().Replace('-', '_');

        if (mode == RefineryIntegrationMode.Off)
            return "integration_off";

        if (mode == RefineryIntegrationMode.Fixture)
            return ProfileFixtureSmoke;

        if (string.Equals(goal, DirectorGoals.SeasonDirectorCheckpoint, System.StringComparison.Ordinal))
            return ProfileLiveDirector;

        return ProfileLiveMock;
    }
}
