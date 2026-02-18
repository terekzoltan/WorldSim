using System.Text.Json.Nodes;
using WorldSimRefineryClient.Apply;
using WorldSim.Contracts.V1;
using WorldSimRefineryClient.Serialization;
using WorldSimRefineryClient.Service;

namespace WorldSim.RefineryClient.Tests;

public sealed class FixtureLiveParityTests
{
    [Fact]
    public async Task TechTreePatch_FixtureAndLive_ProduceSameCanonicalHash_WhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("REFINERY_PARITY_TEST"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var parser = new PatchResponseParser();
        var applier = new PatchApplier();

        var fixtureResponseJson = FixtureLoader.Read("responses/patch-tech-tree-v1.expected.json");
        var fixtureResponse = parser.Parse(fixtureResponseJson, new PatchApplyOptions(true));

        var fixtureState = SimulationPatchState.CreateBaseline();
        applier.Apply(fixtureState, fixtureResponse, new PatchApplyOptions(true));
        var fixtureHash = CanonicalStateSerializer.Sha256(fixtureState);

        var baseUrl = Environment.GetEnvironmentVariable("REFINERY_BASE_URL") ?? "http://localhost:8091";
        using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var serviceClient = new RefineryServiceClient(httpClient, parser);

        var liveRequest = new PatchRequest(
            PatchContract.SchemaVersion,
            "49e95c3f-8df6-45b5-8f47-7d2be30c23f3",
            123,
            42,
            PatchGoals.TechTreePatch,
            new JsonObject { ["world"] = "minimal" },
            null
        );

        var liveResponse = await serviceClient.GetPatchAsync(liveRequest, new PatchApplyOptions(true));
        var liveState = SimulationPatchState.CreateBaseline();
        applier.Apply(liveState, liveResponse, new PatchApplyOptions(true));
        var liveHash = CanonicalStateSerializer.Sha256(liveState);

        Assert.Equal(fixtureHash, liveHash);
    }
}
