using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Simulation;

public enum ItemType
{
    Food
}

public sealed class PersonInventory
{
    readonly Dictionary<ItemType, int> _items = new();

    public const int DefaultCapacitySlots = 3;

    public PersonInventory(int baseCapacitySlots = DefaultCapacitySlots)
    {
        BaseCapacitySlots = Math.Max(0, baseCapacitySlots);
    }

    public int BaseCapacitySlots { get; }
    public int CapacityBonusSlots { get; private set; }
    public int CapacitySlots => Math.Max(0, BaseCapacitySlots + CapacityBonusSlots);
    public int UsedSlots => _items.Values.Sum();
    public int FreeSlots => Math.Max(0, CapacitySlots - UsedSlots);

    public int GetCount(ItemType type)
        => _items.GetValueOrDefault(type, 0);

    public bool CanAdd(ItemType type, int count = 1)
        => count > 0 && count <= FreeSlots;

    public bool TryAdd(ItemType type, int count = 1)
    {
        if (!CanAdd(type, count))
            return false;

        _items[type] = GetCount(type) + count;
        return true;
    }

    public bool TryRemove(ItemType type, int count = 1)
    {
        if (count <= 0)
            return false;

        var current = GetCount(type);
        if (count > current)
            return false;

        var remaining = current - count;
        if (remaining == 0)
            _items.Remove(type);
        else
            _items[type] = remaining;

        return true;
    }

    public void SetCapacityBonusSlots(int bonusSlots)
    {
        CapacityBonusSlots = Math.Max(0, bonusSlots);
    }
}
