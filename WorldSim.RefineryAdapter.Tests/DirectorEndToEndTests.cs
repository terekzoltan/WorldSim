using System.Text.Json.Nodes;
using WorldSim.Contracts.V1;
using WorldSim.Contracts.V2;
using WorldSim.RefineryAdapter.Translation;
using WorldSim.Runtime;

namespace WorldSim.RefineryAdapter.Tests;

/// <summary>
/// End-to-end tests: PatchResponse (built in-process) → Translate → Execute on SimulationRuntime.
/// JSON parsing is covered in RefineryClient.Tests; this layer validates the adapter→runtime boundary.
/// </summary>
public class DirectorEndToEndTests
{
    [Fact]
    public void DirectorOps_FullPipeline_SetsLastDirectorActionStatus()
    {
        var runtime = CreateRuntime();
        var translator = new PatchCommandTranslator();
        var executor = new RuntimePatchCommandExecutor();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-e2e-1",
            42,
            new List<PatchOp>
            {
                new SetColonyDirectiveOp
                {
                    OpId = "op_nudge_e2e",
                    ColonyId = 0,
                    Directive = "PrioritizeFood",
                    DurationTicks = 20
                },
                new AddStoryBeatOp
                {
                    OpId = "op_story_e2e",
                    BeatId = "BEAT_E2E_1",
                    Text = "A lone wanderer arrives at the colony gates.",
                    DurationTicks = 30
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var commands = translator.Translate(response);
        executor.Execute(runtime, commands);

        // LastDirectorActionStatus reflects the last applied op (story beat, applied second)
        Assert.NotNull(runtime.LastDirectorActionStatus);
        Assert.Contains("BEAT_E2E_1", runtime.LastDirectorActionStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectorOps_DuplicateOpId_IsIdempotentOnRuntime()
    {
        var runtime = CreateRuntime();
        var translator = new PatchCommandTranslator();
        var executor = new RuntimePatchCommandExecutor();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-e2e-2",
            42,
            new List<PatchOp>
            {
                new AddStoryBeatOp
                {
                    OpId = "op_story_dedup",
                    BeatId = "BEAT_DEDUP_1",
                    Text = "Season turns.",
                    DurationTicks = 10
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var commands = translator.Translate(response);
        executor.Execute(runtime, commands);
        // Second apply with same beatId should not throw and runtime state remains valid
        executor.Execute(runtime, commands);

        Assert.NotNull(runtime.LastDirectorActionStatus);
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
