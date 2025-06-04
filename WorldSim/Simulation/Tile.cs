using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation
{
    public enum Resource { None, Wood, Stone, Food, Water }
    public struct Tile
    {
        public Resource Type { get; }
        public int Amount { get; private set; }

        public Tile(Resource type, int amount)
        {
            Type = type;
            Amount = amount;
        }

        /// <summary>
        /// Megpróbál kitermelni mennyiség() fát/követ…
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
