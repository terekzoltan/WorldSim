#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WorldSim.Simulation
{
    public class Technology
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Effect { get; set; } = string.Empty;
    }

    public class TechData
    {
        public List<Technology> Techs { get; set; } = new();
    }

    public static class TechTree
    {
        public static List<Technology> Techs { get; private set; } = new();

        public static void Load(string path)
        {
            if (!File.Exists(path)) return;
            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<TechData>(json);
            if (data?.Techs != null)
                Techs = data.Techs;
        }

        public static void Unlock(string id, World world, Colony colony)
        {
            if (colony.UnlockedTechs.Contains(id)) return;
            Technology? tech = Techs.FirstOrDefault(t => t.Id == id);
            if (tech == null) return;
            colony.UnlockedTechs.Add(id);
            ApplyEffect(tech, world, colony);
        }

        static void ApplyEffect(Technology tech, World world, Colony colony)
        {
            switch (tech.Effect)
            {
                case "extra_wood":
                    world.WoodYield = 2;
                    break;
                case "cheap_houses":
                    colony.HouseWoodCost = 5;
                    break;
                case "better_mining":
                    world.StoneYield = 2;
                    break;
                case "better_farming":
                    world.FoodYield = 2;
                    break;
                case "health_boost":
                    world.HealthBonus = 20;
                    world.MaxAge = 100;
                    break;
                case "work_efficiency":
                    world.WorkEfficiencyMultiplier = 1.5f;
                    break;
                case "more_capacity":
                    world.HouseCapacity = 6;
                    break;
                case "resource_sharing":
                    world.ResourceSharingEnabled = true;
                    break;
                case "smarter_people":
                    world.IntelligenceBonus = 3;
                    break;
                case "stronger_people":
                    world.StrengthBonus = 3;
                    break;
                case "faster_movement":
                    colony.MovementSpeedMultiplier = 2.0f;
                    break;
                case "higher_birthrate":
                    world.BirthRateMultiplier = 2.0f;
                    break;
                case "stone_buildings":
                    colony.CanBuildWithStone = true;
                    break;
            }
        }
    }
}
#nullable disable
