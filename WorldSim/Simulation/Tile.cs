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
        public Resource Type { get; private set; }
        public int Amount { get; private set; }

        public Tile(Resource type, int amount)
        {
            Type = type;
            Amount = amount;
        }

        /// <summary>
        /// Tries to harvest the specified quantity of a given resource.
        /// </summary>
        /// <param name="res">The type of resource requested.</param>
        /// <param name="qty">How much to harvest.</param>
        /// <returns>
        /// True if this tile contains the requested resource and has
        /// at least <paramref name="qty"/> amount available; otherwise false.
        /// </returns>
        public bool Harvest(Resource res, int qty)
        {
            if (Type == res && Amount >= qty)
            {
                Amount -= qty;

                // When depleted, convert this tile to empty ground
                if (Amount == 0)
                    Type = Resource.None;

                return true;
            }
            return false;
        }
    }
}
