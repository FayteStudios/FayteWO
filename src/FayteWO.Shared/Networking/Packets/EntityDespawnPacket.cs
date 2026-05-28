namespace FayteWO.Shared.Networking.Packets;

public sealed record EntityDespawnedPacket(
    Guid EntityId,
    string Reason);