using Xunit;
using WorldSim.Contracts.V2;
using WorldSim.RefineryAdapter.Integration;
using WorldSim.Runtime;

namespace WorldSim.RefineryAdapter.Tests;

public sealed class RefineryPatchRuntimeOutputModeTests
{
    [Theory]
    [InlineData("auto", 2, "both", "response")]
    [InlineData("both", 2, "both", "env")]
    [InlineData("story_only", 1, "story_only", "env")]
    [InlineData("nudge_only", 1, "nudge_only", "env")]
    [InlineData("off", 0, "off", "env")]
    public void FixtureDirectorOutputMode_AppliesExpectedOpCount(
        string outputMode,
        int expectedAppliedCount,
        string expectedMode,
        string expectedSource)
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
        Assert.Contains($"source={expectedSource}", patchRuntime.LastStatus, StringComparison.Ordinal);
        Assert.Equal(expectedMode, patchRuntime.LastDirectorExecutionStatus.EffectiveOutputMode);
        Assert.Equal(expectedSource, patchRuntime.LastDirectorExecutionStatus.EffectiveOutputModeSource);
        Assert.Equal("directorStage:mock", patchRuntime.LastDirectorExecutionStatus.Stage);
        Assert.True(patchRuntime.LastDirectorExecutionStatus.IsDirectorGoal);
        Assert.True(patchRuntime.LastDirectorExecutionStatus.BudgetMarkerPresent);
        Assert.Equal(0d, patchRuntime.LastDirectorExecutionStatus.BudgetUsed, 3);

        var snapshotDirector = runtime.GetSnapshot().Director;
        Assert.Equal(expectedMode, snapshotDirector.OutputMode);
        Assert.Equal(expectedSource, snapshotDirector.OutputModeSource);
        Assert.Equal("directorStage:mock", snapshotDirector.StageMarker);
        Assert.True(snapshotDirector.HasBudgetData);
        Assert.Equal(5d, snapshotDirector.MaxInfluenceBudget, 3);
        Assert.Equal(5d, snapshotDirector.RemainingInfluenceBudget, 3);
    }

    [Fact]
    public void AutoMode_UsesResponseModeMarker_WhenPresent()
    {
        var fixturePath = WriteTempDirectorFixture("story_only", includeModeMarker: true);

        try
        {
            var runtime = CreateRuntime();
            var options = new RefineryRuntimeOptions(
                Mode: RefineryIntegrationMode.Fixture,
                Goal: DirectorGoals.SeasonDirectorCheckpoint,
                DirectorOutputMode: "auto",
                FixtureResponsePath: fixturePath,
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
            patchRuntime.Trigger(runtime, tick: 200);
            PumpUntilSettled(patchRuntime);

            Assert.Equal("story_only", patchRuntime.LastDirectorExecutionStatus.EffectiveOutputMode);
            Assert.Equal("response", patchRuntime.LastDirectorExecutionStatus.EffectiveOutputModeSource);
            Assert.Contains("applied=1", patchRuntime.LastStatus, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(fixturePath);
        }
    }

    [Fact]
    public void AutoMode_FallsBackToBoth_WhenMarkerMissing()
    {
        var fixturePath = WriteTempDirectorFixture("both", includeModeMarker: false);

        try
        {
            var runtime = CreateRuntime();
            var options = new RefineryRuntimeOptions(
                Mode: RefineryIntegrationMode.Fixture,
                Goal: DirectorGoals.SeasonDirectorCheckpoint,
                DirectorOutputMode: "auto",
                FixtureResponsePath: fixturePath,
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
            patchRuntime.Trigger(runtime, tick: 201);
            PumpUntilSettled(patchRuntime);

            Assert.Equal("both", patchRuntime.LastDirectorExecutionStatus.EffectiveOutputMode);
            Assert.Equal("fallback", patchRuntime.LastDirectorExecutionStatus.EffectiveOutputModeSource);
            Assert.Contains("applied=2", patchRuntime.LastStatus, StringComparison.Ordinal);
            Assert.False(patchRuntime.LastDirectorExecutionStatus.BudgetMarkerPresent);
            Assert.Equal(0d, patchRuntime.LastDirectorExecutionStatus.BudgetUsed, 3);
        }
        finally
        {
            File.Delete(fixturePath);
        }
    }

    [Fact]
    public void FixtureDirectorOutputMode_MirrorsBudgetUsedMarkerIntoRuntimeState()
    {
        var fixturePath = WriteTempDirectorFixture("both", includeModeMarker: true, includeBudgetMarker: true, budgetUsed: 1.875d);

        try
        {
            var runtime = CreateRuntime();
            var options = new RefineryRuntimeOptions(
                Mode: RefineryIntegrationMode.Fixture,
                Goal: DirectorGoals.SeasonDirectorCheckpoint,
                DirectorOutputMode: "auto",
                FixtureResponsePath: fixturePath,
                ServiceBaseUrl: "http://localhost:8091",
                StrictMode: true,
                RequestSeed: 123,
                LiveTimeoutMs: 1000,
                LiveRetryCount: 0,
                CircuitBreakerSeconds: 5,
                ApplyToWorld: false,
                MinTriggerIntervalMs: 0,
                DirectorMaxBudget: 5d
            );

            var patchRuntime = new RefineryPatchRuntime(options);
            patchRuntime.Trigger(runtime, tick: 300);
            PumpUntilSettled(patchRuntime);

            Assert.True(patchRuntime.LastDirectorExecutionStatus.BudgetMarkerPresent);
            Assert.Equal(1.875d, patchRuntime.LastDirectorExecutionStatus.BudgetUsed, 3);

            var director = runtime.BuildRefinerySnapshot()["director"]?.AsObject();
            Assert.NotNull(director);
            Assert.Equal(5d, director!["maxInfluenceBudget"]?.GetValue<double>() ?? -1d, 3);
            Assert.Equal(1.875d, director["lastCheckpointBudgetUsed"]?.GetValue<double>() ?? -1d, 3);
            Assert.Equal(3.125d, director["remainingInfluenceBudget"]?.GetValue<double>() ?? -1d, 3);
        }
        finally
        {
            File.Delete(fixturePath);
        }
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

    private static string WriteTempDirectorFixture(string mode, bool includeModeMarker, bool includeBudgetMarker = false, double budgetUsed = 0d)
    {
        var json = """
        {
          "schemaVersion": "v1",
          "requestId": "temp-director-output-mode",
          "seed": 321,
          "patch": [
            {
              "op": "addStoryBeat",
              "opId": "op_director_story_1",
              "beatId": "BEAT_SAMPLE_1",
              "text": "Rations tighten this season; avoid expansion and secure food routes.",
              "durationTicks": 24
            },
            {
              "op": "setColonyDirective",
              "opId": "op_director_nudge_1",
              "colonyId": 0,
              "directive": "PrioritizeFood",
              "durationTicks": 18
            }
          ],
          "explain": [
            "directorStage:mock",
            "directorOutputMode:__MODE__",
            "budgetUsed:__BUDGET__",
            "MockPlanner produced deterministic director checkpoint output."
          ],
          "warnings": []
        }
        """;

        json = json.Replace("__BUDGET__", budgetUsed.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        if (includeModeMarker)
        {
            json = json.Replace("__MODE__", mode, StringComparison.Ordinal);
        }
        else
        {
            json = json.Replace("\"directorOutputMode:__MODE__\",\r\n", string.Empty, StringComparison.Ordinal)
                .Replace("\"directorOutputMode:__MODE__\",\n", string.Empty, StringComparison.Ordinal);
        }

        if (!includeBudgetMarker)
        {
            json = json.Replace("\"budgetUsed:__BUDGET__\",\r\n", string.Empty, StringComparison.Ordinal)
                .Replace("\"budgetUsed:__BUDGET__\",\n", string.Empty, StringComparison.Ordinal)
                .Replace("\"budgetUsed:" + budgetUsed.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "\",\r\n", string.Empty, StringComparison.Ordinal)
                .Replace("\"budgetUsed:" + budgetUsed.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "\",\n", string.Empty, StringComparison.Ordinal);
        }

        var path = Path.Combine(Path.GetTempPath(), $"director-fixture-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
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
