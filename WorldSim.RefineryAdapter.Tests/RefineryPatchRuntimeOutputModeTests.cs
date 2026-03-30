using Xunit;
using WorldSim.Contracts.V2;
using WorldSim.RefineryAdapter.Integration;
using WorldSim.Runtime;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;

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
        Assert.Equal("applied", patchRuntime.LastDirectorExecutionStatus.ApplyStatus);
        Assert.True(patchRuntime.LastDirectorExecutionStatus.IsDirectorGoal);
        Assert.True(patchRuntime.LastDirectorExecutionStatus.BudgetMarkerPresent);
        Assert.Equal(0d, patchRuntime.LastDirectorExecutionStatus.BudgetUsed, 3);

        var snapshotDirector = runtime.GetSnapshot().Director;
        Assert.Equal(expectedMode, snapshotDirector.OutputMode);
        Assert.Equal(expectedSource, snapshotDirector.OutputModeSource);
        Assert.Equal("directorStage:mock", snapshotDirector.StageMarker);
        Assert.Equal("applied", snapshotDirector.ApplyStatus);
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
            Assert.Equal("applied", patchRuntime.LastDirectorExecutionStatus.ApplyStatus);

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

    [Fact]
    public void DirectorApplyFailure_PreservesResponseStageModeAndBudget_AndSetsApplyFailedStatus()
    {
        var fixturePath = WriteTempDirectorFailureFixture(includeBudgetMarker: true, budgetUsed: 1.250d);

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
                ApplyToWorld: true,
                MinTriggerIntervalMs: 0,
                DirectorMaxBudget: 5d
            );

            var patchRuntime = new RefineryPatchRuntime(options);
            patchRuntime.Trigger(runtime, tick: 310);
            PumpUntilSettled(patchRuntime);

            Assert.StartsWith("Refinery apply failed:", patchRuntime.LastStatus, StringComparison.Ordinal);
            Assert.Contains("outcome=apply_failed", patchRuntime.LastStatus, StringComparison.Ordinal);
            Assert.Equal("directorStage:mock", patchRuntime.LastDirectorExecutionStatus.Stage);
            Assert.Equal("both", patchRuntime.LastDirectorExecutionStatus.EffectiveOutputMode);
            Assert.Equal("response", patchRuntime.LastDirectorExecutionStatus.EffectiveOutputModeSource);
            Assert.True(patchRuntime.LastDirectorExecutionStatus.BudgetMarkerPresent);
            Assert.Equal(1.25d, patchRuntime.LastDirectorExecutionStatus.BudgetUsed, 3);
            Assert.Equal("apply_failed", patchRuntime.LastDirectorExecutionStatus.ApplyStatus);

            var snapshotDirector = runtime.GetSnapshot().Director;
            Assert.Equal("directorStage:mock", snapshotDirector.StageMarker);
            Assert.Equal("both", snapshotDirector.OutputMode);
            Assert.Equal("response", snapshotDirector.OutputModeSource);
            Assert.Equal("apply_failed", snapshotDirector.ApplyStatus);
            Assert.Equal(0d, snapshotDirector.LastCheckpointBudgetUsed, 3);
            Assert.Contains("unknown colonyId", runtime.LastDirectorActionStatus, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteFile(fixturePath);
        }
    }

    [Fact]
    public void DirectorRequestFailureBeforeResponse_SetsRequestFailedStatus()
    {
        var runtime = CreateRuntime();
        var missingFixture = Path.Combine(Path.GetTempPath(), $"director-fixture-missing-{Guid.NewGuid():N}.json");
        var options = new RefineryRuntimeOptions(
            Mode: RefineryIntegrationMode.Fixture,
            Goal: DirectorGoals.SeasonDirectorCheckpoint,
            DirectorOutputMode: "auto",
            FixtureResponsePath: missingFixture,
            ServiceBaseUrl: "http://localhost:8091",
            StrictMode: true,
            RequestSeed: 123,
            LiveTimeoutMs: 1000,
            LiveRetryCount: 0,
            CircuitBreakerSeconds: 5,
            ApplyToWorld: true,
            MinTriggerIntervalMs: 0,
            DirectorMaxBudget: 5d
        );

        var patchRuntime = new RefineryPatchRuntime(options);
        patchRuntime.Trigger(runtime, tick: 311);
        PumpUntilSettled(patchRuntime);

        Assert.StartsWith("Refinery apply failed:", patchRuntime.LastStatus, StringComparison.Ordinal);
        Assert.Contains("outcome=request_failed", patchRuntime.LastStatus, StringComparison.Ordinal);
        Assert.Equal("request_failed", patchRuntime.LastDirectorExecutionStatus.ApplyStatus);
        Assert.Equal("not_triggered", patchRuntime.LastDirectorExecutionStatus.Stage);
        Assert.Equal("unknown", patchRuntime.LastDirectorExecutionStatus.EffectiveOutputMode);
        Assert.Equal("unknown", patchRuntime.LastDirectorExecutionStatus.EffectiveOutputModeSource);

        var snapshotDirector = runtime.GetSnapshot().Director;
        Assert.Equal("request_failed", snapshotDirector.ApplyStatus);
        Assert.Equal("not_triggered", snapshotDirector.StageMarker);
        Assert.Equal("unknown", snapshotDirector.OutputMode);
        Assert.Equal("unknown", snapshotDirector.OutputModeSource);
        Assert.Contains("Fixture response file not found", runtime.LastDirectorActionStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestFailureAfterCommittedBudget_KeepsPreviousBudgetState()
    {
        var successFixture = WriteTempDirectorFixture("both", includeModeMarker: true, includeBudgetMarker: true, budgetUsed: 1.875d);
        var missingFixture = Path.Combine(Path.GetTempPath(), $"director-fixture-missing-budget-{Guid.NewGuid():N}.json");

        try
        {
            var runtime = CreateRuntime();

            var successRuntime = new RefineryPatchRuntime(new RefineryRuntimeOptions(
                Mode: RefineryIntegrationMode.Fixture,
                Goal: DirectorGoals.SeasonDirectorCheckpoint,
                DirectorOutputMode: "auto",
                FixtureResponsePath: successFixture,
                ServiceBaseUrl: "http://localhost:8091",
                StrictMode: true,
                RequestSeed: 123,
                LiveTimeoutMs: 1000,
                LiveRetryCount: 0,
                CircuitBreakerSeconds: 5,
                ApplyToWorld: false,
                MinTriggerIntervalMs: 0,
                DirectorMaxBudget: 5d
            ));

            successRuntime.Trigger(runtime, tick: 330);
            PumpUntilSettled(successRuntime);
            var budgetBeforeFailure = runtime.GetSnapshot().Director.LastCheckpointBudgetUsed;
            Assert.Equal(1.875d, budgetBeforeFailure, 3);

            var failingRuntime = new RefineryPatchRuntime(new RefineryRuntimeOptions(
                Mode: RefineryIntegrationMode.Fixture,
                Goal: DirectorGoals.SeasonDirectorCheckpoint,
                DirectorOutputMode: "auto",
                FixtureResponsePath: missingFixture,
                ServiceBaseUrl: "http://localhost:8091",
                StrictMode: true,
                RequestSeed: 123,
                LiveTimeoutMs: 1000,
                LiveRetryCount: 0,
                CircuitBreakerSeconds: 5,
                ApplyToWorld: true,
                MinTriggerIntervalMs: 0,
                DirectorMaxBudget: 5d
            ));

            failingRuntime.Trigger(runtime, tick: 331);
            PumpUntilSettled(failingRuntime);

            Assert.Contains("outcome=request_failed", failingRuntime.LastStatus, StringComparison.Ordinal);
            Assert.Equal("request_failed", runtime.GetSnapshot().Director.ApplyStatus);
            Assert.Equal(budgetBeforeFailure, runtime.GetSnapshot().Director.LastCheckpointBudgetUsed, 3);
        }
        finally
        {
            TryDeleteFile(successFixture);
        }
    }

    [Fact]
    public void ApplyFailureAfterCommittedBudget_KeepsPreviousBudgetState()
    {
        var successFixture = WriteTempDirectorFixture("both", includeModeMarker: true, includeBudgetMarker: true, budgetUsed: 1.875d);
        var applyFailFixture = WriteTempDirectorFailureFixture(includeBudgetMarker: true, budgetUsed: 1.250d);

        try
        {
            var runtime = CreateRuntime();

            var successRuntime = new RefineryPatchRuntime(new RefineryRuntimeOptions(
                Mode: RefineryIntegrationMode.Fixture,
                Goal: DirectorGoals.SeasonDirectorCheckpoint,
                DirectorOutputMode: "auto",
                FixtureResponsePath: successFixture,
                ServiceBaseUrl: "http://localhost:8091",
                StrictMode: true,
                RequestSeed: 123,
                LiveTimeoutMs: 1000,
                LiveRetryCount: 0,
                CircuitBreakerSeconds: 5,
                ApplyToWorld: false,
                MinTriggerIntervalMs: 0,
                DirectorMaxBudget: 5d
            ));

            successRuntime.Trigger(runtime, tick: 332);
            PumpUntilSettled(successRuntime);
            var budgetBeforeFailure = runtime.GetSnapshot().Director.LastCheckpointBudgetUsed;
            Assert.Equal(1.875d, budgetBeforeFailure, 3);

            var failingRuntime = new RefineryPatchRuntime(new RefineryRuntimeOptions(
                Mode: RefineryIntegrationMode.Fixture,
                Goal: DirectorGoals.SeasonDirectorCheckpoint,
                DirectorOutputMode: "auto",
                FixtureResponsePath: applyFailFixture,
                ServiceBaseUrl: "http://localhost:8091",
                StrictMode: true,
                RequestSeed: 123,
                LiveTimeoutMs: 1000,
                LiveRetryCount: 0,
                CircuitBreakerSeconds: 5,
                ApplyToWorld: true,
                MinTriggerIntervalMs: 0,
                DirectorMaxBudget: 5d
            ));

            failingRuntime.Trigger(runtime, tick: 333);
            PumpUntilSettled(failingRuntime);

            Assert.Contains("outcome=apply_failed", failingRuntime.LastStatus, StringComparison.Ordinal);
            Assert.Equal("apply_failed", runtime.GetSnapshot().Director.ApplyStatus);
            Assert.Equal(budgetBeforeFailure, runtime.GetSnapshot().Director.LastCheckpointBudgetUsed, 3);
        }
        finally
        {
            TryDeleteFile(successFixture);
            TryDeleteFile(applyFailFixture);
        }
    }

    [Fact]
    public void LiveRequestFailureFormatter_ExposesTimeoutHttpAndConnectionKinds()
    {
        var timeoutText = InvokeLiveFailureFormatter(new OperationCanceledException("request timed out"), attempts: 2);
        Assert.Contains("kind=timeout", timeoutText, StringComparison.Ordinal);
        Assert.Contains("attempts=2", timeoutText, StringComparison.Ordinal);

        var httpText = InvokeLiveFailureFormatter(
            new HttpRequestException("server error", null, HttpStatusCode.ServiceUnavailable),
            attempts: 1);
        Assert.Contains("kind=http_503", httpText, StringComparison.Ordinal);

        var connectionText = InvokeLiveFailureFormatter(
            new HttpRequestException("refused", new SocketException((int)SocketError.ConnectionRefused)),
            attempts: 1);
        Assert.Contains("kind=connection_refused", connectionText, StringComparison.Ordinal);
    }

    [Fact]
    public void BeforeFirstTrigger_AdapterAndRuntimeDirectorStatus_AreCanonicalAndAligned()
    {
        var runtime = CreateRuntime();
        var fixturePath = GetDirectorFixturePath();
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
            ApplyToWorld: true,
            MinTriggerIntervalMs: 0,
            DirectorMaxBudget: 5d
        );

        var patchRuntime = new RefineryPatchRuntime(options);
        var adapterState = patchRuntime.LastDirectorExecutionStatus;
        var snapshot = runtime.GetSnapshot().Director;

        Assert.Equal("not_triggered", adapterState.Stage);
        Assert.Equal("unknown", adapterState.EffectiveOutputMode);
        Assert.Equal("unknown", adapterState.EffectiveOutputModeSource);
        Assert.Equal("not_triggered", adapterState.ApplyStatus);

        Assert.Equal(adapterState.Stage, snapshot.StageMarker);
        Assert.Equal(adapterState.EffectiveOutputMode, snapshot.OutputMode);
        Assert.Equal(adapterState.EffectiveOutputModeSource, snapshot.OutputModeSource);
        Assert.Equal(adapterState.ApplyStatus, snapshot.ApplyStatus);
    }

    [Fact]
    public void DirectorPartialFailure_DoesNotPoisonDedupe_AndSecondAttemptCanApplyAllOps()
    {
        var fixturePath = Path.Combine(Path.GetTempPath(), $"director-fixture-partial-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(fixturePath, BuildDirectorPartialFailureFixtureJson());

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
                ApplyToWorld: true,
                MinTriggerIntervalMs: 0,
                DirectorMaxBudget: 5d
            );

            var patchRuntime = new RefineryPatchRuntime(options);
            patchRuntime.Trigger(runtime, tick: 320);
            PumpUntilSettled(patchRuntime);

            Assert.StartsWith("Refinery apply failed:", patchRuntime.LastStatus, StringComparison.Ordinal);
            Assert.Contains("outcome=apply_failed", patchRuntime.LastStatus, StringComparison.Ordinal);

            var failedSnapshot = runtime.GetSnapshot().Director;
            Assert.Empty(failedSnapshot.ActiveBeats);
            Assert.Empty(failedSnapshot.ActiveDirectives);
            Assert.Equal(0d, failedSnapshot.LastCheckpointBudgetUsed, 3);

            File.WriteAllText(fixturePath, BuildDirectorRecoveryFixtureJson());

            patchRuntime.Trigger(runtime, tick: 321);
            PumpUntilSettled(patchRuntime);

            Assert.StartsWith("Refinery applied:", patchRuntime.LastStatus, StringComparison.Ordinal);
            Assert.Contains("applied=2", patchRuntime.LastStatus, StringComparison.Ordinal);

            var successSnapshot = runtime.GetSnapshot().Director;
            Assert.Single(successSnapshot.ActiveBeats);
            Assert.Single(successSnapshot.ActiveDirectives);
            Assert.Equal("applied", successSnapshot.ApplyStatus);
            Assert.Equal(1.25d, successSnapshot.LastCheckpointBudgetUsed, 3);
        }
        finally
        {
            TryDeleteFile(fixturePath);
        }
    }

    private static void PumpUntilSettled(RefineryPatchRuntime runtime)
    {
        var initialStatus = runtime.LastStatus;
        for (var i = 0; i < 80; i++)
        {
            runtime.Pump();
            if (runtime.LastStatus.StartsWith("Refinery applied:", StringComparison.Ordinal)
                || runtime.LastStatus.StartsWith("Refinery apply failed:", StringComparison.Ordinal))
            {
                if (!string.Equals(runtime.LastStatus, initialStatus, StringComparison.Ordinal))
                    return;
            }

            Thread.Sleep(10);
        }

        throw new TimeoutException("RefineryPatchRuntime did not settle in time. LastStatus=" + runtime.LastStatus);
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

    private static string WriteTempDirectorFailureFixture(bool includeBudgetMarker, double budgetUsed)
    {
        var json = """
        {
          "schemaVersion": "v1",
          "requestId": "temp-director-apply-fail",
          "seed": 321,
          "patch": [
            {
              "op": "setColonyDirective",
              "opId": "op_director_bad_nudge_1",
              "colonyId": 999,
              "directive": "PrioritizeFood",
              "durationTicks": 18
            }
          ],
          "explain": [
            "directorStage:mock",
            "directorOutputMode:both",
            "budgetUsed:__BUDGET__",
            "MockPlanner produced deterministic director checkpoint output."
          ],
          "warnings": []
        }
        """;

        json = json.Replace("__BUDGET__", budgetUsed.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        if (!includeBudgetMarker)
        {
            json = json.Replace("\"budgetUsed:__BUDGET__\",\r\n", string.Empty, StringComparison.Ordinal)
                .Replace("\"budgetUsed:__BUDGET__\",\n", string.Empty, StringComparison.Ordinal)
                .Replace("\"budgetUsed:" + budgetUsed.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "\",\r\n", string.Empty, StringComparison.Ordinal)
                .Replace("\"budgetUsed:" + budgetUsed.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "\",\n", string.Empty, StringComparison.Ordinal);
        }

        var path = Path.Combine(Path.GetTempPath(), $"director-fixture-fail-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string BuildDirectorPartialFailureFixtureJson()
    {
        return """
        {
          "schemaVersion": "v1",
          "requestId": "temp-director-partial-fail",
          "seed": 321,
          "patch": [
            {
              "op": "addStoryBeat",
              "opId": "op_director_story_atomic_1",
              "beatId": "BEAT_ATOMIC_1",
              "text": "Atomicity probe beat",
              "durationTicks": 24
            },
            {
              "op": "setColonyDirective",
              "opId": "op_director_bad_nudge_atomic_1",
              "colonyId": 999,
              "directive": "PrioritizeFood",
              "durationTicks": 18
            }
          ],
          "explain": [
            "directorStage:mock",
            "directorOutputMode:both",
            "budgetUsed:1.250"
          ],
          "warnings": []
        }
        """;
    }

    private static string BuildDirectorRecoveryFixtureJson()
    {
        return """
        {
          "schemaVersion": "v1",
          "requestId": "temp-director-partial-recovery",
          "seed": 321,
          "patch": [
            {
              "op": "addStoryBeat",
              "opId": "op_director_story_atomic_1",
              "beatId": "BEAT_ATOMIC_1",
              "text": "Atomicity probe beat",
              "durationTicks": 24
            },
            {
              "op": "setColonyDirective",
              "opId": "op_director_good_nudge_atomic_1",
              "colonyId": 0,
              "directive": "PrioritizeFood",
              "durationTicks": 18
            }
          ],
          "explain": [
            "directorStage:mock",
            "directorOutputMode:both",
            "budgetUsed:1.250"
          ],
          "warnings": []
        }
        """;
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

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private static string InvokeLiveFailureFormatter(Exception exception, int attempts)
    {
        var method = typeof(RefineryPatchRuntime).GetMethod(
            "DescribeLiveRequestFailure",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var result = method!.Invoke(null, new object?[] { exception, attempts });
        Assert.IsType<string>(result);
        return (string)result!;
    }
}
