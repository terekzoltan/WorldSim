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

public sealed class LowFoodStockConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        var targetFood = Math.Max(3f, context.ColonyPopulation * 1.2f);
        if (context.HomeFood >= targetFood)
            return 0f;

        return Math.Clamp((targetFood - context.HomeFood) / targetFood, 0f, 1f);
    }
}

public sealed class StaminaDeficitConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        return Math.Clamp((100f - context.Stamina) / 100f, 0f, 1f);
    }
}

public sealed class HousingPressureConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        var capacity = Math.Max(1, context.HomeHouseCount * context.HouseCapacity);
        var pressure = context.ColonyPopulation / (float)capacity;
        return Math.Clamp(pressure - 0.85f, 0f, 1f);
    }
}

public sealed class LowStoneStockConsideration : Consideration
{
    private readonly int _threshold;

    public LowStoneStockConsideration(int threshold = 10)
    {
        _threshold = Math.Max(1, threshold);
    }

    public override float Evaluate(in NpcAiContext context)
    {
        if (context.HomeStone >= _threshold)
            return 0f;

        return Math.Clamp((_threshold - context.HomeStone) / (float)_threshold, 0f, 1f);
    }
}

public sealed class ThreatNearbyConsideration : Consideration
{
    private readonly int _threatCap;

    public ThreatNearbyConsideration(int threatCap = 3)
    {
        _threatCap = Math.Max(1, threatCap);
    }

    public override float Evaluate(in NpcAiContext context)
    {
        var directThreatScore = context.DirectThreatScore;
        if (directThreatScore <= 0f)
        {
            var directThreats = Math.Max(0, context.NearbyPredators) + Math.Max(0, Math.Max(context.NearbyHostilePeople, context.NearbyEnemyCount));
            directThreatScore = Math.Clamp(directThreats / (float)_threatCap, 0f, 1f);
        }

        return Math.Clamp(directThreatScore, 0f, 1f);
    }
}

public sealed class LowHealthConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        return Math.Clamp((60f - context.Health) / 60f, 0f, 1f);
    }
}

public sealed class HostileStanceConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        if (context.IsWarStance)
            return 1f;
        if (context.IsHostileStance)
            return 0.75f;
        return 0f;
    }
}

public sealed class ContestedZoneConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        if (context.IsContestedTile)
            return 1f;
        if (context.HasContestedTilesNearby)
            return 0.7f;
        return 0f;
    }
}

public sealed class WarriorRoleConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        return context.IsWarriorRole ? 1f : 0f;
    }
}

public sealed class CommanderRoleConsideration : Consideration
{
    private readonly float _nonCommanderScore;

    public CommanderRoleConsideration(float nonCommanderScore = 0.6f)
    {
        _nonCommanderScore = Math.Clamp(nonCommanderScore, 0f, 1f);
    }

    public override float Evaluate(in NpcAiContext context)
    {
        return context.IsCommander ? 1f : _nonCommanderScore;
    }
}

public sealed class GroupMoraleReadinessConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        if (context.ActiveCombatGroupSize <= 1)
            return 0.55f;

        return Math.Clamp((context.ActiveGroupAverageMorale - 35f) / 55f, 0f, 1f);
    }
}

public sealed class WarPressureConsideration : Consideration
{
    public override float Evaluate(in NpcAiContext context)
    {
        if (context.IsWarStance)
            return 1f;
        if (context.IsHostileStance && context.LocalThreatScore >= 0.4f)
            return 0.8f;
        return 0f;
    }
}

public sealed class LowMilitaryTechCountConsideration : Consideration
{
    private readonly int _threshold;

    public LowMilitaryTechCountConsideration(int threshold = 3)
    {
        _threshold = Math.Max(1, threshold);
    }

    public override float Evaluate(in NpcAiContext context)
    {
        if (context.HomeMilitaryTechCount >= _threshold)
            return 0f;

        return Math.Clamp((_threshold - context.HomeMilitaryTechCount) / (float)_threshold, 0f, 1f);
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
