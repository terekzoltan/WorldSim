using WorldSim.Runtime.Profiles;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class LowCostProfileResolutionTests
{
    [Fact]
    public void ResolveForApp_DefaultsToDevLite()
    {
        var resolution = LowCostProfileResolver.ResolveForApp(null);

        Assert.Equal(LowCostProfileLane.DevLite, resolution.Effective);
        Assert.Equal("default", resolution.Source);
    }

    [Fact]
    public void ResolveForApp_ShowcaseEnv_IsAccepted()
    {
        var resolution = LowCostProfileResolver.ResolveForApp("showcase");

        Assert.Equal(LowCostProfileLane.Showcase, resolution.Effective);
        Assert.Equal("env", resolution.Source);
    }

    [Fact]
    public void ResolveForApp_HeadlessFallsBackToDevLite()
    {
        var resolution = LowCostProfileResolver.ResolveForApp("headless");

        Assert.Equal(LowCostProfileLane.DevLite, resolution.Effective);
        Assert.Equal("fallback_headless_not_supported", resolution.Source);
    }

    [Fact]
    public void ResolveForScenarioRunner_DefaultsToHeadless()
    {
        var resolution = LowCostProfileResolver.ResolveForScenarioRunner(null);

        Assert.Equal(LowCostProfileLane.Headless, resolution.Effective);
        Assert.Equal("default", resolution.Source);
    }

    [Fact]
    public void ResolveForScenarioRunner_DevLiteOverride_IsAccepted()
    {
        var resolution = LowCostProfileResolver.ResolveForScenarioRunner("devlite");

        Assert.Equal(LowCostProfileLane.DevLite, resolution.Effective);
        Assert.Equal("env", resolution.Source);
    }

    [Fact]
    public void ResolveForScenarioRunner_InvalidValue_FallsBackToHeadless()
    {
        var resolution = LowCostProfileResolver.ResolveForScenarioRunner("invalid-lane");

        Assert.Equal(LowCostProfileLane.Headless, resolution.Effective);
        Assert.Equal("fallback_invalid", resolution.Source);
    }
}
