using System.Text.Json.Nodes;
using WorldSimRefineryClient.Apply;
using WorldSim.Contracts.V1;

namespace WorldSim.RefineryClient.Tests;

public sealed class PatchApplySemanticsTests
{
    [Fact]
    public void Idempotence_SameResponseAppliedTwice_KeepsSameState()
    {
        var state = SimulationPatchState.CreateBaseline();
        var applier = new PatchApplier();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-1",
            123,
            new List<PatchOp>
            {
                new AddWorldEventOp
                {
                    OpId = "op_event_1",
                    EventId = "WEATHER_1",
                    Type = "RAIN_BONUS",
                    Params = new JsonObject { ["severity"] = 1 },
                    DurationTicks = 10
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        applier.Apply(state, response);
        var firstHash = CanonicalStateSerializer.Sha256(state);
        applier.Apply(state, response);
        var secondHash = CanonicalStateSerializer.Sha256(state);

        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void Dedupe_TwoOpsSameOpId_AppliesOnce()
    {
        var state = SimulationPatchState.CreateBaseline();
        var applier = new PatchApplier();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-2",
            123,
            new List<PatchOp>
            {
                new AddWorldEventOp
                {
                    OpId = "op_same",
                    EventId = "E1",
                    Type = "RAIN_BONUS",
                    Params = new JsonObject(),
                    DurationTicks = 1
                },
                new AddWorldEventOp
                {
                    OpId = "op_same",
                    EventId = "E2",
                    Type = "DROUGHT",
                    Params = new JsonObject(),
                    DurationTicks = 1
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var result = applier.Apply(state, response);

        Assert.Equal(1, result.AppliedCount);
        Assert.Equal(1, result.DedupedCount);
        Assert.Single(state.EventIds);
    }

    [Fact]
    public void OrderDependency_TweakBeforeAdd_HardFails()
    {
        var state = SimulationPatchState.CreateBaseline();
        var applier = new PatchApplier();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-3",
            123,
            new List<PatchOp>
            {
                new TweakTechOp
                {
                    OpId = "op_tweak",
                    TechId = "agriculture",
                    FieldPath = "cost.research",
                    DeltaNumber = 10
                },
                new AddTechOp
                {
                    OpId = "op_add",
                    TechId = "agriculture",
                    PrereqTechIds = Array.Empty<string>(),
                    Cost = new JsonObject(),
                    Effects = new JsonObject()
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var ex = Assert.Throws<PatchApplyException>(() => applier.Apply(state, response));
        Assert.Contains("requires existing tech", ex.Message, StringComparison.Ordinal);
    }
}
