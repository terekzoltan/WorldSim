using System.Linq;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class PersonInventoryTests
{
    [Fact]
    public void NewInventory_HasDefaultCapacityOfThree()
    {
        var inventory = new PersonInventory();

        Assert.Equal(PersonInventory.DefaultCapacitySlots, inventory.BaseCapacitySlots);
        Assert.Equal(3, inventory.CapacitySlots);
        Assert.Equal(0, inventory.UsedSlots);
        Assert.Equal(3, inventory.FreeSlots);
    }

    [Fact]
    public void Inventory_CanAddAndRemoveFood()
    {
        var inventory = new PersonInventory();

        Assert.True(inventory.TryAdd(ItemType.Food, 2));
        Assert.Equal(2, inventory.GetCount(ItemType.Food));
        Assert.Equal(2, inventory.UsedSlots);
        Assert.Equal(1, inventory.FreeSlots);

        Assert.True(inventory.TryRemove(ItemType.Food));
        Assert.Equal(1, inventory.GetCount(ItemType.Food));
        Assert.Equal(1, inventory.UsedSlots);
        Assert.Equal(2, inventory.FreeSlots);
    }

    [Fact]
    public void Inventory_CannotExceedCapacity()
    {
        var inventory = new PersonInventory();

        Assert.True(inventory.TryAdd(ItemType.Food, 3));

        Assert.False(inventory.CanAdd(ItemType.Food));
        Assert.False(inventory.TryAdd(ItemType.Food));
        Assert.Equal(3, inventory.GetCount(ItemType.Food));
        Assert.Equal(3, inventory.UsedSlots);
        Assert.Equal(0, inventory.FreeSlots);
    }

    [Fact]
    public void Inventory_CannotRemoveMoreThanAvailable()
    {
        var inventory = new PersonInventory();
        Assert.True(inventory.TryAdd(ItemType.Food, 1));

        Assert.False(inventory.TryRemove(ItemType.Food, 2));

        Assert.Equal(1, inventory.GetCount(ItemType.Food));
        Assert.Equal(1, inventory.UsedSlots);
    }

    [Fact]
    public void Inventory_InvalidAddOrRemoveCounts_ReturnFalseAndDoNotMutate()
    {
        var inventory = new PersonInventory();
        Assert.True(inventory.TryAdd(ItemType.Food, 1));

        Assert.False(inventory.CanAdd(ItemType.Food, 0));
        Assert.False(inventory.TryAdd(ItemType.Food, 0));
        Assert.False(inventory.TryAdd(ItemType.Food, -1));
        Assert.False(inventory.TryRemove(ItemType.Food, 0));
        Assert.False(inventory.TryRemove(ItemType.Food, -1));

        Assert.Equal(1, inventory.GetCount(ItemType.Food));
        Assert.Equal(1, inventory.UsedSlots);
        Assert.Equal(2, inventory.FreeSlots);
    }

    [Fact]
    public void Inventory_CapacityBonusIncreasesEffectiveCapacity()
    {
        var inventory = new PersonInventory();

        inventory.SetCapacityBonusSlots(2);

        Assert.Equal(2, inventory.CapacityBonusSlots);
        Assert.Equal(5, inventory.CapacitySlots);
        Assert.True(inventory.TryAdd(ItemType.Food, 5));
        Assert.Equal(5, inventory.UsedSlots);
        Assert.Equal(0, inventory.FreeSlots);
    }

    [Fact]
    public void Inventory_NegativeCapacityBonusClampsToZero()
    {
        var inventory = new PersonInventory();

        inventory.SetCapacityBonusSlots(-3);

        Assert.Equal(0, inventory.CapacityBonusSlots);
        Assert.Equal(3, inventory.CapacitySlots);
    }

    [Fact]
    public void Inventory_CapacityReductionBelowUsedSlotsDoesNotDeleteItemsAndPreventsFurtherAdds()
    {
        var inventory = new PersonInventory();
        inventory.SetCapacityBonusSlots(2);
        Assert.True(inventory.TryAdd(ItemType.Food, 5));

        inventory.SetCapacityBonusSlots(0);

        Assert.Equal(3, inventory.CapacitySlots);
        Assert.Equal(5, inventory.UsedSlots);
        Assert.Equal(0, inventory.FreeSlots);
        Assert.Equal(5, inventory.GetCount(ItemType.Food));
        Assert.False(inventory.CanAdd(ItemType.Food));
        Assert.False(inventory.TryAdd(ItemType.Food));
        Assert.Equal(5, inventory.GetCount(ItemType.Food));
    }

    [Fact]
    public void Inventory_CanAddAndTryAddUseSameCapacityDecision()
    {
        var inventory = new PersonInventory();

        Assert.Equal(inventory.CanAdd(ItemType.Food, 3), inventory.TryAdd(ItemType.Food, 3));
        Assert.Equal(inventory.CanAdd(ItemType.Food, 1), inventory.TryAdd(ItemType.Food, 1));

        Assert.Equal(3, inventory.GetCount(ItemType.Food));
        Assert.Equal(0, inventory.FreeSlots);
    }

    [Fact]
    public void SpawnedPerson_HasDefaultInventory()
    {
        var world = new World(width: 16, height: 16, initialPop: 4, randomSeed: 5101);
        var person = world._people.First();

        Assert.NotNull(person.Inventory);
        Assert.Equal(3, person.Inventory.CapacitySlots);
        Assert.Equal(0, person.Inventory.UsedSlots);
        Assert.Equal(0, person.Inventory.GetCount(ItemType.Food));
    }
}
