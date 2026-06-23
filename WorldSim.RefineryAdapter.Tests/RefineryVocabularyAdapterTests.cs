using WorldSim.Contracts.V2;
using WorldSim.RefineryAdapter.Integration;

namespace WorldSim.RefineryAdapter.Tests;

public sealed class RefineryVocabularyAdapterTests
{
    [Fact]
    public void AdapterKeepsAutoOperatorLocalOutsideSharedOutputVocabulary()
    {
        Assert.DoesNotContain("auto", RefineryVocabulary.SharedOutputModes);

        var baseline = new RefineryRuntimeOptions(
            Mode: RefineryIntegrationMode.Fixture,
            Goal: DirectorGoals.SeasonDirectorCheckpoint,
            DirectorOutputMode: "off",
            FixtureResponsePath: "fixture.json",
            ServiceBaseUrl: "http://localhost:8091",
            StrictMode: true,
            RequestSeed: 123,
            LiveTimeoutMs: 1200,
            LiveRetryCount: 1,
            CircuitBreakerSeconds: 10,
            ApplyToWorld: false,
            MinTriggerIntervalMs: 500);

        var applied = RefineryRuntimeOptions.ApplyOperatorPreset(baseline, RefineryRuntimeOptions.ProfileLiveDirector);

        Assert.Equal("auto", applied.DirectorOutputMode);
    }
}
