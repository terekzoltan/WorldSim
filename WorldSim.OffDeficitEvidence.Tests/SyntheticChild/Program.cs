using WorldSim.OffDeficitEvidence;

// Test-owned workload: no Runtime construction, simulation, source lookup, or output path.
if (args.Length != 2) return 8;
try
{
    if (args[1] == "no-connect") await Task.Delay(Timeout.InfiniteTimeSpan);
    if (args[1] == "wrong-attempt")
        Environment.SetEnvironmentVariable(WorkerAuthorization.AttemptEnvironment, "synthetic-forgery");
    using var parent = await WorkerAuthorization.ConnectChildAsync(args[0],
        args[1] == "wrong-parent" ? int.MaxValue : null);
    if (args[1] == "wait") await Task.Delay(Timeout.InfiniteTimeSpan);
    if (args[1] == "fail") return 7;
    if (args[1] != "success" && args[1] != "wrong-parent") return 9;
    Console.Out.WriteLine("SYNTHETIC_OK");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"SYNTHETIC_REJECTED: {error.GetType().Name}");
    return 6;
}
