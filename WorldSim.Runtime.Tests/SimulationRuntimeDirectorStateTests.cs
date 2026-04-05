using System.IO;
using System;
using System.Reflection;
using System.Text.Json.Nodes;
using WorldSim.Runtime;
using WorldSim.Simulation;
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
        Assert.NotNull(director["effectiveOutputMode"]);
        Assert.NotNull(director["effectiveOutputModeSource"]);
        Assert.NotNull(director["stage"]);
        Assert.NotNull(director["colonyPopulation"]);
        Assert.NotNull(director["foodReservesPct"]);
        Assert.NotNull(director["moraleAvg"]);
        Assert.NotNull(director["economyOutput"]);
        Assert.NotNull(director["activeBeats"]);
        Assert.NotNull(director["activeDirectives"]);
        Assert.NotNull(director["pendingCausalChains"]);
        Assert.NotNull(director["beatCooldownRemainingTicks"]);
        Assert.NotNull(director["maxInfluenceBudget"]);
        Assert.NotNull(director["remainingInfluenceBudget"]);
        Assert.NotNull(director["lastCheckpointBudgetUsed"]);
        Assert.NotNull(director["lastBudgetCheckpointTick"]);
        Assert.NotNull(director["dampeningFactor"]);
        Assert.NotNull(director["activeDomainModifiers"]);
        Assert.NotNull(director["activeGoalBiases"]);
    }

    [Fact]
    public void FreshRuntime_DirectorSnapshot_UsesNotTriggeredUnknownDefaults()
    {
        var runtime = CreateRuntime();

        var director = runtime.GetSnapshot().Director;
        Assert.Equal("not_triggered", director.StageMarker);
        Assert.Equal("unknown", director.OutputMode);
        Assert.Equal("unknown", director.OutputModeSource);
        Assert.Equal("not_triggered", director.ApplyStatus);
    }

    [Fact]
    public void FreshRuntime_BuildRefinerySnapshot_UsesNotTriggeredUnknownDefaults()
    {
        var runtime = CreateRuntime();

        var director = runtime.BuildRefinerySnapshot()["director"]?.AsObject();
        Assert.NotNull(director);
        Assert.Equal("not_triggered", director!["stage"]?.GetValue<string>() ?? string.Empty);
        Assert.Equal("unknown", director["effectiveOutputMode"]?.GetValue<string>() ?? string.Empty);
        Assert.Equal("unknown", director["effectiveOutputModeSource"]?.GetValue<string>() ?? string.Empty);
    }

    [Fact]
    public void DirectorBudgetCheckpoint_ResetAndUsageMirror_AppearInSnapshot()
    {
        var runtime = CreateRuntime();

        runtime.PrepareDirectorCheckpointBudget(maxBudget: 4.5d, tick: 120);
        runtime.RecordDirectorCheckpointBudgetUsed(budgetUsed: 1.875d, tick: 120);

        var director = runtime.BuildRefinerySnapshot()["director"]?.AsObject();
        Assert.NotNull(director);
        Assert.Equal(4.5d, director!["maxInfluenceBudget"]?.GetValue<double>() ?? -1d, 3);
        Assert.Equal(1.875d, director["lastCheckpointBudgetUsed"]?.GetValue<double>() ?? -1d, 3);
        Assert.Equal(2.625d, director["remainingInfluenceBudget"]?.GetValue<double>() ?? -1d, 3);
        Assert.Equal(120L, director["lastBudgetCheckpointTick"]?.GetValue<long>() ?? -1L);
    }

    [Fact]
    public void DirectorBudgetCheckpoint_NewCheckpointResetsPreviousUsage()
    {
        var runtime = CreateRuntime();

        runtime.PrepareDirectorCheckpointBudget(maxBudget: 4d, tick: 200);
        runtime.RecordDirectorCheckpointBudgetUsed(budgetUsed: 1.5d, tick: 200);

        runtime.PrepareDirectorCheckpointBudget(maxBudget: 6d, tick: 250);

        var director = runtime.BuildRefinerySnapshot()["director"]?.AsObject();
        Assert.NotNull(director);
        Assert.Equal(6d, director!["maxInfluenceBudget"]?.GetValue<double>() ?? -1d, 3);
        Assert.Equal(0d, director["lastCheckpointBudgetUsed"]?.GetValue<double>() ?? -1d, 3);
        Assert.Equal(6d, director["remainingInfluenceBudget"]?.GetValue<double>() ?? -1d, 3);
        Assert.Equal(250L, director["lastBudgetCheckpointTick"]?.GetValue<long>() ?? -1L);
    }

    [Fact]
    public void SetDirectorExecutionState_ApplyFailure_IsMirroredInSnapshot()
    {
        var runtime = CreateRuntime();

        runtime.SetDirectorExecutionState(
            effectiveOutputMode: "story_only",
            effectiveOutputModeSource: "response",
            stage: "directorStage:refinery-validated",
            tick: 311,
            isDirectorGoal: true,
            applyStatus: "apply_failed",
            actionStatus: "unknown colonyId in setColonyDirective: 999");

        var snapshot = runtime.GetSnapshot().Director;
        Assert.Equal("directorStage:refinery-validated", snapshot.StageMarker);
        Assert.Equal("story_only", snapshot.OutputMode);
        Assert.Equal("response", snapshot.OutputModeSource);
        Assert.Equal("apply_failed", snapshot.ApplyStatus);
        Assert.Contains("unknown colonyId", snapshot.LastActionStatus, StringComparison.Ordinal);
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

    [Fact]
    public void ApplyStoryBeat_WithCausalChain_RegistersPendingChain_InSnapshot()
    {
        var runtime = CreateRuntime();

        runtime.ApplyStoryBeat(
            "BEAT_PARENT_CHAIN",
            "A warning spreads.",
            durationTicks: 10,
            causalChain: BuildChain(
                metric: "population",
                op: "gt",
                threshold: 0,
                windowTicks: 10,
                followUpBeatId: "BEAT_CHILD_CHAIN",
                followUpText: "The warning becomes action."));

        var director = runtime.BuildRefinerySnapshot()["director"]?.AsObject();
        Assert.NotNull(director);
        var chains = director!["pendingCausalChains"]?.AsArray();
        Assert.NotNull(chains);
        var chain = Assert.Single(chains!);
        Assert.Equal("BEAT_PARENT_CHAIN", chain?["parentBeatId"]?.GetValue<string>());
        Assert.Equal("pending", chain?["status"]?.GetValue<string>());
        Assert.Equal(10, chain?["remainingWindowTicks"]?.GetValue<int>() ?? -1);
        Assert.Equal(0, chain?["triggerCount"]?.GetValue<int>() ?? -1);

        var beats = director["activeBeats"]?.AsArray();
        Assert.NotNull(beats);
        Assert.DoesNotContain(beats!, item => (item?["beatId"]?.GetValue<string>() ?? string.Empty) == "BEAT_CHILD_CHAIN");
    }

    [Fact]
    public void CausalChain_ConditionTrue_TriggersFollowUpExactlyOnce()
    {
        var runtime = CreateRuntime();

        runtime.ApplyStoryBeat(
            "BEAT_PARENT_ONCE",
            "Initial pressure.",
            durationTicks: 12,
            causalChain: BuildChain(
                metric: "population",
                op: "gt",
                threshold: 0,
                windowTicks: 10,
                followUpBeatId: "BEAT_CHILD_ONCE",
                followUpText: "Follow-up fires once."));

        runtime.AdvanceTick(0.25f);
        var afterFirst = runtime.BuildRefinerySnapshot()["director"]?.AsObject();
        Assert.NotNull(afterFirst);
        var beats = afterFirst!["activeBeats"]?.AsArray();
        Assert.NotNull(beats);
        Assert.Contains(beats!, item => (item?["beatId"]?.GetValue<string>() ?? string.Empty) == "BEAT_CHILD_ONCE");

        var chains = afterFirst["pendingCausalChains"]?.AsArray();
        Assert.NotNull(chains);
        var chain = Assert.Single(chains!);
        Assert.Equal("triggered", chain?["status"]?.GetValue<string>());
        Assert.Equal(1, chain?["triggerCount"]?.GetValue<int>() ?? -1);

        runtime.AdvanceTick(0.25f);
        runtime.AdvanceTick(0.25f);

        var afterLater = runtime.BuildRefinerySnapshot()["director"]?.AsObject();
        Assert.NotNull(afterLater);
        var chainsLater = afterLater!["pendingCausalChains"]?.AsArray();
        Assert.NotNull(chainsLater);
        var chainLater = Assert.Single(chainsLater!);
        Assert.Equal("triggered", chainLater?["status"]?.GetValue<string>());
        Assert.Equal(1, chainLater?["triggerCount"]?.GetValue<int>() ?? -1);
    }

    [Fact]
    public void CausalChain_ConditionFalse_ExpiresCleanlyAfterWindow()
    {
        var runtime = CreateRuntime();

        runtime.ApplyStoryBeat(
            "BEAT_PARENT_EXPIRE",
            "A risky omen.",
            durationTicks: 12,
            causalChain: BuildChain(
                metric: "population",
                op: "lt",
                threshold: 0,
                windowTicks: 10,
                followUpBeatId: "BEAT_CHILD_EXPIRE",
                followUpText: "Should never trigger."));

        runtime.AdvanceTick(0.25f);
        var mid = runtime.BuildRefinerySnapshot()["director"]?["pendingCausalChains"]?.AsArray();
        Assert.NotNull(mid);
        Assert.Equal("pending", mid![0]?["status"]?.GetValue<string>());
        Assert.Equal(9, mid[0]?["remainingWindowTicks"]?.GetValue<int>() ?? -1);

        for (var i = 0; i < 9; i++)
            runtime.AdvanceTick(0.25f);

        var end = runtime.BuildRefinerySnapshot()["director"]?["pendingCausalChains"]?.AsArray();
        Assert.NotNull(end);
        Assert.Equal("expired", end![0]?["status"]?.GetValue<string>());
        Assert.Equal(0, end[0]?["remainingWindowTicks"]?.GetValue<int>() ?? -1);
        Assert.Equal(0, end[0]?["triggerCount"]?.GetValue<int>() ?? -1);

        var beats = runtime.BuildRefinerySnapshot()["director"]?["activeBeats"]?.AsArray();
        Assert.NotNull(beats);
        Assert.DoesNotContain(beats!, item => (item?["beatId"]?.GetValue<string>() ?? string.Empty) == "BEAT_CHILD_EXPIRE");
    }

    [Fact]
    public void CausalChain_RejectsWindowTicksBelowMinimum()
    {
        var runtime = CreateRuntime();

        var ex = Assert.Throws<InvalidOperationException>(() => runtime.ApplyStoryBeat(
            "BEAT_PARENT_WINDOW_ONE",
            "One-shot window.",
            durationTicks: 8,
            causalChain: BuildChain(
                metric: "population",
                op: "lt",
                threshold: 0,
                windowTicks: 1,
                followUpBeatId: "BEAT_CHILD_WINDOW_ONE",
                followUpText: "Should not fire.")));

        Assert.Contains("windowTicks must be in [10, 100]", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CausalChain_PopulationEq_RejectsFractionalThreshold()
    {
        var runtime = CreateRuntime();

        var ex = Assert.Throws<InvalidOperationException>(() => runtime.ApplyStoryBeat(
            "BEAT_PARENT_POP_EQ",
            "Population threshold is invalid.",
            durationTicks: 8,
            causalChain: BuildChain(
                metric: "population",
                op: "eq",
                threshold: 24.5,
                windowTicks: 10,
                followUpBeatId: "BEAT_CHILD_POP_EQ",
                followUpText: "Should not register.")));

        Assert.Contains("population eq threshold must be an integer", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CausalChain_FoodReservesPct_UsesPercentScale()
    {
        var runtime = CreateRuntime();
        var world = GetWorld(runtime);
        foreach (var colony in world._colonies)
        {
            colony.Stock[Resource.Food] = 100000;
        }

        runtime.ApplyStoryBeat(
            "BEAT_PARENT_FOOD_SCALE",
            "Granaries overflow.",
            durationTicks: 10,
            causalChain: BuildChain(
                metric: "food_reserves_pct",
                op: "gt",
                threshold: 50,
                windowTicks: 10,
                followUpBeatId: "BEAT_CHILD_FOOD_SCALE",
                followUpText: "Abundance invites celebration."));

        runtime.AdvanceTick(0.25f);
        var beats = runtime.BuildRefinerySnapshot()["director"]?["activeBeats"]?.AsArray();
        Assert.NotNull(beats);
        Assert.Contains(beats!, item => (item?["beatId"]?.GetValue<string>() ?? string.Empty) == "BEAT_CHILD_FOOD_SCALE");
    }

    [Fact]
    public void CausalChain_DoesNotAlterBudgetMirrorState_WhenFollowUpTriggers()
    {
        var runtime = CreateRuntime();
        runtime.PrepareDirectorCheckpointBudget(maxBudget: 5d, tick: 40);
        runtime.RecordDirectorCheckpointBudgetUsed(budgetUsed: 1.25d, tick: 40);

        runtime.ApplyStoryBeat(
            "BEAT_PARENT_BUDGET_GUARD",
            "Pressure mounts.",
            durationTicks: 8,
            causalChain: BuildChain(
                metric: "population",
                op: "gt",
                threshold: 0,
                windowTicks: 10,
                followUpBeatId: "BEAT_CHILD_BUDGET_GUARD",
                followUpText: "Countermove."));

        runtime.AdvanceTick(0.25f);

        var director = runtime.BuildRefinerySnapshot()["director"]?.AsObject();
        Assert.NotNull(director);
        Assert.Equal(5d, director!["maxInfluenceBudget"]?.GetValue<double>() ?? -1d, 3);
        Assert.Equal(1.25d, director["lastCheckpointBudgetUsed"]?.GetValue<double>() ?? -1d, 3);
        Assert.Equal(3.75d, director["remainingInfluenceBudget"]?.GetValue<double>() ?? -1d, 3);
    }

    [Fact]
    public void BuildRefinerySnapshot_DirectorFoodReservesPct_UsesPercentScale()
    {
        var runtime = CreateRuntime();
        var world = GetWorld(runtime);
        foreach (var colony in world._colonies)
        {
            colony.Stock[Resource.Food] = 100000;
        }

        var director = runtime.BuildRefinerySnapshot()["director"]?.AsObject();
        Assert.NotNull(director);
        var foodReservesPct = director!["foodReservesPct"]?.GetValue<double>() ?? -1d;
        Assert.InRange(foodReservesPct, 99.999, 100.0);
    }

    [Fact]
    public void DirectorRenderState_ExposesPendingChainMonitoring()
    {
        var runtime = CreateRuntime();

        runtime.ApplyStoryBeat(
            "BEAT_PARENT_RENDER",
            "Scouts report movement.",
            durationTicks: 9,
            causalChain: BuildChain(
                metric: "morale_avg",
                op: "gt",
                threshold: 1,
                windowTicks: 10,
                followUpBeatId: "BEAT_CHILD_RENDER",
                followUpText: "Morale surge response."));

        var director = runtime.GetSnapshot().Director;
        Assert.Single(director.PendingChains);
        var chain = director.PendingChains[0];
        Assert.Equal("BEAT_PARENT_RENDER", chain.ParentBeatId);
        Assert.Equal("pending", chain.Status);
        Assert.Equal("BEAT_CHILD_RENDER", chain.FollowUpBeatId);

        runtime.AdvanceTick(0.25f);
        var after = runtime.GetSnapshot().Director.PendingChains[0];
        Assert.Equal("triggered", after.Status);
        Assert.Equal(1, after.TriggerCount);
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 10, techPath);
    }

    private static DirectorCausalChainSpec BuildChain(
        string metric,
        string op,
        double threshold,
        int windowTicks,
        string followUpBeatId,
        string followUpText)
    {
        return new DirectorCausalChainSpec(
            new DirectorCausalConditionSpec(metric, op, threshold),
            new DirectorFollowUpBeatSpec(
                followUpBeatId,
                followUpText,
                DurationTicks: 7,
                Effects: Array.Empty<DirectorDomainModifierSpec>()),
            WindowTicks: windowTicks,
            MaxTriggers: 1);
    }

    private static World GetWorld(SimulationRuntime runtime)
    {
        var worldField = typeof(SimulationRuntime).GetField("_world", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(worldField);
        var world = worldField!.GetValue(runtime) as World;
        Assert.NotNull(world);
        return world!;
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
