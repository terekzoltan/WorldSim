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
        public static HashSet<string> Unlocked { get; } = new();

        public static void Load(string path)
        {
            if (!File.Exists(path)) return;
            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<TechData>(json);
            if (data?.Techs != null)
                Techs = data.Techs;
        }

        public static void Unlock(string id, World world)
        {
            if (Unlocked.Contains(id)) return;
            Technology? tech = Techs.FirstOrDefault(t => t.Id == id);
            if (tech == null) return;
            Unlocked.Add(id);
            ApplyEffect(tech, world);
        }

        static void ApplyEffect(Technology tech, World world)
        {
            switch (tech.Effect)
            {
                case "extra_wood":
                    world.WoodYield = 2;
                    break;
                case "cheap_houses":
                    foreach (var c in world._colonies)
                        c.HouseWoodCost = 5;
                    break;
            }
        }
    }
}
#nullable disable
