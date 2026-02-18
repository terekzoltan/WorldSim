using System.Text.Json.Nodes;
using WorldSim.Contracts.V1;
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
        var unlock = Assert.IsType<UnlockTechCommand>(cmd);
        Assert.Equal("agriculture", unlock.TechId);
    }

    [Fact]
    public void Translator_RejectsUnsupportedOpsDeterministically()
    {
        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-2",
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
        Assert.Contains("Adapter supports only addTech currently", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Executor_RejectsUnknownTechDeterministically()
    {
        var runtime = CreateRuntime();
        var executor = new RuntimePatchCommandExecutor();

        var commands = new List<RuntimePatchCommand>
        {
            new UnlockTechCommand("unknown_tech")
        };

        var ex = Assert.Throws<InvalidOperationException>(() => executor.Execute(runtime, commands));
        Assert.Contains("unknown techId", ex.Message, StringComparison.Ordinal);
    }

    private static SimulationRuntime CreateRuntime()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        return new SimulationRuntime(32, 32, 10, techPath);
    }
}
