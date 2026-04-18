using System.Diagnostics;
using System.Text.Json;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class DrilldownTests
{
    [Fact]
    public void Drilldown_ArtifactsWritten_WhenEnabled()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "501",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple,goap",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
                ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "2",
                ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "2"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var drilldownDir = Path.Combine(artifactDir, "drilldown");
        Assert.True(Directory.Exists(drilldownDir));
        Assert.True(File.Exists(Path.Combine(drilldownDir, "index.json")));

        var index = ReadJson(Path.Combine(drilldownDir, "index.json"));
        var runs = index.RootElement.GetProperty("runs");
        Assert.True(runs.GetArrayLength() > 0);

        var firstRunKey = runs[0].GetProperty("runKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(firstRunKey));
        var firstRunDir = Path.Combine(drilldownDir, firstRunKey!);
        Assert.True(File.Exists(Path.Combine(firstRunDir, "timeline.json")));
        Assert.True(File.Exists(Path.Combine(firstRunDir, "events.json")));
        Assert.True(File.Exists(Path.Combine(firstRunDir, "replay.json")));

        var timeline = ReadJson(Path.Combine(firstRunDir, "timeline.json"));
        var firstSample = timeline.RootElement.EnumerateArray().First();
        Assert.True(firstSample.TryGetProperty("combatDeaths", out _));
        Assert.True(firstSample.TryGetProperty("battleTicks", out _));
        Assert.True(firstSample.TryGetProperty("activeBattles", out _));
        Assert.True(firstSample.TryGetProperty("activeCombatGroups", out _));
        Assert.True(firstSample.TryGetProperty("routingPeople", out _));
        Assert.True(firstSample.TryGetProperty("minCombatMorale", out _));
        Assert.True(firstSample.TryGetProperty("contact", out var contactTelemetry));
        Assert.True(contactTelemetry.TryGetProperty("hostileSensed", out _));
        Assert.True(contactTelemetry.TryGetProperty("battlePairings", out _));
        Assert.True(contactTelemetry.TryGetProperty("routingBeforeDamage", out _));
        Assert.True(firstSample.TryGetProperty("ai", out var aiTelemetry));
        Assert.True(aiTelemetry.TryGetProperty("topGoal", out _));
        Assert.True(aiTelemetry.TryGetProperty("topCommand", out _));
        Assert.True(aiTelemetry.TryGetProperty("topReplanReason", out _));
        Assert.True(aiTelemetry.TryGetProperty("topDebugCause", out _));
    }

    [Fact]
    public void Drilldown_NotWritten_WhenDisabled()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "502",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out _,
            out _);

        Assert.True(exitCode is 0 or 2 or 4);
        Assert.False(Directory.Exists(Path.Combine(artifactDir, "drilldown")));

        var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.False(manifest.RootElement.GetProperty("drilldownEnabled").GetBoolean());
    }

    [Fact]
    public void Drilldown_DeterministicWorstRunSelection()
    {
        var env = new Dictionary<string, string>
        {
            ["WORLDSIM_SCENARIO_SEEDS"] = "503,504",
            ["WORLDSIM_SCENARIO_PLANNERS"] = "simple,goap",
            ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
            ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
            ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "3",
            ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "3"
        };

        var artifactA = CreateArtifactDir();
        var exitA = RunScenarioRunner(artifactA, env, out _, out _);
        Assert.True(exitA is 0 or 2 or 4);

        var artifactB = CreateArtifactDir();
        var exitB = RunScenarioRunner(artifactB, env, out _, out _);
        Assert.True(exitB is 0 or 2 or 4);

        var keysA = ReadJson(Path.Combine(artifactA, "drilldown", "index.json")).RootElement
            .GetProperty("runs").EnumerateArray().Select(item => item.GetProperty("runKey").GetString()).ToArray();
        var keysB = ReadJson(Path.Combine(artifactB, "drilldown", "index.json")).RootElement
            .GetProperty("runs").EnumerateArray().Select(item => item.GetProperty("runKey").GetString()).ToArray();

        Assert.Equal(keysA, keysB);
    }

    [Fact]
    public void Drilldown_ModeAll_WithBaseline_WritesAllArtifacts()
    {
        var baselineArtifact = CreateArtifactDir();
        var baselineExit = RunScenarioRunner(
            baselineArtifact,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "505",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out _,
            out _);
        Assert.True(baselineExit is 0 or 2 or 4);

        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "505",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_MODE"] = "all",
                ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = Path.Combine(baselineArtifact, "summary.json")
            },
            out _,
            out _);

        Assert.True(exitCode is 0 or 2 or 4);
        Assert.True(File.Exists(Path.Combine(artifactDir, "assertions.json")));
        Assert.True(File.Exists(Path.Combine(artifactDir, "compare.json")));
        Assert.True(File.Exists(Path.Combine(artifactDir, "perf.json")));
        Assert.True(File.Exists(Path.Combine(artifactDir, "drilldown", "index.json")));
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
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-scenario-drilldown-{Guid.NewGuid():N}");
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
