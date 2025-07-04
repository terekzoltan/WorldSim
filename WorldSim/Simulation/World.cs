using Microsoft.Xna.Framework;
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

        // Amount of wood gathered per successful harvest
        public int WoodYield { get; set; } = 1;

        readonly Random _rng = new();

        public World(int width, int height, int initialPop)
        {
            Width = width;
            Height = height;
            _map = new Tile[width, height];

            // 1. Randomly scatter resources
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    _map[x, y] = new Tile(
                        _rng.NextDouble() < 0.05 ? Resource.Wood :
                        _rng.NextDouble() < 0.02 ? Resource.Stone :
                                                   Resource.None,
                        _rng.Next(20, 100));

            // 2. Multiple colonies on the map (3 as example)
            int colonyCount = 4;
            for (int ci = 0; ci < colonyCount; ci++)
            {
                
                (int, int) colPos = (
                    _rng.Next(Width / 4, Width * 3 / 4) , // spawn near the center of the map
                    _rng.Next(Height / 4, Height * 3 / 4)
                );
                

                Colony col = new Colony(ci, colPos);
                _colonies.Add(col);

                col.Color = ci switch 
                { 
                    0 => Color.Red,    
                    1 => Color.Blue,   
                    2 => Color.Yellow,
                    3 => Color.Purple,
                    _ => Color.White   
                };

                // 3. Generate residents for this colony
                int pop = initialPop / colonyCount;
                for (int i = 0; i < pop; i++)
                    _people.Add(Person.Spawn(col, RandomFreePos()));
            }
        }

        // Tick-based update
        public void Update(float dt)
        {
            List<Person> births = new();
            for (int i = _people.Count - 1; i >= 0; i--)
            {
                if (!_people[i].Update(this, dt, births))
                    _people.RemoveAt(i);
            }
            _people.AddRange(births);
            foreach (Colony c in _colonies) c.Update(dt);
        }

        // Can be tested like this:
        public bool TryHarvest((int x, int y) pos, Resource need, int qty)
        {
            ref Tile tile = ref _map[pos.x, pos.y];
            return tile.Harvest(need, qty);
        }

        (int, int) RandomFreePos()
            => (_rng.Next(Width), _rng.Next(Height));
        /*(int, int) colPos = ci switch
                {
                    0 => (0, 0),
                    1 => (0, 0),
                    2 => (0, 0),
                    3 => (0, 0),
                    _ => (0,0)
                };*/

        /// <summary>Returns the tile at (x,y) from _map.</summary>
        public Tile GetTile(int x, int y)
            => _map[x, y];
    }
}
