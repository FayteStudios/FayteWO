using System.Text.Json;

namespace FayteWO.Shared.Networking;

public static class PacketSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static string Serialize<TPayload>(PacketType packetType, TPayload payload)
    {
        JsonElement payloadElement = JsonSerializer.SerializeToElement(payload, JsonOptions);
        NetworkPacket packet = new(packetType, payloadElement);

        return JsonSerializer.Serialize(packet, JsonOptions);
    }

    public static NetworkPacket DeserializeEnvelope(string json)
    {
        NetworkPacket? packet = JsonSerializer.Deserialize<NetworkPacket>(json, JsonOptions);

        if (packet is null)
        {
            throw new InvalidOperationException("Failed to deserialize network packet.");
        }

        return packet;
    }

    public static TPayload DeserializePayload<TPayload>(NetworkPacket packet)
    {
        TPayload? payload = packet.Payload.Deserialize<TPayload>(JsonOptions);

        if (payload is null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize payload for packet type {packet.Type}.");
        }

        return payload;
    }
}