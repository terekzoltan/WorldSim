namespace WorldSim.Integration;

public sealed record RefineryRuntimeOptions(
    RefineryIntegrationMode Mode,
    string Goal,
    string FixtureResponsePath,
    string ServiceBaseUrl,
    bool StrictMode,
    long RequestSeed,
    int LiveTimeoutMs,
    int LiveRetryCount,
    int CircuitBreakerSeconds,
    bool ApplyToWorld,
    int MinTriggerIntervalMs
)
{
    public static RefineryRuntimeOptions FromEnvironment(string baseDirectory)
    {
        var modeRaw = System.Environment.GetEnvironmentVariable("REFINERY_INTEGRATION_MODE") ?? "off";
        var mode = modeRaw.Trim().ToLowerInvariant() switch
        {
            "fixture" => RefineryIntegrationMode.Fixture,
            "live" => RefineryIntegrationMode.Live,
            _ => RefineryIntegrationMode.Off
        };

        var goal = System.Environment.GetEnvironmentVariable("REFINERY_GOAL") ?? "TECH_TREE_PATCH";
        var strictMode = !string.Equals(System.Environment.GetEnvironmentVariable("REFINERY_LENIENT"), "true", System.StringComparison.OrdinalIgnoreCase);
        var serviceBaseUrl = System.Environment.GetEnvironmentVariable("REFINERY_BASE_URL") ?? "http://localhost:8091";
        var requestSeed = ParseLongEnv("REFINERY_REQUEST_SEED", 123L);
        var timeoutMs = ParseIntEnv("REFINERY_TIMEOUT_MS", 1200);
        var retryCount = ParseIntEnv("REFINERY_RETRY_COUNT", 1);
        var breakerSeconds = ParseIntEnv("REFINERY_BREAKER_SECONDS", 10);
        var applyToWorld = string.Equals(System.Environment.GetEnvironmentVariable("REFINERY_APPLY_TO_WORLD"), "true", System.StringComparison.OrdinalIgnoreCase);
        var minTriggerIntervalMs = ParseIntEnv("REFINERY_MIN_TRIGGER_MS", 500);

        var defaultFixture = System.IO.Path.Combine(
            FindRepoRoot(baseDirectory),
            "refinery-service-java",
            "examples",
            "responses",
            "patch-tech-tree-v1.expected.json"
        );
        var fixtureResponsePath = System.Environment.GetEnvironmentVariable("REFINERY_FIXTURE_RESPONSE") ?? defaultFixture;

        return new RefineryRuntimeOptions(
            mode,
            goal,
            fixtureResponsePath,
            serviceBaseUrl,
            strictMode,
            requestSeed,
            timeoutMs,
            retryCount,
            breakerSeconds,
            applyToWorld,
            minTriggerIntervalMs
        );
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
}
