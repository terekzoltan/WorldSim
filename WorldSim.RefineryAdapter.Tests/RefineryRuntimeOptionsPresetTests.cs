using WorldSim.Contracts.V2;
using WorldSim.RefineryAdapter.Integration;
using Xunit;

namespace WorldSim.RefineryAdapter.Tests;

public sealed class RefineryRuntimeOptionsPresetTests
{
    [Fact]
    public void NormalizeOperatorPresetName_AcceptsUnderscoreAndHyphenForms()
    {
        Assert.Equal(RefineryRuntimeOptions.ProfileFixtureSmoke, RefineryRuntimeOptions.NormalizeOperatorPresetName("fixture_smoke"));
        Assert.Equal(RefineryRuntimeOptions.ProfileFixtureSmoke, RefineryRuntimeOptions.NormalizeOperatorPresetName("fixture-smoke"));
        Assert.Equal(RefineryRuntimeOptions.ProfileLiveMock, RefineryRuntimeOptions.NormalizeOperatorPresetName("live_mock"));
        Assert.Equal(RefineryRuntimeOptions.ProfileLiveDirector, RefineryRuntimeOptions.NormalizeOperatorPresetName("live-director"));
    }

    [Fact]
    public void ApplyOperatorPreset_LiveDirector_EnforcesOperationalDefaults()
    {
        var baseline = new RefineryRuntimeOptions(
            Mode: RefineryIntegrationMode.Fixture,
            Goal: "TECH_TREE_PATCH",
            DirectorOutputMode: "off",
            FixtureResponsePath: "fixture.json",
            ServiceBaseUrl: "http://localhost:8091",
            StrictMode: true,
            RequestSeed: 123,
            LiveTimeoutMs: 1200,
            LiveRetryCount: 1,
            CircuitBreakerSeconds: 10,
            ApplyToWorld: false,
            MinTriggerIntervalMs: 500
        );

        var applied = RefineryRuntimeOptions.ApplyOperatorPreset(baseline, RefineryRuntimeOptions.ProfileLiveDirector);

        Assert.Equal(RefineryIntegrationMode.Live, applied.Mode);
        Assert.Equal(DirectorGoals.SeasonDirectorCheckpoint, applied.Goal);
        Assert.Equal("auto", applied.DirectorOutputMode);
        Assert.True(applied.ApplyToWorld);
        Assert.Equal(0, applied.LiveRetryCount);
        Assert.True(applied.LiveTimeoutMs >= 12000);
        Assert.Equal(RefineryRuntimeOptions.ProfileLiveDirector, applied.OperatorProfileName);
    }

    [Fact]
    public void ApplyOperatorPreset_UsesBaselineValuesForRoundTrip()
    {
        var baseline = new RefineryRuntimeOptions(
            Mode: RefineryIntegrationMode.Fixture,
            Goal: DirectorGoals.SeasonDirectorCheckpoint,
            DirectorOutputMode: "auto",
            FixtureResponsePath: "fixture.json",
            ServiceBaseUrl: "http://localhost:8091",
            StrictMode: true,
            RequestSeed: 123,
            LiveTimeoutMs: 2400,
            LiveRetryCount: 2,
            CircuitBreakerSeconds: 10,
            ApplyToWorld: false,
            MinTriggerIntervalMs: 500);

        var liveDirector = RefineryRuntimeOptions.ApplyOperatorPreset(baseline, RefineryRuntimeOptions.ProfileLiveDirector);
        var liveMock = RefineryRuntimeOptions.ApplyOperatorPreset(baseline, RefineryRuntimeOptions.ProfileLiveMock);

        Assert.True(liveDirector.LiveTimeoutMs >= 12000);
        Assert.Equal(0, liveDirector.LiveRetryCount);

        Assert.Equal(2400, liveMock.LiveTimeoutMs);
        Assert.Equal(2, liveMock.LiveRetryCount);
        Assert.Equal(RefineryRuntimeOptions.ProfileLiveMock, liveMock.OperatorProfileName);
    }

    [Fact]
    public void NextOperatorPresetName_CyclesThroughKnownPresetOrder()
    {
        Assert.Equal(RefineryRuntimeOptions.ProfileLiveMock, RefineryRuntimeOptions.NextOperatorPresetName(RefineryRuntimeOptions.ProfileFixtureSmoke));
        Assert.Equal(RefineryRuntimeOptions.ProfileLiveDirector, RefineryRuntimeOptions.NextOperatorPresetName(RefineryRuntimeOptions.ProfileLiveMock));
        Assert.Equal(RefineryRuntimeOptions.ProfileFixtureSmoke, RefineryRuntimeOptions.NextOperatorPresetName(RefineryRuntimeOptions.ProfileLiveDirector));
    }
}
