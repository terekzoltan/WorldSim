using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation
{
    public enum Resource { None, Wood, Stone, Food, Water }
    public class Tile
    {
        public Resource Type { get; }
        public int Amount { get; private set; }

        public Tile(Resource type, int amount)
        {
            Type = type;
            Amount = amount;
        }

        /// <summary>
        /// Tries to harvest the specified amount of wood or stone...
        /// </summary>
        public bool Harvest(int qty)
        {
            if (Type != Resource.None && Amount >= qty)
            {
                Amount -= qty;
                return true;
            }
            return false;
        }
    }
}
