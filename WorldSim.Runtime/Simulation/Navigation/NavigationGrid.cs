namespace WorldSim.Simulation.Navigation;

public sealed class NavigationGrid
{
    private readonly World _world;
    private int _topologyVersion;
    private int _lastTopologySignature = int.MinValue;

    public NavigationGrid(World world)
    {
        _world = world;
    }

    public int Width => _world.Width;
    public int Height => _world.Height;
    public int TopologyVersion
    {
        get
        {
            int signature = ComputeTopologySignature();
            if (signature != _lastTopologySignature)
            {
                _lastTopologySignature = signature;
                _topologyVersion++;
            }

            return _topologyVersion;
        }
    }

    public bool InBounds(int x, int y)
        => x >= 0 && y >= 0 && x < _world.Width && y < _world.Height;

    public bool IsBlocked(int x, int y, int moverColonyId)
    {
        if (!InBounds(x, y))
            return true;

        if (_world.GetTile(x, y).Ground == Ground.Water)
            return true;

        if (_world.Houses.Any(h => h.Pos.x == x && h.Pos.y == y))
            return true;

        if (_world.SpecializedBuildings.Any(b => b.Pos.x == x && b.Pos.y == y))
            return true;

        return false;
    }

    private int ComputeTopologySignature()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + _world.Width;
            hash = (hash * 31) + _world.Height;

            foreach (var house in _world.Houses)
            {
                hash = (hash * 31) + house.Pos.x;
                hash = (hash * 31) + house.Pos.y;
                hash = (hash * 31) + house.Owner.Id;
            }

            foreach (var building in _world.SpecializedBuildings)
            {
                hash = (hash * 31) + building.Pos.x;
                hash = (hash * 31) + building.Pos.y;
                hash = (hash * 31) + building.Owner.Id;
                hash = (hash * 31) + (int)building.Kind;
            }

            return hash;
        }
    }
}
