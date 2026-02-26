namespace WorldSim.Simulation.Navigation;

public sealed class NavigationGrid
{
    private readonly World _world;

    public NavigationGrid(World world)
    {
        _world = world;
    }

    public int Width => _world.Width;
    public int Height => _world.Height;
    public int TopologyVersion => _world.NavigationTopologyVersion;

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
}
