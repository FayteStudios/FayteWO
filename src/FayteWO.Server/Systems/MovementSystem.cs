using FayteWO.Shared.Entities;
using FayteWO.Shared.World;

namespace FayteWO.Server.Systems;

public sealed class MovementSystem
{
    private readonly Dictionary<int, Tile> _tileDefinitions;

    public MovementSystem(IEnumerable<Tile> tileDefinitions)
    {
        _tileDefinitions = tileDefinitions.ToDictionary(tile => tile.TileId);
    }

    public bool TryMove(PlayerState player, Direction direction, Chunk chunk, out string reason)
    {
        TilePosition targetPosition = player.Position.Offset(direction);

        int localX = Chunk.WorldToLocalCoordinate(targetPosition.X);
        int localY = Chunk.WorldToLocalCoordinate(targetPosition.Y);
        int localZ = targetPosition.Z - chunk.ChunkZ;

        if (!chunk.ContainsLocalPosition(localX, localY, localZ))
        {
            reason = $"Target position {targetPosition} is outside the loaded chunk.";
            return false;
        }

        int targetTileId = chunk.GetTileId(localX, localY, localZ);

        if (!_tileDefinitions.TryGetValue(targetTileId, out Tile? targetTile))
        {
            reason = $"Unknown tile ID {targetTileId} at {targetPosition}.";
            return false;
        }

        if (targetTile.BlocksMovement)
        {
            reason = $"Movement blocked by {targetTile.Name} at {targetPosition}.";
            return false;
        }

        player.SetPosition(targetPosition);
        reason = "Movement successful.";
        return true;
    }
}