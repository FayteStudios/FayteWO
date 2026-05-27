using System.Text.Json;

namespace FayteWO.Shared.Networking;

public sealed record NetworkPacket(
    PacketType Type,
    JsonElement Payload);