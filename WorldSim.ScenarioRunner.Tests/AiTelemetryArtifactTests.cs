using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class AiTelemetryArtifactTests
{
    [Fact]
    public void RunResults_ContainAiTelemetryBlock()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "701",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var firstRun = summary.RootElement.GetProperty("runs").EnumerateArray().First();
        var ai = firstRun.GetProperty("ai");
        Assert.True(ai.GetProperty("decisionCount").GetInt32() > 0);
        Assert.True(ai.GetProperty("goalCounts").GetArrayLength() > 0);
        Assert.True(ai.GetProperty("commandCounts").GetArrayLength() > 0);
        Assert.True(ai.GetProperty("targetKindCounts").GetArrayLength() > 0);
        var latest = ai.GetProperty("latestDecision");
        Assert.True(latest.GetProperty("actorId").GetInt32() > 0);
        Assert.False(string.IsNullOrWhiteSpace(latest.GetProperty("selectedGoal").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(latest.GetProperty("nextCommand").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(latest.GetProperty("targetKind").GetString()));
    }

    [Fact]
    public void Drilldown_TimelineContainsCompactAiTopFields()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "702",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
                ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "1",
                ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "2"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        using var index = ReadJson(Path.Combine(artifactDir, "drilldown", "index.json"));
        var runKey = index.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("runKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(runKey));

        using var timeline = ReadJson(Path.Combine(artifactDir, "drilldown", runKey!, "timeline.json"));
        var firstSample = timeline.RootElement.EnumerateArray().First();
        var ai = firstSample.GetProperty("ai");
        Assert.True(ai.TryGetProperty("topGoal", out _));
        Assert.True(ai.TryGetProperty("topGoalCount", out _));
        Assert.True(ai.TryGetProperty("topCommand", out _));
        Assert.True(ai.TryGetProperty("topCommandCount", out _));
        Assert.True(ai.TryGetProperty("topReplanReason", out _));
        Assert.True(ai.TryGetProperty("topReplanReasonCount", out _));
        Assert.True(ai.TryGetProperty("topDebugCause", out _));
        Assert.True(ai.TryGetProperty("topDebugCauseCount", out _));
    }

    [Fact]
    public void AiTelemetry_IsDeterministicAcrossRepeatedRuns()
    {
        var env = new Dictionary<string, string>
        {
            ["WORLDSIM_SCENARIO_SEEDS"] = "703",
            ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
            ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
        };

        var artifactA = CreateArtifactDir();
        var exitA = RunScenarioRunner(artifactA, env, out var stdoutA, out var stderrA);
        Assert.True(exitA is 0 or 2 or 4, $"Unexpected exit code {exitA}\nSTDOUT:\n{stdoutA}\nSTDERR:\n{stderrA}");

        var artifactB = CreateArtifactDir();
        var exitB = RunScenarioRunner(artifactB, env, out var stdoutB, out var stderrB);
        Assert.True(exitB is 0 or 2 or 4, $"Unexpected exit code {exitB}\nSTDOUT:\n{stdoutB}\nSTDERR:\n{stderrB}");

        using var summaryA = ReadJson(Path.Combine(artifactA, "summary.json"));
        using var summaryB = ReadJson(Path.Combine(artifactB, "summary.json"));
        var aiA = summaryA.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ai").GetRawText();
        var aiB = summaryB.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ai").GetRawText();

        Assert.Equal(aiA, aiB);
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

        startInfo.Environment["WORLDSIM_SCENARIO_TICKS"] = "8";
        startInfo.Environment["WORLDSIM_SCENARIO_ARTIFACT_DIR"] = artifactDir;
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_MODE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_ASSERT");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_COMPARE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_PERF");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_PERF_FAIL");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_DELTA_FAIL");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_BASELINE_PATH");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_DRILLDOWN");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_DRILLDOWN_TOP");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_SAMPLE_EVERY");
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");

        foreach (var pair in env)
            startInfo.Environment[pair.Key] = pair.Value;

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        stdout = process.StandardOutput.ReadToEnd();
        stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static JsonDocument ReadJson(string path)
    {
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    private static string CreateArtifactDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-scenario-ai-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WorldSim.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate WorldSim.sln from test base directory.");
    }
}
