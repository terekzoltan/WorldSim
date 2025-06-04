using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation
{
    public class World
    {
        public readonly int Width, Height;
        Tile[,] _map;
        public List<Person> _people = new();
        public List<Colony> _colonies = new();

        readonly Random _rng = new();

        public World(int width, int height, int initialPop)
        {
            Width = width;
            Height = height;
            _map = new Tile[width, height];

            // 1. Erőforrások véletlen szórása
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    _map[x, y] = new Tile(
                        _rng.NextDouble() < 0.05 ? Resource.Wood :
                        _rng.NextDouble() < 0.02 ? Resource.Stone :
                                                   Resource.None,
                        _rng.Next(20, 100));

            // 2. Egyetlen kolónia a térkép közepén
            // 2. Több kolónia a pályán (3 db példa)
            int colonyCount = 3; // új sor
            for (int ci = 0; ci < colonyCount; ci++) // új sor
            { // új sor
                var colPos = ( // új sor
                    _rng.Next(Width / 4, Width * 3 / 4), // új sor (térkép középmezőjébe spawnol)  
                    _rng.Next(Height / 4, Height * 3 / 4) // új sor
                ); // új sor
                var col = new Colony(ci, colPos); // új sor
                _colonies.Add(col); // új sor
                                    // 3. Emberek legenerálása ehhez a kolóniához // új sor
                int pop = initialPop / colonyCount; // új sor
                for (int i = 0; i < pop; i++) // új sor
                    _people.Add(Person.Spawn(col, RandomFreePos())); // új sor
            }
        }

        // Tick-alapú frissítés
        public void Update(float dt)
        {
            foreach (var p in _people) p.Update(this, dt);
            foreach (var c in _colonies) c.Update(dt);
        }

        // Ki lehet próbálni így:
        public bool TryHarvest((int x, int y) pos, Resource need, int qty)
            => _map[pos.x, pos.y].Harvest(qty);

        (int, int) RandomFreePos()
            => (_rng.Next(Width), _rng.Next(Height));

        /// <summary>Visszaadja az (x,y) tile-t a _map-ből.</summary>
        public Tile GetTile(int x, int y)
            => _map[x, y];
    }
}
