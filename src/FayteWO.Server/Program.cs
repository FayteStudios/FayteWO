using FayteWO.Server.Systems;
using FayteWO.Shared.Entities;
using FayteWO.Shared.World;

Console.WriteLine("FayteWO Server Starting...");

Tile grass = new Tile(1, "Grass");
Tile wall = new Tile(2, "Stone Wall", TileFlags.BlocksMovement | TileFlags.BlocksSight);

List<Tile> tileDefinitions =
[
    grass,
    wall
];

WorldMap worldMap = new WorldMap();

Chunk chunk00 = CreateFilledChunk(0, 0, grass.TileId);
Chunk chunk10 = CreateFilledChunk(1, 0, grass.TileId);

worldMap.AddChunk(chunk00);
worldMap.AddChunk(chunk10);

// Put a wall at world position 2,0.
worldMap.TrySetTileId(new TilePosition(2, 0, 0), wall.TileId);

// Put a wall across the chunk boundary at world position 33,0.
worldMap.TrySetTileId(new TilePosition(33, 0, 0), wall.TileId);

PlayerState player = new PlayerState(
    Guid.NewGuid(),
    "TestPlayer",
    new TilePosition(30, 0, 0));

MovementSystem movementSystem = new MovementSystem(worldMap, tileDefinitions);

Console.WriteLine($"Spawned player: {player}");

TryMoveAndPrint(Direction.East); // 31,0
TryMoveAndPrint(Direction.East); // 32,0, crosses into chunk 1,0
TryMoveAndPrint(Direction.East); // 33,0, blocked by wall
TryMoveAndPrint(Direction.West); // 31,0

Console.WriteLine();
Console.WriteLine($"Final player state: {player}");
Console.WriteLine("Server test complete.");

Chunk CreateFilledChunk(int chunkX, int chunkY, int tileId)
{
    Chunk chunk = new Chunk(chunkX, chunkY);

    for (int x = 0; x < Chunk.Size; x++)
    {
        for (int y = 0; y < Chunk.Size; y++)
        {
            chunk.SetTileId(x, y, tileId);
        }
    }

    return chunk;
}

void TryMoveAndPrint(Direction direction)
{
    Console.WriteLine();
    Console.WriteLine($"Trying to move {direction}...");

    bool moved = movementSystem.TryMove(player, direction, out string reason);

    Console.WriteLine(moved ? "Move accepted." : "Move rejected.");
    Console.WriteLine(reason);
    Console.WriteLine($"Player position: {player.Position}");
}