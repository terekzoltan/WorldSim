using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class RefineryLaneArtifactTests
{
    [Fact]
    public void RefineryFixture_WritesAdditiveArtifactTree()
    {
        var artifactDir = CreateArtifactDir();
        var fixturePath = CreateFixtureResponseFile(
            "directorStage:refinery-validated",
            "directorOutputMode:both",
            "budgetUsed:1.250",
            "directorSolverPath:validated_core",
            "directorSolverStatus:success",
            "directorSolverGeneratorResult:success",
            "directorSolverExtraction:success",
            "directorSolverValidatedCoverage:story_core",
            "directorSolverValidatedCoverage:directive_core",
            "directorSolverUnsupported:none",
            "directorSolverDiagnostic:story_core_ready");
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_LANE"] = "refinery_fixture",
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_TICKS"] = "4",
                ["WORLDSIM_SCENARIO_REFINERY_TRIGGER_EVERY"] = "2",
                ["WORLDSIM_SCENARIO_REFINERY_FIXTURE_RESPONSE"] = fixturePath,
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);

        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("smr/v1", manifest.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("refinery", manifest.RootElement.GetProperty("laneType").GetString());
        Assert.True(manifest.RootElement.GetProperty("refineryEnabled").GetBoolean());
        Assert.Equal("fixture", manifest.RootElement.GetProperty("refineryProfile").GetString());
        Assert.Equal(1, manifest.RootElement.GetProperty("checkpointCount").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("refineryAppliedCount").GetInt32());
        Assert.Equal(0, manifest.RootElement.GetProperty("refineryApplyFailedCount").GetInt32());
        Assert.Equal(0, manifest.RootElement.GetProperty("refineryRequestFailedCount").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("refineryValidatedCount").GetInt32());

        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        Assert.Equal("smr/refinery/v1", summary.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("refinery", summary.RootElement.GetProperty("laneType").GetString());
        Assert.Equal("fixture", summary.RootElement.GetProperty("refineryProfile").GetString());
        Assert.Single(summary.RootElement.GetProperty("runs").EnumerateArray());

        using var refinerySummary = ReadJson(Path.Combine(artifactDir, "refinery", "summary.json"));
        Assert.Equal(1, refinerySummary.RootElement.GetProperty("checkpointCount").GetInt32());
        Assert.Equal(1, refinerySummary.RootElement.GetProperty("appliedCount").GetInt32());
        Assert.Equal(1, refinerySummary.RootElement.GetProperty("validatedCount").GetInt32());
        Assert.Equal(1, refinerySummary.RootElement.GetProperty("directorSolverValidatedCoverageCheckpointCounts").GetProperty("story_core").GetInt32());
        Assert.Equal(1, refinerySummary.RootElement.GetProperty("directorSolverValidatedCoverageCheckpointCounts").GetProperty("directive_core").GetInt32());

        using var index = ReadJson(Path.Combine(artifactDir, "refinery", "index.json"));
        var runKey = index.RootElement.GetProperty("runs").EnumerateArray().Single().GetProperty("runKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(runKey));

        using var checkpoint = ReadJson(Path.Combine(artifactDir, "refinery", runKey!, "checkpoints", "001.json"));
        Assert.Equal(1, checkpoint.RootElement.GetProperty("triggerIndex").GetInt32());
        Assert.Equal("applied", checkpoint.RootElement.GetProperty("terminalOutcome").GetString());
        Assert.Equal("applied", checkpoint.RootElement.GetProperty("applyStatus").GetString());
        Assert.NotEqual("not_triggered", checkpoint.RootElement.GetProperty("terminalOutcome").GetString());
        Assert.True(checkpoint.RootElement.TryGetProperty("explainMarkers", out _));
        Assert.Equal("validated_core", checkpoint.RootElement.GetProperty("directorSolverPath").GetString());
        Assert.Equal("success", checkpoint.RootElement.GetProperty("directorSolverStatus").GetString());
        Assert.Equal("success", checkpoint.RootElement.GetProperty("directorSolverGeneratorResult").GetString());
        Assert.Equal("success", checkpoint.RootElement.GetProperty("directorSolverExtraction").GetString());
        Assert.Equal(2, checkpoint.RootElement.GetProperty("directorSolverValidatedCoverage").GetArrayLength());
        Assert.Equal("none", checkpoint.RootElement.GetProperty("directorSolverUnsupported")[0].GetString());
        Assert.Equal("story_core_ready", checkpoint.RootElement.GetProperty("directorSolverDiagnostic")[0].GetString());
        Assert.Equal(0, checkpoint.RootElement.GetProperty("solverParseWarnings").GetArrayLength());
        Assert.True(checkpoint.RootElement.TryGetProperty("rawStatusText", out _));

        Assert.True(Directory.GetFiles(Path.Combine(artifactDir, "runs"), "*.json").Length == 1, $"Missing run artifact\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    [Fact]
    public void RefineryFixture_DirectorStageValidatedAlone_DoesNotCountSolverValidation()
    {
        var artifactDir = CreateArtifactDir();
        var fixturePath = CreateFixtureResponseFile(
            "directorStage:refinery-validated",
            "directorOutputMode:both",
            "budgetUsed:0.000");

        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_LANE"] = "refinery_fixture",
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_TICKS"] = "4",
                ["WORLDSIM_SCENARIO_REFINERY_TRIGGER_EVERY"] = "2",
                ["WORLDSIM_SCENARIO_REFINERY_FIXTURE_RESPONSE"] = fixturePath,
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out _,
            out _);

        Assert.Equal(0, exitCode);

        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(0, manifest.RootElement.GetProperty("refineryValidatedCount").GetInt32());

        using var refinerySummary = ReadJson(Path.Combine(artifactDir, "refinery", "summary.json"));
        Assert.Equal(0, refinerySummary.RootElement.GetProperty("validatedCount").GetInt32());
        Assert.Equal(JsonValueKind.Object, refinerySummary.RootElement.GetProperty("directorSolverValidatedCoverageCheckpointCounts").ValueKind);
        Assert.Empty(refinerySummary.RootElement.GetProperty("directorSolverValidatedCoverageCheckpointCounts").EnumerateObject());
    }

    [Fact]
    public void RefineryLiveMock_UnavailableService_RecordsRequestFailedCheckpoint()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_LANE"] = "refinery_live_mock",
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_TICKS"] = "2",
                ["WORLDSIM_SCENARIO_REFINERY_TRIGGER_EVERY"] = "1",
                ["WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS"] = "3000",
                ["WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS"] = "200",
                ["REFINERY_BASE_URL"] = "http://127.0.0.1:65530",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);

        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("live_mock", manifest.RootElement.GetProperty("refineryProfile").GetString());
        Assert.Equal(0, manifest.RootElement.GetProperty("refineryApplyFailedCount").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("refineryRequestFailedCount").GetInt32());

        using var index = ReadJson(Path.Combine(artifactDir, "refinery", "index.json"));
        var runKey = index.RootElement.GetProperty("runs").EnumerateArray().Single().GetProperty("runKey").GetString();
        using var checkpoint = ReadJson(Path.Combine(artifactDir, "refinery", runKey!, "checkpoints", "001.json"));
        Assert.Equal("request_failed", checkpoint.RootElement.GetProperty("terminalOutcome").GetString());
        Assert.Equal("request_failed", checkpoint.RootElement.GetProperty("applyStatus").GetString());
        Assert.True(checkpoint.RootElement.TryGetProperty("requestFailureKind", out _), $"Missing requestFailureKind\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        using var anomalies = ReadJson(Path.Combine(artifactDir, "anomalies.json"));
        Assert.Contains(anomalies.RootElement.EnumerateArray(), anomaly => anomaly.GetProperty("id").GetString() == "ANOM-RDIR-REQUEST-FAIL-HIGH");
    }

    [Fact]
    public void RefineryLiveMock_FakeServerSuccess_UsesHttpAndPersistsSolverMarkers()
    {
        var artifactDir = CreateArtifactDir();
        using var server = TemporaryPatchServer.Start(CreateResponseJson(
            "directorStage:refinery-validated",
            "directorOutputMode:both",
            "budgetUsed:1.875",
            "directorSolverPath:validated_core",
            "directorSolverStatus:success",
            "directorSolverGeneratorResult:success",
            "directorSolverExtraction:success",
            "directorSolverValidatedCoverage:story_core",
            "directorSolverValidatedCoverage:directive_core",
            "directorSolverUnsupported:none",
            "directorSolverDiagnostic:live_mock_probe"));

        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_LANE"] = "refinery_live_mock",
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_TICKS"] = "2",
                ["WORLDSIM_SCENARIO_REFINERY_TRIGGER_EVERY"] = "1",
                ["WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS"] = "3000",
                ["WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS"] = "500",
                ["REFINERY_BASE_URL"] = server.BaseUrl,
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        Assert.True(server.RequestCount > 0, $"Expected HTTP request to fake server\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.Contains("/v1/patch", server.RequestPaths);

        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("live_mock", manifest.RootElement.GetProperty("refineryProfile").GetString());
        Assert.Equal(1, manifest.RootElement.GetProperty("refineryValidatedCount").GetInt32());

        using var refinerySummary = ReadJson(Path.Combine(artifactDir, "refinery", "summary.json"));
        Assert.Equal(1, refinerySummary.RootElement.GetProperty("validatedCount").GetInt32());
        Assert.Equal(1, refinerySummary.RootElement.GetProperty("directorSolverPathHistogram").GetProperty("validated_core").GetInt32());
        Assert.Equal(1, refinerySummary.RootElement.GetProperty("directorSolverValidatedCoverageCheckpointCounts").GetProperty("story_core").GetInt32());
        Assert.Equal(1, refinerySummary.RootElement.GetProperty("directorSolverValidatedCoverageCheckpointCounts").GetProperty("directive_core").GetInt32());

        using var index = ReadJson(Path.Combine(artifactDir, "refinery", "index.json"));
        var runKey = index.RootElement.GetProperty("runs").EnumerateArray().Single().GetProperty("runKey").GetString();
        using var checkpoint = ReadJson(Path.Combine(artifactDir, "refinery", runKey!, "checkpoints", "001.json"));
        Assert.Equal("validated_core", checkpoint.RootElement.GetProperty("directorSolverPath").GetString());
        Assert.Equal("success", checkpoint.RootElement.GetProperty("directorSolverStatus").GetString());
        Assert.Equal("success", checkpoint.RootElement.GetProperty("directorSolverExtraction").GetString());
        Assert.Equal(2, checkpoint.RootElement.GetProperty("directorSolverValidatedCoverage").GetArrayLength());
        Assert.Equal(0, checkpoint.RootElement.GetProperty("solverParseWarnings").GetArrayLength());
    }

    [Fact]
    public void RefineryFixture_UnknownSolverEnums_AreStoredAndReportedWithoutCrash()
    {
        var artifactDir = CreateArtifactDir();
        var fixturePath = CreateFixtureResponseFile(
            "directorStage:mock",
            "directorOutputMode:both",
            "budgetUsed:0.000",
            "directorSolverPath:future_path",
            "directorSolverStatus:future_status",
            "directorSolverExtraction:future_extraction",
            "directorSolverValidatedCoverage:none",
            "directorSolverUnsupported:none",
            "directorSolverDiagnostic:future_code");

        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_LANE"] = "refinery_fixture",
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_TICKS"] = "4",
                ["WORLDSIM_SCENARIO_REFINERY_TRIGGER_EVERY"] = "2",
                ["WORLDSIM_SCENARIO_REFINERY_FIXTURE_RESPONSE"] = fixturePath,
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out _,
            out _);

        Assert.Equal(0, exitCode);

        using var index = ReadJson(Path.Combine(artifactDir, "refinery", "index.json"));
        var runKey = index.RootElement.GetProperty("runs").EnumerateArray().Single().GetProperty("runKey").GetString();
        using var checkpoint = ReadJson(Path.Combine(artifactDir, "refinery", runKey!, "checkpoints", "001.json"));
        Assert.Equal("future_path", checkpoint.RootElement.GetProperty("directorSolverPath").GetString());
        Assert.Equal("future_status", checkpoint.RootElement.GetProperty("directorSolverStatus").GetString());
        Assert.Equal("future_extraction", checkpoint.RootElement.GetProperty("directorSolverExtraction").GetString());
        Assert.Equal(3, checkpoint.RootElement.GetProperty("solverParseWarnings").GetArrayLength());

        using var anomalies = ReadJson(Path.Combine(artifactDir, "anomalies.json"));
        Assert.Contains(anomalies.RootElement.EnumerateArray(), anomaly => anomaly.GetProperty("id").GetString() == "ANOM-RDIR-SOLVER-MARKER-UNKNOWN");
    }

    [Fact]
    public void RefineryLiveValidator_DoesNotRequirePaidConfirmOrRehearsal()
    {
        var artifactDir = CreateArtifactDir();
        using var server = TemporaryPatchServer.Start(CreateResponseJson(
            "directorStage:refinery-validated",
            "directorOutputMode:both",
            "budgetUsed:1.000",
            "llmStage:disabled",
            "llmCompletionCount:0",
            "llmRetryRounds:0"));
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_LANE"] = "refinery_live_validator",
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_TICKS"] = "2",
                ["WORLDSIM_SCENARIO_REFINERY_TRIGGER_EVERY"] = "1",
                ["WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS"] = "3000",
                ["WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS"] = "500",
                ["REFINERY_BASE_URL"] = server.BaseUrl,
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        Assert.True(server.RequestCount > 0, $"Expected validator HTTP request\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("validator", manifest.RootElement.GetProperty("refineryProfile").GetString());
        Assert.False(manifest.RootElement.GetProperty("paidConfirmPresent").GetBoolean());
        Assert.Equal(1, manifest.RootElement.GetProperty("checkpointCount").GetInt32());
        using var refinerySummary = ReadJson(Path.Combine(artifactDir, "refinery", "summary.json"));
        Assert.Equal(0, refinerySummary.RootElement.GetProperty("observedCompletionCount").GetInt32());
        Assert.Equal(0, refinerySummary.RootElement.GetProperty("observedRetryRounds").GetInt32());
    }

    [Fact]
    public void RefineryLiveValidator_ObservedCompletion_ReturnsAnomalyGateFail()
    {
        var artifactDir = CreateArtifactDir();
        using var server = TemporaryPatchServer.Start(CreateResponseJson(
            "directorStage:refinery-validated",
            "directorOutputMode:both",
            "budgetUsed:1.000",
            "llmStage:candidate",
            "llmCompletionCount:1",
            "llmRetryRounds:0"));
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_LANE"] = "refinery_live_validator",
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_TICKS"] = "2",
                ["WORLDSIM_SCENARIO_REFINERY_TRIGGER_EVERY"] = "1",
                ["WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS"] = "3000",
                ["WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS"] = "500",
                ["REFINERY_BASE_URL"] = server.BaseUrl,
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out _,
            out _);

        Assert.Equal(4, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("anomaly_gate_fail", manifest.RootElement.GetProperty("exitReason").GetString());
        Assert.Equal(1, manifest.RootElement.GetProperty("observedCompletionCount").GetInt32());
        using var anomalies = ReadJson(Path.Combine(artifactDir, "anomalies.json"));
        Assert.Contains(anomalies.RootElement.EnumerateArray(), anomaly => anomaly.GetProperty("id").GetString() == "ANOM-RDIR-VALIDATOR-PAID-COMPLETION");
    }

    [Fact]
    public void RefineryLivePaid_MissingConfirm_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_REFINERY_PAID_CONFIRM", string.Empty),
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("config_error", manifest.RootElement.GetProperty("exitReason").GetString());
        Assert.False(manifest.RootElement.GetProperty("paidConfirmPresent").GetBoolean());
    }

    [Fact]
    public void RefineryLivePaid_MissingRehearsal_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var env = ValidPaidMicroEnv(CreateArtifactDir());
        env.Remove("WORLDSIM_SCENARIO_REFINERY_REHEARSAL_ARTIFACT");
        var exitCode = RunScenarioRunner(artifactDir, env, out _, out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("config_error", manifest.RootElement.GetProperty("exitReason").GetString());
    }

    [Fact]
    public void RefineryLivePaid_RedRehearsal_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 1, applyFailed: 0);
        var exitCode = RunScenarioRunner(artifactDir, ValidPaidMicroEnv(rehearsalDir), out _, out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("config_error", manifest.RootElement.GetProperty("exitReason").GetString());
    }

    [Fact]
    public void RefineryLivePaid_FullCapture_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_REFINERY_CAPTURE", "full"),
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("config_error", manifest.RootElement.GetProperty("exitReason").GetString());
    }

    [Fact]
    public void RefineryLivePaid_WrongPresetShape_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_SEEDS", "101"),
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(1, manifest.RootElement.GetProperty("estimatedCompletions").GetInt32());
    }

    [Fact]
    public void RefineryLivePaid_WrongPlannerShape_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_PLANNERS", "simple,goap"),
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(4, manifest.RootElement.GetProperty("estimatedCompletions").GetInt32());
    }

    [Fact]
    public void RefineryLivePaid_WrongConfigShape_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var configsJson = "[{\"Name\":\"a\",\"Width\":64,\"Height\":40,\"InitialPop\":24,\"Ticks\":4,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0},{\"Name\":\"b\",\"Width\":64,\"Height\":40,\"InitialPop\":24,\"Ticks\":4,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0}]";
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_CONFIGS_JSON", configsJson),
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(4, manifest.RootElement.GetProperty("estimatedCompletions").GetInt32());
    }

    [Fact]
    public void RefineryLivePaid_WrongCheckpointShape_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(
                WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_REFINERY_TRIGGER_TICKS", "2,3"),
                "WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS",
                "2"),
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(2, manifest.RootElement.GetProperty("estimatedCompletions").GetInt32());
    }

    [Fact]
    public void RefineryLivePaid_InvalidNumericEnv_ReturnsConfigErrorInsteadOfClamping()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS", "not-a-number"),
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("config_error", manifest.RootElement.GetProperty("exitReason").GetString());
    }

    [Fact]
    public void RefineryLivePaid_CustomPreset_IsDeferredConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_REFINERY_PAID_PRESET", "custom"),
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("custom", manifest.RootElement.GetProperty("paidPreset").GetString());
    }

    [Fact]
    public void RefineryLivePaid_EstimateCapFail_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_REFINERY_MAX_COMPLETIONS", "1"),
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(2, manifest.RootElement.GetProperty("estimatedCompletions").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("maxCompletions").GetInt32());
    }

    [Fact]
    public void RefineryLivePaid_MissingExpectedJavaRetries_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var env = ValidPaidMicroEnv(rehearsalDir);
        env.Remove("WORLDSIM_SCENARIO_REFINERY_EXPECTED_JAVA_MAX_RETRIES");
        var exitCode = RunScenarioRunner(artifactDir, env, out _, out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("config_error", manifest.RootElement.GetProperty("exitReason").GetString());
    }

    [Fact]
    public void RefineryLivePaid_WrongExpectedJavaRetries_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_REFINERY_EXPECTED_JAVA_MAX_RETRIES", "1"),
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("config_error", manifest.RootElement.GetProperty("exitReason").GetString());
    }

    [Fact]
    public void RefineryLivePaid_FakeServerSuccess_StaysWithinObservedCap()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        using var server = TemporaryPatchServer.Start(CreateResponseJson(
            "directorStage:refinery-validated",
            "directorOutputMode:both",
            "budgetUsed:1.000",
            "llmStage:candidate",
            "llmCompletionCount:1",
            "llmRetryRounds:0"));

        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "REFINERY_BASE_URL", server.BaseUrl),
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        Assert.True(server.RequestCount > 0, $"Expected paid fake-server request\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("live_paid", manifest.RootElement.GetProperty("refineryProfile").GetString());
        Assert.Equal("paid_micro_total2", manifest.RootElement.GetProperty("paidPreset").GetString());
        Assert.Equal(2, manifest.RootElement.GetProperty("estimatedCompletions").GetInt32());
        Assert.Equal(2, manifest.RootElement.GetProperty("observedCompletionCount").GetInt32());
        Assert.Equal(0, manifest.RootElement.GetProperty("expectedJavaDirectorMaxRetries").GetInt32());
        Assert.True(File.Exists(Path.Combine(artifactDir, "refinery", "scorecard.json")));
        AssertArtifactsDoNotContainSecrets(artifactDir);
    }

    [Fact]
    public void RefineryLivePaid_ObservedCompletionOverCap_ReturnsAnomalyGateFail()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        using var server = TemporaryPatchServer.Start(CreateResponseJson(
            "directorStage:refinery-validated",
            "directorOutputMode:both",
            "budgetUsed:1.000",
            "llmStage:candidate",
            "llmCompletionCount:9",
            "llmRetryRounds:0"));

        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(ValidPaidMicroEnv(rehearsalDir), "REFINERY_BASE_URL", server.BaseUrl),
            out _,
            out _);

        Assert.Equal(4, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("anomaly_gate_fail", manifest.RootElement.GetProperty("exitReason").GetString());
        Assert.Equal(18, manifest.RootElement.GetProperty("observedCompletionCount").GetInt32());
        using var anomalies = ReadJson(Path.Combine(artifactDir, "anomalies.json"));
        Assert.Contains(anomalies.RootElement.EnumerateArray(), anomaly => anomaly.GetProperty("id").GetString() == "ANOM-RDIR-PAID-COMPLETION-CAP-EXCEEDED");
    }

    [Fact]
    public void RefineryLivePaid_CostEstimateOnly_WritesPreflightArtifactWithoutHttpCall()
    {
        var artifactDir = CreateArtifactDir();
        var rehearsalDir = CreateRehearsalArtifact(exitCode: 0, requestFailed: 0, applyFailed: 0);
        var rehearsalManifestPath = Path.Combine(rehearsalDir, "manifest.json");
        var exitCode = RunScenarioRunner(
            artifactDir,
            WithEnv(
                WithEnv(
                    WithEnv(ValidPaidMicroEnv(rehearsalDir), "WORLDSIM_SCENARIO_REFINERY_REHEARSAL_ARTIFACT", rehearsalManifestPath),
                    "WORLDSIM_SCENARIO_REFINERY_COST_ESTIMATE_ONLY",
                    "true"),
                "REFINERY_BASE_URL",
                "http://127.0.0.1:65529"),
            out _,
            out _);

        Assert.Equal(0, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("live_paid", manifest.RootElement.GetProperty("refineryProfile").GetString());
        Assert.True(manifest.RootElement.GetProperty("costEstimateOnly").GetBoolean());
        Assert.Equal(2, manifest.RootElement.GetProperty("estimatedCompletions").GetInt32());
        Assert.Equal(8, manifest.RootElement.GetProperty("maxCompletions").GetInt32());
        Assert.True(manifest.RootElement.GetProperty("paidConfirmPresent").GetBoolean());
        Assert.Equal(rehearsalManifestPath, manifest.RootElement.GetProperty("rehearsalArtifact").GetString());
        Assert.Equal(0, manifest.RootElement.GetProperty("checkpointCount").GetInt32());
        Assert.True(File.Exists(Path.Combine(artifactDir, "refinery", "preflight.json")));

        AssertArtifactsDoNotContainSecrets(artifactDir);
    }

    [Fact]
    public void RefinerySeasonBoundary_IsDeterministicConfigErrorInTr2D()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_LANE"] = "refinery_fixture",
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_TICKS"] = "4",
                ["WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY"] = "season_boundary"
            },
            out _,
            out _);

        Assert.Equal(3, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("config_error", manifest.RootElement.GetProperty("exitReason").GetString());
        Assert.Equal("season_boundary", manifest.RootElement.GetProperty("checkpointPolicy").GetString());
    }

    [Fact]
    public void CoreDefault_DoesNotEmitRefineryManifestFields()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_TICKS"] = "4",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.False(manifest.RootElement.TryGetProperty("laneType", out _));
        Assert.False(Directory.Exists(Path.Combine(artifactDir, "refinery")), $"Core lane unexpectedly wrote refinery artifacts\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    [Fact]
    public void ModeAll_DoesNotRunPaidOrRefineryLaneByDefault()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_MODE"] = "all",
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_TICKS"] = "4",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected mode=all core exit {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.False(manifest.RootElement.TryGetProperty("refineryEnabled", out _));
        Assert.False(Directory.Exists(Path.Combine(artifactDir, "refinery")));
    }

    private static int RunScenarioRunner(
        string artifactDir,
        IReadOnlyDictionary<string, string> env,
        out string stdout,
        out string stderr)
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "WorldSim.ScenarioRunner", "WorldSim.ScenarioRunner.csproj");
        var startInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\"")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.Environment["WORLDSIM_SCENARIO_ARTIFACT_DIR"] = artifactDir;
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_LANE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_MODE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_ASSERT");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_COMPARE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_PERF");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_PERF_FAIL");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_DELTA_FAIL");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_CONFIGS_JSON");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_TRIGGER_EVERY");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_TRIGGER_TICKS");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_WAIT_MODE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_RETRY_COUNT");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_FIXTURE_RESPONSE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_CAPTURE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_PAID_PRESET");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_PAID_CONFIRM");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_REHEARSAL_ARTIFACT");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_MAX_COMPLETIONS");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_COST_ESTIMATE_ONLY");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_REFINERY_EXPECTED_JAVA_MAX_RETRIES");
        startInfo.Environment.Remove("PLANNER_LLM_API_KEY");
        startInfo.Environment.Remove("REFINERY_BASE_URL");
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");

        foreach (var pair in env)
            startInfo.Environment[pair.Key] = pair.Value;

        var result = ScenarioRunnerProcess.Run(startInfo, artifactDir);
        stdout = result.Stdout;
        stderr = result.Stderr;
        return result.ExitCode;
    }

    private static Dictionary<string, string> ValidPaidMicroEnv(string rehearsalDir)
        => new(StringComparer.Ordinal)
        {
            ["WORLDSIM_SCENARIO_LANE"] = "refinery_live_paid",
            ["WORLDSIM_SCENARIO_SEEDS"] = "101,202",
            ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
            ["WORLDSIM_SCENARIO_TICKS"] = "4",
            ["WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY"] = "tick_list",
            ["WORLDSIM_SCENARIO_REFINERY_TRIGGER_TICKS"] = "2",
            ["WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS"] = "1",
            ["WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS"] = "3000",
            ["WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS"] = "500",
            ["WORLDSIM_SCENARIO_REFINERY_RETRY_COUNT"] = "0",
            ["WORLDSIM_SCENARIO_REFINERY_EXPECTED_JAVA_MAX_RETRIES"] = "0",
            ["WORLDSIM_SCENARIO_REFINERY_PAID_PRESET"] = "paid_micro_total2",
            ["WORLDSIM_SCENARIO_REFINERY_PAID_CONFIRM"] = "I_UNDERSTAND_OPENROUTER_COSTS",
            ["WORLDSIM_SCENARIO_REFINERY_REHEARSAL_ARTIFACT"] = rehearsalDir,
            ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
        };

    private static Dictionary<string, string> WithEnv(Dictionary<string, string> env, string key, string value)
    {
        env[key] = value;
        return env;
    }

    private static string CreateRehearsalArtifact(int exitCode, int requestFailed, int applyFailed)
    {
        var dir = CreateArtifactDir();
        var manifest = new JsonObject
        {
            ["schemaVersion"] = "smr/v1",
            ["laneType"] = "refinery",
            ["refineryEnabled"] = true,
            ["refineryProfile"] = "validator",
            ["exitCode"] = exitCode,
            ["checkpointCount"] = 1,
            ["refineryRequestFailedCount"] = requestFailed,
            ["refineryApplyFailedCount"] = applyFailed
        };
        File.WriteAllText(Path.Combine(dir, "manifest.json"), manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return dir;
    }

    private static void AssertArtifactsDoNotContainSecrets(string artifactDir)
    {
        var allArtifacts = string.Join('\n', Directory.EnumerateFiles(artifactDir, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("I_UNDERSTAND_OPENROUTER_COSTS", allArtifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("PLANNER_LLM_API_KEY", allArtifacts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", allArtifacts, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonDocument ReadJson(string path)
    {
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    private static string CreateFixtureResponseFile(params string[] explainMarkers)
    {
        var path = Path.Combine(Path.GetTempPath(), $"worldsim-refinery-fixture-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, CreateResponseJson(explainMarkers));
        return path;
    }

    private static string CreateResponseJson(params string[] explainMarkers)
    {
        var repoRoot = FindRepoRoot();
        var templatePath = Path.Combine(repoRoot, "refinery-service-java", "examples", "responses", "patch-season-director-v1.expected.json");
        var node = JsonNode.Parse(File.ReadAllText(templatePath))?.AsObject()
                   ?? throw new InvalidOperationException("Invalid fixture template json.");
        if (node["patch"] is JsonArray patchArray && patchArray.Count > 0 && patchArray[0] is JsonObject storyBeat)
            storyBeat["severity"] = "minor";
        var explainArray = new JsonArray();
        foreach (var marker in explainMarkers)
            explainArray.Add(JsonValue.Create(marker));
        node["explain"] = explainArray;
        node["warnings"] = new JsonArray();
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateArtifactDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-refinery-artifacts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WorldSim.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate WorldSim.sln");
    }

    private sealed class TemporaryPatchServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly string _responseJson;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;
        private readonly List<string> _requestPaths = new();

        private TemporaryPatchServer(TcpListener listener, string responseJson)
        {
            _listener = listener;
            _responseJson = responseJson;
            _serverTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public string BaseUrl => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        public int RequestCount => _requestPaths.Count;
        public IReadOnlyList<string> RequestPaths => _requestPaths;

        public static TemporaryPatchServer Start(string responseJson)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return new TemporaryPatchServer(listener, responseJson);
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using var clientHandle = client;
            using var stream = client.GetStream();
            var requestHeaderBytes = ReadHeaderBytes(stream);
            if (requestHeaderBytes.Length == 0)
                return;

            var headerText = Encoding.ASCII.GetString(requestHeaderBytes);
            var headerLines = headerText.Split("\r\n", StringSplitOptions.None);
            var requestLine = headerLines[0];
            var path = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ElementAtOrDefault(1) ?? string.Empty;
            lock (_requestPaths)
                _requestPaths.Add(path);

            var contentLength = 0;
            var chunked = false;
            foreach (var line in headerLines.Skip(1))
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                const string contentLengthPrefix = "Content-Length:";
                const string transferEncodingPrefix = "Transfer-Encoding:";
                if (line.StartsWith(contentLengthPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line[contentLengthPrefix.Length..].Trim(), out contentLength);
                }
                else if (line.StartsWith(transferEncodingPrefix, StringComparison.OrdinalIgnoreCase)
                         && line[transferEncodingPrefix.Length..].Trim().Contains("chunked", StringComparison.OrdinalIgnoreCase))
                {
                    chunked = true;
                }
            }

            if (contentLength > 0)
            {
                ReadExactBytes(stream, contentLength);
            }
            else if (chunked)
            {
                ReadChunkedBody(stream);
            }

            var bodyBytes = Encoding.UTF8.GetBytes(_responseJson);
            var header = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
            var responseHeaderBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(responseHeaderBytes, cancellationToken);
            await stream.WriteAsync(bodyBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static byte[] ReadHeaderBytes(NetworkStream stream)
        {
            var buffer = new List<byte>(512);
            while (true)
            {
                var next = stream.ReadByte();
                if (next < 0)
                    break;

                buffer.Add((byte)next);
                var count = buffer.Count;
                if (count >= 4
                    && buffer[count - 4] == '\r'
                    && buffer[count - 3] == '\n'
                    && buffer[count - 2] == '\r'
                    && buffer[count - 1] == '\n')
                {
                    break;
                }
            }

            return buffer.ToArray();
        }

        private static void ReadExactBytes(NetworkStream stream, int byteCount)
        {
            var buffer = new byte[4096];
            var remaining = byteCount;
            while (remaining > 0)
            {
                var read = stream.Read(buffer, 0, Math.Min(buffer.Length, remaining));
                if (read <= 0)
                    break;
                remaining -= read;
            }
        }

        private static void ReadChunkedBody(NetworkStream stream)
        {
            while (true)
            {
                var sizeLine = ReadAsciiLine(stream);
                if (string.IsNullOrWhiteSpace(sizeLine))
                    continue;

                var semicolonIndex = sizeLine.IndexOf(';');
                var sizeToken = semicolonIndex >= 0 ? sizeLine[..semicolonIndex] : sizeLine;
                var chunkSize = Convert.ToInt32(sizeToken.Trim(), 16);
                if (chunkSize == 0)
                {
                    ReadAsciiLine(stream);
                    break;
                }

                ReadExactBytes(stream, chunkSize);
                ReadExactBytes(stream, 2);
            }
        }

        private static string ReadAsciiLine(NetworkStream stream)
        {
            var bytes = new List<byte>(64);
            while (true)
            {
                var next = stream.ReadByte();
                if (next < 0)
                    break;

                if (next == '\n')
                    break;

                if (next != '\r')
                    bytes.Add((byte)next);
            }

            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                _serverTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
            _cts.Dispose();
        }
    }
}
