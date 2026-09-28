using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WorldSim.OffDeficitEvidence;

// A locator in the child environment is not an authorization token. Both endpoints
// verify the OS identity of the other endpoint over a live, one-instance pipe.
internal static class WorkerAuthorization
{
    internal const string PipeEnvironment = "WORLDSIM_OFFDEFICIT_PARENT_PIPE";
    internal const string AttemptEnvironment = "WORLDSIM_OFFDEFICIT_ATTEMPT";

    internal static async Task<(string Output, string Error, int ExitCode)> RunSupervisedAsync(
        ProcessStartInfo start, string attemptId, string key, TimeSpan maximum,
        int? expectedClientPidForTest = null, bool disconnectForTest = false)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows process identity is required.");
        var name = "worldsim-e11off-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance);
        start.Environment[PipeEnvironment] = name;
        start.Environment[AttemptEnvironment] = attemptId;
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var deadline = new CancellationTokenSource(maximum);
        using var process = Process.Start(start) ?? throw new IOException("Cannot launch isolated child.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            var exit = process.WaitForExitAsync(deadline.Token);
            var authorize = AuthorizeAsync(pipe, process, expectedClientPidForTest ?? process.Id,
                attemptId, key, deadline.Token);
            var first = await Task.WhenAny(exit, authorize);
            if (first == exit && !authorize.IsCompletedSuccessfully)
            {
                await exit;
                throw new InvalidDataException("Child exited without parent authorization.");
            }
            await authorize;
            if (disconnectForTest) pipe.Dispose();
            await exit;
            return (await output, await errors, process.ExitCode);
        }
        catch (Exception error)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            if (error is OperationCanceledException)
                throw new TimeoutException("Supervised case/handshake exceeded its deadline; no retry.", error);
            throw;
        }
    }

    static async Task AuthorizeAsync(NamedPipeServerStream pipe, Process process, int expectedClientPid,
        string attemptId, string key, CancellationToken cancellation)
    {
        await pipe.WaitForConnectionAsync(cancellation);
        if (process.HasExited || !WindowsPipes.GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var actual)
            || actual != (uint)expectedClientPid)
            throw new InvalidDataException("Worker pipe client is not the launched child.");
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await writer.WriteLineAsync($"HELLO|{attemptId}|{key}|{challenge}".AsMemory(), cancellation);
        var echo = "ECHO|" + Digest("echo", attemptId, key, challenge);
        if (await reader.ReadLineAsync(cancellation) != echo || process.HasExited)
            throw new InvalidDataException("Worker challenge or case/attempt binding failed.");
        // This server has one instance, and the allow response is issued only once.
        await writer.WriteLineAsync(("ALLOW|" + Digest("allow", attemptId, key, challenge)).AsMemory(), cancellation);
    }

    internal static async Task<ChildSession> ConnectChildAsync(string key, int? expectedParentPidForTest = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows process identity is required.");
        var name = Environment.GetEnvironmentVariable(PipeEnvironment);
        var attemptId = Environment.GetEnvironmentVariable(AttemptEnvironment);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(attemptId)
            || !Measurement.CaseSelection.Split(',').Contains(key))
            throw new InvalidDataException("Live parent authorization is required before worker entry.");
        var actualParent = WindowsPipes.ParentProcessId();
        using var parent = Process.GetProcessById(actualParent);
        if (parent.HasExited) throw new InvalidDataException("Worker parent is not live.");
        var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await pipe.ConnectAsync(5_000, timeout.Token);
            if (!WindowsPipes.GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var serverPid)
                || serverPid != (uint)(expectedParentPidForTest ?? actualParent) || parent.HasExited)
                throw new InvalidDataException("Worker pipe server is not the live OS parent.");
            var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            var hello = (await reader.ReadLineAsync(timeout.Token))?.Split('|');
            if (hello is not { Length: 4 } || hello[0] != "HELLO" || hello[1] != attemptId
                || hello[2] != key || hello[3].Length != 64)
                throw new InvalidDataException("Worker handshake case/attempt mismatch.");
            await writer.WriteLineAsync(("ECHO|" + Digest("echo", attemptId, key, hello[3])).AsMemory(), timeout.Token);
            if (await reader.ReadLineAsync(timeout.Token) != "ALLOW|" + Digest("allow", attemptId, key, hello[3])
                || parent.HasExited)
                throw new InvalidDataException("Worker parent did not authorize this case.");
            var session = new ChildSession(pipe, reader, writer);
            session.MonitorParent();
            return session;
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    static string Digest(string prefix, string attempt, string key, string challenge) =>
        Measurement.Hash(Encoding.UTF8.GetBytes($"{prefix}|{attempt}|{key}|{challenge}"));

    internal static async Task MonitorParentAsync(Func<Task<string?>> read,
        Func<bool> isCompleted, Action terminateWorker)
    {
        try
        {
            // The parent sends nothing after authorization. EOF, an unexpected
            // message, or a failed read all mean the worker lost supervision.
            _ = await read();
        }
        catch (Exception)
        {
            // Read initiation and awaiting can both fail. Never fault the
            // detached task while an incomplete worker could keep ticking.
        }
        if (!isCompleted()) terminateWorker();
    }

    internal sealed class ChildSession(NamedPipeClientStream pipe, StreamReader reader, StreamWriter writer) : IDisposable
    {
        volatile bool completed;

        internal void MonitorParent()
        {
            _ = MonitorParentAsync(() => reader.ReadLineAsync(), () => completed,
                () => Environment.Exit(1));
        }

        public void Dispose()
        {
            completed = true;
            writer.Dispose();
            reader.Dispose();
            pipe.Dispose();
        }
    }
}

internal static class WindowsPipes
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ProcessEntry
    {
        public uint Size, Usage, ProcessId;
        public IntPtr HeapId;
        public uint ModuleId, ThreadCount, ParentId;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string FileName;
    }

    internal static int ParentProcessId()
    {
        var snapshot = CreateToolhelp32Snapshot(0x00000002, 0); // TH32CS_SNAPPROCESS
        if (snapshot == new IntPtr(-1)) throw new InvalidDataException("Cannot inspect OS worker parent.");
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            for (var found = Process32First(snapshot, ref entry); found; found = Process32Next(snapshot, ref entry))
                if (entry.ProcessId == (uint)Environment.ProcessId && entry.ParentId != 0)
                    return checked((int)entry.ParentId);
        }
        finally { CloseHandle(snapshot); }
        throw new InvalidDataException("OS worker parent identity is unavailable.");
    }
}
