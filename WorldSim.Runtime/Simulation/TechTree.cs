#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorldSim.Simulation
{
    public class Technology
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("effect")]
        public string Effect { get; set; } = string.Empty;

        [JsonPropertyName("prerequisites")]
        public List<string> Prerequisites { get; set; } = new();

        [JsonPropertyName("cost")]
        public Dictionary<string, int> Cost { get; set; } = new();
    }

    public readonly record struct TechUnlockResult(bool Success, string Reason)
    {
        public static TechUnlockResult Ok(string reason = "Unlocked") => new(true, reason);
        public static TechUnlockResult Fail(string reason) => new(false, reason);
    }

    public class TechData
    {
        [JsonPropertyName("techs")]
        public List<Technology> Techs { get; set; } = new();
    }

    public static class TechTree
    {
        public static List<Technology> Techs { get; private set; } = new();

        public static void Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Technology file not found.", path);
            }

            string json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<TechData>(json);
            if (data?.Techs != null)
            {
                Techs = data.Techs;
            }

            if (Techs.Count == 0)
            {
                throw new InvalidOperationException(
                    "No technologies were loaded. Check JSON shape and expected 'techs' key."
                );
            }
        }

        public static TechUnlockResult TryUnlock(string id, World world, Colony colony)
        {
            if (colony.UnlockedTechs.Contains(id))
                return TechUnlockResult.Fail("Already unlocked");

            Technology? tech = Techs.FirstOrDefault(t => t.Id == id);
            if (tech == null)
                return TechUnlockResult.Fail("Unknown technology");

            if (!world.AllowFreeTechUnlocks)
            {
                var prereq = ValidatePrerequisites(tech, colony);
                if (!prereq.Success)
                    return prereq;

                var funds = ValidateCost(tech, colony);
                if (!funds.Success)
                    return funds;

                SpendCost(tech, colony);
            }

            colony.UnlockedTechs.Add(id);
            ApplyEffect(tech, world, colony);
            return TechUnlockResult.Ok();
        }

        public static void Unlock(string id, World world, Colony colony)
        {
            _ = TryUnlock(id, world, colony);
        }

        static TechUnlockResult ValidatePrerequisites(Technology tech, Colony colony)
        {
            if (tech.Prerequisites == null || tech.Prerequisites.Count == 0)
                return TechUnlockResult.Ok();

            foreach (var req in tech.Prerequisites)
            {
                if (!colony.UnlockedTechs.Contains(req))
                    return TechUnlockResult.Fail($"Missing prerequisite: {req}");
            }

            return TechUnlockResult.Ok();
        }

        static TechUnlockResult ValidateCost(Technology tech, Colony colony)
        {
            if (tech.Cost == null || tech.Cost.Count == 0)
                return TechUnlockResult.Ok();

            foreach (var kv in tech.Cost)
            {
                if (!TryParseResource(kv.Key, out var resource) || resource is Resource.None or Resource.Water)
                    return TechUnlockResult.Fail($"Invalid cost resource: {kv.Key}");

                int required = Math.Max(0, kv.Value);
                if (colony.Stock.GetValueOrDefault(resource, 0) < required)
                    return TechUnlockResult.Fail($"Insufficient {resource}");
            }

            return TechUnlockResult.Ok();
        }

        static void SpendCost(Technology tech, Colony colony)
        {
            if (tech.Cost == null)
                return;

            foreach (var kv in tech.Cost)
            {
                if (!TryParseResource(kv.Key, out var resource) || resource is Resource.None or Resource.Water)
                    continue;

                int required = Math.Max(0, kv.Value);
                colony.Stock[resource] = Math.Max(0, colony.Stock.GetValueOrDefault(resource, 0) - required);
            }
        }

        static bool TryParseResource(string key, out Resource resource)
            => Enum.TryParse<Resource>(key, true, out resource);

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
                    world.IronYield = 2;
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
                case "golden_trade":
                    world.BirthRateMultiplier = Math.Max(world.BirthRateMultiplier, 1.2f);
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
