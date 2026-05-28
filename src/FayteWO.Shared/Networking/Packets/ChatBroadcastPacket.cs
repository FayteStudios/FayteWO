namespace FayteWO.Shared.Networking.Packets;

public sealed record ChatBroadcastPacket(
    Guid SenderId,
    string SenderName,
    string Message);