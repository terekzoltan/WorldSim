using System.Net;
using System.Text.Json;
using WorldSim.SmrLab.Artifacts;

namespace WorldSim.SmrLab.Tests;

public sealed class BrowserEndpointTests
{
    [Fact]
    public async Task LoopbackBrowser_ListsRealBundlesExposesTimelineComparisonAndReport()
    {
        const string old = "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-001";
        const string newer = "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-wave10-001";
        var store = new ArtifactStore(ArtifactStoreTests.Workspace(), [old, newer]);
        await using var app = LabHost.Create(store, port: 0);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(LabHost.BoundAddress(app)) };
        var page = await client.GetStringAsync("/");
        Assert.Contains("SMR Lab — artifact browser", page);
        Assert.Contains("/lab.js", page);
        var script = await client.GetStringAsync("/lab.js");
        Assert.Contains("textContent", script);
        Assert.DoesNotContain("innerHTML", script);
        using var bundleJson = JsonDocument.Parse(await client.GetStringAsync("/api/bundles"));
        var bundles = bundleJson.RootElement;
        Assert.Equal(27, bundles[0].GetProperty("runs").GetArrayLength());
        Assert.Equal(9, bundles[1].GetProperty("runs").GetArrayLength());
        var oldBundle = store.ListBundles()[0];
        var newBundle = store.ListBundles()[1];
        var oldRun = oldBundle.Runs.Single(r => r.Config == "standard-default" && r.Planner == "Goap" && r.Seed == "202");
        var newRun = newBundle.Runs.Single(r => r.Config == "default" && r.Planner == "Goap" && r.Seed == "101");
        using var runJson = JsonDocument.Parse(await client.GetStringAsync($"/api/bundles/{oldBundle.Id}/runs/{oldRun.Id}"));
        Assert.Equal(25, runJson.RootElement.GetProperty("sampleEvery").GetInt32());
        Assert.Equal(101, runJson.RootElement.GetProperty("samples")[1].GetProperty("food").GetDouble());
        using var newerJson = JsonDocument.Parse(await client.GetStringAsync($"/api/bundles/{newBundle.Id}/runs/{newRun.Id}"));
        Assert.Equal(25, newerJson.RootElement.GetProperty("sampleEvery").GetInt32());
        Assert.Equal(24, newerJson.RootElement.GetProperty("samples")[0].GetProperty("people").GetDouble());
        var qs = $"leftBundle={oldBundle.Id}&leftRun={oldRun.Id}&rightBundle={newBundle.Id}&rightRun={newRun.Id}";
        using var comparison = JsonDocument.Parse(await client.GetStringAsync("/api/compare?" + qs));
        Assert.Contains(comparison.RootElement.GetProperty("differences").EnumerateArray(), d => d.GetProperty("field").GetString() == "source");
        var report = await client.GetStringAsync("/api/report?" + qs);
        Assert.Contains("Remaining uncertainty", report);
        Assert.Contains(oldRun.Source, report);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/bundles/../../missing")).StatusCode);
        using var foreign = new HttpRequestMessage(HttpMethod.Get, "/api/bundles");
        foreign.Headers.TryAddWithoutValidation("Origin", "https://foreign.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(foreign)).StatusCode);
        using var rebound = new HttpRequestMessage(HttpMethod.Get, "/api/bundles");
        rebound.Headers.Host = "foreign.example";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(rebound)).StatusCode);
        await app.StopAsync();
    }
}
