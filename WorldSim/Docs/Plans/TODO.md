


- IDEIG DOLGOZZON NE INSTANT (X)

- IRON, GOLD belerak, megerteni distributeation

- Tervezési mintákkal + Ágensterv elkezd(most meg processing)


Tervezesi mintak:
-STRATEGY, OBSERVER, DI?, FACTORY, STATE, (Template Method, Mediator, Memento)




viz generalas ilyesmi:

void GenerateLake(Tile[,] map, int startX, int startY, int size)
{
    Random rnd = new Random();
    Queue<(int, int)> queue = new Queue<(int, int)>();
    queue.Enqueue((startX, startY));
    int placed = 0;

    while (queue.Count > 0 && placed < size)
    {
        var (x, y) = queue.Dequeue();
        if (x < 0 || y < 0 || x >= map.GetLength(0) || y >= map.GetLength(1)) continue;

        if (map[x, y].Type != TileType.Water) 
        {
            map[x, y].Type = TileType.Water;
            placed++;
            // Add neighbors randomly
            foreach (var (dx, dy) in new (int, int)[]{(1,0),(-1,0),(0,1),(0,-1)})
            {
                if (rnd.NextDouble() < 0.7) // 70% chance to spread
                    queue.Enqueue((x + dx, y + dy));
            }
        }
    }
}
