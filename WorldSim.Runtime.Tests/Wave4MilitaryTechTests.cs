using System;
using System.IO;
using System.Linq;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave4MilitaryTechTests
{
    [Fact]
    public void TechnologyFile_ContainsWave4MilitaryAndFortificationTechs()
    {
        TechTree.Load(GetTechPath());

        var requiredIds = new[]
        {
            "weaponry",
            "armor_smithing",
            "military_training",
            "war_drums",
            "scouts",
            "advanced_tactics",
            "fortification",
            "advanced_fortification",
            "siege_craft"
        };

        foreach (var id in requiredIds)
            Assert.Contains(TechTree.Techs, tech => tech.Id == id);

        Assert.Contains("mining", TechTree.Techs.First(tech => tech.Id == "weaponry").Prerequisites);
        Assert.Contains("weaponry", TechTree.Techs.First(tech => tech.Id == "armor_smithing").Prerequisites);
        Assert.Contains("construction", TechTree.Techs.First(tech => tech.Id == "military_training").Prerequisites);
        Assert.Contains("construction", TechTree.Techs.First(tech => tech.Id == "fortification").Prerequisites);
        Assert.Contains("fortification", TechTree.Techs.First(tech => tech.Id == "advanced_fortification").Prerequisites);
    }

    [Fact]
    public void UnlockingFortificationTech_EnablesFortificationGate()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 32, height: 20, initialPop: 12, randomSeed: 6001)
        {
            RequireFortificationTechUnlock = true,
            AllowFreeTechUnlocks = true
        };
        var colony = world._colonies[0];

        Assert.False(world.CanBuildFortifications(colony));

        var unlock = TechTree.TryUnlock("fortification", world, colony);

        Assert.True(unlock.Success);
        Assert.True(colony.FortificationsUnlocked);
        Assert.True(world.CanBuildFortifications(colony));
    }

    [Fact]
    public void UnlockingMilitaryEffects_UpdatesRuntimeModifiers()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 32, height: 20, initialPop: 12, randomSeed: 6002)
        {
            AllowFreeTechUnlocks = true
        };
        var colony = world._colonies[0];

        Assert.Equal(1f, world.CombatDamageBonusMultiplier);
        Assert.Equal(1f, world.CombatDefenseBonusMultiplier);
        Assert.False(colony.WarriorRoleUnlocked);

        Assert.True(TechTree.TryUnlock("weaponry", world, colony).Success);
        Assert.True(TechTree.TryUnlock("armor_smithing", world, colony).Success);
        Assert.True(TechTree.TryUnlock("military_training", world, colony).Success);
        Assert.True(TechTree.TryUnlock("war_drums", world, colony).Success);
        Assert.True(TechTree.TryUnlock("scouts", world, colony).Success);
        Assert.True(TechTree.TryUnlock("advanced_tactics", world, colony).Success);
        Assert.True(TechTree.TryUnlock("siege_craft", world, colony).Success);

        Assert.True(world.CombatDamageBonusMultiplier > 1f);
        Assert.True(world.CombatDefenseBonusMultiplier > 1f);
        Assert.True(world.SiegeDamageMultiplier > 1f);
        Assert.True(colony.WarriorRoleUnlocked);
        Assert.True(colony.CombatMoraleBonus > 0f);
        Assert.True(colony.ScoutRadiusBonus > 0);
        Assert.True(colony.FormationsUnlocked);
    }

    private static string GetTechPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var path = Path.Combine(current.FullName, "Tech", "technologies.json");
            if (File.Exists(path))
                return path;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json");
    }
}
