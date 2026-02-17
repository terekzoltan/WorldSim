using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation;

public class House
{
    public Colony Owner { get; }
    public (int x, int y) Pos { get; }
    public int Capacity { get; }
    public float Comfort { get; set; } = 1f;

    public House(Colony owner, (int x, int y) pos, int capacity)
    {
        Owner = owner;
        Pos = pos;
        Capacity = Math.Max(1, capacity);
    }
}
