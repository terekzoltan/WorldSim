using Xunit;
using WorldSim.Contracts.V2;
using WorldSimRefineryClient.Apply;
using WorldSimRefineryClient.Serialization;

namespace WorldSim.RefineryClient.Tests;

public sealed class PatchResponseParserCampaignTests
{
    [Fact]
    public void Parse_CampaignOps_AreDeserializedToV2Types()
    {
        const string json = """
        {
          "schemaVersion": "v1",
          "requestId": "req-campaign",
          "seed": 44,
          "patch": [
            {
              "op": "declareWar",
              "opId": "op_war_1",
              "attackerFactionId": 1,
              "defenderFactionId": 2,
              "reason": "border skirmish"
            },
            {
              "op": "proposeTreaty",
              "opId": "op_treaty_1",
              "proposerFactionId": 2,
              "receiverFactionId": 1,
              "treatyKind": "ceasefire",
              "note": "30 tick cooldown"
            }
          ],
          "explain": [],
          "warnings": []
        }
        """;

        var parser = new PatchResponseParser();
        var response = parser.Parse(json, new PatchApplyOptions(true));

        var declareWar = Assert.IsType<DeclareWarOp>(response.Patch[0]);
        Assert.Equal(1, declareWar.AttackerFactionId);
        Assert.Equal(2, declareWar.DefenderFactionId);

        var treaty = Assert.IsType<ProposeTreatyOp>(response.Patch[1]);
        Assert.Equal("ceasefire", treaty.TreatyKind);
        Assert.Equal(2, treaty.ProposerFactionId);
        Assert.Equal(1, treaty.ReceiverFactionId);
    }

    [Fact]
    public void Parse_DeclareWarSelfTarget_FailsDeterministically()
    {
        const string json = """
        {
          "schemaVersion": "v1",
          "requestId": "req-bad-war",
          "seed": 45,
          "patch": [
            {
              "op": "declareWar",
              "opId": "op_war_bad_1",
              "attackerFactionId": 1,
              "defenderFactionId": 1
            }
          ],
          "explain": [],
          "warnings": []
        }
        """;

        var parser = new PatchResponseParser();
        var ex = Assert.Throws<PatchApplyException>(() => parser.Parse(json, new PatchApplyOptions(true)));
        Assert.Contains("attackerFactionId != defenderFactionId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_DeclareWarNegativeFactionId_FailsDeterministically()
    {
        const string json = """
        {
          "schemaVersion": "v1",
          "requestId": "req-bad-war-negative",
          "seed": 45,
          "patch": [
            {
              "op": "declareWar",
              "opId": "op_war_bad_2",
              "attackerFactionId": -1,
              "defenderFactionId": 1
            }
          ],
          "explain": [],
          "warnings": []
        }
        """;

        var parser = new PatchResponseParser();
        var ex = Assert.Throws<PatchApplyException>(() => parser.Parse(json, new PatchApplyOptions(true)));
        Assert.Contains("declareWar.attackerFactionId must be >= 0", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ProposeTreatyInvalidKind_FailsDeterministically()
    {
        const string json = """
        {
          "schemaVersion": "v1",
          "requestId": "req-bad-treaty",
          "seed": 46,
          "patch": [
            {
              "op": "proposeTreaty",
              "opId": "op_treaty_bad_1",
              "proposerFactionId": 1,
              "receiverFactionId": 2,
              "treatyKind": "alliance"
            }
          ],
          "explain": [],
          "warnings": []
        }
        """;

        var parser = new PatchResponseParser();
        var ex = Assert.Throws<PatchApplyException>(() => parser.Parse(json, new PatchApplyOptions(true)));
        Assert.Contains("Unsupported proposeTreaty.treatyKind", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ProposeTreatyNegativeFactionId_FailsDeterministically()
    {
        const string json = """
        {
          "schemaVersion": "v1",
          "requestId": "req-bad-treaty-negative",
          "seed": 47,
          "patch": [
            {
              "op": "proposeTreaty",
              "opId": "op_treaty_bad_2",
              "proposerFactionId": 1,
              "receiverFactionId": -2,
              "treatyKind": "ceasefire"
            }
          ],
          "explain": [],
          "warnings": []
        }
        """;

        var parser = new PatchResponseParser();
        var ex = Assert.Throws<PatchApplyException>(() => parser.Parse(json, new PatchApplyOptions(true)));
        Assert.Contains("proposeTreaty.receiverFactionId must be >= 0", ex.Message, StringComparison.Ordinal);
    }
}
