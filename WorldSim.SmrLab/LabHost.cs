using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using WorldSim.SmrLab.Artifacts;
using WorldSim.SmrLab.ManualRuns;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorldSim.SmrLab;

public static class LabHost
{
    public static WebApplication Create(ArtifactStore store, int port = 5217, ManualRunManager? manual = null)
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
        var manualToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        app.Use(async (context, next) =>
        {
            // Read endpoints retain A's origin rule. The manual POST additionally requires
            // an exact Origin and an unpredictable per-host session token before reservation.
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
        app.MapGet("/api/manual/options", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(new
            {
                enabled = manual is not null, token = manual is null ? null : manualToken,
                presets = ManualRunContract.Presets, planners = ManualRunContract.Planners,
                modes = ManualRunContract.Modes, timeoutSeconds = ManualRunContract.TimeoutSeconds,
                blocker = manual?.Blocker, runnerHash = manual?.RunnerHash
            });
        });
        app.MapGet("/api/manual/jobs", () => manual is null ? Results.NotFound() : Results.Json(manual.List()));
        app.MapGet("/api/manual/jobs/{id}", (string id) => manual?.Get(id) is { } job
            ? Results.Json(job) : Results.NotFound());
        app.MapPost("/api/manual/jobs", async (HttpContext context) =>
        {
            if (manual is null) return Results.NotFound();
            var origin = context.Request.Headers.Origin.ToString();
            var supplied = context.Request.Headers["X-Lab-Token"].ToString();
            if (origin != $"http://127.0.0.1:{context.Connection.LocalPort}" ||
                supplied.Length != manualToken.Length ||
                !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied), Encoding.ASCII.GetBytes(manualToken)))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            using var buffer = new MemoryStream();
            var chunk = new byte[1024];
            int count;
            while ((count = await context.Request.Body.ReadAsync(chunk)) > 0)
            {
                if (buffer.Length + count > 4096) return Results.BadRequest("Manual request exceeds 4 KiB.");
                buffer.Write(chunk, 0, count);
            }
            try
            {
                using var json = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
                if (json.RootElement.ValueKind != JsonValueKind.Object) return Results.BadRequest("Expected one typed selection.");
                var fields = new HashSet<string>(StringComparer.Ordinal)
                    { "intentId", "profile", "preset", "seed", "planner", "ticks", "mode" };
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in json.RootElement.EnumerateObject())
                    if (!fields.Contains(property.Name) || !seen.Add(property.Name))
                        return Results.BadRequest("Unknown or duplicate request field.");
                if (seen.Count != fields.Count) return Results.BadRequest("Missing manual-run setting.");
                var request = JsonSerializer.Deserialize<ManualRunRequest>(buffer.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (request is null) return Results.BadRequest("Invalid manual-run selection.");
                var result = manual.Start(request);
                return result.Error is { } error ? Results.Problem(error, statusCode: result.Status)
                    : Results.Json(result.Job, statusCode: result.Status);
            }
            catch (Exception ex) when (ex is JsonException or FormatException)
            {
                return Results.BadRequest("Malformed manual-run selection.");
            }
        });
        return app;
    }

    public static string BoundAddress(WebApplication app) => app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
}
