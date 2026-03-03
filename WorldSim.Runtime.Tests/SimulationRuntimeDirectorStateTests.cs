using System.IO;
using System.Text.Json.Nodes;
using WorldSim.Runtime;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class SimulationRuntimeDirectorStateTests
{
    [Fact]
    public void ApplyStoryBeat_SetsCooldown_AndCooldownAdvancesWithTicks()
    {
        var runtime = CreateRuntime();

        runtime.ApplyStoryBeat("BEAT_COOLDOWN", "Harvest omens spread", durationTicks: 30);
        var first = runtime.BuildRefinerySnapshot();
        int cooldownBefore = first["director"]?["beatCooldownRemainingTicks"]?.GetValue<int>() ?? -1;

        runtime.AdvanceTick(0.25f);
        var second = runtime.BuildRefinerySnapshot();
        int cooldownAfter = second["director"]?["beatCooldownRemainingTicks"]?.GetValue<int>() ?? -1;

        Assert.True(cooldownBefore > 0);
        Assert.Equal(cooldownBefore - 1, cooldownAfter);
    }

    [Fact]
    public void ApplyStoryBeat_AppearsInDirectorSnapshot()
    {
        var runtime = CreateRuntime();

        runtime.ApplyStoryBeat("BEAT_SNAPSHOT", "A comet appears", durationTicks: 15);
        var snapshot = runtime.BuildRefinerySnapshot();

        var beats = snapshot["director"]?["activeBeats"]?.AsArray();
        Assert.NotNull(beats);
        Assert.Contains(beats!, item => (item?["beatId"]?.GetValue<string>() ?? string.Empty) == "BEAT_SNAPSHOT");
    }

    [Fact]
    public void ApplyColonyDirective_AppearsInDirectorSnapshot()
    {
        var runtime = CreateRuntime();

        runtime.ApplyColonyDirective(0, "PrioritizeFood", durationTicks: 25);
        var snapshot = runtime.BuildRefinerySnapshot();

        var directives = snapshot["director"]?["activeDirectives"]?.AsArray();
        Assert.NotNull(directives);
        Assert.Contains(directives!, item =>
            (item?["colonyId"]?.GetValue<int>() ?? -1) == 0
            && (item?["directive"]?.GetValue<string>() ?? string.Empty) == "PrioritizeFood");
    }

    [Fact]
    public void BuildRefinerySnapshot_ContainsDirectorFields()
    {
        var runtime = CreateRuntime();
        runtime.AdvanceTick(0.25f);

        var snapshot = runtime.BuildRefinerySnapshot();
        var director = snapshot["director"]?.AsObject();

        Assert.NotNull(director);
        Assert.NotNull(director!["currentTick"]);
        Assert.NotNull(director["currentSeason"]);
        Assert.NotNull(director["colonyPopulation"]);
        Assert.NotNull(director["foodReservesPct"]);
        Assert.NotNull(director["moraleAvg"]);
        Assert.NotNull(director["economyOutput"]);
        Assert.NotNull(director["activeBeats"]);
        Assert.NotNull(director["activeDirectives"]);
        Assert.NotNull(director["beatCooldownRemainingTicks"]);
        Assert.NotNull(director["remainingInfluenceBudget"]);
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 10, techPath);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var techPath = Path.Combine(current.FullName, "Tech", "technologies.json");
            if (File.Exists(techPath))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json");
    }
}
