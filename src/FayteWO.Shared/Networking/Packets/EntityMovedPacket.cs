using FayteWO.Shared.World;

namespace FayteWO.Shared.Networking.Packets;

public sealed record EntityMovedPacket(
    Guid EntityId,
    TilePosition FromPosition,
    TilePosition ToPosition,
    Direction Direction);