using System;

namespace WorldSim.Simulation
{
    public enum Resource { None, Wood, Stone, Iron, Gold, Food, Water }
    public enum Ground { Dirt, Water, Grass }

    public sealed class ResourceNode
    {
        public Resource Type { get; }
        public int Amount { get; private set; }

        public ResourceNode(Resource type, int amount)
        {
            Type = type;
            Amount = amount;
        }

        public bool Consume(int qty)
        {
            if (Amount < qty) return false;
            Amount -= qty;
            return true;
        }
    }

    public class Tile
    {
        public Ground Ground { get; }
        public ResourceNode? Node { get; private set; }

        public Tile(Ground ground, ResourceNode? node = null)
        {
            Ground = ground;
            Node = node;
        }

        /// <summary>
        /// Tries to harvest from the resource node on this tile (if any).
        /// </summary>
        public bool Harvest(Resource res, int qty)
        {
            if (Node == null) return false;
            if (Node.Type != res) return false;
            if (!Node.Consume(qty)) return false;

            // When depleted, remove the node so the icon disappears.
            if (Node.Amount == 0)
                Node = null;

            return true;
        }
    }
}
