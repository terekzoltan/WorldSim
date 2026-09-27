using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using WorldSim.SmrLab.Artifacts;

namespace WorldSim.SmrLab;

public static class LabHost
{
    public static WebApplication Create(ArtifactStore store, int port = 5217)
    {
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = store.WorkspaceRoot,
            WebRootPath = Path.Combine(store.WorkspaceRoot, "WorldSim.SmrLab", "wwwroot")
        });
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            // No endpoint mutates artifacts; still reject foreign origins and DNS-rebound hostnames.
            var host = context.Request.Host.Host;
            var actualPort = context.Connection.LocalPort;
            var origin = context.Request.Headers.Origin.ToString();
            if (host != "127.0.0.1" || (origin.Length != 0 && origin != $"http://127.0.0.1:{actualPort}"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            context.Response.Headers.ContentSecurityPolicy =
                "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'none'";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            await next();
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapGet("/api/roots", () => Results.Json(store.Roots));
        app.MapGet("/api/bundles", () => Results.Json(store.ListBundles()));
        app.MapGet("/api/bundles/{bundleId}", (string bundleId) =>
            store.GetBundle(bundleId) is { } bundle ? Results.Json(bundle) : Results.NotFound());
        app.MapGet("/api/bundles/{bundleId}/runs/{runId}", (string bundleId, string runId) =>
            store.GetRun(bundleId, runId) is { } run ? Results.Json(run) : Results.NotFound());
        app.MapGet("/api/compare", (string leftBundle, string leftRun, string rightBundle, string rightRun) =>
            store.Compare(leftBundle, leftRun, rightBundle, rightRun) is { } result
                ? Results.Json(result) : Results.NotFound());
        app.MapGet("/api/report", (string leftBundle, string leftRun, string rightBundle, string rightRun) =>
            store.Compare(leftBundle, leftRun, rightBundle, rightRun) is { } result
                ? Results.Text(store.Report(result), "text/markdown; charset=utf-8") : Results.NotFound());
        return app;
    }

    public static string BoundAddress(WebApplication app) => app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
}
