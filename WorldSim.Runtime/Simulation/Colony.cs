using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Simulation
{
    public enum Faction
    {
        Sylvars,
        Obsidari,
        Aetheri,
        Chirita
    }

    public class Colony
    {
        public int Id { get; }
        public (int x, int y) Origin;
        public Faction Faction { get; }
        public string Name { get; }

        public Dictionary<Resource, int> Stock = new()
        {
            [Resource.Wood] = 0,
            [Resource.Stone] = 0,
            [Resource.Iron] = 0,
            [Resource.Gold] = 0,
            [Resource.Food] = 0
        };
        public int HouseCount = 0;
        public int HouseWoodCost { get; set; } = 50;
        public int HouseStoneCost { get; set; } = 15;
        public bool CanBuildWithStone { get; set; } = false;
        public float MovementSpeedMultiplier { get; set; } = 1.0f;
        public HashSet<string> UnlockedTechs { get; } = new();
        public float Morale { get; private set; } = 55f;
        public float WoodGatherMultiplier { get; private set; } = 1f;
        public float StoneGatherMultiplier { get; private set; } = 1f;
        public float IronGatherMultiplier { get; private set; } = 1f;
        public float GoldGatherMultiplier { get; private set; } = 1f;
        public float FoodGatherMultiplier { get; private set; } = 1f;
        public float ColonyWorkMultiplier { get; private set; } = 1f;
        public int FoodReserveBonus { get; private set; }
        public int ToolCharges { get; set; }
        public int FarmPlotCount { get; private set; }
        public int WorkshopCount { get; private set; }
        public int StorehouseCount { get; private set; }

        float _age;
        readonly float _baseWoodGatherMultiplier = 1f;
        readonly float _baseStoneGatherMultiplier = 1f;
        readonly float _baseIronGatherMultiplier = 1f;
        readonly float _baseGoldGatherMultiplier = 1f;
        readonly float _baseFoodGatherMultiplier = 1f;

        public Colony(int id, (int, int) startPos)
        {
            Id = id;
            Origin = startPos;
            (Faction, Name) = id switch
            {
                0 => (Faction.Sylvars, "Sylvars"),
                1 => (Faction.Obsidari, "Obsidari"),
                2 => (Faction.Aetheri, "Aetheri"),
                3 => (Faction.Chirita, "Chirita"),
                _ => (Faction.Sylvars, $"Colony{id}")
            };

            switch (Faction)
            {
                case Faction.Sylvars:
                    FoodGatherMultiplier = 1.2f;
                    WoodGatherMultiplier = 1.05f;
                    break;
                case Faction.Obsidari:
                    StoneGatherMultiplier = 1.2f;
                    IronGatherMultiplier = 1.1f;
                    WoodGatherMultiplier = 0.95f;
                    break;
            }

            _baseWoodGatherMultiplier = WoodGatherMultiplier;
            _baseStoneGatherMultiplier = StoneGatherMultiplier;
            _baseIronGatherMultiplier = IronGatherMultiplier;
            _baseGoldGatherMultiplier = GoldGatherMultiplier;
            _baseFoodGatherMultiplier = FoodGatherMultiplier;
        }

        public void UpdateInfrastructure(int farms, int workshops, int storehouses)
        {
            FarmPlotCount = Math.Max(0, farms);
            WorkshopCount = Math.Max(0, workshops);
            StorehouseCount = Math.Max(0, storehouses);

            var farmBonus = FarmPlotCount * 0.08f;
            var workshopGatherBonus = WorkshopCount * 0.05f;

            FoodGatherMultiplier = _baseFoodGatherMultiplier + farmBonus + workshopGatherBonus;
            WoodGatherMultiplier = _baseWoodGatherMultiplier + workshopGatherBonus;
            StoneGatherMultiplier = _baseStoneGatherMultiplier + workshopGatherBonus;
            IronGatherMultiplier = _baseIronGatherMultiplier + workshopGatherBonus;
            GoldGatherMultiplier = _baseGoldGatherMultiplier + workshopGatherBonus;
            ColonyWorkMultiplier = 1f + WorkshopCount * 0.08f;
            FoodReserveBonus = StorehouseCount * 6;
        }

        public void Update(World world, float dt)
        {
            _age += dt;

            int people = world._people.Count(p => p.Home == this);
            if (people == 0)
            {
                Morale = Math.Max(5f, Morale - dt * 0.3f);
                return;
            }

            float foodPerCapita = Stock[Resource.Food] / (float)Math.Max(1, people);
            int capacity = Math.Max(1, HouseCount * world.HouseCapacity);
            float housingPressure = people / (float)capacity;
            int criticalHungry = world._people.Count(p => p.Home == this && p.Needs.GetValueOrDefault("Hunger", 0f) >= 85f);
            float criticalRatio = criticalHungry / (float)people;

            float factionBaseline = Faction switch
            {
                Faction.Sylvars => 58f,
                Faction.Obsidari => 54f,
                _ => 52f
            };

            float target = factionBaseline;
            target += Math.Clamp((foodPerCapita - 2f) * 7f, -20f, 20f);
            target -= Math.Max(0f, housingPressure - 1f) * 28f;
            target -= criticalRatio * 35f;

            target = Math.Clamp(target, 5f, 95f);
            float blend = 0.12f;
            Morale = Math.Clamp(Morale + (target - Morale) * blend, 0f, 100f);
        }
    }
}
