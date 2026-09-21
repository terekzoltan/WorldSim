using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldSim.Runtime;
using WorldSim.Runtime.Diagnostics;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;
using Xunit;
using Xunit.Abstractions;

namespace WorldSim.Runtime.Tests;

public sealed class Wave11SmrRecruitmentMortalityEvidenceTests
{
    const int MapWidth = 64;
    const int MapHeight = 40;
    const int InitialHumanPopulation = 24;
    const int TickCount = 1200;
    const float TickSeconds = 0.25f;
    const int MaxSamplesPerRun = TickCount + 1;
    const int MaxCanonicalSamples = MaxSamplesPerRun * 4;
    const int MaxReplaySamples = MaxSamplesPerRun * 2;

    const string AcceptedRuntimeBase = "db8d18a799f12fcf39dd593e7a42f6811663c21e";
    const string AcceptedRuntimeTree = "fc62cdd7885f6f6c3997486ba27213aee5771c03";
    const string RetainedCandidateReceiptSha256 = "56e7f0081d6119ce642da3fb235a0f21a4482338cabbcd1044ffa241dc3471df";

    static readonly DiagnosticCase[] Cases =
    [
        new(101, NpcPlannerMode.Goap, "goap", PredatorHumanEnabled: false),
        new(101, NpcPlannerMode.Goap, "goap", PredatorHumanEnabled: true),
        new(202, NpcPlannerMode.Simple, "simple", PredatorHumanEnabled: false),
        new(202, NpcPlannerMode.Simple, "simple", PredatorHumanEnabled: true)
    ];

    readonly ITestOutputHelper _output;

    public Wave11SmrRecruitmentMortalityEvidenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BoundedOnOffEvidence_IsDeterministicAccountedAndProvenanceBound()
    {
        var runs = Cases.Select(RunScenario).ToList();

        Assert.Equal(
            new[] { "101/goap/off", "101/goap/on", "202/simple/off", "202/simple/on" },
            runs.Select(RunKey));
        Assert.Equal(runs.Count, runs.Select(RunKey).Distinct(StringComparer.Ordinal).Count());

        AssertPair(runs[0], runs[1]);
        AssertPair(runs[2], runs[3]);

        var replayOff = RunScenario(Cases[2]);
        var replayOn = RunScenario(Cases[3]);
        AssertSemanticRunEqual(runs[2], replayOff);
        AssertSemanticRunEqual(runs[3], replayOn);

        var canonicalSampleCount = runs.Sum(run => run.Samples.Count);
        var replaySampleCount = replayOff.Samples.Count + replayOn.Samples.Count;
        Assert.True(canonicalSampleCount <= MaxCanonicalSamples, $"Canonical samples: {canonicalSampleCount}");
        Assert.True(replaySampleCount <= MaxReplaySamples, $"Replay samples: {replaySampleCount}");

        var deltas = new[]
        {
            BuildModeDelta(runs[0], runs[1]),
            BuildModeDelta(runs[2], runs[3])
        };
        var canonicalRunsJson = SerializeRuns(runs);
        var canonicalRunsSha256 = Convert.ToHexString(SHA256.HashData(canonicalRunsJson)).ToLowerInvariant();
        var payload = SerializePayload(runs, deltas, canonicalRunsSha256, canonicalSampleCount, replaySampleCount);

        _output.WriteLine($"E11H_SMR_RECRUITMENT_MORTALITY_V1={Encoding.UTF8.GetString(payload)}");
    }

    static DiagnosticRun RunScenario(DiagnosticCase diagnosticCase)
    {
        var techPath = Path.Combine(FindRepoRoot(), "Tech", "technologies.json");
        var runtime = new SimulationRuntime(
            width: MapWidth,
            height: MapHeight,
            initialPopulation: InitialHumanPopulation,
            technologyFilePath: techPath,
            aiOptions: new RuntimeAiOptions
            {
                PlannerMode = diagnosticCase.Planner,
                PolicyMode = NpcPolicyMode.GlobalPlanner
            },
            randomSeed: diagnosticCase.Seed);
        runtime.ConfigureScenarioRunnerWorldOptions(
            enableCombatPrimitives: true,
            enableDiplomacy: false,
            enableSiege: true,
            enablePredatorHumanAttacks: diagnosticCase.PredatorHumanEnabled,
            stoneBuildingsEnabled: false,
            birthRateMultiplier: 1f,
            movementSpeedMultiplier: 1f,
            animalReplenishmentChancePerSecond: null,
            predatorReplenishmentChance: null,
            foodRegrowthMinSeconds: null,
            foodRegrowthJitterSeconds: null,
            emergencyRescuePolicy: EmergencyRescuePolicy.Disabled);

        var initialTelemetry = runtime.BuildScenarioRunTelemetrySnapshot();
        var initialLifecycle = runtime.GetSnapshot().EcologyDetails.LifecycleCounters;
        var initialHerbivores = initialTelemetry.Ecology.Herbivores;
        var initialPredators = initialTelemetry.Ecology.Predators;
        var previous = BuildSample(0, initialTelemetry, initialLifecycle, initialHerbivores, initialPredators);
        var samples = new List<DiagnosticSample> { previous };
        var firstEvents = new FirstEventAccumulator();

        for (var tick = 1; tick <= TickCount; tick++)
        {
            runtime.AdvanceTick(TickSeconds);
            var telemetry = runtime.BuildScenarioRunTelemetrySnapshot();
            var lifecycle = runtime.GetSnapshot().EcologyDetails.LifecycleCounters;
            var current = BuildSample(tick, telemetry, lifecycle, initialHerbivores, initialPredators);

            firstEvents.Observe(previous, current);
            var firstZeroTransition = (previous.Herbivores > 0 && current.Herbivores == 0)
                || (previous.Predators > 0 && current.Predators == 0);
            if (tick == 1
                || tick % 100 == 0
                || tick == TickCount
                || firstZeroTransition
                || HasDiagnosticTransition(previous, current))
            {
                samples.Add(current);
            }

            previous = current;
        }

        var finalTelemetry = runtime.BuildScenarioRunTelemetrySnapshot();
        var ecology = finalTelemetry.Ecology;
        var firstEventTicks = firstEvents.ToSnapshot();

        Assert.Equal(diagnosticCase.PredatorHumanEnabled, finalTelemetry.EnablePredatorHumanAttacks);
        Assert.Equal("disabled", ecology.EmergencyRescuePolicy);
        Assert.Equal(0, ecology.EmergencyRescues);
        Assert.Equal(0, ecology.HerbivoreReplenishmentSpawns);
        Assert.Equal(0, ecology.PredatorReplenishmentSpawns);
        if (!diagnosticCase.PredatorHumanEnabled)
        {
            Assert.Equal(0, ecology.PredatorHumanHits);
            Assert.Equal(0, finalTelemetry.PredatorKillsByHumans);
        }

        Assert.Equal(ecology.FirstPredatorHumanContactTick, firstEventTicks.FirstReportedPredatorHumanHitTick);
        Assert.Equal(ecology.FirstPredatorHuntTick, firstEventTicks.FirstPredatorHuntTick);
        Assert.Equal(ecology.FirstHerbivoreGrazingTick, firstEventTicks.FirstHerbivoreGrazingTick);
        Assert.Equal(ecology.FirstPredatorDeathTick, firstEventTicks.FirstPredatorDeathTick);
        Assert.Equal(ecology.FirstHerbivoreDeathTick, firstEventTicks.FirstHerbivoreDeathTick);
        Assert.Equal(ecology.FirstPredatorBirthTick, firstEventTicks.FirstPredatorBirthTick);
        Assert.Equal(ecology.FirstHerbivoreBirthTick, firstEventTicks.FirstHerbivoreBirthTick);
        Assert.Equal(ecology.FirstZeroHerbivoreTick, firstEventTicks.FirstZeroHerbivoreTick);
        Assert.Equal(ecology.FirstZeroPredatorTick, firstEventTicks.FirstZeroPredatorTick);

        Assert.True(samples.Count <= MaxSamplesPerRun, $"{RunKey(diagnosticCase)} samples: {samples.Count}");
        Assert.Equal(samples.Count, samples.Select(sample => sample.Tick).Distinct().Count());
        Assert.True(samples.Zip(samples.Skip(1), (left, right) => left.Tick < right.Tick).All(inOrder => inOrder));

        return new DiagnosticRun(
            diagnosticCase.Seed,
            diagnosticCase.PlannerWireValue,
            diagnosticCase.PredatorHumanEnabled,
            initialHerbivores,
            initialPredators,
            ecology.TicksWithZeroHerbivores,
            ecology.TicksWithZeroPredators,
            firstEventTicks,
            samples);
    }

    static DiagnosticSample BuildSample(
        int tick,
        ScenarioRunTelemetrySnapshot telemetry,
        EcologyLifecycleCounterRenderData lifecycle,
        int initialHerbivores,
        int initialPredators)
    {
        var ecology = telemetry.Ecology;
        var derivedHerbivoreMortality = checked(
            initialHerbivores
            + lifecycle.HerbivoreBirths
            + ecology.HerbivoreReplenishmentSpawns
            - ecology.Herbivores);
        var unclassifiedHerbivoreMortality = checked(
            derivedHerbivoreMortality
            - lifecycle.HerbivoreStarvations
            - ecology.MeatFromHunt);
        var derivedPredatorMortality = checked(
            initialPredators
            + lifecycle.PredatorBirths
            + ecology.PredatorReplenishmentSpawns
            - ecology.Predators);
        var predatorOtherDeaths = checked(
            ecology.PredatorDeaths
            - lifecycle.PredatorStarvations
            - telemetry.PredatorKillsByHumans);

        Assert.Equal(derivedPredatorMortality, ecology.PredatorDeaths);
        AssertNonNegative(
            ecology.Herbivores,
            ecology.Predators,
            lifecycle.HerbivoreBirths,
            lifecycle.PredatorBirths,
            lifecycle.HerbivoreStarvations,
            lifecycle.PredatorStarvations,
            ecology.MeatFromHunt,
            ecology.PredatorDeaths,
            telemetry.PredatorKillsByHumans,
            predatorOtherDeaths,
            derivedHerbivoreMortality,
            unclassifiedHerbivoreMortality,
            ecology.ActiveFoodNodes,
            ecology.DepletedFoodNodes,
            ecology.PlantFoodConsumedByAnimals,
            ecology.PredatorHumanHits,
            ecology.EmergencyRescues,
            ecology.HerbivoreReplenishmentSpawns,
            ecology.PredatorReplenishmentSpawns);

        return new DiagnosticSample(
            tick,
            ecology.Herbivores,
            ecology.Predators,
            lifecycle.HerbivoreBirths,
            lifecycle.PredatorBirths,
            lifecycle.HerbivoreStarvations,
            lifecycle.PredatorStarvations,
            ecology.MeatFromHunt,
            ecology.PredatorDeaths,
            telemetry.PredatorKillsByHumans,
            predatorOtherDeaths,
            derivedHerbivoreMortality,
            unclassifiedHerbivoreMortality,
            ecology.ActiveFoodNodes,
            ecology.DepletedFoodNodes,
            ecology.PlantFoodConsumedByAnimals,
            ecology.PredatorHumanHits,
            ecology.EmergencyRescues,
            ecology.HerbivoreReplenishmentSpawns,
            ecology.PredatorReplenishmentSpawns);
    }

    static bool HasDiagnosticTransition(DiagnosticSample previous, DiagnosticSample current)
        => current.HerbivoreBirths != previous.HerbivoreBirths
            || current.PredatorBirths != previous.PredatorBirths
            || current.HerbivoreStarvations != previous.HerbivoreStarvations
            || current.PredatorStarvations != previous.PredatorStarvations
            || current.SuccessfulPredatorCapturesProxy != previous.SuccessfulPredatorCapturesProxy
            || current.PlantFoodConsumedByAnimals != previous.PlantFoodConsumedByAnimals
            || current.PredatorDeaths != previous.PredatorDeaths
            || current.PredatorKillsByHumans != previous.PredatorKillsByHumans
            || current.PredatorHumanHits != previous.PredatorHumanHits
            || current.EmergencyRescues != previous.EmergencyRescues
            || current.HerbivoreReplenishmentSpawns != previous.HerbivoreReplenishmentSpawns
            || current.PredatorReplenishmentSpawns != previous.PredatorReplenishmentSpawns;

    static void AssertPair(DiagnosticRun off, DiagnosticRun on)
    {
        Assert.False(off.PredatorHumanEnabled);
        Assert.True(on.PredatorHumanEnabled);
        Assert.Equal(off.Seed, on.Seed);
        Assert.Equal(off.Planner, on.Planner);
        Assert.Equal(off.InitialHerbivores, on.InitialHerbivores);
        Assert.Equal(off.InitialPredators, on.InitialPredators);
    }

    static void AssertSemanticRunEqual(DiagnosticRun expected, DiagnosticRun actual)
    {
        var expectedJson = SerializeRuns([expected]);
        var actualJson = SerializeRuns([actual]);
        Assert.True(expectedJson.AsSpan().SequenceEqual(actualJson), $"Replay drift: {RunKey(expected)}");
    }

    static ModeDelta BuildModeDelta(DiagnosticRun off, DiagnosticRun on)
    {
        var offFinal = off.Samples[^1];
        var onFinal = on.Samples[^1];
        return new ModeDelta(
            off.Seed,
            off.Planner,
            checked(onFinal.Herbivores - offFinal.Herbivores),
            checked(onFinal.Predators - offFinal.Predators),
            checked(onFinal.HerbivoreBirths - offFinal.HerbivoreBirths),
            checked(onFinal.PredatorBirths - offFinal.PredatorBirths),
            checked(onFinal.HerbivoreStarvations - offFinal.HerbivoreStarvations),
            checked(onFinal.PredatorStarvations - offFinal.PredatorStarvations),
            checked(onFinal.SuccessfulPredatorCapturesProxy - offFinal.SuccessfulPredatorCapturesProxy),
            checked(onFinal.PredatorDeaths - offFinal.PredatorDeaths),
            checked(onFinal.PredatorKillsByHumans - offFinal.PredatorKillsByHumans),
            checked(onFinal.UnclassifiedHerbivoreMortality - offFinal.UnclassifiedHerbivoreMortality),
            checked(on.TicksWithZeroHerbivores - off.TicksWithZeroHerbivores),
            checked(on.TicksWithZeroPredators - off.TicksWithZeroPredators));
    }

    static byte[] SerializeRuns(IReadOnlyList<DiagnosticRun> runs)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartArray();
        foreach (var run in runs)
            WriteRun(writer, run);
        writer.WriteEndArray();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    static byte[] SerializePayload(
        IReadOnlyList<DiagnosticRun> runs,
        IReadOnlyList<ModeDelta> deltas,
        string canonicalRunsSha256,
        int canonicalSampleCount,
        int replaySampleCount)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", "e11h_smr_recruitment_mortality_v1");
        writer.WriteString("acceptedRuntimeBase", AcceptedRuntimeBase);
        writer.WriteString("acceptedRuntimeTree", AcceptedRuntimeTree);
        writer.WriteString("retainedCandidateReceiptSha256", RetainedCandidateReceiptSha256);
        writer.WriteString("firstPredatorHumanContactSemantics", "first ReportPredatorHumanHit hit/contact signal; not spatial proximity");
        writer.WriteNumber("maxSamplesPerRun", MaxSamplesPerRun);
        writer.WriteNumber("maxCanonicalSamples", MaxCanonicalSamples);
        writer.WriteNumber("maxReplaySamples", MaxReplaySamples);
        writer.WriteNumber("canonicalSampleCount", canonicalSampleCount);
        writer.WriteNumber("replaySampleCount", replaySampleCount);
        writer.WriteString("canonicalRunsSha256", canonicalRunsSha256);
        writer.WritePropertyName("configuration");
        WriteConfiguration(writer);
        writer.WritePropertyName("runs");
        writer.WriteStartArray();
        foreach (var run in runs)
            WriteRun(writer, run);
        writer.WriteEndArray();
        writer.WritePropertyName("deltas");
        writer.WriteStartArray();
        foreach (var delta in deltas)
            WriteDelta(writer, delta);
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    static void WriteConfiguration(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("mapWidth", MapWidth);
        writer.WriteNumber("mapHeight", MapHeight);
        writer.WriteNumber("initialHumanPopulation", InitialHumanPopulation);
        writer.WriteNumber("ticks", TickCount);
        writer.WriteNumber("dtSeconds", TickSeconds);
        writer.WriteString("policyMode", "global_planner");
        writer.WriteBoolean("combatPrimitives", true);
        writer.WriteBoolean("diplomacy", false);
        writer.WriteBoolean("siege", true);
        writer.WriteBoolean("stoneBuildings", false);
        writer.WriteNumber("birthRateMultiplier", 1f);
        writer.WriteNumber("movementSpeedMultiplier", 1f);
        writer.WriteString("emergencyRescuePolicy", "disabled");
        writer.WriteBoolean("replenishmentOverridesApplied", false);
        writer.WriteBoolean("regrowthOverridesApplied", false);
        writer.WriteEndObject();
    }

    static void WriteRun(Utf8JsonWriter writer, DiagnosticRun run)
    {
        writer.WriteStartObject();
        writer.WriteNumber("seed", run.Seed);
        writer.WriteString("planner", run.Planner);
        writer.WriteBoolean("predatorHumanEnabled", run.PredatorHumanEnabled);
        writer.WriteNumber("initialHerbivores", run.InitialHerbivores);
        writer.WriteNumber("initialPredators", run.InitialPredators);
        writer.WriteNumber("ticksWithZeroHerbivores", run.TicksWithZeroHerbivores);
        writer.WriteNumber("ticksWithZeroPredators", run.TicksWithZeroPredators);
        writer.WritePropertyName("firstEvents");
        WriteFirstEvents(writer, run.FirstEvents);
        writer.WritePropertyName("samples");
        writer.WriteStartArray();
        foreach (var sample in run.Samples)
            WriteSample(writer, sample);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    static void WriteFirstEvents(Utf8JsonWriter writer, FirstEventTicks events)
    {
        writer.WriteStartObject();
        WriteNullableNumber(writer, "firstReportedPredatorHumanHitTick", events.FirstReportedPredatorHumanHitTick);
        WriteNullableNumber(writer, "firstHumanPredatorKillTick", events.FirstHumanPredatorKillTick);
        WriteNullableNumber(writer, "firstPredatorHuntTick", events.FirstPredatorHuntTick);
        WriteNullableNumber(writer, "firstHerbivoreGrazingTick", events.FirstHerbivoreGrazingTick);
        WriteNullableNumber(writer, "firstHerbivoreStarvationTick", events.FirstHerbivoreStarvationTick);
        WriteNullableNumber(writer, "firstPredatorStarvationTick", events.FirstPredatorStarvationTick);
        WriteNullableNumber(writer, "firstHerbivoreBirthTick", events.FirstHerbivoreBirthTick);
        WriteNullableNumber(writer, "firstPredatorBirthTick", events.FirstPredatorBirthTick);
        WriteNullableNumber(writer, "firstHerbivoreDeathTick", events.FirstHerbivoreDeathTick);
        WriteNullableNumber(writer, "firstPredatorDeathTick", events.FirstPredatorDeathTick);
        WriteNullableNumber(writer, "firstZeroHerbivoreTick", events.FirstZeroHerbivoreTick);
        WriteNullableNumber(writer, "firstZeroPredatorTick", events.FirstZeroPredatorTick);
        writer.WriteEndObject();
    }

    static void WriteSample(Utf8JsonWriter writer, DiagnosticSample sample)
    {
        writer.WriteStartObject();
        writer.WriteNumber("tick", sample.Tick);
        writer.WriteNumber("herbivores", sample.Herbivores);
        writer.WriteNumber("predators", sample.Predators);
        writer.WriteNumber("herbivoreBirths", sample.HerbivoreBirths);
        writer.WriteNumber("predatorBirths", sample.PredatorBirths);
        writer.WriteNumber("herbivoreStarvations", sample.HerbivoreStarvations);
        writer.WriteNumber("predatorStarvations", sample.PredatorStarvations);
        writer.WriteNumber("successfulPredatorCapturesProxy", sample.SuccessfulPredatorCapturesProxy);
        writer.WriteNumber("predatorDeaths", sample.PredatorDeaths);
        writer.WriteNumber("predatorKillsByHumans", sample.PredatorKillsByHumans);
        writer.WriteNumber("predatorOtherDeaths", sample.PredatorOtherDeaths);
        writer.WriteNumber("derivedHerbivoreMortality", sample.DerivedHerbivoreMortality);
        writer.WriteNumber("unclassifiedHerbivoreMortality", sample.UnclassifiedHerbivoreMortality);
        writer.WriteNumber("activeFoodNodes", sample.ActiveFoodNodes);
        writer.WriteNumber("depletedFoodNodes", sample.DepletedFoodNodes);
        writer.WriteNumber("plantFoodConsumedByAnimals", sample.PlantFoodConsumedByAnimals);
        writer.WriteNumber("predatorHumanHits", sample.PredatorHumanHits);
        writer.WriteNumber("emergencyRescues", sample.EmergencyRescues);
        writer.WriteNumber("herbivoreReplenishmentSpawns", sample.HerbivoreReplenishmentSpawns);
        writer.WriteNumber("predatorReplenishmentSpawns", sample.PredatorReplenishmentSpawns);
        writer.WriteEndObject();
    }

    static void WriteDelta(Utf8JsonWriter writer, ModeDelta delta)
    {
        writer.WriteStartObject();
        writer.WriteNumber("seed", delta.Seed);
        writer.WriteString("planner", delta.Planner);
        writer.WriteString("direction", "on_minus_off");
        writer.WriteNumber("finalHerbivores", delta.FinalHerbivores);
        writer.WriteNumber("finalPredators", delta.FinalPredators);
        writer.WriteNumber("herbivoreBirths", delta.HerbivoreBirths);
        writer.WriteNumber("predatorBirths", delta.PredatorBirths);
        writer.WriteNumber("herbivoreStarvations", delta.HerbivoreStarvations);
        writer.WriteNumber("predatorStarvations", delta.PredatorStarvations);
        writer.WriteNumber("successfulPredatorCapturesProxy", delta.SuccessfulPredatorCapturesProxy);
        writer.WriteNumber("predatorDeaths", delta.PredatorDeaths);
        writer.WriteNumber("predatorKillsByHumans", delta.PredatorKillsByHumans);
        writer.WriteNumber("unclassifiedHerbivoreMortality", delta.UnclassifiedHerbivoreMortality);
        writer.WriteNumber("ticksWithZeroHerbivores", delta.TicksWithZeroHerbivores);
        writer.WriteNumber("ticksWithZeroPredators", delta.TicksWithZeroPredators);
        writer.WriteEndObject();
    }

    static void WriteNullableNumber(Utf8JsonWriter writer, string name, int? value)
    {
        if (value.HasValue)
            writer.WriteNumber(name, value.Value);
        else
            writer.WriteNull(name);
    }

    static void AssertNonNegative(params int[] values)
        => Assert.All(values, value => Assert.True(value >= 0, $"Observed negative diagnostic value {value}."));

    static string RunKey(DiagnosticCase diagnosticCase)
        => $"{diagnosticCase.Seed}/{diagnosticCase.PlannerWireValue}/{(diagnosticCase.PredatorHumanEnabled ? "on" : "off")}";

    static string RunKey(DiagnosticRun run)
        => $"{run.Seed}/{run.Planner}/{(run.PredatorHumanEnabled ? "on" : "off")}";

    static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Tech", "technologies.json")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json.");
    }

    sealed class FirstEventAccumulator
    {
        int? _firstReportedPredatorHumanHitTick;
        int? _firstHumanPredatorKillTick;
        int? _firstPredatorHuntTick;
        int? _firstHerbivoreGrazingTick;
        int? _firstHerbivoreStarvationTick;
        int? _firstPredatorStarvationTick;
        int? _firstHerbivoreBirthTick;
        int? _firstPredatorBirthTick;
        int? _firstHerbivoreDeathTick;
        int? _firstPredatorDeathTick;
        int? _firstZeroHerbivoreTick;
        int? _firstZeroPredatorTick;

        public void Observe(DiagnosticSample previous, DiagnosticSample current)
        {
            SetFirstOnIncrease(ref _firstReportedPredatorHumanHitTick, previous.PredatorHumanHits, current.PredatorHumanHits, current.Tick);
            SetFirstOnIncrease(ref _firstHumanPredatorKillTick, previous.PredatorKillsByHumans, current.PredatorKillsByHumans, current.Tick);
            SetFirstOnIncrease(ref _firstPredatorHuntTick, previous.SuccessfulPredatorCapturesProxy, current.SuccessfulPredatorCapturesProxy, current.Tick);
            SetFirstOnIncrease(ref _firstHerbivoreGrazingTick, previous.PlantFoodConsumedByAnimals, current.PlantFoodConsumedByAnimals, current.Tick);
            SetFirstOnIncrease(ref _firstHerbivoreStarvationTick, previous.HerbivoreStarvations, current.HerbivoreStarvations, current.Tick);
            SetFirstOnIncrease(ref _firstPredatorStarvationTick, previous.PredatorStarvations, current.PredatorStarvations, current.Tick);
            SetFirstOnIncrease(ref _firstHerbivoreBirthTick, previous.HerbivoreBirths, current.HerbivoreBirths, current.Tick);
            SetFirstOnIncrease(ref _firstPredatorBirthTick, previous.PredatorBirths, current.PredatorBirths, current.Tick);
            SetFirstOnIncrease(ref _firstHerbivoreDeathTick, previous.DerivedHerbivoreMortality, current.DerivedHerbivoreMortality, current.Tick);
            SetFirstOnIncrease(ref _firstPredatorDeathTick, previous.PredatorDeaths, current.PredatorDeaths, current.Tick);
            if (!_firstZeroHerbivoreTick.HasValue && previous.Herbivores > 0 && current.Herbivores == 0)
                _firstZeroHerbivoreTick = current.Tick;
            if (!_firstZeroPredatorTick.HasValue && previous.Predators > 0 && current.Predators == 0)
                _firstZeroPredatorTick = current.Tick;
        }

        public FirstEventTicks ToSnapshot()
            => new(
                _firstReportedPredatorHumanHitTick,
                _firstHumanPredatorKillTick,
                _firstPredatorHuntTick,
                _firstHerbivoreGrazingTick,
                _firstHerbivoreStarvationTick,
                _firstPredatorStarvationTick,
                _firstHerbivoreBirthTick,
                _firstPredatorBirthTick,
                _firstHerbivoreDeathTick,
                _firstPredatorDeathTick,
                _firstZeroHerbivoreTick,
                _firstZeroPredatorTick);

        static void SetFirstOnIncrease(ref int? target, int previous, int current, int tick)
        {
            if (!target.HasValue && current > previous)
                target = tick;
        }
    }

    sealed record DiagnosticCase(int Seed, NpcPlannerMode Planner, string PlannerWireValue, bool PredatorHumanEnabled);

    sealed record DiagnosticRun(
        int Seed,
        string Planner,
        bool PredatorHumanEnabled,
        int InitialHerbivores,
        int InitialPredators,
        int TicksWithZeroHerbivores,
        int TicksWithZeroPredators,
        FirstEventTicks FirstEvents,
        IReadOnlyList<DiagnosticSample> Samples);

    sealed record FirstEventTicks(
        int? FirstReportedPredatorHumanHitTick,
        int? FirstHumanPredatorKillTick,
        int? FirstPredatorHuntTick,
        int? FirstHerbivoreGrazingTick,
        int? FirstHerbivoreStarvationTick,
        int? FirstPredatorStarvationTick,
        int? FirstHerbivoreBirthTick,
        int? FirstPredatorBirthTick,
        int? FirstHerbivoreDeathTick,
        int? FirstPredatorDeathTick,
        int? FirstZeroHerbivoreTick,
        int? FirstZeroPredatorTick);

    sealed record DiagnosticSample(
        int Tick,
        int Herbivores,
        int Predators,
        int HerbivoreBirths,
        int PredatorBirths,
        int HerbivoreStarvations,
        int PredatorStarvations,
        int SuccessfulPredatorCapturesProxy,
        int PredatorDeaths,
        int PredatorKillsByHumans,
        int PredatorOtherDeaths,
        int DerivedHerbivoreMortality,
        int UnclassifiedHerbivoreMortality,
        int ActiveFoodNodes,
        int DepletedFoodNodes,
        int PlantFoodConsumedByAnimals,
        int PredatorHumanHits,
        int EmergencyRescues,
        int HerbivoreReplenishmentSpawns,
        int PredatorReplenishmentSpawns);

    sealed record ModeDelta(
        int Seed,
        string Planner,
        int FinalHerbivores,
        int FinalPredators,
        int HerbivoreBirths,
        int PredatorBirths,
        int HerbivoreStarvations,
        int PredatorStarvations,
        int SuccessfulPredatorCapturesProxy,
        int PredatorDeaths,
        int PredatorKillsByHumans,
        int UnclassifiedHerbivoreMortality,
        int TicksWithZeroHerbivores,
        int TicksWithZeroPredators);
}
