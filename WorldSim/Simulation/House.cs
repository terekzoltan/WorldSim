using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace WorldSim.Simulation;

public class House
{
    public Colony Owner { get; }
    public (int x, int y) Pos { get; }

    public House(Colony owner, (int x, int y) pos)
    {
        Owner = owner;
        Pos = pos;
    }
}
