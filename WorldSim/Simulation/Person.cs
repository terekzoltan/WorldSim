using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation;

public enum Job { Idle, GatherWood, GatherStone, BuildHouse }

public class Person
{
    public (int x, int y) Pos;
    public Job Current = Job.Idle;
    public float Health = 100;
    public float Age = 0;
    public int Strength, Intelligence;
    public Colony Home => _home;
    public Color Color => _home.Color;

    Colony _home;
    Random _rng = new();

    const int WoodWorkTime = 5;
    const int StoneWorkTime = 8;
    const int BuildHouseTime = 20;

    int _doingJob = 0; // csinalni hogy ideig dolgozzon ne instant

    private Person(Colony home, (int, int) pos)
    {
        _home = home;
        Pos = pos;
        Strength = _rng.Next(3, 11);
        Intelligence = _rng.Next(3, 11);
    }

    public static Person Spawn(Colony home, (int, int) pos)
        => new Person(home, pos);

    public static Person SpawnWithBonus(Colony home, (int, int) pos, World world)
    {
        var person = new Person(home, pos);
        person.Strength = Math.Min(20, person.Strength + world.StrengthBonus);
        person.Intelligence = Math.Min(20, person.Intelligence + world.IntelligenceBonus);
        person.Health += world.HealthBonus;
        return person;
    }

    public bool Update(World w, float dt, List<Person> births)
    {
        Age += dt/10;
        if (Age > w.MaxAge)
            return false;

        // simple reproduction chance if there is housing capacity
        int colonyPop = w._people.Count(p => p.Home == _home);
        int capacity = _home.HouseCount * w.HouseCapacity;
        if (Age >= 18 && Age <= 60 && colonyPop < capacity && _rng.NextDouble() < (0.001 * w.BirthRateMultiplier))
        {
            births.Add(Person.SpawnWithBonus(_home, Pos, w));
        }

        if (_doingJob > 0 && Current != Job.Idle)
        {
            _doingJob--;
            if (_doingJob <= 0)
            {
                switch (Current)
                {
                    case Job.GatherWood:
                        if (w.TryHarvest(Pos, Resource.Wood, 1))
                            _home.Stock[Resource.Wood] += w.WoodYield;
                        else
                            Wander(w);
                        break;

                    case Job.GatherStone:
                        if (w.TryHarvest(Pos, Resource.Stone, 1))
                            _home.Stock[Resource.Stone] += w.StoneYield;
                        else
                            Wander(w);
                        break;

                    case Job.BuildHouse:
                        // Szükséges házak száma, de legalább annyi, mint a már meglévő házak (nem csökkenhet a házak száma).
                        int maxHouses = Math.Max(_home.HouseCount, (int)Math.Ceiling((colonyPop + 3) / (double)w.HouseCapacity));
                        if (_home.HouseCount < maxHouses)
                        {
                            if (w.StoneBuildingsEnabled && _home.CanBuildWithStone && _home.Stock[Resource.Stone] >= _home.HouseStoneCost)
                            {
                                _home.Stock[Resource.Stone] -= _home.HouseStoneCost;
                                _home.HouseCount++;
                                w.AddHouse(_home, Pos);
                            }
                            else if (_home.Stock[Resource.Wood] >= _home.HouseWoodCost)
                            {
                                _home.Stock[Resource.Wood] -= _home.HouseWoodCost;
                                _home.HouseCount++;
                                w.AddHouse(_home, Pos);
                            }
                        }
                        break;
                }

                Current = Job.Idle;
            }
        }
        else if (Current == Job.Idle)
        {
            // 1) Ha a jelenlegi tile-on van node → ahhoz illeszkedő munka
            var hereNode = w.GetTile(Pos.x, Pos.y).Node;
            if (hereNode != null && hereNode.Amount > 0)
            {
                if (hereNode.Type == Resource.Wood)
                {
                    Current = Job.GatherWood;
                    _doingJob = Math.Max(1, (int)MathF.Ceiling(WoodWorkTime / w.WorkEfficiencyMultiplier));
                    return true;
                }
                if (hereNode.Type == Resource.Stone)
                {
                    Current = Job.GatherStone;
                    _doingJob = Math.Max(1, (int)MathF.Ceiling(StoneWorkTime / w.WorkEfficiencyMultiplier));
                    return true;
                }
            }

            // 2) Keresd meg a legközelebbi node-ot (kis rádiusz), és lépj felé
            if (TryMoveTowardsNearestResource(w, searchRadius: 3))
            {
                // mozogtunk egyet a cél felé, maradjon Idle, következő tickben újra próbál
                return true;
            }

            // 3) Ha semmi a közelben, akkor fallback (építés/gyűjtés random)
            AssignRandomJob(w);
        }
        return true;
    }

    void AssignRandomJob(World w)
    {
        double r = _rng.NextDouble();
        if (r < 0.4)
        {
            Current = Job.GatherWood;
            _doingJob = Math.Max(1, (int)MathF.Ceiling(WoodWorkTime / w.WorkEfficiencyMultiplier));
        }
        else if (r < 0.8)
        {
            Current = Job.GatherStone;
            _doingJob = Math.Max(1, (int)MathF.Ceiling(StoneWorkTime / w.WorkEfficiencyMultiplier));
        }
        else
        {
            Current = Job.BuildHouse;
            _doingJob = Math.Max(1, (int)MathF.Ceiling(BuildHouseTime / w.WorkEfficiencyMultiplier));
        }
    }

    bool TryMoveTowardsNearestResource(World w, int searchRadius)
    {
        (int x, int y)? bestPos = null;
        int bestDist = int.MaxValue;
        Resource bestType = Resource.None;

        for (int r = 1; r <= searchRadius; r++)
        {
            // Manhattan-gyűrű bejárása
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = Pos.x + dx;
                    int ny = Pos.y + dy;
                    if (nx < 0 || ny < 0 || nx >= w.Width || ny >= w.Height) continue;
                    int md = Math.Abs(dx) + Math.Abs(dy);
                    if (md > r) continue;

                    var node = w.GetTile(nx, ny).Node;
                    if (node == null || node.Amount <= 0) continue;
                    if (node.Type != Resource.Wood && node.Type != Resource.Stone) continue;

                    if (md < bestDist)
                    {
                        bestDist = md;
                        bestPos = (nx, ny);
                        bestType = node.Type;
                    }
                }
            }
            if (bestPos != null) break; // legközelebbi megvan
        }

        if (bestPos == null) return false;

        // Ha már rajta állunk (biztonság), itt is indíthatunk munkát
        if (bestDist == 0)
        {
            if (bestType == Resource.Wood)
            {
                Current = Job.GatherWood;
                _doingJob = Math.Max(1, (int)MathF.Ceiling(WoodWorkTime / w.WorkEfficiencyMultiplier));
            }
            else
            {
                Current = Job.GatherStone;
                _doingJob = Math.Max(1, (int)MathF.Ceiling(StoneWorkTime / w.WorkEfficiencyMultiplier));
            }
            return true;
        }

        // Lépjünk egyet a cél felé (max colony sebességgel)
        MoveTowards(w, bestPos.Value, (int)_home.MovementSpeedMultiplier);
        return true;
    }

    void MoveTowards(World w, (int x, int y) target, int maxStep)
    {
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

            // Csak akkor lépünk, ha nem víz a cél tile
            if (w.GetTile(nx, ny).Ground != Ground.Water)
            {
                cx = nx;
                cy = ny;
            }
            else
            {
                // Ha víz lenne, megállunk
                break;
            }
        }

        Pos = (cx, cy);
    }

    void Wander(World w)
    {
        int moveDistance = (int)_home.MovementSpeedMultiplier;
        int tries = 8;
        for (int i = 0; i < tries; i++)
        {
            int nx = Math.Clamp(Pos.x + _rng.Next(-moveDistance, moveDistance + 1), 0, w.Width - 1);
            int ny = Math.Clamp(Pos.y + _rng.Next(-moveDistance, moveDistance + 1), 0, w.Height - 1);
            if (w.GetTile(nx, ny).Ground != Ground.Water)
            {
                Pos = (nx, ny);
                return;
            }
        }
        // Ha nem találtunk szárazat, maradunk
    }
}
