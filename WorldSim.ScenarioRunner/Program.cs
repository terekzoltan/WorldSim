using System.Text.Json;
using WorldSim.Simulation;

var seeds = ParseCsvInt(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_SEEDS")) ?? new[] { 101, 202, 303 };
var planners = ParsePlannerModes(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_PLANNERS"));
var outputMode = ParseOutputMode(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_OUTPUT"));
var configs = ParseScenarioConfigs(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_CONFIGS_JSON"), outputMode);

if (configs.Count == 0)
{
    var fallbackTicks = ParseInt(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_TICKS"), 1200);
    var fallbackDt = ParseFloat(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_DT"), 0.25f);
    configs.Add(new ScenarioConfig(
        Name: "default",
        Width: 64,
        Height: 40,
        InitialPop: 24,
        Ticks: fallbackTicks,
        Dt: fallbackDt,
        EnableCombatPrimitives: false,
        EnableDiplomacy: false,
        StoneBuildingsEnabled: false,
        BirthRateMultiplier: 1f,
        MovementSpeedMultiplier: 1f));
}

var runs = new List<ScenarioRunResult>(configs.Count * planners.Count * seeds.Length);
foreach (var config in configs.OrderBy(c => c.Name, StringComparer.Ordinal))
{
    foreach (var planner in planners)
    {
        foreach (var seed in seeds.OrderBy(s => s))
        {
            var world = new World(
                width: config.Width,
                height: config.Height,
                initialPop: config.InitialPop,
                brainFactory: _ => new RuntimeNpcBrain(planner, $"ScenarioRunner:{planner}"),
                randomSeed: seed)
            {
                EnableCombatPrimitives = config.EnableCombatPrimitives,
                EnableDiplomacy = config.EnableDiplomacy,
                StoneBuildingsEnabled = config.StoneBuildingsEnabled,
                BirthRateMultiplier = config.BirthRateMultiplier,
                MovementSpeedMultiplier = config.MovementSpeedMultiplier
            };

            for (var i = 0; i < config.Ticks; i++)
                world.Update(config.Dt);

            runs.Add(BuildRunResult(world, config, planner, seed));
        }
    }
}

WriteOutput(runs, outputMode, seeds, planners, configs);
return;

static ScenarioRunResult BuildRunResult(World world, ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    var livingColonies = world._colonies.Count(colony => world._people.Any(person => person.Home == colony && person.Health > 0f));
    var totalFood = world._colonies.Sum(colony => colony.Stock[Resource.Food]);
    var totalPeople = world._people.Count(person => person.Health > 0f);
    var avgFoodPerPerson = totalPeople > 0 ? totalFood / (float)totalPeople : 0f;

    return new ScenarioRunResult(
        ConfigName: config.Name,
        PlannerMode: planner.ToString(),
        Seed: seed,
        Width: config.Width,
        Height: config.Height,
        InitialPop: config.InitialPop,
        Ticks: config.Ticks,
        Dt: config.Dt,
        EnableCombatPrimitives: config.EnableCombatPrimitives,
        EnableDiplomacy: config.EnableDiplomacy,
        StoneBuildingsEnabled: config.StoneBuildingsEnabled,
        BirthRateMultiplier: config.BirthRateMultiplier,
        MovementSpeedMultiplier: config.MovementSpeedMultiplier,
        LivingColonies: livingColonies,
        People: totalPeople,
        Food: totalFood,
        AverageFoodPerPerson: avgFoodPerPerson,
        DeathsOldAge: world.TotalDeathsOldAge,
        DeathsStarvation: world.TotalDeathsStarvation,
        DeathsPredator: world.TotalDeathsPredator,
        DeathsOther: world.TotalDeathsOther,
        DeathsStarvationRecent60s: world.RecentDeathsStarvation60s,
        DeathsStarvationWithFood: world.TotalStarvationDeathsWithFood,
        OverlapResolveMoves: world.TotalOverlapResolveMoves,
        CrowdDissipationMoves: world.TotalCrowdDissipationMoves,
        BirthFallbackToOccupied: world.TotalBirthFallbackToOccupiedCount,
        BirthFallbackToParent: world.TotalBirthFallbackToParentCount,
        BuildSiteResets: world.TotalBuildSiteResetCount,
        NoProgressBackoffResource: world.TotalNoProgressBackoffResource,
        NoProgressBackoffBuild: world.TotalNoProgressBackoffBuild,
        NoProgressBackoffFlee: world.TotalNoProgressBackoffFlee,
        NoProgressBackoffCombat: world.TotalNoProgressBackoffCombat,
        DenseNeighborhoodTicks: world.DenseNeighborhoodTicks,
        LastTickDenseActors: world.LastTickDenseActors);
}

static void WriteOutput(
    IReadOnlyList<ScenarioRunResult> runs,
    ScenarioOutputMode outputMode,
    IReadOnlyList<int> seeds,
    IReadOnlyList<NpcPlannerMode> planners,
    IReadOnlyList<ScenarioConfig> configs)
{
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = outputMode == ScenarioOutputMode.Json
    };

    switch (outputMode)
    {
        case ScenarioOutputMode.Json:
            var envelope = new ScenarioRunEnvelope(
                GeneratedAtUtc: DateTime.UtcNow,
                SeedCount: seeds.Count,
                PlannerCount: planners.Count,
                ConfigCount: configs.Count,
                Runs: runs.ToList());
            Console.WriteLine(JsonSerializer.Serialize(envelope, jsonOptions));
            break;

        case ScenarioOutputMode.Jsonl:
            foreach (var run in runs)
                Console.WriteLine(JsonSerializer.Serialize(run, jsonOptions));
            break;

        default:
            Console.WriteLine($"ScenarioRunner matrix | seeds=[{string.Join(",", seeds)}] planners=[{string.Join(",", planners)}] configs={configs.Count}");
            foreach (var run in runs)
            {
                Console.WriteLine(
                    $"config={run.ConfigName} planner={run.PlannerMode} seed={run.Seed} livingCols={run.LivingColonies} people={run.People} food={run.Food} avgFpp={run.AverageFoodPerPerson:0.00} " +
                    $"cluster(overlap/dissipate/denseTicks/lastDense)={run.OverlapResolveMoves}/{run.CrowdDissipationMoves}/{run.DenseNeighborhoodTicks}/{run.LastTickDenseActors} " +
                    $"birthFallback(occupied/parent)={run.BirthFallbackToOccupied}/{run.BirthFallbackToParent} " +
                    $"buildSiteResets={run.BuildSiteResets} " +
                    $"backoff(resource/build/flee/combat)={run.NoProgressBackoffResource}/{run.NoProgressBackoffBuild}/{run.NoProgressBackoffFlee}/{run.NoProgressBackoffCombat}");
            }
            break;
    }
}

static ScenarioOutputMode ParseOutputMode(string? raw)
{
    if (string.Equals(raw, "json", StringComparison.OrdinalIgnoreCase))
        return ScenarioOutputMode.Json;
    if (string.Equals(raw, "text", StringComparison.OrdinalIgnoreCase))
        return ScenarioOutputMode.Text;
    return ScenarioOutputMode.Jsonl;
}

static List<NpcPlannerMode> ParsePlannerModes(string? raw)
{
    var defaults = new List<NpcPlannerMode>
    {
        NpcPlannerMode.Simple,
        NpcPlannerMode.Goap,
        NpcPlannerMode.Htn
    };

    if (string.IsNullOrWhiteSpace(raw))
        return defaults;

    var result = new List<NpcPlannerMode>();
    foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!Enum.TryParse<NpcPlannerMode>(token, ignoreCase: true, out var parsed))
            continue;
        if (result.Contains(parsed))
            continue;
        result.Add(parsed);
    }

    return result.Count > 0 ? result : defaults;
}

static List<ScenarioConfig> ParseScenarioConfigs(string? raw, ScenarioOutputMode outputMode)
{
    if (string.IsNullOrWhiteSpace(raw))
        return new List<ScenarioConfig>();

    try
    {
        var parsed = JsonSerializer.Deserialize<List<ScenarioConfig>>(raw);
        if (parsed == null)
            return new List<ScenarioConfig>();

        return parsed
            .Where(config => config.Width > 0 && config.Height > 0 && config.InitialPop > 0 && config.Ticks > 0 && config.Dt > 0f)
            .Select(config => config with
            {
                Name = string.IsNullOrWhiteSpace(config.Name) ? "config" : config.Name
            })
            .ToList();
    }
    catch (Exception ex)
    {
        if (outputMode != ScenarioOutputMode.Jsonl)
            Console.WriteLine($"Warning: invalid WORLDSIM_SCENARIO_CONFIGS_JSON ({ex.Message}). Using defaults.");
        return new List<ScenarioConfig>();
    }
}

static int ParseInt(string? value, int fallback)
    => int.TryParse(value, out var parsed) ? parsed : fallback;

static float ParseFloat(string? value, float fallback)
    => float.TryParse(value, out var parsed) ? parsed : fallback;

static int[]? ParseCsvInt(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return null;

    var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var parsed = new List<int>(items.Length);
    foreach (var item in items)
    {
        if (!int.TryParse(item, out var num))
            continue;
        parsed.Add(num);
    }

    return parsed.Count > 0 ? parsed.ToArray() : null;
}

enum ScenarioOutputMode
{
    Text,
    Jsonl,
    Json
}

sealed record ScenarioConfig(
    string Name,
    int Width,
    int Height,
    int InitialPop,
    int Ticks,
    float Dt,
    bool EnableCombatPrimitives,
    bool EnableDiplomacy,
    bool StoneBuildingsEnabled,
    float BirthRateMultiplier,
    float MovementSpeedMultiplier);

sealed record ScenarioRunResult(
    string ConfigName,
    string PlannerMode,
    int Seed,
    int Width,
    int Height,
    int InitialPop,
    int Ticks,
    float Dt,
    bool EnableCombatPrimitives,
    bool EnableDiplomacy,
    bool StoneBuildingsEnabled,
    float BirthRateMultiplier,
    float MovementSpeedMultiplier,
    int LivingColonies,
    int People,
    int Food,
    float AverageFoodPerPerson,
    int DeathsOldAge,
    int DeathsStarvation,
    int DeathsPredator,
    int DeathsOther,
    int DeathsStarvationRecent60s,
    int DeathsStarvationWithFood,
    int OverlapResolveMoves,
    int CrowdDissipationMoves,
    int BirthFallbackToOccupied,
    int BirthFallbackToParent,
    int BuildSiteResets,
    int NoProgressBackoffResource,
    int NoProgressBackoffBuild,
    int NoProgressBackoffFlee,
    int NoProgressBackoffCombat,
    int DenseNeighborhoodTicks,
    int LastTickDenseActors);

sealed record ScenarioRunEnvelope(
    DateTime GeneratedAtUtc,
    int SeedCount,
    int PlannerCount,
    int ConfigCount,
    List<ScenarioRunResult> Runs);
