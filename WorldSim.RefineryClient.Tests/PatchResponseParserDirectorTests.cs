using Xunit;
using WorldSim.Contracts.V2;
using WorldSimRefineryClient.Apply;
using WorldSimRefineryClient.Serialization;

namespace WorldSim.RefineryClient.Tests;

public sealed class PatchResponseParserDirectorTests
{
    [Fact]
    public void Parse_DirectorOps_AreDeserializedToV2Types()
    {
        const string json = """
        {
          "schemaVersion": "v1",
          "requestId": "req-director",
          "seed": 321,
          "patch": [
            {
              "op": "addStoryBeat",
              "opId": "op_story_1",
              "beatId": "BEAT_SAMPLE_1",
              "text": "The harvest moon rises.",
              "durationTicks": 24,
              "causalChain": {
                "type": "causal_chain",
                "condition": {
                  "metric": "food_reserves_pct",
                  "operator": "lt",
                  "threshold": 35
                },
                "followUpBeat": {
                  "beatId": "BEAT_SAMPLE_1_FOLLOW",
                  "text": "Emergency rationing begins.",
                  "durationTicks": 12,
                  "severity": "major",
                  "effects": [
                    {
                      "type": "domain_modifier",
                      "domain": "morale",
                      "modifier": -0.08,
                      "durationTicks": 12
                    }
                  ]
                },
                "windowTicks": 20,
                "maxTriggers": 1
              }
            },
            {
              "op": "setColonyDirective",
              "opId": "op_nudge_1",
              "colonyId": 0,
              "directive": "PrioritizeFood",
              "durationTicks": 18
            }
          ],
          "explain": ["directorStage:mock"],
          "warnings": []
        }
        """;

        var parser = new PatchResponseParser();
        var response = parser.Parse(json, new PatchApplyOptions(true));

        var story = Assert.IsType<AddStoryBeatOp>(response.Patch[0]);
        Assert.Equal("BEAT_SAMPLE_1", story.BeatId);
        Assert.NotNull(story.CausalChain);
        Assert.Equal("causal_chain", story.CausalChain!.Type);
        Assert.Equal("food_reserves_pct", story.CausalChain.Condition.Metric);
        Assert.Equal("BEAT_SAMPLE_1_FOLLOW", story.CausalChain.FollowUpBeat.BeatId);
        Assert.Equal(1, story.CausalChain.MaxTriggers);
        var directive = Assert.IsType<SetColonyDirectiveOp>(response.Patch[1]);
        Assert.Equal("PrioritizeFood", directive.Directive);
    }

    [Fact]
    public void Parse_UnknownOp_FailsDeterministically()
    {
        const string json = """
        {
          "schemaVersion": "v1",
          "requestId": "req-bad-op",
          "seed": 1,
          "patch": [
            {
              "op": "doSomethingElse",
              "opId": "bad_1"
            }
          ],
          "explain": [],
          "warnings": []
        }
        """;

        var parser = new PatchResponseParser();
        var ex = Assert.Throws<PatchApplyException>(() => parser.Parse(json, new PatchApplyOptions(true)));
        Assert.Contains("Unknown op 'doSomethingElse'", ex.Message, StringComparison.Ordinal);
    }
}
