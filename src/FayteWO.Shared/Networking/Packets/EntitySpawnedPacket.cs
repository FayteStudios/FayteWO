using FayteWO.Shared.World;

namespace FayteWO.Shared.Networking.Packets;

public sealed record EntitySpawnedPacket(
    Guid EntityId,
    string Name,
    TilePosition Position);