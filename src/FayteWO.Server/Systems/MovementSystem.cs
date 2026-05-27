using FayteWO.Shared.Entities;
using FayteWO.Shared.World;

namespace FayteWO.Server.Systems;

public sealed class MovementSystem
{
    private readonly WorldMap _worldMap;
    private readonly Dictionary<int, Tile> _tileDefinitions;

    public MovementSystem(WorldMap worldMap, IEnumerable<Tile> tileDefinitions)
    {
        _worldMap = worldMap;
        _tileDefinitions = tileDefinitions.ToDictionary(tile => tile.TileId);
    }

    public bool TryMove(PlayerState player, Direction direction, out string reason)
    {
        TilePosition targetPosition = player.Position.Offset(direction);

        if (!_worldMap.TryGetTileId(targetPosition, out int targetTileId))
        {
            reason = $"Target position {targetPosition} is outside the loaded world.";
            return false;
        }

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