namespace FayteWO.Shared.Networking.Packets;

public sealed record WhisperMessagePacket(
    string TargetUsername,
    string Message);