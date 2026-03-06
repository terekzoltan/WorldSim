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
