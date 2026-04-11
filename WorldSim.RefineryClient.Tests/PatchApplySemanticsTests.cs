using System.Text.Json.Nodes;
using Xunit;
using WorldSimRefineryClient.Apply;
using WorldSim.Contracts.V1;
using WorldSim.Contracts.V2;

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

    [Fact]
    public void DirectorOps_AreAppliedAndDedupedByOpId()
    {
        var state = SimulationPatchState.CreateBaseline();
        var applier = new PatchApplier();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-4",
            987,
            new List<PatchOp>
            {
                new AddStoryBeatOp
                {
                    OpId = "op_story_1",
                    BeatId = "BEAT_SAMPLE_1",
                    Text = "A restless wind crosses the valley.",
                    DurationTicks = 24
                },
                new SetColonyDirectiveOp
                {
                    OpId = "op_nudge_1",
                    ColonyId = 0,
                    Directive = "PrioritizeFood",
                    DurationTicks = 18
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var first = applier.Apply(state, response);
        var second = applier.Apply(state, response);

        Assert.Equal(2, first.AppliedCount);
        Assert.Equal(2, second.DedupedCount);
        Assert.Contains("BEAT_SAMPLE_1", state.StoryBeatIds);
        Assert.Equal("PrioritizeFood", state.ColonyDirectives[0]);
    }

    [Fact]
    public void StagedPatchState_DoesNotMutateLiveStateUntilCommitted()
    {
        var liveState = SimulationPatchState.CreateBaseline();
        var stagedState = liveState.Clone();
        var applier = new PatchApplier();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-stage-1",
            123,
            new List<PatchOp>
            {
                new AddStoryBeatOp
                {
                    OpId = "op_story_stage_1",
                    BeatId = "BEAT_STAGE_1",
                    Text = "Staged beat",
                    DurationTicks = 20
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        applier.Apply(stagedState, response, new PatchApplyOptions(true));

        Assert.DoesNotContain("op_story_stage_1", liveState.AppliedOpIds);
        Assert.DoesNotContain("BEAT_STAGE_1", liveState.StoryBeatIds);

        liveState.CopyFrom(stagedState);

        Assert.Contains("op_story_stage_1", liveState.AppliedOpIds);
        Assert.Contains("BEAT_STAGE_1", liveState.StoryBeatIds);
    }

    [Fact]
    public void CampaignOps_AreApplied_WithDeterministicBookkeeping()
    {
        var state = SimulationPatchState.CreateBaseline();
        var applier = new PatchApplier();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-p4b-supported",
            321,
            new List<PatchOp>
            {
                new DeclareWarOp
                {
                    OpId = "op_war_1",
                    AttackerFactionId = 1,
                    DefenderFactionId = 2,
                    Reason = "border pressure"
                },
                new ProposeTreatyOp
                {
                    OpId = "op_treaty_1",
                    ProposerFactionId = 2,
                    ReceiverFactionId = 1,
                    TreatyKind = "ceasefire",
                    Note = "30-tick pause"
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var first = applier.Apply(state, response, new PatchApplyOptions(StrictMode: true));
        var second = applier.Apply(state, response, new PatchApplyOptions(StrictMode: true));

        Assert.Equal(2, first.AppliedCount);
        Assert.Equal(2, second.DedupedCount);
        Assert.Contains((1, 2), state.DeclaredWars);
        Assert.Contains((2, 1, "ceasefire"), state.TreatyProposals);
    }

    [Fact]
    public void CampaignWarBookkeeping_NormalizesFactionPair()
    {
        var state = SimulationPatchState.CreateBaseline();
        var applier = new PatchApplier();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-p4b-war-normalized",
            322,
            new List<PatchOp>
            {
                new DeclareWarOp
                {
                    OpId = "op_war_2",
                    AttackerFactionId = 3,
                    DefenderFactionId = 1
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        applier.Apply(state, response, new PatchApplyOptions(StrictMode: true));

        Assert.Contains((1, 3), state.DeclaredWars);
    }

    [Fact]
    public void CampaignTreatyBookkeeping_NormalizesTreatyKind_AndTreatsEquivalentKeysAsNoOp()
    {
        var state = SimulationPatchState.CreateBaseline();
        var applier = new PatchApplier();

        var firstResponse = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-p4b-treaty-normalized-1",
            400,
            new List<PatchOp>
            {
                new ProposeTreatyOp
                {
                    OpId = "op_treaty_norm_1",
                    ProposerFactionId = 2,
                    ReceiverFactionId = 1,
                    TreatyKind = " CeaseFire ",
                    Note = "spacing + case variant"
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var secondResponse = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-p4b-treaty-normalized-2",
            401,
            new List<PatchOp>
            {
                new ProposeTreatyOp
                {
                    OpId = "op_treaty_norm_2",
                    ProposerFactionId = 2,
                    ReceiverFactionId = 1,
                    TreatyKind = "ceasefire",
                    Note = "canonical"
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var first = applier.Apply(state, firstResponse, new PatchApplyOptions(StrictMode: true));
        var second = applier.Apply(state, secondResponse, new PatchApplyOptions(StrictMode: true));

        Assert.Equal(1, first.AppliedCount);
        Assert.Equal(1, second.NoOpCount);
        Assert.Contains((2, 1, "ceasefire"), state.TreatyProposals);
        Assert.Single(state.TreatyProposals);
    }

    [Fact]
    public void CampaignApplyPath_RejectsInvalidFactionId_Deterministically()
    {
        var state = SimulationPatchState.CreateBaseline();
        var applier = new PatchApplier();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-p4b-invalid-faction",
            402,
            new List<PatchOp>
            {
                new DeclareWarOp
                {
                    OpId = "op_war_invalid_1",
                    AttackerFactionId = 99,
                    DefenderFactionId = 1
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var ex = Assert.Throws<PatchApplyException>(() => applier.Apply(state, response, new PatchApplyOptions(StrictMode: true)));
        Assert.Contains("current valid faction ids: 0..3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CampaignApplyPath_RejectsSelfTargetedTreaty_Deterministically()
    {
        var state = SimulationPatchState.CreateBaseline();
        var applier = new PatchApplier();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-p4b-self-target-treaty",
            403,
            new List<PatchOp>
            {
                new ProposeTreatyOp
                {
                    OpId = "op_treaty_self_1",
                    ProposerFactionId = 2,
                    ReceiverFactionId = 2,
                    TreatyKind = "ceasefire"
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var ex = Assert.Throws<PatchApplyException>(() => applier.Apply(state, response, new PatchApplyOptions(StrictMode: true)));
        Assert.Contains("proposerFactionId != receiverFactionId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CampaignApplyPath_RejectsUnsupportedTreatyKind_Deterministically()
    {
        var state = SimulationPatchState.CreateBaseline();
        var applier = new PatchApplier();

        var response = new PatchResponse(
            PatchContract.SchemaVersion,
            "req-p4b-invalid-kind",
            404,
            new List<PatchOp>
            {
                new ProposeTreatyOp
                {
                    OpId = "op_treaty_invalid_1",
                    ProposerFactionId = 2,
                    ReceiverFactionId = 1,
                    TreatyKind = "alliance"
                }
            },
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        var ex = Assert.Throws<PatchApplyException>(() => applier.Apply(state, response, new PatchApplyOptions(StrictMode: true)));
        Assert.Contains("Unsupported proposeTreaty.treatyKind", ex.Message, StringComparison.Ordinal);
    }
}
