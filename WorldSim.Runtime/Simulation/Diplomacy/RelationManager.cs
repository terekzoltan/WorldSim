using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Simulation.Diplomacy;

public sealed class RelationManager
{
    private readonly Dictionary<(Faction left, Faction right), double> _pressureScores = new();
    private readonly Dictionary<(Faction left, Faction right), int> _stanceCooldownTicks = new();

    public void Tick(World world)
    {
        DecrementCooldowns();

        var borderPressure = MeasureBorderPressure(world);
        var skirmishPressure = MeasureSkirmishPressure(world);

        foreach (var pair in EnumeratePairs(world))
        {
            borderPressure.TryGetValue(pair, out int border);
            skirmishPressure.TryGetValue(pair, out int skirmish);

            var currentScore = _pressureScores.GetValueOrDefault(pair, 0d);
            var nextScore = Math.Clamp(currentScore + (border * 0.35d) + (skirmish * 8d) - 1.2d, 0d, 220d);
            _pressureScores[pair] = nextScore;

            var target = ResolveTargetStance(nextScore);
            var current = world.GetFactionStance(pair.left, pair.right);
            if (target == current)
                continue;

            if (_stanceCooldownTicks.GetValueOrDefault(pair, 0) > 0)
                continue;

            world.SetFactionStance(pair.left, pair.right, target);
            _stanceCooldownTicks[pair] = target == Stance.Neutral ? 180 : 90;
        }
    }

    private static Stance ResolveTargetStance(double score)
    {
        if (score >= 120d)
            return Stance.War;
        if (score >= 55d)
            return Stance.Hostile;
        return Stance.Neutral;
    }

    private void DecrementCooldowns()
    {
        foreach (var key in _stanceCooldownTicks.Keys.ToList())
        {
            var value = _stanceCooldownTicks[key];
            if (value <= 1)
                _stanceCooldownTicks.Remove(key);
            else
                _stanceCooldownTicks[key] = value - 1;
        }
    }

    private static Dictionary<(Faction left, Faction right), int> MeasureBorderPressure(World world)
    {
        var result = new Dictionary<(Faction left, Faction right), int>();
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                int owner = world.GetTileOwnerColonyId(x, y);
                if (owner < 0)
                    continue;

                if (x + 1 < world.Width)
                    AddIfBorder(world, result, owner, world.GetTileOwnerColonyId(x + 1, y));
                if (y + 1 < world.Height)
                    AddIfBorder(world, result, owner, world.GetTileOwnerColonyId(x, y + 1));
            }
        }

        return result;
    }

    private static Dictionary<(Faction left, Faction right), int> MeasureSkirmishPressure(World world)
    {
        var result = new Dictionary<(Faction left, Faction right), int>();
        for (int i = 0; i < world._people.Count; i++)
        {
            var a = world._people[i];
            if (a.Health <= 0f)
                continue;

            for (int j = i + 1; j < world._people.Count; j++)
            {
                var b = world._people[j];
                if (b.Health <= 0f || a.Home.Faction == b.Home.Faction)
                    continue;

                int distance = Math.Abs(a.Pos.x - b.Pos.x) + Math.Abs(a.Pos.y - b.Pos.y);
                if (distance > 2)
                    continue;

                var key = NormalizePair(a.Home.Faction, b.Home.Faction);
                result[key] = result.GetValueOrDefault(key, 0) + 1;
            }
        }

        return result;
    }

    private static void AddIfBorder(World world, Dictionary<(Faction left, Faction right), int> map, int ownerA, int ownerB)
    {
        if (ownerA < 0 || ownerB < 0 || ownerA == ownerB)
            return;

        var colonyA = world._colonies.FirstOrDefault(c => c.Id == ownerA);
        var colonyB = world._colonies.FirstOrDefault(c => c.Id == ownerB);
        if (colonyA is null || colonyB is null || colonyA.Faction == colonyB.Faction)
            return;

        var key = NormalizePair(colonyA.Faction, colonyB.Faction);
        map[key] = map.GetValueOrDefault(key, 0) + 1;
    }

    private static List<(Faction left, Faction right)> EnumeratePairs(World world)
    {
        var factions = world._colonies.Select(c => c.Faction).Distinct().OrderBy(f => f).ToList();
        var result = new List<(Faction left, Faction right)>();
        for (int i = 0; i < factions.Count; i++)
        {
            for (int j = i + 1; j < factions.Count; j++)
                result.Add((factions[i], factions[j]));
        }

        return result;
    }

    private static (Faction left, Faction right) NormalizePair(Faction a, Faction b)
        => a <= b ? (a, b) : (b, a);
}
