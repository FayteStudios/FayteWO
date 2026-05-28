namespace FayteWO.Shared.Networking.Packets;

public sealed record WhisperReceivedPacket(
    Guid SenderId,
    string SenderName,
    string TargetName,
    string Message,
    bool IsOutgoingCopy);