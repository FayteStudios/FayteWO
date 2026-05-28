using FayteWO.Shared.World;

namespace FayteWO.Shared.Networking.Packets;

public sealed record TileChangedPacket(
    TilePosition Position,
    int TileId);