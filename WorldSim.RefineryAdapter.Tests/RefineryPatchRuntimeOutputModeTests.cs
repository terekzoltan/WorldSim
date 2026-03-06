using Xunit;
using WorldSim.Contracts.V2;
using WorldSim.RefineryAdapter.Integration;
using WorldSim.Runtime;

namespace WorldSim.RefineryAdapter.Tests;

public sealed class RefineryPatchRuntimeOutputModeTests
{
    [Theory]
    [InlineData("auto", 2, "both")]
    [InlineData("both", 2, "both")]
    [InlineData("story_only", 1, "story_only")]
    [InlineData("nudge_only", 1, "nudge_only")]
    [InlineData("off", 0, "off")]
    public void FixtureDirectorOutputMode_AppliesExpectedOpCount(string outputMode, int expectedAppliedCount, string expectedMode)
    {
        var runtime = CreateRuntime();
        var options = new RefineryRuntimeOptions(
            Mode: RefineryIntegrationMode.Fixture,
            Goal: DirectorGoals.SeasonDirectorCheckpoint,
            DirectorOutputMode: outputMode,
            FixtureResponsePath: GetDirectorFixturePath(),
            ServiceBaseUrl: "http://localhost:8091",
            StrictMode: true,
            RequestSeed: 123,
            LiveTimeoutMs: 1000,
            LiveRetryCount: 0,
            CircuitBreakerSeconds: 5,
            ApplyToWorld: false,
            MinTriggerIntervalMs: 0
        );

        var patchRuntime = new RefineryPatchRuntime(options);
        patchRuntime.Trigger(runtime, tick: 100);
        PumpUntilSettled(patchRuntime);

        Assert.Contains("Refinery applied:", patchRuntime.LastStatus, StringComparison.Ordinal);
        Assert.Contains($"applied={expectedAppliedCount}", patchRuntime.LastStatus, StringComparison.Ordinal);
        Assert.Contains($"mode={expectedMode}", patchRuntime.LastStatus, StringComparison.Ordinal);
    }

    private static void PumpUntilSettled(RefineryPatchRuntime runtime)
    {
        for (var i = 0; i < 80; i++)
        {
            runtime.Pump();
            if (runtime.LastStatus.StartsWith("Refinery applied:", StringComparison.Ordinal)
                || runtime.LastStatus.StartsWith("Refinery apply failed:", StringComparison.Ordinal))
            {
                return;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("RefineryPatchRuntime did not settle in time.");
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 10, techPath);
    }

    private static string GetDirectorFixturePath()
    {
        var repoRoot = FindRepoRoot();
        return Path.Combine(repoRoot, "refinery-service-java", "examples", "responses", "patch-season-director-v1.expected.json");
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "refinery-service-java"))
                && File.Exists(Path.Combine(current.FullName, "Tech", "technologies.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
