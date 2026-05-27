using FayteWO.Shared.World;

Console.WriteLine("FayteWO Server Starting...");

Tile grass = new Tile(1, "Grass");
Tile wall = new Tile(2, "Stone Wall", TileFlags.BlocksMovement | TileFlags.BlocksSight);

Chunk chunk = new Chunk(0, 0);

chunk.SetTileId(0, 0, grass.TileId);
chunk.SetTileId(1, 0, wall.TileId);

TilePosition start = new TilePosition(0, 0, 0);
TilePosition moved = start.Offset(Direction.East);

Console.WriteLine($"Created tiles: {grass}, {wall}");
Console.WriteLine($"Start position: {start}");
Console.WriteLine($"Moved east to: {moved}");
Console.WriteLine($"Tile at local 0,0: {chunk.GetTileId(0, 0)}");
Console.WriteLine($"Tile at local 1,0: {chunk.GetTileId(1, 0)}");

Console.WriteLine("Server test complete.");