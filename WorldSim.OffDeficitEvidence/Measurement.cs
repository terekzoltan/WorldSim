using System.Security.Cryptography;
using System.Text.Json;

namespace WorldSim.OffDeficitEvidence;

public static class Measurement
{
    public const string Schema = "e11h_off_deficit_per_tick_v1";
    public const string CaseSelection = "101/goap/off,202/simple/off";
    public const int TickCap = 888;
    public const string PrivateDirectory = ".artifacts/smr/wave11-e11-h-step5c4-off-deficit-measurement-001";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string ParseEntry(string[] args)
    {
        if (args.Length == 0)
            return "NOT_RUN";
        if (args.Length == 4 && args[0] == "--execute"
            && args[1] == $"--cases={CaseSelection}"
            && args[2] == $"--tick-cap={TickCap}"
            && args[3] == "--out")
            throw new ArgumentException("Missing output path.");
        if (args.Length != 5 || args[0] is not ("--execute" or "--preflight")
            || args[1] != $"--cases={CaseSelection}"
            || args[2] != $"--tick-cap={TickCap}"
            || args[3] != "--out" || string.IsNullOrWhiteSpace(args[4]))
            throw new ArgumentException("Only --execute or --preflight with exact --cases=101/goap/off,202/simple/off --tick-cap=888 --out PRIVATE_PATH is supported.");
        return args[4];
    }

    public static Sample SampleAt(int tick, Raw raw, Raw initial, Sample? previous)
    {
        if (tick == 0 && previous is not null || tick > 0 && (previous is null || previous.Tick != tick - 1))
            throw new InvalidDataException("Nonconsecutive sample tick.");
        if (initial.Herbivores != 8 || raw.Herbivores < 0 || raw.Predators < 0
            || raw.EmergencyRescuePolicy != "disabled" || raw.EmergencyRescues != initial.EmergencyRescues
            || raw.HerbivoreReplenishment < initial.HerbivoreReplenishment)
            throw new InvalidDataException("Invalid initial population, rescue policy, or observed population/counters.");
        var births = checked(raw.Births - initial.Births);
        var captures = checked(raw.Captures - initial.Captures);
        var starvations = checked(raw.Starvations - initial.Starvations);
        var replenishment = checked(raw.HerbivoreReplenishment - initial.HerbivoreReplenishment);
        var foodConsumed = checked(raw.PlantFoodConsumed - initial.PlantFoodConsumed);
        if (births < 0 || captures < 0 || starvations < 0 || replenishment < 0 || foodConsumed < 0
            || raw.ActiveFoodNodes < 0 || raw.DepletedFoodNodes < 0)
            throw new InvalidDataException("Negative cumulative or food counter.");
        var residual = checked(initial.Herbivores + births + replenishment - raw.Herbivores - captures - starvations);
        var deficit = checked(captures + starvations + residual - births - replenishment);
        if (residual < 0 || deficit != initial.Herbivores - raw.Herbivores)
            throw new InvalidDataException("Negative residual or population accounting mismatch.");
        if (tick == 0 && (births != 0 || captures != 0 || starvations != 0 || replenishment != 0 || residual != 0))
            throw new InvalidDataException("Tick-zero counters must be zero-relative.");
        if (previous is not null && (births < previous.Births || captures < previous.Captures
            || starvations < previous.Starvations || replenishment < previous.Replenishment
            || foodConsumed < previous.PlantFoodConsumed))
            throw new InvalidDataException("Cumulative counter decreased.");
        return new Sample(tick, raw.Herbivores, raw.Predators, births, captures, starvations,
            replenishment, residual, deficit, raw.ActiveFoodNodes, raw.DepletedFoodNodes, foodConsumed,
            raw.EmergencyRescues - initial.EmergencyRescues);
    }

    public static Run ValidateRun(Run run)
    {
        if (run.Key is not ("101/goap/off" or "202/simple/off") || run.Samples.Length is < 1 or > TickCap + 1
            || run.Samples[0].Tick != 0 || run.Samples[0].Herbivores != 8 || run.Samples[0].Residual != 0)
            throw new InvalidDataException("Invalid run identity or initial state.");
        for (var i = 0; i < run.Samples.Length; i++)
        {
            var sample = run.Samples[i];
            if (sample.Tick != i || sample.Residual < 0 || sample.Herbivores != 8 - sample.Deficit
                || sample.Deficit != sample.Captures + sample.Starvations + sample.Residual
                    - sample.Births - sample.Replenishment
                || sample.EmergencyRescues != 0 || sample.Replenishment != 0
                || (i > 0 && (run.Samples[i - 1].Herbivores == 0
                    || sample.Births < run.Samples[i - 1].Births
                    || sample.Captures < run.Samples[i - 1].Captures
                    || sample.Starvations < run.Samples[i - 1].Starvations
                    || sample.PlantFoodConsumed < run.Samples[i - 1].PlantFoodConsumed)))
                throw new InvalidDataException("Incomplete or inconsistent timeline.");
        }
        var last = run.Samples[^1];
        if (run.StopReason != (last.Herbivores == 0 ? "first_zero" : "tick_cap")
            || last.Herbivores > 0 && last.Tick != TickCap || last.Tick != run.StopTick
            || run.ElapsedMilliseconds < 0 || run.ElapsedMilliseconds > 120_000)
            throw new InvalidDataException("Incomplete run or case time limit exceeded.");
        return run;
    }

    public static Summary Summarize(Run run)
    {
        ValidateRun(run);
        var samples = run.Samples;
        int? firstDecline = null, firstBelow = null, persistent = null;
        var intervals = new List<Interval>();
        int? start = null;
        for (var i = 1; i < samples.Length; i++)
        {
            if (firstDecline is null && samples[i].Herbivores < samples[i - 1].Herbivores)
                firstDecline = i;
            if (samples[i].Deficit > 0)
            {
                firstBelow ??= i;
                start ??= i;
            }
            if (start.HasValue && (samples[i].Deficit <= 0 || i == samples.Length - 1))
            {
                var end = samples[i].Deficit > 0 ? i : i - 1;
                var before = samples[start.Value - 1];
                var last = samples[end];
                intervals.Add(new Interval(start.Value, end, last.Births - before.Births,
                    last.Captures - before.Captures, last.Starvations - before.Starvations,
                    last.Replenishment - before.Replenishment, last.Residual - before.Residual,
                    last.ActiveFoodNodes - before.ActiveFoodNodes,
                    last.DepletedFoodNodes - before.DepletedFoodNodes,
                    last.PlantFoodConsumed - before.PlantFoodConsumed));
                start = null;
            }
        }
        if (run.StopReason == "first_zero" && intervals.Count > 0 && intervals[^1].EndTick == run.StopTick)
            persistent = intervals[^1].StartTick;
        return new Summary(run.Key, firstDecline, firstBelow, persistent,
            run.StopReason != "first_zero", run.StopTick, run.Key == "101/goap/off" ? 780 : 888,
            intervals.ToArray());
    }

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

public sealed record Raw(int Herbivores, int Predators, int Births, int Captures, int Starvations,
    int HerbivoreReplenishment, int ActiveFoodNodes, int DepletedFoodNodes, int PlantFoodConsumed,
    int EmergencyRescues, string EmergencyRescuePolicy);
public sealed record Sample(int Tick, int Herbivores, int Predators, int Births, int Captures,
    int Starvations, int Replenishment, int Residual, int Deficit, int ActiveFoodNodes,
    int DepletedFoodNodes, int PlantFoodConsumed, int EmergencyRescues);
public sealed record Run(string Key, Sample[] Samples, string StopReason, int StopTick, long ElapsedMilliseconds);
public sealed record Interval(int StartTick, int EndTick, int Births, int Captures, int Starvations,
    int Replenishment, int ResidualNet, int ActiveFoodNodesDelta, int DepletedFoodNodesDelta,
    int PlantFoodConsumedDelta);
public sealed record Summary(string Key, int? FirstOneTickDecline, int? FirstBelowEight,
    int? PersistentToStopOnset, bool RightCensored, int StopTick, int HistoricalFirstZeroComparison,
    Interval[] Intervals);
public sealed record SourceIdentity(string CheckoutCommit, string CheckoutTree, string AcceptedRuntimeBase,
    string AcceptedBaseTree, string RuntimeTree, string AiTree, string TechBlob, string DiagnosticTestBlob);
public sealed record Configuration(int Width, int Height, int InitialHumanPopulation, float DtSeconds,
    string Policy, bool Combat, bool Diplomacy, bool Siege, bool StoneBuildings,
    float BirthRateMultiplier, float MovementSpeedMultiplier, string EmergencyRescuePolicy,
    bool ReplenishmentOverridesApplied, bool RegrowthOverridesApplied, int TickCap);
public sealed record Payload(string SchemaVersion, SourceIdentity Source, Configuration Configuration,
    Run[] Runs, Summary[] Summaries);
public sealed record Manifest(string SchemaVersion, SourceIdentity Source, Configuration Configuration,
    string[] RunKeys, int[] SampleCounts, int[] StopTicks, string[] StopReasons,
    int ByteCount, string Sha256);
