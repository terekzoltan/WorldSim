using System;
using System.Collections.Generic;

namespace WorldSim.Simulation.Military;

public enum ArmyForageStatus
{
    Succeeded,
    Failed
}

public enum ArmyForageFailureReason
{
    None,
    InvalidConsumerKey,
    ForagerDead,
    SourceOutOfBounds,
    SourceOutOfRange,
    WaterTile,
    NoResourceNode,
    WrongResource,
    DepletedFood,
    ConsumerCapReached,
    NoYield,
    HarvestFailed
}

public sealed class ArmyForagingState
{
    private readonly Dictionary<string, int> _foodGainedByConsumer = new(StringComparer.Ordinal);

    public int Attempts { get; private set; }
    public int Successes { get; private set; }
    public int Failures { get; private set; }
    public int FoodGained { get; private set; }
    public int LastSourceX { get; private set; } = -1;
    public int LastSourceY { get; private set; } = -1;
    public string LastConsumerKey { get; private set; } = string.Empty;
    public ArmyForageStatus LastStatus { get; private set; } = ArmyForageStatus.Failed;
    public ArmyForageFailureReason LastFailureReason { get; private set; } = ArmyForageFailureReason.None;

    public int GetFoodGainedForConsumer(string consumerKey)
    {
        var normalized = ArmyForagingModel.NormalizeConsumerKey(consumerKey);
        return normalized.Length == 0 ? 0 : _foodGainedByConsumer.GetValueOrDefault(normalized, 0);
    }

    internal void RecordFailure(int sourceX, int sourceY, string consumerKey, ArmyForageFailureReason reason)
    {
        Attempts++;
        Failures++;
        LastSourceX = sourceX;
        LastSourceY = sourceY;
        LastConsumerKey = consumerKey;
        LastStatus = ArmyForageStatus.Failed;
        LastFailureReason = reason;
    }

    internal void RecordSuccess(int sourceX, int sourceY, string consumerKey, int foodGained)
    {
        var safeFoodGained = Math.Max(0, foodGained);
        Attempts++;
        Successes++;
        FoodGained = SaturatingAdd(FoodGained, safeFoodGained);
        LastSourceX = sourceX;
        LastSourceY = sourceY;
        LastConsumerKey = consumerKey;
        LastStatus = ArmyForageStatus.Succeeded;
        LastFailureReason = ArmyForageFailureReason.None;
        _foodGainedByConsumer[consumerKey] = SaturatingAdd(_foodGainedByConsumer.GetValueOrDefault(consumerKey, 0), safeFoodGained);
    }

    private static int SaturatingAdd(int left, int right)
    {
        long result = (long)Math.Max(0, left) + Math.Max(0, right);
        return result > int.MaxValue ? int.MaxValue : (int)result;
    }
}

public sealed record ArmyForagingOptions(
    int MaxFoodPerAttempt = 2,
    int MaxFoodPerConsumer = 8,
    int MaxSourceDistance = 1)
{
    public static ArmyForagingOptions Default { get; } = new();

    internal ArmyForagingOptions Normalized()
        => this with
        {
            MaxFoodPerAttempt = Math.Max(0, MaxFoodPerAttempt),
            MaxFoodPerConsumer = Math.Max(0, MaxFoodPerConsumer),
            MaxSourceDistance = Math.Max(0, MaxSourceDistance)
        };
}

public sealed record ArmyForageResult(
    ArmyForageStatus Status,
    ArmyForageFailureReason FailureReason,
    string ConsumerKey,
    int SourceX,
    int SourceY,
    int SourceFoodBefore,
    int SourceFoodAfter,
    int RationPoolFoodBefore,
    int RationPoolFoodAfter,
    int FoodGained,
    int ConsumerFoodGainedBefore,
    int ConsumerFoodGainedAfter);

public static class ArmyForagingModel
{
    public static ArmyForageResult TryForageToRationPool(
        World world,
        Person forager,
        ArmyRationPoolState rationPool,
        ArmyForagingState state,
        int sourceX,
        int sourceY,
        string consumerKey,
        ArmyForagingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(forager);
        ArgumentNullException.ThrowIfNull(rationPool);
        ArgumentNullException.ThrowIfNull(state);

        var normalizedConsumerKey = NormalizeConsumerKey(consumerKey);
        var poolBefore = rationPool.RationPoolFood;
        if (normalizedConsumerKey.Length == 0)
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.InvalidConsumerKey, 0, poolBefore, poolBefore, 0, 0);

        var consumerBefore = state.GetFoodGainedForConsumer(normalizedConsumerKey);
        if (forager.Health <= 0f)
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.ForagerDead, 0, poolBefore, poolBefore, consumerBefore, consumerBefore);

        if (sourceX < 0 || sourceY < 0 || sourceX >= world.Width || sourceY >= world.Height)
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.SourceOutOfBounds, 0, poolBefore, poolBefore, consumerBefore, consumerBefore);

        var normalizedOptions = (options ?? ArmyForagingOptions.Default).Normalized();
        var distance = Math.Max(Math.Abs(forager.Pos.x - sourceX), Math.Abs(forager.Pos.y - sourceY));
        if (distance > normalizedOptions.MaxSourceDistance)
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.SourceOutOfRange, 0, poolBefore, poolBefore, consumerBefore, consumerBefore);

        var tile = world.GetTile(sourceX, sourceY);
        if (tile.Ground == Ground.Water)
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.WaterTile, GetNodeAmount(tile.Node), poolBefore, poolBefore, consumerBefore, consumerBefore);

        if (tile.Node == null)
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.NoResourceNode, 0, poolBefore, poolBefore, consumerBefore, consumerBefore);

        if (tile.Node.Type != Resource.Food)
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.WrongResource, tile.Node.Amount, poolBefore, poolBefore, consumerBefore, consumerBefore);

        var sourceFoodBefore = tile.Node.Amount;
        if (sourceFoodBefore <= 0)
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.DepletedFood, sourceFoodBefore, poolBefore, poolBefore, consumerBefore, consumerBefore);

        var remainingConsumerCap = Math.Max(0, normalizedOptions.MaxFoodPerConsumer - consumerBefore);
        if (remainingConsumerCap <= 0)
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.ConsumerCapReached, sourceFoodBefore, poolBefore, poolBefore, consumerBefore, consumerBefore);

        // World.TryHarvest/Tile.Harvest is all-or-nothing, so request only a quantity
        // proven available from the node, consumer cap, and ration-pool capacity.
        var poolCapacity = int.MaxValue - poolBefore;
        var yield = Math.Min(Math.Min(sourceFoodBefore, normalizedOptions.MaxFoodPerAttempt), Math.Min(remainingConsumerCap, poolCapacity));
        if (yield <= 0)
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.NoYield, sourceFoodBefore, poolBefore, poolBefore, consumerBefore, consumerBefore);

        if (!world.TryHarvest((sourceX, sourceY), Resource.Food, yield))
            return BuildFailure(state, sourceX, sourceY, normalizedConsumerKey, ArmyForageFailureReason.HarvestFailed, sourceFoodBefore, poolBefore, poolBefore, consumerBefore, consumerBefore);

        rationPool.AddRations(yield);
        state.RecordSuccess(sourceX, sourceY, normalizedConsumerKey, yield);

        var sourceFoodAfter = GetNodeAmount(world.GetTile(sourceX, sourceY).Node);
        return new ArmyForageResult(
            ArmyForageStatus.Succeeded,
            ArmyForageFailureReason.None,
            normalizedConsumerKey,
            sourceX,
            sourceY,
            sourceFoodBefore,
            sourceFoodAfter,
            poolBefore,
            rationPool.RationPoolFood,
            yield,
            consumerBefore,
            state.GetFoodGainedForConsumer(normalizedConsumerKey));
    }

    internal static string NormalizeConsumerKey(string? consumerKey)
        => string.IsNullOrWhiteSpace(consumerKey) ? string.Empty : consumerKey.Trim();

    private static ArmyForageResult BuildFailure(
        ArmyForagingState state,
        int sourceX,
        int sourceY,
        string consumerKey,
        ArmyForageFailureReason failureReason,
        int sourceFoodBefore,
        int poolBefore,
        int poolAfter,
        int consumerBefore,
        int consumerAfter)
    {
        state.RecordFailure(sourceX, sourceY, consumerKey, failureReason);
        return new ArmyForageResult(
            ArmyForageStatus.Failed,
            failureReason,
            consumerKey,
            sourceX,
            sourceY,
            sourceFoodBefore,
            sourceFoodBefore,
            poolBefore,
            poolAfter,
            FoodGained: 0,
            consumerBefore,
            consumerAfter);
    }

    private static int GetNodeAmount(ResourceNode? node)
        => node?.Amount ?? 0;
}
