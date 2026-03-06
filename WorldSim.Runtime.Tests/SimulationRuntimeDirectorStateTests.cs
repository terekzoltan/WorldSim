using System.IO;
using System;
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

        runtime.ApplyStoryBeat(
            "BEAT_COOLDOWN",
            "Harvest omens spread",
            durationTicks: 30,
            effects: new[]
            {
                new DirectorDomainModifierSpec("economy", 0.10, DurationTicks: 30)
            });
        var first = runtime.BuildRefinerySnapshot();
        int cooldownBefore = first["director"]?["beatCooldownRemainingTicks"]?.GetValue<int>() ?? -1;

        runtime.AdvanceTick(0.25f);
        var second = runtime.BuildRefinerySnapshot();
        int cooldownAfter = second["director"]?["beatCooldownRemainingTicks"]?.GetValue<int>() ?? -1;

        Assert.True(cooldownBefore > 0);
        Assert.Equal(cooldownBefore - 1, cooldownAfter);
    }

    [Fact]
    public void ApplyStoryBeat_MinorBeat_HasNoCooldownAndNoModifiers()
    {
        var runtime = CreateRuntime();

        runtime.ApplyStoryBeat("BEAT_MINOR", "A quiet day passes", durationTicks: 12);
        var snapshot = runtime.BuildRefinerySnapshot();

        int cooldown = snapshot["director"]?["beatCooldownRemainingTicks"]?.GetValue<int>() ?? -1;
        var mods = snapshot["director"]?["activeDomainModifiers"]?.AsArray();
        var beats = snapshot["director"]?["activeBeats"]?.AsArray();

        Assert.Equal(0, cooldown);
        Assert.NotNull(mods);
        Assert.Empty(mods!);
        Assert.Contains(beats!, beat => (beat?["severity"]?.GetValue<string>() ?? string.Empty) == "Minor");
    }

    [Fact]
    public void ApplyStoryBeat_EpicBeat_RegistersEffects_AndSetsEpicCooldown()
    {
        var runtime = CreateRuntime();

        runtime.ApplyStoryBeat(
            "BEAT_EPIC",
            "A kingdom-wide shock reshapes priorities.",
            durationTicks: 16,
            effects: new[]
            {
                new DirectorDomainModifierSpec("food", 0.10, DurationTicks: 16),
                new DirectorDomainModifierSpec("economy", -0.08, DurationTicks: 16),
                new DirectorDomainModifierSpec("morale", 0.06, DurationTicks: 16)
            });

        var snapshot = runtime.BuildRefinerySnapshot();
        int cooldown = snapshot["director"]?["beatCooldownRemainingTicks"]?.GetValue<int>() ?? -1;
        var mods = snapshot["director"]?["activeDomainModifiers"]?.AsArray();
        var beats = snapshot["director"]?["activeBeats"]?.AsArray();

        Assert.True(cooldown >= 40);
        Assert.NotNull(mods);
        Assert.Equal(3, mods!.Count);
        Assert.Contains(beats!, beat => (beat?["severity"]?.GetValue<string>() ?? string.Empty) == "Epic");
    }

    [Fact]
    public void ApplyStoryBeat_RejectsMoreThanThreeEffects()
    {
        var runtime = CreateRuntime();

        var ex = Assert.Throws<InvalidOperationException>(() => runtime.ApplyStoryBeat(
            "BEAT_TOO_MANY",
            "Overloaded candidate",
            durationTicks: 14,
            effects: new[]
            {
                new DirectorDomainModifierSpec("food", 0.05, DurationTicks: 14),
                new DirectorDomainModifierSpec("economy", 0.05, DurationTicks: 14),
                new DirectorDomainModifierSpec("morale", 0.05, DurationTicks: 14),
                new DirectorDomainModifierSpec("research", 0.05, DurationTicks: 14)
            }));

        Assert.Contains("max 3", ex.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.NotNull(director["dampeningFactor"]);
        Assert.NotNull(director["activeDomainModifiers"]);
        Assert.NotNull(director["activeGoalBiases"]);
    }

    [Fact]
    public void ApplyStoryBeat_WithEffects_RegistersDomainModifiers_AndDecays()
    {
        var runtime = CreateRuntime();

        runtime.ApplyStoryBeat(
            "BEAT_EFFECT",
            "A harsh wind slows work.",
            durationTicks: 10,
            effects: new[]
            {
                new DirectorDomainModifierSpec("economy", 0.20, DurationTicks: 10)
            });

        var first = runtime.BuildRefinerySnapshot();
        var mods = first["director"]?["activeDomainModifiers"]?.AsArray();
        Assert.NotNull(mods);
        Assert.True(mods!.Count >= 1);

        var economy = mods
            .Select(item => item?.AsObject())
            .FirstOrDefault(obj => (obj?["domain"]?.GetValue<string>() ?? string.Empty) == "economy");
        Assert.NotNull(economy);

        var eff0 = economy!["effectiveModifier"]?.GetValue<double>() ?? -999;
        Assert.InRange(eff0, 0.19, 0.200001);

        runtime.AdvanceTick(0.25f);
        var second = runtime.BuildRefinerySnapshot();
        var mods2 = second["director"]?["activeDomainModifiers"]?.AsArray();
        var economy2 = mods2
            ?.Select(item => item?.AsObject())
            .FirstOrDefault(obj => (obj?["domain"]?.GetValue<string>() ?? string.Empty) == "economy");
        Assert.NotNull(economy2);

        var eff1 = economy2!["effectiveModifier"]?.GetValue<double>() ?? -999;
        Assert.InRange(eff1, 0.17, 0.19);
    }

    [Fact]
    public void ApplyColonyDirective_WithBiases_RegistersGoalBiases()
    {
        var runtime = CreateRuntime();

        runtime.ApplyColonyDirective(
            colonyId: 0,
            directive: "CustomDirective",
            durationTicks: 10,
            biases: new[]
            {
                new DirectorGoalBiasSpec("gathering", 0.40, DurationTicks: 10)
            });

        var snapshot = runtime.BuildRefinerySnapshot();
        var biases = snapshot["director"]?["activeGoalBiases"]?.AsArray();
        Assert.NotNull(biases);
        Assert.Contains(biases!, item =>
            (item?["colonyId"]?.GetValue<int>() ?? -1) == 0
            && (item?["goalCategory"]?.GetValue<string>() ?? string.Empty).Equals("gathering", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DirectorDampeningFactor_Zero_ProducesNoActiveDomainModifiers()
    {
        const string key = "REFINERY_DIRECTOR_DAMPENING";
        var prev = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, "0");
            var runtime = CreateRuntime();
            runtime.ApplyStoryBeat(
                "BEAT_NO_EFFECT",
                "Narrative only",
                durationTicks: 10,
                effects: new[] { new DirectorDomainModifierSpec("economy", 0.20, DurationTicks: 10) });

            var snapshot = runtime.BuildRefinerySnapshot();
            var mods = snapshot["director"]?["activeDomainModifiers"]?.AsArray();
            Assert.NotNull(mods);
            Assert.Empty(mods!);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, prev);
        }
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
