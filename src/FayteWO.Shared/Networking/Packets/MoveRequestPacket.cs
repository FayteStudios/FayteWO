using FayteWO.Shared.World;

namespace FayteWO.Shared.Networking.Packets;

public sealed record MoveRequestPacket(
    Guid PlayerId,
    Direction Direction);