using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldSim.Runtime;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;
using Xunit;
using Xunit.Abstractions;

namespace WorldSim.Runtime.Tests;

public sealed class Wave11LifecycleDiagnosticsTests
{
    const int MapWidth = 64;
    const int MapHeight = 40;
    const int InitialHumanPopulation = 24;
    const int TickCount = 1200;
    const float TickSeconds = 0.25f;

    static readonly ScenarioIdentity[] Identities =
    [
        new(101, NpcPlannerMode.Simple, "simple"),
        new(101, NpcPlannerMode.Goap, "goap"),
        new(202, NpcPlannerMode.Simple, "simple"),
        new(202, NpcPlannerMode.Goap, "goap"),
        new(202, NpcPlannerMode.Htn, "htn")
    ];

    readonly ITestOutputHelper _output;

    public Wave11LifecycleDiagnosticsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void LockedPredatorPreyRecruitmentAndMortalityLedger_IsDeterministicAndAccounted()
    {
        var canonicalTimer = Stopwatch.StartNew();
        var rows = new List<LifecycleDiagnosticRow>(Identities.Length * 2);
        foreach (var identity in Identities)
        {
            rows.Add(RunScenario(identity, predatorHumanEnabled: false));
            rows.Add(RunScenario(identity, predatorHumanEnabled: true));
        }
        canonicalTimer.Stop();

        AssertCanonicalRows(rows);
        var deltas = BuildDeltas(rows);

        var anchorTimer = Stopwatch.StartNew();
        var anchorIdentity = Identities.Single(identity => identity.Seed == 101 && identity.Planner == NpcPlannerMode.Goap);
        var repeatedOff = RunScenario(anchorIdentity, predatorHumanEnabled: false);
        var repeatedOn = RunScenario(anchorIdentity, predatorHumanEnabled: true);
        anchorTimer.Stop();

        Assert.Equal(rows.Single(row => row.Seed == 101 && row.Planner == "goap" && !row.PredatorHumanEnabled), repeatedOff);
        Assert.Equal(rows.Single(row => row.Seed == 101 && row.Planner == "goap" && row.PredatorHumanEnabled), repeatedOn);

        var canonicalRowsJson = SerializeRows(rows);
        var canonicalRowsSha256 = Convert.ToHexString(SHA256.HashData(canonicalRowsJson)).ToLowerInvariant();
        var ledgerJson = SerializeLedger(rows, deltas, canonicalRowsSha256);
        var canonicalElapsedMs = canonicalTimer.ElapsedMilliseconds;
        var anchorElapsedMs = anchorTimer.ElapsedMilliseconds;
        var twelveSimulationsElapsedMs = checked(canonicalElapsedMs + anchorElapsedMs);

        _output.WriteLine($"LIFECYCLE_DIAGNOSTICS_V1={Encoding.UTF8.GetString(ledgerJson)}");
        _output.WriteLine(
            $"LIFECYCLE_DIAGNOSTICS_TIMING_V1={{\"canonicalTenRowsElapsedMs\":{canonicalElapsedMs}," +
            $"\"anchorReplayElapsedMs\":{anchorElapsedMs},\"twelveSimulationsElapsedMs\":{twelveSimulationsElapsedMs}}}");
    }

    static LifecycleDiagnosticRow RunScenario(ScenarioIdentity identity, bool predatorHumanEnabled)
    {
        var techPath = Path.Combine(FindRepoRoot(), "Tech", "technologies.json");
        var runtime = new SimulationRuntime(
            width: MapWidth,
            height: MapHeight,
            initialPopulation: InitialHumanPopulation,
            technologyFilePath: techPath,
            aiOptions: new RuntimeAiOptions
            {
                PlannerMode = identity.Planner,
                PolicyMode = NpcPolicyMode.GlobalPlanner
            },
            randomSeed: identity.Seed);
        runtime.ConfigureScenarioRunnerWorldOptions(
            enableCombatPrimitives: true,
            enableDiplomacy: false,
            enableSiege: true,
            enablePredatorHumanAttacks: predatorHumanEnabled,
            stoneBuildingsEnabled: false,
            birthRateMultiplier: 1f,
            movementSpeedMultiplier: 1f,
            animalReplenishmentChancePerSecond: null,
            predatorReplenishmentChance: null,
            foodRegrowthMinSeconds: null,
            foodRegrowthJitterSeconds: null,
            emergencyRescuePolicy: EmergencyRescuePolicy.Disabled);

        var initialRun = runtime.BuildScenarioRunTelemetrySnapshot();
        for (var tick = 0; tick < TickCount; tick++)
            runtime.AdvanceTick(TickSeconds);

        var finalRun = runtime.BuildScenarioRunTelemetrySnapshot();
        var ecology = finalRun.Ecology;
        var lifecycle = runtime.GetSnapshot().EcologyDetails.LifecycleCounters;
        var derivedHerbivoreMortality = checked(
            initialRun.Ecology.Herbivores
            + lifecycle.HerbivoreBirths
            + ecology.HerbivoreReplenishmentSpawns
            - ecology.Herbivores);
        var unclassifiedHerbivoreMortality = checked(
            derivedHerbivoreMortality
            - lifecycle.HerbivoreStarvations
            - ecology.MeatFromHunt);
        var derivedPredatorMortality = checked(
            initialRun.Ecology.Predators
            + lifecycle.PredatorBirths
            + ecology.PredatorReplenishmentSpawns
            - ecology.Predators);
        var predatorOtherDeaths = checked(
            ecology.PredatorDeaths
            - lifecycle.PredatorStarvations
            - finalRun.PredatorKillsByHumans);

        Assert.Equal(predatorHumanEnabled, finalRun.EnablePredatorHumanAttacks);
        Assert.Equal(derivedPredatorMortality, ecology.PredatorDeaths);

        return new LifecycleDiagnosticRow(
            Seed: identity.Seed,
            Planner: identity.PlannerWireValue,
            PredatorHumanEnabled: predatorHumanEnabled,
            InitialHerbivores: initialRun.Ecology.Herbivores,
            InitialPredators: initialRun.Ecology.Predators,
            FinalHerbivores: ecology.Herbivores,
            FinalPredators: ecology.Predators,
            TicksWithZeroHerbivores: ecology.TicksWithZeroHerbivores,
            TicksWithZeroPredators: ecology.TicksWithZeroPredators,
            FirstZeroHerbivoreTick: ecology.FirstZeroHerbivoreTick,
            FirstZeroPredatorTick: ecology.FirstZeroPredatorTick,
            HerbivoreBirths: lifecycle.HerbivoreBirths,
            PredatorBirths: lifecycle.PredatorBirths,
            HerbivoreStarvations: lifecycle.HerbivoreStarvations,
            PredatorStarvations: lifecycle.PredatorStarvations,
            SuccessfulPredatorCapturesProxy: ecology.MeatFromHunt,
            PredatorDeaths: ecology.PredatorDeaths,
            PredatorKillsByHumans: finalRun.PredatorKillsByHumans,
            PredatorOtherDeaths: predatorOtherDeaths,
            DerivedHerbivoreMortality: derivedHerbivoreMortality,
            UnclassifiedHerbivoreMortality: unclassifiedHerbivoreMortality,
            ActiveFoodNodes: ecology.ActiveFoodNodes,
            DepletedFoodNodes: ecology.DepletedFoodNodes,
            PlantFoodConsumedByAnimals: ecology.PlantFoodConsumedByAnimals,
            PredatorHumanHits: ecology.PredatorHumanHits,
            FirstPredatorHuntTick: ecology.FirstPredatorHuntTick,
            FirstHerbivoreGrazingTick: ecology.FirstHerbivoreGrazingTick,
            FirstPredatorDeathTick: ecology.FirstPredatorDeathTick,
            FirstHerbivoreDeathTick: ecology.FirstHerbivoreDeathTick,
            FirstPredatorBirthTick: ecology.FirstPredatorBirthTick,
            FirstHerbivoreBirthTick: ecology.FirstHerbivoreBirthTick,
            EmergencyRescuePolicy: ecology.EmergencyRescuePolicy,
            EmergencyRescues: ecology.EmergencyRescues,
            HerbivoreReplenishmentSpawns: ecology.HerbivoreReplenishmentSpawns,
            PredatorReplenishmentSpawns: ecology.PredatorReplenishmentSpawns);
    }

    static void AssertCanonicalRows(IReadOnlyList<LifecycleDiagnosticRow> rows)
    {
        Assert.Equal(10, rows.Count);
        Assert.Equal(rows.Count, rows.Select(RowKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            new[]
            {
                "101/simple/off", "101/simple/on", "101/goap/off", "101/goap/on",
                "202/simple/off", "202/simple/on", "202/goap/off", "202/goap/on",
                "202/htn/off", "202/htn/on"
            },
            rows.Select(RowKey));

        foreach (var row in rows)
        {
            var key = RowKey(row);
            Assert.True(row.InitialHerbivores > 0, key);
            Assert.True(row.InitialPredators > 0, key);
            AssertNonNegative(row, key);
            Assert.Equal("disabled", row.EmergencyRescuePolicy);
            Assert.Equal(0, row.EmergencyRescues);
            Assert.Equal(0, row.HerbivoreReplenishmentSpawns);
            Assert.Equal(0, row.PredatorReplenishmentSpawns);

            if (!row.PredatorHumanEnabled)
            {
                Assert.Equal(0, row.PredatorHumanHits);
                Assert.Equal(0, row.PredatorKillsByHumans);
            }
        }

        for (var index = 0; index < rows.Count; index += 2)
        {
            var off = rows[index];
            var on = rows[index + 1];
            Assert.False(off.PredatorHumanEnabled);
            Assert.True(on.PredatorHumanEnabled);
            Assert.Equal(off.Seed, on.Seed);
            Assert.Equal(off.Planner, on.Planner);
            Assert.Equal(off.InitialHerbivores, on.InitialHerbivores);
            Assert.Equal(off.InitialPredators, on.InitialPredators);
        }
    }

    static void AssertNonNegative(LifecycleDiagnosticRow row, string key)
    {
        var values = new[]
        {
            row.InitialHerbivores,
            row.InitialPredators,
            row.FinalHerbivores,
            row.FinalPredators,
            row.TicksWithZeroHerbivores,
            row.TicksWithZeroPredators,
            row.HerbivoreBirths,
            row.PredatorBirths,
            row.HerbivoreStarvations,
            row.PredatorStarvations,
            row.SuccessfulPredatorCapturesProxy,
            row.PredatorDeaths,
            row.PredatorKillsByHumans,
            row.PredatorOtherDeaths,
            row.DerivedHerbivoreMortality,
            row.UnclassifiedHerbivoreMortality,
            row.ActiveFoodNodes,
            row.DepletedFoodNodes,
            row.PlantFoodConsumedByAnimals,
            row.PredatorHumanHits,
            row.EmergencyRescues,
            row.HerbivoreReplenishmentSpawns,
            row.PredatorReplenishmentSpawns
        };
        Assert.All(values, value => Assert.True(value >= 0, $"{key}: observed negative diagnostic value {value}."));
    }

    static IReadOnlyList<LifecycleDiagnosticDelta> BuildDeltas(IReadOnlyList<LifecycleDiagnosticRow> rows)
    {
        var deltas = new List<LifecycleDiagnosticDelta>(Identities.Length);
        for (var index = 0; index < rows.Count; index += 2)
        {
            var off = rows[index];
            var on = rows[index + 1];
            deltas.Add(new LifecycleDiagnosticDelta(
                Seed: off.Seed,
                Planner: off.Planner,
                OffPredatorHumanEnabled: off.PredatorHumanEnabled,
                OnPredatorHumanEnabled: on.PredatorHumanEnabled,
                InitialHerbivoresDelta: checked(on.InitialHerbivores - off.InitialHerbivores),
                InitialPredatorsDelta: checked(on.InitialPredators - off.InitialPredators),
                FinalHerbivoresDelta: checked(on.FinalHerbivores - off.FinalHerbivores),
                FinalPredatorsDelta: checked(on.FinalPredators - off.FinalPredators),
                TicksWithZeroHerbivoresDelta: checked(on.TicksWithZeroHerbivores - off.TicksWithZeroHerbivores),
                TicksWithZeroPredatorsDelta: checked(on.TicksWithZeroPredators - off.TicksWithZeroPredators),
                HerbivoreBirthsDelta: checked(on.HerbivoreBirths - off.HerbivoreBirths),
                PredatorBirthsDelta: checked(on.PredatorBirths - off.PredatorBirths),
                HerbivoreStarvationsDelta: checked(on.HerbivoreStarvations - off.HerbivoreStarvations),
                PredatorStarvationsDelta: checked(on.PredatorStarvations - off.PredatorStarvations),
                SuccessfulPredatorCapturesProxyDelta: checked(on.SuccessfulPredatorCapturesProxy - off.SuccessfulPredatorCapturesProxy),
                PredatorDeathsDelta: checked(on.PredatorDeaths - off.PredatorDeaths),
                PredatorKillsByHumansDelta: checked(on.PredatorKillsByHumans - off.PredatorKillsByHumans),
                PredatorOtherDeathsDelta: checked(on.PredatorOtherDeaths - off.PredatorOtherDeaths),
                DerivedHerbivoreMortalityDelta: checked(on.DerivedHerbivoreMortality - off.DerivedHerbivoreMortality),
                UnclassifiedHerbivoreMortalityDelta: checked(on.UnclassifiedHerbivoreMortality - off.UnclassifiedHerbivoreMortality),
                ActiveFoodNodesDelta: checked(on.ActiveFoodNodes - off.ActiveFoodNodes),
                DepletedFoodNodesDelta: checked(on.DepletedFoodNodes - off.DepletedFoodNodes),
                PlantFoodConsumedByAnimalsDelta: checked(on.PlantFoodConsumedByAnimals - off.PlantFoodConsumedByAnimals),
                PredatorHumanHitsDelta: checked(on.PredatorHumanHits - off.PredatorHumanHits),
                EmergencyRescuesDelta: checked(on.EmergencyRescues - off.EmergencyRescues),
                HerbivoreReplenishmentSpawnsDelta: checked(on.HerbivoreReplenishmentSpawns - off.HerbivoreReplenishmentSpawns),
                PredatorReplenishmentSpawnsDelta: checked(on.PredatorReplenishmentSpawns - off.PredatorReplenishmentSpawns),
                FirstZeroHerbivoreTick: new NullableTickPair(off.FirstZeroHerbivoreTick, on.FirstZeroHerbivoreTick),
                FirstZeroPredatorTick: new NullableTickPair(off.FirstZeroPredatorTick, on.FirstZeroPredatorTick),
                FirstPredatorHuntTick: new NullableTickPair(off.FirstPredatorHuntTick, on.FirstPredatorHuntTick),
                FirstHerbivoreGrazingTick: new NullableTickPair(off.FirstHerbivoreGrazingTick, on.FirstHerbivoreGrazingTick),
                FirstPredatorDeathTick: new NullableTickPair(off.FirstPredatorDeathTick, on.FirstPredatorDeathTick),
                FirstHerbivoreDeathTick: new NullableTickPair(off.FirstHerbivoreDeathTick, on.FirstHerbivoreDeathTick),
                FirstPredatorBirthTick: new NullableTickPair(off.FirstPredatorBirthTick, on.FirstPredatorBirthTick),
                FirstHerbivoreBirthTick: new NullableTickPair(off.FirstHerbivoreBirthTick, on.FirstHerbivoreBirthTick),
                EmergencyRescuePolicy: new StringPair(off.EmergencyRescuePolicy, on.EmergencyRescuePolicy)));
        }

        Assert.Equal(5, deltas.Count);
        Assert.All(deltas, delta =>
        {
            Assert.False(delta.OffPredatorHumanEnabled);
            Assert.True(delta.OnPredatorHumanEnabled);
            Assert.Equal(0, delta.InitialHerbivoresDelta);
            Assert.Equal(0, delta.InitialPredatorsDelta);
        });
        return deltas;
    }

    static byte[] SerializeRows(IReadOnlyList<LifecycleDiagnosticRow> rows)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartArray();
        foreach (var row in rows)
            WriteRow(writer, row);
        writer.WriteEndArray();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    static byte[] SerializeLedger(
        IReadOnlyList<LifecycleDiagnosticRow> rows,
        IReadOnlyList<LifecycleDiagnosticDelta> deltas,
        string canonicalRowsSha256)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", "lifecycle_diagnostics_v1");
        writer.WriteNumber("mapWidth", MapWidth);
        writer.WriteNumber("mapHeight", MapHeight);
        writer.WriteNumber("initialHumanPopulation", InitialHumanPopulation);
        writer.WriteNumber("ticks", TickCount);
        writer.WriteNumber("dtSeconds", TickSeconds);
        writer.WriteString("emergencyRescuePolicy", "disabled");
        writer.WriteBoolean("replenishmentOverridesApplied", false);
        writer.WritePropertyName("rows");
        writer.WriteStartArray();
        foreach (var row in rows)
            WriteRow(writer, row);
        writer.WriteEndArray();
        writer.WritePropertyName("deltas");
        writer.WriteStartArray();
        foreach (var delta in deltas)
            WriteDelta(writer, delta);
        writer.WriteEndArray();
        writer.WriteString("canonicalRowsSha256", canonicalRowsSha256);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    static void WriteRow(Utf8JsonWriter writer, LifecycleDiagnosticRow row)
    {
        writer.WriteStartObject();
        writer.WriteNumber("seed", row.Seed);
        writer.WriteString("planner", row.Planner);
        writer.WriteBoolean("predatorHumanEnabled", row.PredatorHumanEnabled);
        writer.WriteNumber("initialHerbivores", row.InitialHerbivores);
        writer.WriteNumber("initialPredators", row.InitialPredators);
        writer.WriteNumber("finalHerbivores", row.FinalHerbivores);
        writer.WriteNumber("finalPredators", row.FinalPredators);
        writer.WriteNumber("ticksWithZeroHerbivores", row.TicksWithZeroHerbivores);
        writer.WriteNumber("ticksWithZeroPredators", row.TicksWithZeroPredators);
        WriteNullableNumber(writer, "firstZeroHerbivoreTick", row.FirstZeroHerbivoreTick);
        WriteNullableNumber(writer, "firstZeroPredatorTick", row.FirstZeroPredatorTick);
        writer.WriteNumber("herbivoreBirths", row.HerbivoreBirths);
        writer.WriteNumber("predatorBirths", row.PredatorBirths);
        writer.WriteNumber("herbivoreStarvations", row.HerbivoreStarvations);
        writer.WriteNumber("predatorStarvations", row.PredatorStarvations);
        writer.WriteNumber("successfulPredatorCapturesProxy", row.SuccessfulPredatorCapturesProxy);
        writer.WriteNumber("predatorDeaths", row.PredatorDeaths);
        writer.WriteNumber("predatorKillsByHumans", row.PredatorKillsByHumans);
        writer.WriteNumber("predatorOtherDeaths", row.PredatorOtherDeaths);
        writer.WriteNumber("derivedHerbivoreMortality", row.DerivedHerbivoreMortality);
        writer.WriteNumber("unclassifiedHerbivoreMortality", row.UnclassifiedHerbivoreMortality);
        writer.WriteNumber("activeFoodNodes", row.ActiveFoodNodes);
        writer.WriteNumber("depletedFoodNodes", row.DepletedFoodNodes);
        writer.WriteNumber("plantFoodConsumedByAnimals", row.PlantFoodConsumedByAnimals);
        writer.WriteNumber("predatorHumanHits", row.PredatorHumanHits);
        WriteNullableNumber(writer, "firstPredatorHuntTick", row.FirstPredatorHuntTick);
        WriteNullableNumber(writer, "firstHerbivoreGrazingTick", row.FirstHerbivoreGrazingTick);
        WriteNullableNumber(writer, "firstPredatorDeathTick", row.FirstPredatorDeathTick);
        WriteNullableNumber(writer, "firstHerbivoreDeathTick", row.FirstHerbivoreDeathTick);
        WriteNullableNumber(writer, "firstPredatorBirthTick", row.FirstPredatorBirthTick);
        WriteNullableNumber(writer, "firstHerbivoreBirthTick", row.FirstHerbivoreBirthTick);
        writer.WriteString("emergencyRescuePolicy", row.EmergencyRescuePolicy);
        writer.WriteNumber("emergencyRescues", row.EmergencyRescues);
        writer.WriteNumber("herbivoreReplenishmentSpawns", row.HerbivoreReplenishmentSpawns);
        writer.WriteNumber("predatorReplenishmentSpawns", row.PredatorReplenishmentSpawns);
        writer.WriteEndObject();
    }

    static void WriteDelta(Utf8JsonWriter writer, LifecycleDiagnosticDelta delta)
    {
        writer.WriteStartObject();
        writer.WriteNumber("seed", delta.Seed);
        writer.WriteString("planner", delta.Planner);
        writer.WriteBoolean("offPredatorHumanEnabled", delta.OffPredatorHumanEnabled);
        writer.WriteBoolean("onPredatorHumanEnabled", delta.OnPredatorHumanEnabled);
        writer.WriteNumber("initialHerbivoresDelta", delta.InitialHerbivoresDelta);
        writer.WriteNumber("initialPredatorsDelta", delta.InitialPredatorsDelta);
        writer.WriteNumber("finalHerbivoresDelta", delta.FinalHerbivoresDelta);
        writer.WriteNumber("finalPredatorsDelta", delta.FinalPredatorsDelta);
        writer.WriteNumber("ticksWithZeroHerbivoresDelta", delta.TicksWithZeroHerbivoresDelta);
        writer.WriteNumber("ticksWithZeroPredatorsDelta", delta.TicksWithZeroPredatorsDelta);
        writer.WriteNumber("herbivoreBirthsDelta", delta.HerbivoreBirthsDelta);
        writer.WriteNumber("predatorBirthsDelta", delta.PredatorBirthsDelta);
        writer.WriteNumber("herbivoreStarvationsDelta", delta.HerbivoreStarvationsDelta);
        writer.WriteNumber("predatorStarvationsDelta", delta.PredatorStarvationsDelta);
        writer.WriteNumber("successfulPredatorCapturesProxyDelta", delta.SuccessfulPredatorCapturesProxyDelta);
        writer.WriteNumber("predatorDeathsDelta", delta.PredatorDeathsDelta);
        writer.WriteNumber("predatorKillsByHumansDelta", delta.PredatorKillsByHumansDelta);
        writer.WriteNumber("predatorOtherDeathsDelta", delta.PredatorOtherDeathsDelta);
        writer.WriteNumber("derivedHerbivoreMortalityDelta", delta.DerivedHerbivoreMortalityDelta);
        writer.WriteNumber("unclassifiedHerbivoreMortalityDelta", delta.UnclassifiedHerbivoreMortalityDelta);
        writer.WriteNumber("activeFoodNodesDelta", delta.ActiveFoodNodesDelta);
        writer.WriteNumber("depletedFoodNodesDelta", delta.DepletedFoodNodesDelta);
        writer.WriteNumber("plantFoodConsumedByAnimalsDelta", delta.PlantFoodConsumedByAnimalsDelta);
        writer.WriteNumber("predatorHumanHitsDelta", delta.PredatorHumanHitsDelta);
        writer.WriteNumber("emergencyRescuesDelta", delta.EmergencyRescuesDelta);
        writer.WriteNumber("herbivoreReplenishmentSpawnsDelta", delta.HerbivoreReplenishmentSpawnsDelta);
        writer.WriteNumber("predatorReplenishmentSpawnsDelta", delta.PredatorReplenishmentSpawnsDelta);
        WriteNullablePair(writer, "firstZeroHerbivoreTick", delta.FirstZeroHerbivoreTick);
        WriteNullablePair(writer, "firstZeroPredatorTick", delta.FirstZeroPredatorTick);
        WriteNullablePair(writer, "firstPredatorHuntTick", delta.FirstPredatorHuntTick);
        WriteNullablePair(writer, "firstHerbivoreGrazingTick", delta.FirstHerbivoreGrazingTick);
        WriteNullablePair(writer, "firstPredatorDeathTick", delta.FirstPredatorDeathTick);
        WriteNullablePair(writer, "firstHerbivoreDeathTick", delta.FirstHerbivoreDeathTick);
        WriteNullablePair(writer, "firstPredatorBirthTick", delta.FirstPredatorBirthTick);
        WriteNullablePair(writer, "firstHerbivoreBirthTick", delta.FirstHerbivoreBirthTick);
        writer.WritePropertyName("emergencyRescuePolicy");
        writer.WriteStartObject();
        writer.WriteString("off", delta.EmergencyRescuePolicy.Off);
        writer.WriteString("on", delta.EmergencyRescuePolicy.On);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    static void WriteNullablePair(Utf8JsonWriter writer, string name, NullableTickPair pair)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        WriteNullableNumber(writer, "off", pair.Off);
        WriteNullableNumber(writer, "on", pair.On);
        writer.WriteEndObject();
    }

    static void WriteNullableNumber(Utf8JsonWriter writer, string name, int? value)
    {
        if (value.HasValue)
            writer.WriteNumber(name, value.Value);
        else
            writer.WriteNull(name);
    }

    static string RowKey(LifecycleDiagnosticRow row)
        => $"{row.Seed}/{row.Planner}/{(row.PredatorHumanEnabled ? "on" : "off")}";

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

    sealed record ScenarioIdentity(int Seed, NpcPlannerMode Planner, string PlannerWireValue);

    sealed record NullableTickPair(int? Off, int? On);

    sealed record StringPair(string Off, string On);

    sealed record LifecycleDiagnosticRow(
        int Seed,
        string Planner,
        bool PredatorHumanEnabled,
        int InitialHerbivores,
        int InitialPredators,
        int FinalHerbivores,
        int FinalPredators,
        int TicksWithZeroHerbivores,
        int TicksWithZeroPredators,
        int? FirstZeroHerbivoreTick,
        int? FirstZeroPredatorTick,
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
        int? FirstPredatorHuntTick,
        int? FirstHerbivoreGrazingTick,
        int? FirstPredatorDeathTick,
        int? FirstHerbivoreDeathTick,
        int? FirstPredatorBirthTick,
        int? FirstHerbivoreBirthTick,
        string EmergencyRescuePolicy,
        int EmergencyRescues,
        int HerbivoreReplenishmentSpawns,
        int PredatorReplenishmentSpawns);

    sealed record LifecycleDiagnosticDelta(
        int Seed,
        string Planner,
        bool OffPredatorHumanEnabled,
        bool OnPredatorHumanEnabled,
        int InitialHerbivoresDelta,
        int InitialPredatorsDelta,
        int FinalHerbivoresDelta,
        int FinalPredatorsDelta,
        int TicksWithZeroHerbivoresDelta,
        int TicksWithZeroPredatorsDelta,
        int HerbivoreBirthsDelta,
        int PredatorBirthsDelta,
        int HerbivoreStarvationsDelta,
        int PredatorStarvationsDelta,
        int SuccessfulPredatorCapturesProxyDelta,
        int PredatorDeathsDelta,
        int PredatorKillsByHumansDelta,
        int PredatorOtherDeathsDelta,
        int DerivedHerbivoreMortalityDelta,
        int UnclassifiedHerbivoreMortalityDelta,
        int ActiveFoodNodesDelta,
        int DepletedFoodNodesDelta,
        int PlantFoodConsumedByAnimalsDelta,
        int PredatorHumanHitsDelta,
        int EmergencyRescuesDelta,
        int HerbivoreReplenishmentSpawnsDelta,
        int PredatorReplenishmentSpawnsDelta,
        NullableTickPair FirstZeroHerbivoreTick,
        NullableTickPair FirstZeroPredatorTick,
        NullableTickPair FirstPredatorHuntTick,
        NullableTickPair FirstHerbivoreGrazingTick,
        NullableTickPair FirstPredatorDeathTick,
        NullableTickPair FirstHerbivoreDeathTick,
        NullableTickPair FirstPredatorBirthTick,
        NullableTickPair FirstHerbivoreBirthTick,
        StringPair EmergencyRescuePolicy);
}
