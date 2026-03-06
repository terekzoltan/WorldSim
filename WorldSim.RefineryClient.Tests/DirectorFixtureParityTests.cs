using System.Text.Json.Nodes;
using Xunit;
using WorldSim.Contracts.V1;
using WorldSim.Contracts.V2;
using WorldSimRefineryClient.Apply;
using WorldSimRefineryClient.Serialization;
using WorldSimRefineryClient.Service;

namespace WorldSim.RefineryClient.Tests;

public sealed class DirectorFixtureParityTests
{
    [Fact]
    public void RecordedSnapshot_ExpectedResponse_Apply_ProducesExpectedState()
    {
        var parser = new PatchResponseParser();
        var applier = new PatchApplier();

        var requestJson = FixtureLoader.Read("requests/patch-season-director-v1.json");
        var requestRoot = JsonNode.Parse(requestJson)?.AsObject() ?? throw new InvalidOperationException("Invalid request fixture JSON.");
        var goal = requestRoot["goal"]?.GetValue<string>() ?? string.Empty;
        Assert.Equal(DirectorGoals.SeasonDirectorCheckpoint, goal);

        var responseJson = FixtureLoader.Read("responses/patch-season-director-v1.expected.json");
        var response = parser.Parse(responseJson, new PatchApplyOptions(true));

        var state = SimulationPatchState.CreateBaseline();
        var result = applier.Apply(state, response, new PatchApplyOptions(true));

        Assert.Equal(2, result.AppliedCount);
        Assert.Equal(0, result.DedupedCount);
        Assert.Equal(0, result.NoOpCount);
        Assert.Contains("BEAT_SAMPLE_1", state.StoryBeatIds);
        Assert.Equal("PrioritizeFood", state.ColonyDirectives[0]);
    }

    [Fact]
    public async Task SeasonDirector_FixtureAndLive_ProduceSameCanonicalHash_WhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("REFINERY_PARITY_TEST"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var parser = new PatchResponseParser();
        var applier = new PatchApplier();

        var fixtureResponseJson = FixtureLoader.Read("responses/patch-season-director-v1.expected.json");
        var fixtureResponse = parser.Parse(fixtureResponseJson, new PatchApplyOptions(true));

        var fixtureState = SimulationPatchState.CreateBaseline();
        applier.Apply(fixtureState, fixtureResponse, new PatchApplyOptions(true));
        var fixtureHash = CanonicalStateSerializer.Sha256(fixtureState);

        var baseUrl = Environment.GetEnvironmentVariable("REFINERY_BASE_URL") ?? "http://localhost:8091";
        using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var serviceClient = new RefineryServiceClient(httpClient, parser);

        var requestJson = FixtureLoader.Read("requests/patch-season-director-v1.json");
        var request = ParsePatchRequest(requestJson);

        var liveResponse = await serviceClient.GetPatchAsync(request, new PatchApplyOptions(true));
        var liveState = SimulationPatchState.CreateBaseline();
        applier.Apply(liveState, liveResponse, new PatchApplyOptions(true));
        var liveHash = CanonicalStateSerializer.Sha256(liveState);

        Assert.Equal(fixtureHash, liveHash);
    }

    private static PatchRequest ParsePatchRequest(string requestJson)
    {
        var root = JsonNode.Parse(requestJson)?.AsObject() ?? throw new InvalidOperationException("Invalid request fixture JSON.");

        var schemaVersion = root["schemaVersion"]?.GetValue<string>()
                            ?? throw new InvalidOperationException("schemaVersion missing from request fixture.");
        var requestId = root["requestId"]?.GetValue<string>()
                        ?? throw new InvalidOperationException("requestId missing from request fixture.");
        var seed = root["seed"]?.GetValue<long>()
                   ?? throw new InvalidOperationException("seed missing from request fixture.");
        var tick = root["tick"]?.GetValue<long>()
                   ?? throw new InvalidOperationException("tick missing from request fixture.");
        var goal = root["goal"]?.GetValue<string>()
                   ?? throw new InvalidOperationException("goal missing from request fixture.");
        var snapshot = root["snapshot"]?.AsObject()
                       ?? throw new InvalidOperationException("snapshot missing from request fixture.");
        var constraints = root["constraints"] as JsonObject;

        return new PatchRequest(schemaVersion, requestId, seed, tick, goal, snapshot, constraints);
    }
}
