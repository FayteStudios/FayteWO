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

Chunk chunk = new Chunk(0, 0);

// Fill the chunk with grass.
for (int x = 0; x < Chunk.Size; x++)
{
    for (int y = 0; y < Chunk.Size; y++)
    {
        chunk.SetTileId(x, y, grass.TileId);
    }
}

// Put a wall at world/local position 2,0.
chunk.SetTileId(2, 0, wall.TileId);

PlayerState player = new PlayerState(
    Guid.NewGuid(),
    "TestPlayer",
    new TilePosition(0, 0, 0));

MovementSystem movementSystem = new MovementSystem(tileDefinitions);

Console.WriteLine($"Spawned player: {player}");

TryMoveAndPrint(Direction.East);
TryMoveAndPrint(Direction.East);
TryMoveAndPrint(Direction.East);

Console.WriteLine($"Final player state: {player}");
Console.WriteLine("Server test complete.");

void TryMoveAndPrint(Direction direction)
{
    Console.WriteLine();
    Console.WriteLine($"Trying to move {direction}...");

    bool moved = movementSystem.TryMove(player, direction, chunk, out string reason);

    Console.WriteLine(moved ? "Move accepted." : "Move rejected.");
    Console.WriteLine(reason);
    Console.WriteLine($"Player position: {player.Position}");
}