using Microsoft.Xna.Framework;
using System;

namespace WorldSim.Simulation;

public class Animal
{
    public (int x, int y) Pos;
    public Color Color => Color.Brown;

    readonly Random _rng = new();

    private Animal((int, int) pos)
    {
        Pos = pos;
    }

    public static Animal Spawn((int, int) pos) => new Animal(pos);

    public void Update(World w, float dt)
    {
        // Simple random walk (1 tile step)
        int step = 1;
        Pos = (
            Math.Clamp(Pos.x + _rng.Next(-step, step + 1), 0, w.Width - 1),
            Math.Clamp(Pos.y + _rng.Next(-step, step + 1), 0, w.Height - 1)
        );
    }
}