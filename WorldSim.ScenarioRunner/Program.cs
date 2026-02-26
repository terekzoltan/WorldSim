using WorldSim.Simulation;

var seeds = ParseCsvInt(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_SEEDS")) ?? new[] { 101, 202, 303 };
var ticks = ParseInt(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_TICKS"), 1200);
var dt = ParseFloat(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_DT"), 0.25f);

Console.WriteLine($"ScenarioRunner | seeds=[{string.Join(",", seeds)}] ticks={ticks} dt={dt:0.###}");

foreach (var seed in seeds)
{
    var world = new World(width: 64, height: 40, initialPop: 24, randomSeed: seed);
    for (var i = 0; i < ticks; i++)
        world.Update(dt);

    var livingColonies = world._colonies.Count(c => world._people.Any(p => p.Home == c && p.Health > 0f));
    var totalFood = world._colonies.Sum(c => c.Stock[Resource.Food]);
    var totalPeople = world._people.Count(p => p.Health > 0f);
    var avgFoodPerPerson = totalPeople > 0 ? totalFood / (float)totalPeople : 0f;

    Console.WriteLine(
        $"seed={seed} livingColonies={livingColonies} people={totalPeople} food={totalFood} avgFpp={avgFoodPerPerson:0.00} " +
        $"deaths(age/starv/pred/other)={world.TotalDeathsOldAge}/{world.TotalDeathsStarvation}/{world.TotalDeathsPredator}/{world.TotalDeathsOther} " +
        $"starv60s={world.RecentDeathsStarvation60s} starvWithFood={world.TotalStarvationDeathsWithFood}");
}

return;

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
