using System;

namespace WorldSim.AI;

public sealed class HungerConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        return Math.Clamp(context.Hunger / 100f, 0f, 1f);
    }
}

public sealed class LowWoodStockConsideration : Consideration
{
    private readonly int _threshold;

    public LowWoodStockConsideration(int threshold = 5)
    {
        _threshold = Math.Max(1, threshold);
    }

    public override float Evaluate(in NpcAiContext context)
    {
        if (context.HomeWood >= _threshold)
            return 0f;

        return Math.Clamp((_threshold - context.HomeWood) / (float)_threshold, 0f, 1f);
    }
}

public sealed class BuildHouseFeasibleConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        var canAffordWood = context.HomeWood >= context.HouseWoodCost;
        var canAffordStone = context.StoneBuildingsEnabled
            && context.CanBuildWithStone
            && context.HomeStone >= context.HouseStoneCost;
        if (!canAffordWood && !canAffordStone)
            return 0f;

        var capacity = context.HomeHouseCount * context.HouseCapacity;
        var capacityLow = context.ColonyPopulation >= capacity;
        return capacityLow ? 1f : 0.25f;
    }
}

public sealed class InvertedConsideration : Consideration
{
    private readonly Consideration _inner;

    public InvertedConsideration(Consideration inner)
    {
        _inner = inner;
    }

    public override float Evaluate(in NpcAiContext context)
    {
        return 1f - Math.Clamp(_inner.Evaluate(context), 0f, 1f);
    }
}
