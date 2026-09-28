using System.Diagnostics;
using System.Text.Json;
using WorldSim.Runtime;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;

namespace WorldSim.OffDeficitEvidence;

public static class Program
{
    static readonly string[] Keys = ["101/goap/off", "202/simple/off"];
    static readonly Configuration Configuration = new(64, 40, 24, 0.25f, "global_planner",
        Combat: true, Diplomacy: false, Siege: true, StoneBuildings: false,
        BirthRateMultiplier: 1f, MovementSpeedMultiplier: 1f, EmergencyRescuePolicy: "disabled",
        ReplenishmentOverridesApplied: false, RegrowthOverridesApplied: false, Measurement.TickCap);

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0].StartsWith("--worker=", StringComparison.Ordinal))
            {
                var key = args[0]["--worker=".Length..];
                if (!Keys.Contains(key)) throw new InvalidDataException("Unknown worker case.");
                // Authenticate against the live OS parent before touching source or simulation.
                using var parent = await WorkerAuthorization.ConnectChildAsync(key);
                var root = SourceAndStore.FindRoot();
                SourceAndStore.Verify(root);
                var run = Collect(root, key);
                Console.Out.Write(JsonSerializer.Serialize(run, Measurement.Json));
                return 0;
            }
            var requested = Measurement.ParseEntry(args);
            if (requested == "NOT_RUN")
            {
                Console.Out.WriteLine("NOT_RUN: explicit two-OFF-case --execute selector required; no simulation or evidence produced.");
                return 0;
            }
            var rootPath = SourceAndStore.FindRoot();
            var destination = SourceAndStore.ExactDestination(rootPath, requested);
            var source = SourceAndStore.Verify(rootPath);
            // Constructor/configuration reads are preflight only: no AdvanceTick here.
            foreach (var key in Keys)
                VerifyTickZero(CreateRuntime(rootPath, key));
            Console.Error.WriteLine($"PREFLIGHT_OK checkout={source.CheckoutCommit} tree={source.CheckoutTree} cases={Measurement.CaseSelection} H0=8 destination-unused");
            if (args[0] == "--preflight")
            {
                Console.Out.WriteLine("PREFLIGHT_ONLY: source/configuration and measured tick-zero state verified; no case advanced, no evidence written.");
                return 0;
            }

            var runs = await CollectAuthorizedAsync(destination, source, Configuration,
                (key, attemptId, maximum) => RunOnce(rootPath, key, attemptId, maximum));
            var payload = new Payload(Measurement.Schema, source, Configuration, runs,
                runs.Select(Measurement.Summarize).ToArray());
            // The attempt markers are already durable; evidence is published only after both valid cases.
            var manifest = SourceAndStore.WriteVerified(destination, payload);
            Console.Out.WriteLine($"EVIDENCE_OK path={destination} sha256={manifest.Sha256} bytes={manifest.ByteCount} "
                + $"stops={string.Join(',', manifest.StopTicks)} counts={string.Join(',', manifest.SampleCounts)}");
            foreach (var summary in payload.Summaries)
                Console.Out.WriteLine($"SUMMARY {summary.Key} decline={summary.FirstOneTickDecline} below8={summary.FirstBelowEight} "
                    + $"persistent={summary.PersistentToStopOnset} censored={summary.RightCensored} stop={summary.StopTick} "
                    + $"historicalComparison={summary.HistoricalFirstZeroComparison}");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"EVIDENCE_FAILED: {error.GetType().Name}: {error.Message}");
            return 1;
        }
    }

    // Internal test seam: a synthetic launcher exercises the exact attempt/order gate
    // without giving the production command line an alternate worker or output path.
    internal static async Task<Run[]> CollectAuthorizedAsync(string destination, SourceIdentity source,
        Configuration configuration, Func<string, string, TimeSpan, Task<Run>> launcher)
    {
        var claim = SourceAndStore.ReserveAttempt(destination, source, configuration);
        var total = Stopwatch.StartNew();
        var runs = new List<Run>(2);
        for (var index = 0; index < Keys.Length; index++)
        {
            var key = Keys[index];
            var remaining = TimeSpan.FromSeconds(240) - total.Elapsed;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException("Total collection exceeded 240 seconds before next case; no further case launched.");
            claim.AboutToLaunch(index, key);
            try
            {
                // One isolated, authenticated child per case; the deadline includes its handshake.
                var run = await launcher(key, claim.Id, remaining < TimeSpan.FromSeconds(120)
                    ? remaining : TimeSpan.FromSeconds(120));
                runs.Add(Measurement.ValidateRun(run));
                if (total.Elapsed >= TimeSpan.FromSeconds(240))
                    throw new TimeoutException("Total collection exceeded 240 seconds; evidence incomplete.");
                claim.Complete(index, run);
                Console.Error.WriteLine($"CASE_OK {key} stop={run.StopTick} reason={run.StopReason} elapsedMs={run.ElapsedMilliseconds}");
            }
            catch (Exception error)
            {
                claim.Fail(index, error);
                throw;
            }
        }
        return runs.ToArray();
    }

    static async Task<Run> RunOnce(string root, string key, string attemptId, TimeSpan maximum)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("--worker=" + key);
        var result = await WorkerAuthorization.RunSupervisedAsync(start, attemptId, key, maximum);
        if (result.ExitCode != 0)
            throw new InvalidDataException($"{key} failed/incomplete (exit {result.ExitCode}): {result.Error}");
        var run = JsonSerializer.Deserialize<Run>(result.Output, Measurement.Json)
            ?? throw new InvalidDataException($"{key} returned no complete run.");
        if (run.Key != key) throw new InvalidDataException("Wrong worker run key.");
        return run;
    }

    static SimulationRuntime CreateRuntime(string root, string key)
    {
        if (!Keys.Contains(key)) throw new InvalidDataException("Unapproved run key.");
        var runtime = new SimulationRuntime(width: Configuration.Width, height: Configuration.Height,
            initialPopulation: Configuration.InitialHumanPopulation,
            technologyFilePath: Path.Combine(root, "Tech", "technologies.json"),
            aiOptions: new RuntimeAiOptions
            {
                PlannerMode = key == Keys[0] ? NpcPlannerMode.Goap : NpcPlannerMode.Simple,
                PolicyMode = NpcPolicyMode.GlobalPlanner
            }, randomSeed: key == Keys[0] ? 101 : 202);
        runtime.ConfigureScenarioRunnerWorldOptions(enableCombatPrimitives: true, enableDiplomacy: false,
            enableSiege: true, enablePredatorHumanAttacks: false, stoneBuildingsEnabled: false,
            birthRateMultiplier: 1f, movementSpeedMultiplier: 1f,
            animalReplenishmentChancePerSecond: null, predatorReplenishmentChance: null,
            foodRegrowthMinSeconds: null, foodRegrowthJitterSeconds: null,
            emergencyRescuePolicy: EmergencyRescuePolicy.Disabled);
        return runtime;
    }

    static Raw Observe(SimulationRuntime runtime)
    {
        var telemetry = runtime.BuildScenarioRunTelemetrySnapshot();
        var eco = telemetry.Ecology;
        var counters = runtime.GetSnapshot().EcologyDetails.LifecycleCounters;
        if (telemetry.EnablePredatorHumanAttacks || eco.PredatorHumanHits != 0
            || telemetry.PredatorKillsByHumans != 0)
            throw new InvalidDataException("OFF mode control failed.");
        return new Raw(eco.Herbivores, eco.Predators, counters.HerbivoreBirths,
            eco.MeatFromHunt, counters.HerbivoreStarvations, eco.HerbivoreReplenishmentSpawns,
            eco.ActiveFoodNodes, eco.DepletedFoodNodes, eco.PlantFoodConsumedByAnimals,
            eco.EmergencyRescues, eco.EmergencyRescuePolicy);
    }

    static Raw VerifyTickZero(SimulationRuntime runtime)
    {
        var initial = Observe(runtime);
        if (initial.Herbivores != 8 || initial.Predators != 2 || initial.EmergencyRescues != 0
            || initial.HerbivoreReplenishment != 0 || initial.EmergencyRescuePolicy != "disabled")
            throw new InvalidDataException("Measured tick-zero state/configuration differs from the accepted OFF cases.");
        Measurement.SampleAt(0, initial, initial, null);
        return initial;
    }

    static Run Collect(string root, string key)
    {
        var watch = Stopwatch.StartNew();
        var runtime = CreateRuntime(root, key);
        var initial = VerifyTickZero(runtime);
        var samples = new List<Sample>(Measurement.TickCap + 1)
        {
            Measurement.SampleAt(0, initial, initial, null)
        };
        for (var tick = 1; tick <= Measurement.TickCap; tick++)
        {
            if (watch.ElapsedMilliseconds >= 120_000)
                throw new TimeoutException($"{key} elapsed more than 120 seconds; no retry.");
            runtime.AdvanceTick(Configuration.DtSeconds);
            var sample = Measurement.SampleAt(tick, Observe(runtime), initial, samples[^1]);
            samples.Add(sample);
            if (watch.ElapsedMilliseconds >= 120_000)
                throw new TimeoutException($"{key} elapsed more than 120 seconds; no retry.");
            if (sample.Herbivores == 0) break;
        }
        watch.Stop();
        var last = samples[^1];
        return Measurement.ValidateRun(new Run(key, samples.ToArray(),
            last.Herbivores == 0 ? "first_zero" : "tick_cap", last.Tick, watch.ElapsedMilliseconds));
    }
}
