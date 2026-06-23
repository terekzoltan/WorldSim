using WorldSim.Contracts.V2;
using WorldSimRefineryClient.Apply;
using WorldSimRefineryClient.Serialization;

namespace WorldSim.RefineryClient.Tests;

public sealed class RefineryVocabularyTests
{
    [Fact]
    public void SharedOutputModes_ExcludeAdapterLocalAuto()
    {
        Assert.Equal(["both", "story_only", "nudge_only", "off"], RefineryVocabulary.SharedOutputModes);
        Assert.DoesNotContain("auto", RefineryVocabulary.SharedOutputModes);
    }

    [Fact]
    public void SymbolicVocabulary_KeepsExpectedStableValues()
    {
        Assert.Equal(["minor", "major", "epic"], RefineryVocabulary.Severities);
        Assert.Equal(["food", "morale", "economy", "military", "research"], RefineryVocabulary.Domains);
        Assert.Equal(["farming", "gathering", "crafting", "building", "social", "military", "research", "rest"], RefineryVocabulary.GoalCategories);
        Assert.Equal(["ceasefire", "peace_talks"], RefineryVocabulary.TreatyKinds);
    }

    [Fact]
    public void Parser_UsesSharedTreatyVocabularyWithoutChangingDiagnostics()
    {
        const string json = """
        {
          "schemaVersion": "v1",
          "requestId": "req-bad-treaty-vocabulary",
          "seed": 46,
          "patch": [
            {
              "op": "proposeTreaty",
              "opId": "op_treaty_bad_vocabulary",
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
        Assert.Contains("Unsupported proposeTreaty.treatyKind 'alliance'. Expected one of: ceasefire, peace_talks.", ex.Message, StringComparison.Ordinal);
    }
}
