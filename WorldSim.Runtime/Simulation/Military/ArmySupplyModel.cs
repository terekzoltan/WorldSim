using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Simulation.Military;

public sealed class ArmySupplyState
{
    public float FractionalFoodDemand { get; private set; }
    public int SustainedOutOfSupplyTicks { get; private set; }

    internal void SetFractionalFoodDemand(float value)
        => FractionalFoodDemand = float.IsFinite(value) ? Math.Clamp(value, 0f, 0.9999f) : 0f;

    internal void RecordSupplyPressure(bool isOutOfSupply)
        => SustainedOutOfSupplyTicks = isOutOfSupply ? SustainedOutOfSupplyTicks + 1 : 0;
}

public sealed record ArmySupplyOptions(
    float FoodConsumedPerPersonPerSecond = 0.02f,
    float LowSupplyFoodPerPersonThreshold = 1f,
    float LowSupplyMoraleLossPerSecond = 0f,
    float OutOfSupplyMoraleLossPerSecond = 1.5f,
    float OutOfSupplyStaminaLossPerSecond = 3f,
    int RouteAfterOutOfSupplyTicks = 5,
    float RouteMoraleThreshold = 20f,
    float RouteStaminaThreshold = 12f,
    int RoutingTicks = 6)
{
    public static ArmySupplyOptions Default { get; } = new();

    internal ArmySupplyOptions Normalized()
        => this with
        {
            FoodConsumedPerPersonPerSecond = ClampFinite(FoodConsumedPerPersonPerSecond, 0f, 100f),
            LowSupplyFoodPerPersonThreshold = ClampFinite(LowSupplyFoodPerPersonThreshold, 0f, 100f),
            LowSupplyMoraleLossPerSecond = ClampFinite(LowSupplyMoraleLossPerSecond, 0f, 100f),
            OutOfSupplyMoraleLossPerSecond = ClampFinite(OutOfSupplyMoraleLossPerSecond, 0f, 100f),
            OutOfSupplyStaminaLossPerSecond = ClampFinite(OutOfSupplyStaminaLossPerSecond, 0f, 100f),
            RouteAfterOutOfSupplyTicks = Math.Max(1, RouteAfterOutOfSupplyTicks),
            RouteMoraleThreshold = ClampFinite(RouteMoraleThreshold, 0f, 100f),
            RouteStaminaThreshold = ClampFinite(RouteStaminaThreshold, 0f, 100f),
            RoutingTicks = Math.Max(1, RoutingTicks)
        };

    private static float ClampFinite(float value, float min, float max)
        => float.IsFinite(value) ? Math.Clamp(value, min, max) : min;
}

public sealed record ArmySupplyTickResult(
    int ActiveMemberCount,
    int CarriedFoodBefore,
    int CarriedFoodAfter,
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

public static class ArmySupplyModel
{
    public static ArmySupplyTickResult Tick(
        IReadOnlyList<Person> members,
        ArmySupplyState state,
        float dt,
        ArmySupplyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(state);

        var activeMembers = members
            .Where(member => member.Health > 0f)
            .OrderBy(member => member.Id)
            .ToList();
        var carriedFoodBefore = CountCarriedFood(activeMembers);

        if (dt <= 0f || activeMembers.Count == 0)
        {
            return new ArmySupplyTickResult(
                ActiveMemberCount: activeMembers.Count,
                CarriedFoodBefore: carriedFoodBefore,
                CarriedFoodAfter: carriedFoodBefore,
                FoodDemandUnits: 0,
                FoodConsumed: 0,
                UnmetFoodDemandUnits: 0,
                FractionalFoodDemand: state.FractionalFoodDemand,
                IsLowSupply: false,
                IsOutOfSupply: false,
                SustainedOutOfSupplyTicks: state.SustainedOutOfSupplyTicks,
                AttritionEventCount: 0,
                RoutedMemberCount: 0,
                MoraleDeltaApplied: 0f,
                StaminaDeltaApplied: 0f);
        }

        var normalized = (options ?? ArmySupplyOptions.Default).Normalized();
        var demand = (activeMembers.Count * normalized.FoodConsumedPerPersonPerSecond * dt) + state.FractionalFoodDemand;
        var foodDemandUnits = Math.Max(0, (int)MathF.Floor(demand));
        state.SetFractionalFoodDemand(demand - foodDemandUnits);

        var consumed = ConsumeFood(activeMembers, foodDemandUnits);
        var unmet = Math.Max(0, foodDemandUnits - consumed);
        var carriedFoodAfter = CountCarriedFood(activeMembers);
        var isLowSupply = carriedFoodAfter > 0
                          && carriedFoodAfter < activeMembers.Count * normalized.LowSupplyFoodPerPersonThreshold;
        var isOutOfSupply = unmet > 0;
        state.RecordSupplyPressure(isOutOfSupply);

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
        if (isOutOfSupply && state.SustainedOutOfSupplyTicks >= normalized.RouteAfterOutOfSupplyTicks)
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

        return new ArmySupplyTickResult(
            ActiveMemberCount: activeMembers.Count,
            CarriedFoodBefore: carriedFoodBefore,
            CarriedFoodAfter: carriedFoodAfter,
            FoodDemandUnits: foodDemandUnits,
            FoodConsumed: consumed,
            UnmetFoodDemandUnits: unmet,
            FractionalFoodDemand: state.FractionalFoodDemand,
            IsLowSupply: isLowSupply,
            IsOutOfSupply: isOutOfSupply,
            SustainedOutOfSupplyTicks: state.SustainedOutOfSupplyTicks,
            AttritionEventCount: attritionEvents,
            RoutedMemberCount: routed,
            MoraleDeltaApplied: moraleDelta,
            StaminaDeltaApplied: staminaDelta);
    }

    private static int CountCarriedFood(IEnumerable<Person> members)
        => members.Sum(member => member.Inventory.GetCount(ItemType.Food));

    private static int ConsumeFood(IEnumerable<Person> members, int demand)
    {
        var remaining = Math.Max(0, demand);
        var consumed = 0;
        foreach (var member in members)
        {
            if (remaining <= 0)
                break;

            var carried = member.Inventory.GetCount(ItemType.Food);
            var take = Math.Min(carried, remaining);
            if (take <= 0)
                continue;

            if (!member.Inventory.TryRemove(ItemType.Food, take))
                continue;

            remaining -= take;
            consumed += take;
        }

        return consumed;
    }
}
