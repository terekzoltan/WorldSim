using System;
using System.Linq;

namespace WorldSim.Simulation;

public sealed class HungerConsideration : Consideration
{
    // Needs["Hunger"] is 0..100 (lower = better). Map to 0..1 (higher = more hungry).
    public override float Evaluate(Person p, World w)
    {
        float h = p.Needs.TryGetValue("Hunger", out var value) ? value : 20f; // 0..100
        float v = Math.Clamp(h / 100f, 0f, 1f);             // 0 (full) .. 1 (hungry)
        return v;
    }
}

public sealed class LowWoodStockConsideration : Consideration
{
    private readonly int _threshold;
    public LowWoodStockConsideration(int threshold = 5) => _threshold = threshold;

    public override float Evaluate(Person p, World w)
    {
        int wood = p.Home.Stock[Resource.Wood];
        if (wood >= _threshold) return 0f;
        // Linear scale: less wood ? higher score
        float v = Math.Clamp((_threshold - wood) / (float)_threshold, 0f, 1f);
        return v;
    }
}

// Prefer building if affordable; stronger if capacity is currently low
public sealed class BuildHouseFeasibleConsideration : Consideration
{
    public override float Evaluate(Person p, World w)
    {
        int wood = p.Home.Stock[Resource.Wood];
        bool canAfford = wood >= p.Home.HouseWoodCost;

        int colonyPop = w._people.Count(x => x.Home == p.Home);
        int capacity  = p.Home.HouseCount * w.HouseCapacity;
        bool capacityLow = colonyPop >= capacity;

        if (!canAfford) return 0f;
        return capacityLow ? 1f : 0.25f;
    }
}