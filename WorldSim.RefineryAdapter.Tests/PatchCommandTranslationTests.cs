using System.Text.Json.Nodes;
using Xunit;
using WorldSim.Contracts.V1;
using WorldSim.Contracts.V2;
using WorldSim.RefineryAdapter.Translation;
using WorldSim.Runtime;

namespace WorldSim.RefineryAdapter.Tests;

public class PatchCommandTranslationTests
{
    [Fact]
    public void Translator_ConvertsAddTechToUnlockCommand()
    {
        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-1",
            123,
            new List<PatchOp>
            {
                new AddTechOp
                {
                    OpId = "op_1",
                    TechId = "agriculture",
                    PrereqTechIds = Array.Empty<string>(),
                    Cost = new JsonObject { ["research"] = 80 },
                    Effects = new JsonObject()
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var translator = new PatchCommandTranslator();
        var commands = translator.Translate(response);

        var cmd = Assert.Single(commands);
        var unlock = Assert.IsType<UnlockTechRuntimeCommand>(cmd);
        Assert.Equal("agriculture", unlock.TechId);
    }

    [Fact]
    public void Translator_ConvertsDirectorOpsToRuntimeCommands()
    {
        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-3",
            123,
            new List<PatchOp>
            {
                new AddStoryBeatOp
                {
                    OpId = "op_story_1",
                    Severity = "major",
                    BeatId = "BEAT_SAMPLE_1",
                    Text = "Storm clouds gather at the colony edge.",
                    DurationTicks = 20,
                    Effects = new[]
                    {
                        new EffectEntry
                        {
                            Type = "domain_modifier",
                            Domain = "economy",
                            Modifier = 0.10,
                            DurationTicks = 20
                        }
                    },
                    CausalChain = new CausalChainEntry
                    {
                        Type = "causal_chain",
                        Condition = new CausalCondition
                        {
                            Metric = "food_reserves_pct",
                            Operator = "lt",
                            Threshold = 35
                        },
                        FollowUpBeat = new CausalFollowUpBeat
                        {
                            BeatId = "BEAT_FOLLOW_1",
                            Text = "Emergency rationing begins.",
                            DurationTicks = 12,
                            Severity = "major",
                            Effects = new[]
                            {
                                BuildEffect("morale", -0.08, 12)
                            }
                        },
                        WindowTicks = 20,
                        MaxTriggers = 1
                    }
                },
                new SetColonyDirectiveOp
                {
                    OpId = "op_nudge_1",
                    ColonyId = 0,
                    Directive = "PrioritizeFood",
                    DurationTicks = 18,
                    Biases = new[]
                    {
                        new GoalBiasEntry
                        {
                            Type = "goal_bias",
                            GoalCategory = "farming",
                            Weight = 0.30,
                            DurationTicks = 18
                        }
                    }
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var translator = new PatchCommandTranslator();
        var commands = translator.Translate(response);

        Assert.Collection(
            commands,
            item =>
            {
                var story = Assert.IsType<ApplyStoryBeatRuntimeCommand>(item);
                Assert.Equal("BEAT_SAMPLE_1", story.BeatId);
                var effect = Assert.Single(story.Effects);
                Assert.Equal("economy", effect.Domain);
                Assert.True(story.CausalChain.HasValue);
                Assert.Equal("food_reserves_pct", story.CausalChain.Value.Condition.Metric);
                Assert.Equal("BEAT_FOLLOW_1", story.CausalChain.Value.FollowUpBeat.BeatId);
                Assert.Equal(20, story.CausalChain.Value.WindowTicks);
            },
            item =>
            {
                var nudge = Assert.IsType<ApplyColonyDirectiveRuntimeCommand>(item);
                Assert.Equal(0, nudge.ColonyId);
                Assert.Equal("PrioritizeFood", nudge.Directive);
                var bias = Assert.Single(nudge.Biases);
                Assert.Equal("farming", bias.GoalCategory);
            }
        );
    }

    [Fact]
    public void Translator_AllowsMinorBeatWithZeroEffects()
    {
        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-s3a-minor",
            777,
            new List<PatchOp>
            {
                new AddStoryBeatOp
                {
                    OpId = "op_story_minor_1",
                    Severity = "minor",
                    BeatId = "BEAT_MINOR_1",
                    Text = "Scouts report calm borders.",
                    DurationTicks = 12,
                    Effects = Array.Empty<EffectEntry>()
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var translator = new PatchCommandTranslator();
        var commands = translator.Translate(response);

        var story = Assert.IsType<ApplyStoryBeatRuntimeCommand>(Assert.Single(commands));
        Assert.Empty(story.Effects);
    }

    [Fact]
    public void Translator_RejectsSeverityMismatchDeterministically()
    {
        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-s3a-mismatch",
            778,
            new List<PatchOp>
            {
                new AddStoryBeatOp
                {
                    OpId = "op_story_bad_1",
                    Severity = "minor",
                    BeatId = "BEAT_BAD_1",
                    Text = "Unexpected pressure on supply chains.",
                    DurationTicks = 16,
                    Effects = new[]
                    {
                        new EffectEntry
                        {
                            Type = "domain_modifier",
                            Domain = "food",
                            Modifier = -0.10,
                            DurationTicks = 16
                        }
                    }
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var translator = new PatchCommandTranslator();
        var ex = Assert.Throws<InvalidOperationException>(() => translator.Translate(response));
        Assert.Contains("Story beat severity mismatch", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Translator_RejectsEffectCountAboveEpicLimit()
    {
        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-s3a-overflow",
            779,
            new List<PatchOp>
            {
                new AddStoryBeatOp
                {
                    OpId = "op_story_bad_2",
                    Severity = "epic",
                    BeatId = "BEAT_BAD_2",
                    Text = "Overloaded scenario candidate.",
                    DurationTicks = 20,
                    Effects = new[]
                    {
                        BuildEffect("food", 0.05, 20),
                        BuildEffect("economy", 0.05, 20),
                        BuildEffect("morale", 0.05, 20),
                        BuildEffect("research", 0.05, 20)
                    }
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var translator = new PatchCommandTranslator();
        var ex = Assert.Throws<InvalidOperationException>(() => translator.Translate(response));
        Assert.Contains("max 3 effects", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Translator_RejectsUnsupportedOpsDeterministically()
    {
        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-4",
            123,
            new List<PatchOp>
            {
                new AddWorldEventOp
                {
                    OpId = "op_2",
                    EventId = "WEATHER_1",
                    Type = "RAIN_BONUS",
                    Params = new JsonObject(),
                    DurationTicks = 10
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var translator = new PatchCommandTranslator();
        var ex = Assert.Throws<NotSupportedException>(() => translator.Translate(response));
        Assert.Contains("Adapter supports addTech/addStoryBeat/setColonyDirective only", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Translator_RejectsCampaignOpsUntilP4B()
    {
        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-p4a-campaign-op",
            123,
            new List<PatchOp>
            {
                new DeclareWarOp
                {
                    OpId = "op_war_1",
                    AttackerFactionId = 1,
                    DefenderFactionId = 2,
                    Reason = "border pressure"
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var translator = new PatchCommandTranslator();
        var ex = Assert.Throws<NotSupportedException>(() => translator.Translate(response));
        Assert.Contains("Adapter supports addTech/addStoryBeat/setColonyDirective only", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Executor_RejectsUnknownTechDeterministically()
    {
        var runtime = CreateRuntime();
        var executor = new RuntimePatchCommandExecutor();

        var commands = new List<RuntimePatchCommand>
        {
            new UnlockTechRuntimeCommand("unknown_tech")
        };

        var ex = Assert.Throws<InvalidOperationException>(() => executor.Execute(runtime, commands));
        Assert.Contains("unknown techId", ex.Message, StringComparison.Ordinal);
        Assert.Contains("loadedTechCount=", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Executor_RejectsUnknownDirectiveDeterministically()
    {
        var runtime = CreateRuntime();
        var executor = new RuntimePatchCommandExecutor();

        var commands = new List<RuntimePatchCommand>
        {
            new ApplyColonyDirectiveRuntimeCommand(0, "UnknownDirective", 10, Array.Empty<DirectorGoalBiasSpec>())
        };

        var ex = Assert.Throws<InvalidOperationException>(() => executor.Execute(runtime, commands));
        Assert.Contains("unknown directive", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Executor_AppliesStoryBeatCausalChain_EndToEnd()
    {
        var runtime = CreateRuntime();
        var executor = new RuntimePatchCommandExecutor();

        var commands = new List<RuntimePatchCommand>
        {
            new ApplyStoryBeatRuntimeCommand(
                "BEAT_PARENT_EXECUTOR",
                "Pressure mounts.",
                14,
                Array.Empty<DirectorDomainModifierSpec>(),
                new DirectorCausalChainSpec(
                    new DirectorCausalConditionSpec("population", "gt", 0),
                    new DirectorFollowUpBeatSpec(
                        "BEAT_CHILD_EXECUTOR",
                        "Follow-up engages.",
                        12,
                        Array.Empty<DirectorDomainModifierSpec>()),
                    WindowTicks: 10,
                    MaxTriggers: 1))
        };

        executor.ValidateDirectorBatch(runtime, commands);
        executor.Execute(runtime, commands);

        runtime.AdvanceTick(0.25f);

        var beats = runtime.BuildRefinerySnapshot()["director"]?["activeBeats"]?.AsArray();
        Assert.NotNull(beats);
        Assert.Contains(beats!, item => (item?["beatId"]?.GetValue<string>() ?? string.Empty) == "BEAT_CHILD_EXECUTOR");
    }

    [Fact]
    public void Executor_RejectsUnknownColonyDeterministically()
    {
        var runtime = CreateRuntime();
        var executor = new RuntimePatchCommandExecutor();

        var commands = new List<RuntimePatchCommand>
        {
            new ApplyColonyDirectiveRuntimeCommand(999, "PrioritizeFood", 10, Array.Empty<DirectorGoalBiasSpec>())
        };

        var ex = Assert.Throws<InvalidOperationException>(() => executor.Execute(runtime, commands));
        Assert.Contains("unknown colonyId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_LoadsKnownTechs_FromTechnologiesJson()
    {
        var runtime = CreateRuntime();

        Assert.True(runtime.LoadedTechCount > 0);
        Assert.True(runtime.IsKnownTech("agriculture"));
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 10, techPath);
    }

    private static EffectEntry BuildEffect(string domain, double modifier, int durationTicks)
    {
        return new EffectEntry
        {
            Type = "domain_modifier",
            Domain = domain,
            Modifier = modifier,
            DurationTicks = durationTicks
        };
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var techPath = Path.Combine(current.FullName, "Tech", "technologies.json");
            if (File.Exists(techPath))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json");
    }
}
