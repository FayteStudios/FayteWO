using FayteWO.Server.Systems;
using FayteWO.Shared.Entities;
using FayteWO.Shared.Networking.Packets;
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

worldMap.TrySetTileId(new TilePosition(33, 0, 0), wall.TileId);

PlayerState player = new PlayerState(
    Guid.NewGuid(),
    "TestPlayer",
    new TilePosition(30, 0, 0));

MovementSystem movementSystem = new MovementSystem(worldMap, tileDefinitions);

Console.WriteLine($"Spawned player: {player}");

ChunkDataPacket chunkPacket = new ChunkDataPacket(
    new ChunkPosition(chunk00.ChunkX, chunk00.ChunkY, chunk00.ChunkZ),
    Chunk.Size,
    chunk00.Height,
    chunk00.ToFlatTileIdArray());

Console.WriteLine();
Console.WriteLine($"Created chunk packet for chunk {chunkPacket.ChunkPosition}");
Console.WriteLine($"Chunk packet contains {chunkPacket.TileIds.Length} tile IDs.");

HandleMoveRequest(new MoveRequestPacket(player.PlayerId, Direction.East));
HandleMoveRequest(new MoveRequestPacket(player.PlayerId, Direction.East));
HandleMoveRequest(new MoveRequestPacket(player.PlayerId, Direction.East));

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

void HandleMoveRequest(MoveRequestPacket packet)
{
    Console.WriteLine();
    Console.WriteLine($"Received MoveRequest: Player={packet.PlayerId}, Direction={packet.Direction}");

    if (packet.PlayerId != player.PlayerId)
    {
        Console.WriteLine("Move rejected: unknown player.");
        return;
    }

    TilePosition fromPosition = player.Position;

    bool moved = movementSystem.TryMove(player, packet.Direction, out string reason);

    Console.WriteLine(moved ? "Move accepted." : "Move rejected.");
    Console.WriteLine(reason);

    if (!moved)
    {
        return;
    }

    EntityMovedPacket movedPacket = new EntityMovedPacket(
        player.PlayerId,
        fromPosition,
        player.Position,
        packet.Direction);

    Console.WriteLine(
        $"Created EntityMovedPacket: Entity={movedPacket.EntityId}, From={movedPacket.FromPosition}, To={movedPacket.ToPosition}");
}