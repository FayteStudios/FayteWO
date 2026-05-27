namespace FayteWO.Shared.Networking.Packets;

public sealed record LoginRequestPacket(
    string Username,
    string Password);