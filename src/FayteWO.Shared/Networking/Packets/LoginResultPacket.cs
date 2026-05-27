using FayteWO.Shared.World;

namespace FayteWO.Shared.Networking.Packets;

public sealed record LoginResultPacket(
    bool Success,
    string Message,
    Guid? PlayerId,
    TilePosition? SpawnPosition);