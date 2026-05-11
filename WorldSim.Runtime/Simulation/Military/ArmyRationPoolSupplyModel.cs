using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Simulation.Military;

public sealed class ArmyRationPoolState
{
    public ArmyRationPoolState(int rationPoolFood = 0)
    {
        RationPoolFood = Math.Max(0, rationPoolFood);
    }

    public int RationPoolFood { get; private set; }

    internal void AddRations(int amount)
        => RationPoolFood = SaturatingAdd(RationPoolFood, Math.Max(0, amount));

    internal int ConsumeRations(int demand)
    {
        var consumed = Math.Min(RationPoolFood, Math.Max(0, demand));
        RationPoolFood -= consumed;
        return consumed;
    }

    internal int ClearRations()
    {
        var returned = RationPoolFood;
        RationPoolFood = 0;
        return returned;
    }

    private static int SaturatingAdd(int left, int right)
    {
        long result = (long)Math.Max(0, left) + Math.Max(0, right);
        return result > int.MaxValue ? int.MaxValue : (int)result;
    }
}

public sealed record ArmyRationReservationOptions(
    int MinHomeReserveFoodPerPerson = 2,
    float MaxReserveFraction = 0.25f,
    int CampaignDaysBudget = 3,
    int FoodPerWarriorPerDay = 1)
{
    public static ArmyRationReservationOptions Default { get; } = new();

    internal ArmyRationReservationOptions Normalized()
        => this with
        {
            MinHomeReserveFoodPerPerson = Math.Max(0, MinHomeReserveFoodPerPerson),
            MaxReserveFraction = float.IsFinite(MaxReserveFraction) ? Math.Clamp(MaxReserveFraction, 0f, 1f) : 0f,
            CampaignDaysBudget = Math.Max(0, CampaignDaysBudget),
            FoodPerWarriorPerDay = Math.Max(0, FoodPerWarriorPerDay)
        };
}

public enum ArmyRationReservationStatus
{
    Reserved,
    AlreadyReserved,
    InvalidArmySize,
    NoEligibleFood
}

public sealed record ArmyRationReservationResult(
    ArmyRationReservationStatus Status,
    int ColonyFoodBefore,
    int ColonyFoodAfter,
    int HomePopulation,
    int ArmySize,
    int MinHomeReserveFood,
    int MaxReserveByFraction,
    int DesiredFood,
    int ReservedFood,
    int RationPoolFoodAfter);

public sealed record ArmyRationPoolReturnResult(
    int ReturnedFood,
    int ColonyFoodBefore,
    int ColonyFoodAfter,
    int RationPoolFoodAfter);

public sealed record ArmyRationPoolSupplyTickResult(
    int ActiveMemberCount,
    int RationPoolFoodBefore,
    int RationPoolFoodAfter,
    int FoodDemandUnits,
    int FoodConsumed,
    int UnmetFoodDemandUnits,
    float FractionalFoodDemand,
    bool IsLowSupply,
    bool IsOutOfSupply,
    int SustainedOutOfSupplyTicks,
    int AttritionEventCount,
    int RoutedMemberCount,
    float MoraleDeltaApplied,
    float StaminaDeltaApplied);

public static class ArmyRationPoolSupplyModel
{
    public static ArmyRationReservationResult ReserveRations(
        Colony home,
        int homePopulation,
        int armySize,
        ArmyRationPoolState rationPool,
        ArmyRationReservationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(rationPool);

        var normalized = (options ?? ArmyRationReservationOptions.Default).Normalized();
        var safeHomePopulation = Math.Max(0, homePopulation);
        var safeArmySize = Math.Max(0, armySize);
        var colonyFoodBefore = home.Stock[Resource.Food];
        var minHomeReserve = SaturatingMultiply(safeHomePopulation, normalized.MinHomeReserveFoodPerPerson, 1);
        var maxByFraction = Math.Max(0, (int)MathF.Floor(colonyFoodBefore * normalized.MaxReserveFraction));
        var desiredFood = SaturatingMultiply(
            safeArmySize,
            normalized.CampaignDaysBudget,
            normalized.FoodPerWarriorPerDay);

        if (rationPool.RationPoolFood > 0)
        {
            return BuildReservationResult(
                ArmyRationReservationStatus.AlreadyReserved,
                colonyFoodBefore,
                colonyFoodBefore,
                safeHomePopulation,
                safeArmySize,
                minHomeReserve,
                maxByFraction,
                desiredFood,
                reservedFood: 0,
                rationPool.RationPoolFood);
        }

        if (safeArmySize <= 0 || desiredFood <= 0)
        {
            return BuildReservationResult(
                ArmyRationReservationStatus.InvalidArmySize,
                colonyFoodBefore,
                colonyFoodBefore,
                safeHomePopulation,
                safeArmySize,
                minHomeReserve,
                maxByFraction,
                desiredFood,
                reservedFood: 0,
                rationPool.RationPoolFood);
        }

        var eligibleFood = Math.Max(0, colonyFoodBefore - minHomeReserve);
        var reserved = Math.Min(Math.Min(maxByFraction, desiredFood), eligibleFood);
        if (reserved <= 0)
        {
            return BuildReservationResult(
                ArmyRationReservationStatus.NoEligibleFood,
                colonyFoodBefore,
                colonyFoodBefore,
                safeHomePopulation,
                safeArmySize,
                minHomeReserve,
                maxByFraction,
                desiredFood,
                reservedFood: 0,
                rationPool.RationPoolFood);
        }

        home.Stock[Resource.Food] = colonyFoodBefore - reserved;
        rationPool.AddRations(reserved);

        return BuildReservationResult(
            ArmyRationReservationStatus.Reserved,
            colonyFoodBefore,
            home.Stock[Resource.Food],
            safeHomePopulation,
            safeArmySize,
            minHomeReserve,
            maxByFraction,
            desiredFood,
            reserved,
            rationPool.RationPoolFood);
    }

    // Caller contract: run either ration-pool fallback mode or carried-inventory mode for an army tick, never both.
    public static ArmyRationPoolSupplyTickResult TickRationPool(
        IReadOnlyList<Person> members,
        ArmySupplyState supplyState,
        ArmyRationPoolState rationPool,
        float dt,
        ArmySupplyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(supplyState);
        ArgumentNullException.ThrowIfNull(rationPool);

        var activeMembers = members
            .Where(member => member.Health > 0f)
            .OrderBy(member => member.Id)
            .ToList();
        var poolFoodBefore = rationPool.RationPoolFood;

        if (dt <= 0f || activeMembers.Count == 0)
        {
            return new ArmyRationPoolSupplyTickResult(
                ActiveMemberCount: activeMembers.Count,
                RationPoolFoodBefore: poolFoodBefore,
                RationPoolFoodAfter: poolFoodBefore,
                FoodDemandUnits: 0,
                FoodConsumed: 0,
                UnmetFoodDemandUnits: 0,
                FractionalFoodDemand: supplyState.FractionalFoodDemand,
                IsLowSupply: false,
                IsOutOfSupply: false,
                SustainedOutOfSupplyTicks: supplyState.SustainedOutOfSupplyTicks,
                AttritionEventCount: 0,
                RoutedMemberCount: 0,
                MoraleDeltaApplied: 0f,
                StaminaDeltaApplied: 0f);
        }

        var normalized = (options ?? ArmySupplyOptions.Default).Normalized();
        var demand = (activeMembers.Count * normalized.FoodConsumedPerPersonPerSecond * dt) + supplyState.FractionalFoodDemand;
        var foodDemandUnits = Math.Max(0, (int)MathF.Floor(demand));
        supplyState.SetFractionalFoodDemand(demand - foodDemandUnits);

        var consumed = rationPool.ConsumeRations(foodDemandUnits);
        var unmet = Math.Max(0, foodDemandUnits - consumed);
        var poolFoodAfter = rationPool.RationPoolFood;
        var isLowSupply = poolFoodAfter > 0
                          && poolFoodAfter < activeMembers.Count * normalized.LowSupplyFoodPerPersonThreshold;
        var isOutOfSupply = unmet > 0;
        supplyState.RecordSupplyPressure(isOutOfSupply);

        var moraleDelta = 0f;
        var staminaDelta = 0f;
        if (isOutOfSupply)
        {
            moraleDelta = -normalized.OutOfSupplyMoraleLossPerSecond * dt;
            staminaDelta = -normalized.OutOfSupplyStaminaLossPerSecond * dt;
        }
        else if (isLowSupply && normalized.LowSupplyMoraleLossPerSecond > 0f)
        {
            moraleDelta = -normalized.LowSupplyMoraleLossPerSecond * dt;
        }

        var attritionEvents = 0;
        if (moraleDelta != 0f || staminaDelta != 0f)
        {
            foreach (var member in activeMembers)
            {
                if (moraleDelta != 0f)
                    member.ApplyMoraleDelta(moraleDelta);
                if (staminaDelta != 0f)
                    member.ApplyStaminaDelta(staminaDelta);
                attritionEvents++;
            }
        }

        var routed = 0;
        if (isOutOfSupply && supplyState.SustainedOutOfSupplyTicks >= normalized.RouteAfterOutOfSupplyTicks)
        {
            foreach (var member in activeMembers)
            {
                if (member.IsRouting)
                    continue;
                if (member.CombatMorale > normalized.RouteMoraleThreshold && member.Stamina > normalized.RouteStaminaThreshold)
                    continue;
                if (member.BeginRouting(normalized.RoutingTicks))
                    routed++;
            }
        }

        return new ArmyRationPoolSupplyTickResult(
            ActiveMemberCount: activeMembers.Count,
            RationPoolFoodBefore: poolFoodBefore,
            RationPoolFoodAfter: poolFoodAfter,
            FoodDemandUnits: foodDemandUnits,
            FoodConsumed: consumed,
            UnmetFoodDemandUnits: unmet,
            FractionalFoodDemand: supplyState.FractionalFoodDemand,
            IsLowSupply: isLowSupply,
            IsOutOfSupply: isOutOfSupply,
            SustainedOutOfSupplyTicks: supplyState.SustainedOutOfSupplyTicks,
            AttritionEventCount: attritionEvents,
            RoutedMemberCount: routed,
            MoraleDeltaApplied: moraleDelta,
            StaminaDeltaApplied: staminaDelta);
    }

    public static ArmyRationPoolReturnResult ReturnRemainingRations(Colony home, ArmyRationPoolState rationPool)
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(rationPool);

        var colonyFoodBefore = home.Stock[Resource.Food];
        var returned = rationPool.ClearRations();
        home.Stock[Resource.Food] = SaturatingAdd(colonyFoodBefore, returned);

        return new ArmyRationPoolReturnResult(
            ReturnedFood: returned,
            ColonyFoodBefore: colonyFoodBefore,
            ColonyFoodAfter: home.Stock[Resource.Food],
            RationPoolFoodAfter: rationPool.RationPoolFood);
    }

    private static ArmyRationReservationResult BuildReservationResult(
        ArmyRationReservationStatus status,
        int colonyFoodBefore,
        int colonyFoodAfter,
        int homePopulation,
        int armySize,
        int minHomeReserveFood,
        int maxReserveByFraction,
        int desiredFood,
        int reservedFood,
        int rationPoolFoodAfter)
        => new(
            status,
            colonyFoodBefore,
            colonyFoodAfter,
            homePopulation,
            armySize,
            minHomeReserveFood,
            maxReserveByFraction,
            desiredFood,
            reservedFood,
            rationPoolFoodAfter);

    private static int SaturatingMultiply(int first, int second, int third)
    {
        var result = 1;
        foreach (var factor in new[] { Math.Max(0, first), Math.Max(0, second), Math.Max(0, third) })
        {
            if (factor == 0)
                return 0;
            if (result > int.MaxValue / factor)
                return int.MaxValue;
            result *= factor;
        }

        return result;
    }

    private static int SaturatingAdd(int left, int right)
    {
        long result = (long)Math.Max(0, left) + Math.Max(0, right);
        return result > int.MaxValue ? int.MaxValue : (int)result;
    }
}
