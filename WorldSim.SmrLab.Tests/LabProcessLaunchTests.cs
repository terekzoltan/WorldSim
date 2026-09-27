using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit.Sdk;

namespace WorldSim.SmrLab.Tests;

public sealed class LabProcessLaunchTests
{
    private const string Old = "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-001";
    private const string New = "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-wave10-001";

    [Fact]
    public async Task DirectDll_FromOwnCheckout_ServesBothRealBundlesWhileChildIsAlive()
    {
        var workspace = ArtifactStoreTests.Workspace();
        var port = FreeLoopbackPort();
        await using var child = StartLab(workspace, port);

        try
        {
            // A live child plus HTTP is insufficient: another listener could answer in the port race.
            await child.WaitForOwnListenerAsync(TimeSpan.FromSeconds(20));
            using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}/"),
                Timeout = TimeSpan.FromSeconds(2)
            };
            await WaitForPage(client, child);
            child.AssertAlive("HTTP 200");
            using var response = await client.GetAsync("api/bundles");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var bundles = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(2, bundles.RootElement.GetArrayLength());
            Assert.Equal(27, bundles.RootElement[0].GetProperty("runs").GetArrayLength());
            Assert.Equal(9, bundles.RootElement[1].GetProperty("runs").GetArrayLength());
            child.AssertAlive("27/9 API response");
        }
        catch (Exception e)
        {
            throw new XunitException($"Owned Lab child failed on port {port}: {e.Message}; {child.Diagnostics}");
        }
    }

    [Fact]
    public async Task OccupiedPort_CannotSatisfyOwnedChildListenerReadiness()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            await using var child = StartLab(ArtifactStoreTests.Workspace(), port);
            var failure = await Assert.ThrowsAsync<XunitException>(() =>
                child.WaitForOwnListenerAsync(TimeSpan.FromSeconds(10)));
            Assert.Contains($"did not bind loopback port {port}", failure.Message);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await child.Process.WaitForExitAsync(deadline.Token);
            Assert.NotEqual(0, child.Process.ExitCode);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task DirectDll_FromForeignCwd_ExitsWithCheckoutGuardFailure()
    {
        using var fixture = new ArtifactStoreTests.SyntheticFixture();
        var foreign = fixture.CreateDirectory("foreign-cwd");
        var port = FreeLoopbackPort();
        await using var child = StartLab(foreign, port);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await child.Process.WaitForExitAsync(deadline.Token);
            await child.DrainOutputAsync();
            Assert.NotEqual(0, child.Process.ExitCode);
            Assert.Contains("SMR Lab must launch from its own inferred checkout", child.Diagnostics);
        }
        catch (Exception e)
        {
            throw new XunitException($"Foreign-CWD child did not reject its own launch: {e.Message}; {child.Diagnostics}");
        }
    }

    private static async Task WaitForPage(HttpClient client, OwnedLabProcess child)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!deadline.IsCancellationRequested)
        {
            child.AssertAlive("waiting for HTTP 200");
            try
            {
                using var response = await client.GetAsync("/", deadline.Token);
                child.AssertAlive("HTTP page response");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var html = await response.Content.ReadAsStringAsync(deadline.Token);
                    Assert.Contains("SMR Lab — artifact browser", html);
                    return;
                }
            }
            catch (HttpRequestException) when (!deadline.IsCancellationRequested) { }
            catch (TaskCanceledException) when (!deadline.IsCancellationRequested) { }
            await Task.Delay(100, deadline.Token);
        }
        throw new XunitException("Timed out waiting for the owned Lab process to serve HTTP 200.");
    }

    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static OwnedLabProcess StartLab(string cwd, int port)
    {
        var workspace = ArtifactStoreTests.Workspace();
        var target = new DirectoryInfo(AppContext.BaseDirectory);
        var dll = Path.Combine(workspace, "WorldSim.SmrLab", "bin", target.Parent!.Name, target.Name, "WorldSim.SmrLab.dll");
        Assert.True(File.Exists(dll), $"Build the Lab DLL before launching: {dll}");
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { dll, "--root", Old, "--root", New, "--port", port.ToString() })
            info.ArgumentList.Add(argument);
        var process = Process.Start(info) ?? throw new XunitException("Lab child process did not start.");
        return new OwnedLabProcess(process, port);
    }

    private sealed class OwnedLabProcess : IAsyncDisposable
    {
        private readonly StringBuilder _output = new();
        private readonly object _lock = new();
        private readonly Task _stdout;
        private readonly Task _stderr;
        private readonly TaskCompletionSource<bool> _listenerReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _port;

        public Process Process { get; }
        public string Diagnostics
        {
            get
            {
                lock (_lock) return $"pid={Process.Id} exit={(Process.HasExited ? Process.ExitCode.ToString() : "running")} output={_output}";
            }
        }

        public OwnedLabProcess(Process process, int port)
        {
            Process = process;
            _port = port;
            _stdout = Drain(process.StandardOutput, stdout: true);
            _stderr = Drain(process.StandardError, stdout: false);
        }

        public void AssertAlive(string point) => Assert.True(!Process.HasExited, $"Lab child exited at {point}: {Diagnostics}");

        public async Task WaitForOwnListenerAsync(TimeSpan timeout)
        {
            // Only this Process's stdout can complete this signal. ASP.NET emits the line after bind.
            var completed = await Task.WhenAny(_listenerReady.Task, Process.WaitForExitAsync(), Task.Delay(timeout));
            if (completed != _listenerReady.Task || !_listenerReady.Task.IsCompletedSuccessfully || Process.HasExited)
                throw new XunitException($"Owned child pid={Process.Id} did not bind loopback port {_port} within {timeout}: {Diagnostics}");
            AssertAlive("owned listener readiness");
        }

        private async Task Drain(StreamReader reader, bool stdout)
        {
            var buffer = new char[512];
            // Keep complete stdout lines for the readiness matcher, independent of capped diagnostics.
            var line = new StringBuilder();
            var overlong = false;
            var expected = $"Now listening on: http://127.0.0.1:{_port}";
            int count;
            while ((count = await reader.ReadAsync(buffer)) > 0)
            {
                lock (_lock)
                {
                    var remaining = 4096 - _output.Length;
                    if (remaining > 0) _output.Append(buffer, 0, Math.Min(count, remaining));
                }
                if (!stdout) continue;
                for (var i = 0; i < count; i++)
                {
                    if (buffer[i] == '\n')
                    {
                        if (!overlong && line.ToString().Trim() == expected)
                            _listenerReady.TrySetResult(true);
                        line.Clear();
                        overlong = false;
                    }
                    else if (line.Length < 512) line.Append(buffer[i]);
                    else overlong = true;
                }
            }
        }

        public async Task DrainOutputAsync() => await Task.WhenAll(_stdout, _stderr).WaitAsync(TimeSpan.FromSeconds(5));

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!Process.HasExited) Process.Kill(entireProcessTree: true);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await Process.WaitForExitAsync(deadline.Token);
                await DrainOutputAsync();
            }
            finally { Process.Dispose(); }
        }
    }
}
