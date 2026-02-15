using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace WorldSim.Simulation;

public abstract class Animal
{
    public (int x, int y) Pos;
    public abstract Color Color { get; }

    public bool IsAlive { get; internal protected set; } = true;

    protected int Speed { get; init; }
    protected int Vision { get; init; }

    protected readonly Random _rng = new();

    // Toggle that enforces StepTowards only occurring every second invocation.
    // True means the next StepTowards call is allowed; after allowing it we set it to false.
    private bool _allowStep = true;

    protected Animal((int x, int y) pos, int speed, int vision)
    {
        Pos = pos;
        Speed = Math.Max(1, speed);
        Vision = Math.Max(1, vision);
    }

    // Chance [0..1) to actually perform a RandomStep when RandomStep is invoked.
    // Default 1.0 (always do RandomStep). Subclasses can override to reduce wandering frequency.
    protected virtual double RandomStepChance => 0.7;

    // Factory: produces either Herbivore (prey) or Predator (carnivore)
    public static Animal Spawn((int x, int y) pos)
    {
        // Simple ratio: 70% herbivores, 30% predators
        return new Random().NextDouble() < 0.7
            ? new Herbivore(pos)
            : new Predator(pos);
    }

    public abstract void Update(World w, float dt);

    protected static int Manhattan((int x, int y) a, (int x, int y) b)
        => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    protected void StepTowards(World w, (int x, int y) target, int maxStep)
    {
        // Enforce "only move every other tick" semantics.
        // If not allowed this tick, flip the toggle so next call is allowed and skip movement now.
        if (!_allowStep)
        {
            _allowStep = true;
            return;
        }

        // Consume this tick's movement allowance.
        _allowStep = false;

        int remaining = Math.Max(1, maxStep);
        int cx = Pos.x, cy = Pos.y;

        while (remaining-- > 0 && (cx != target.x || cy != target.y))
        {
            int dx = target.x - cx;
            int dy = target.y - cy;

            int nx = cx, ny = cy;
            if (Math.Abs(dx) >= Math.Abs(dy))
                nx += Math.Sign(dx);
            else
                ny += Math.Sign(dy);

            nx = Math.Clamp(nx, 0, w.Width - 1);
            ny = Math.Clamp(ny, 0, w.Height - 1);

            if (w.GetTile(nx, ny).Ground != Ground.Water)
            {
                cx = nx;
                cy = ny;
            }
            else
            {
                break; // stop if water blocks the way
            }
        }

        Pos = (cx, cy);
    }

    protected void RandomStep(World w, int maxStep)
    {
        // Probabilistic early-out: only attempt a random step with some chance.
        if (_rng.NextDouble() >= RandomStepChance)
            return;

        // Align animal wander speed with people: at most 1 tile step (4/8-neighborhood)
        int step = 1;

        int tries = 6;
        for (int i = 0; i < tries; i++)
        {
            int nx = Math.Clamp(Pos.x + _rng.Next(-step, step + 1), 0, w.Width - 1);
            int ny = Math.Clamp(Pos.y + _rng.Next(-step, step + 1), 0, w.Height - 1);
            if (w.GetTile(nx, ny).Ground != Ground.Water)
            {
                Pos = (nx, ny);
                return;
            }
        }
        // if blocked by water around, stay in place
    }
}

public sealed class Herbivore : Animal
{
    // Light green for visibility
    public override Color Color => Color.LightGreen;

    public Herbivore((int x, int y) pos) : base(pos, speed: 1, vision: 5) { }

    // Herbivores wander less frequently
    protected override double RandomStepChance => 0.5;

    public override void Update(World w, float dt)
    {
        if (!IsAlive) return;

        // If spawned on water, immediately disappear
        if (w.GetTile(Pos.x, Pos.y).Ground == Ground.Water)
        {
            IsAlive = false;
            return;
        }

        // Look for nearest predator in vision
        Predator? nearestPred = null;
        int best = int.MaxValue;

        foreach (var a in w._animals)
        {
            if (a is Predator p && p.IsAlive)
            {
                int d = Manhattan(Pos, p.Pos);
                if (d < best)
                {
                    best = d;
                    nearestPred = p;
                }
            }
        }

        if (nearestPred != null && best <= Vision)
        {
            // Flee: step in opposite direction from predator (no extra speed boost)
            var (px, py) = nearestPred.Pos;
            var target = (x: Pos.x + Math.Clamp(Pos.x - px, -1, 1) * Speed,
                          y: Pos.y + Math.Clamp(Pos.y - py, -1, 1) * Speed);
            target = (Math.Clamp(target.x, 0, w.Width - 1), Math.Clamp(target.y, 0, w.Height - 1));
            StepTowards(w, target, Speed);
        }
        else if (TryEatNearbyFood(w))
        {
            // staying fed keeps herbivores clustered around food spots
        }
        else if (TryMoveTowardsFood(w))
        {
            // move purposefully to food when calm
        }
        else
        {
            // Wander calmly
            RandomStep(w, Speed);
        }   
    }

    private bool TryEatNearbyFood(World w)
    {
        return w.TryHarvest(Pos, Resource.Food, 1);
    }

    private bool TryMoveTowardsFood(World w)
    {
        (int x, int y)? bestPos = null;
        int bestDist = int.MaxValue;

        for (int dy = -Vision; dy <= Vision; dy++)
        {
            for (int dx = -Vision; dx <= Vision; dx++)
            {
                int nx = Pos.x + dx;
                int ny = Pos.y + dy;
                if (nx < 0 || ny < 0 || nx >= w.Width || ny >= w.Height) continue;

                int md = Math.Abs(dx) + Math.Abs(dy);
                if (md == 0 || md > Vision) continue;

                var node = w.GetTile(nx, ny).Node;
                if (node == null || node.Type != Resource.Food || node.Amount <= 0) continue;

                if (md < bestDist)
                {
                    bestDist = md;
                    bestPos = (nx, ny);
                }
            }
        }

        if (bestPos == null)
            return false;

        StepTowards(w, bestPos.Value, Speed);
        return true;
    }
}

public sealed class Predator : Animal
{
    // Red for visibility
    public override Color Color => Color.Red;


    public Predator((int x, int y) pos) : base(pos, speed: 1, vision: 6) { }

    // Mild nerf so herbivores do not collapse too early.
    private const double CaptureSuccessChance = 0.65;

    // Predators wander a bit more often than herbivores
    protected override double RandomStepChance => 0.8;

    public override void Update(World w, float dt)
    {
        if (!IsAlive) return;

        // Seek nearest herbivore in vision
        Herbivore? nearestPrey = null;
        int best = int.MaxValue;

        foreach (var a in w._animals)
        {
            if (a is Herbivore h && h.IsAlive)
            {
                int d = Manhattan(Pos, h.Pos);
                if (d < best)
                {
                    best = d;
                    nearestPrey = h;
                }
            }
        }

        if (nearestPrey != null && best <= Vision)
        {
            // Chase
            StepTowards(w, nearestPrey.Pos, Speed);

            // Capture if on same tile
            if (Pos == nearestPrey.Pos && nearestPrey.IsAlive && _rng.NextDouble() < CaptureSuccessChance)
            {
                nearestPrey.IsAlive = false; // prey is removed by World after updates
            }
        }
        else
        {
            // Patrol/wander
            RandomStep(w, Speed);
        }
    }
}
