using System;
using System.IO;
using System.Linq;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave4ColonyEquipmentTests
{
    [Fact]
    public void ColonyEquipmentLevels_AreClampedBetweenZeroAndThree()
    {
        var colony = new Colony(0, (2, 2));

        colony.SetWeaponLevelClamped(-4);
        colony.SetArmorLevelClamped(99);

        Assert.Equal(0, colony.WeaponLevel);
        Assert.Equal(3, colony.ArmorLevel);
    }

    [Fact]
    public void TechUnlock_SetsWeaponAndArmorLevels()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 30, height: 20, initialPop: 12, randomSeed: 7101)
        {
            AllowFreeTechUnlocks = true
        };
        var colony = world._colonies[0];

        Assert.True(TechTree.TryUnlock("weaponry", world, colony).Success);
        Assert.True(TechTree.TryUnlock("armor_smithing", world, colony).Success);

        Assert.Equal(1, colony.WeaponLevel);
        Assert.Equal(1, colony.ArmorLevel);
    }

    [Fact]
    public void DifferentWeaponLevels_ProduceDifferentCombatDamage()
    {
        var world = new World(width: 30, height: 20, initialPop: 12, randomSeed: 7102);
        var colonyHigh = world._colonies[0];
        var colonyLow = world._colonies[1];

        var attackerHigh = world._people.First(person => person.Home == colonyHigh);
        var attackerLow = world._people.First(person => person.Home == colonyLow);
        var defender = world._people.First(person => person.Home == world._colonies[2]);

        attackerHigh.Profession = Profession.Hunter;
        attackerLow.Profession = Profession.Hunter;
        defender.Profession = Profession.Hunter;
        colonyHigh.SetWeaponLevelClamped(3);
        colonyLow.SetWeaponLevelClamped(0);
        defender.Home.SetArmorLevelClamped(0);
        world.CombatDamageBonusMultiplier = 1f;
        world.CombatDefenseBonusMultiplier = 1f;

        var highDamage = attackerHigh.ScaleOutgoingCombatDamage(world, 10f);
        var lowDamage = attackerLow.ScaleOutgoingCombatDamage(world, 10f);

        defender.Health = 100f;
        defender.ApplyCombatDamage(world, highDamage, "TestHigh");
        var highDelta = 100f - defender.Health;

        defender.Health = 100f;
        defender.ApplyCombatDamage(world, lowDamage, "TestLow");
        var lowDelta = 100f - defender.Health;

        Assert.True(highDamage > lowDamage);
        Assert.True(highDelta > lowDelta);
    }

    [Fact]
    public void HigherArmorLevel_ReducesIncomingCombatDamage_ForWarrior()
    {
        var world = new World(width: 30, height: 20, initialPop: 12, randomSeed: 7103)
        {
            CombatDefenseBonusMultiplier = 1f
        };
        var defender = world._people.First();
        defender.Profession = Profession.Hunter;

        defender.Home.SetArmorLevelClamped(0);
        defender.Health = 100f;
        defender.ApplyCombatDamage(world, 20f, "TestA0");
        var damageAtZero = 100f - defender.Health;

        defender.Home.SetArmorLevelClamped(3);
        defender.Health = 100f;
        defender.ApplyCombatDamage(world, 20f, "TestA3");
        var damageAtThree = 100f - defender.Health;

        Assert.True(damageAtThree < damageAtZero);
    }

    [Fact]
    public void Snapshot_ExportsColonyEquipmentLevels()
    {
        var world = new World(width: 30, height: 20, initialPop: 12, randomSeed: 7104);
        var colony = world._colonies[0];
        colony.SetWeaponLevelClamped(2);
        colony.SetArmorLevelClamped(1);

        var snapshot = WorldSnapshotBuilder.Build(world);
        var colonyView = snapshot.Colonies.First(entry => entry.Id == colony.Id);

        Assert.Equal(2, colonyView.WeaponLevel);
        Assert.Equal(1, colonyView.ArmorLevel);
    }

    private static string GetTechPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var techPath = Path.Combine(current.FullName, "Tech", "technologies.json");
            if (File.Exists(techPath))
                return techPath;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json");
    }
}
