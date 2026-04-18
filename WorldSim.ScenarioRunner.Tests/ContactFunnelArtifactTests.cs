using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class ContactFunnelArtifactTests
{
    [Fact]
    public void RunResults_ContainContactTelemetryBlock()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "711",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var firstRun = summary.RootElement.GetProperty("runs").EnumerateArray().First();
        var contact = firstRun.GetProperty("contact");
        Assert.True(contact.TryGetProperty("hostileSensed", out _));
        Assert.True(contact.TryGetProperty("pursueStarts", out _));
        Assert.True(contact.TryGetProperty("adjacentContacts", out _));
        Assert.True(contact.TryGetProperty("factionCombatDamageEvents", out _));
        Assert.True(contact.TryGetProperty("factionCombatDeaths", out _));
        Assert.True(contact.TryGetProperty("routingStarts", out _));
        Assert.True(contact.TryGetProperty("battlePairings", out _));
        Assert.True(contact.TryGetProperty("battleTicksWithDamage", out _));
        Assert.True(contact.TryGetProperty("battleTicksWithDeaths", out _));
        Assert.True(contact.TryGetProperty("routingBeforeDamage", out _));
        Assert.True(contact.TryGetProperty("firstHostileSenseTick", out _));
        Assert.True(contact.TryGetProperty("firstRoutingBeforeDamageTick", out _));
    }

    [Fact]
    public void Drilldown_TimelineContainsCompactContactFields()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "712",
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
        var contact = firstSample.GetProperty("contact");
        Assert.True(contact.TryGetProperty("hostileSensed", out _));
        Assert.True(contact.TryGetProperty("pursueStarts", out _));
        Assert.True(contact.TryGetProperty("adjacentContacts", out _));
        Assert.True(contact.TryGetProperty("factionCombatDamageEvents", out _));
        Assert.True(contact.TryGetProperty("factionCombatDeaths", out _));
        Assert.True(contact.TryGetProperty("routingStarts", out _));
        Assert.True(contact.TryGetProperty("battlePairings", out _));
        Assert.True(contact.TryGetProperty("battleTicksWithDamage", out _));
        Assert.True(contact.TryGetProperty("battleTicksWithDeaths", out _));
        Assert.True(contact.TryGetProperty("routingBeforeDamage", out _));
    }

    [Fact]
    public void ContactTelemetry_IsDeterministicAcrossRepeatedRuns()
    {
        var env = new Dictionary<string, string>
        {
            ["WORLDSIM_SCENARIO_SEEDS"] = "713",
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
        var contactA = summaryA.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("contact").GetRawText();
        var contactB = summaryB.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("contact").GetRawText();

        Assert.Equal(contactA, contactB);
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
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-scenario-contact-{Guid.NewGuid():N}");
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
