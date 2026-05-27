using System.Net.Sockets;
using System.Text;
using FayteWO.Shared.Networking;
using FayteWO.Shared.Networking.Packets;
using FayteWO.Shared.World;

const string host = "127.0.0.1";
const int port = 7777;

// For this first TCP test, copy the PlayerId printed by the server if needed.
// For now, this intentionally uses an empty/unknown ID so you can see rejection.
// In the next milestone, the client will login and receive the real PlayerId.
Guid playerId = Guid.Empty;

Console.WriteLine("FayteWO Client Starting...");
Console.WriteLine($"Connecting to {host}:{port}");

SendMoveRequest(Direction.East);
SendMoveRequest(Direction.East);
SendMoveRequest(Direction.East);

Console.WriteLine("Client test complete.");

void SendMoveRequest(Direction direction)
{
    MoveRequestPacket moveRequest = new MoveRequestPacket(playerId, direction);

    string outgoingJson = PacketSerializer.Serialize(PacketType.MoveRequest, moveRequest);

    using TcpClient client = new TcpClient();
    client.Connect(host, port);

    using NetworkStream stream = client.GetStream();
    using StreamReader reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
    using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)
    {
        AutoFlush = true
    };

    Console.WriteLine();
    Console.WriteLine($"Sending MoveRequest: {outgoingJson}");
    writer.WriteLine(outgoingJson);

    string? responseJson = reader.ReadLine();

    if (string.IsNullOrWhiteSpace(responseJson))
    {
        Console.WriteLine("No response received.");
        return;
    }

    Console.WriteLine($"Received response: {responseJson}");

    NetworkPacket responsePacket = PacketSerializer.DeserializeEnvelope(responseJson);

    switch (responsePacket.Type)
    {
        case PacketType.EntityMoved:
            EntityMovedPacket moved = PacketSerializer.DeserializePayload<EntityMovedPacket>(responsePacket);
            Console.WriteLine($"Entity moved from {moved.FromPosition} to {moved.ToPosition}");
            break;

        case PacketType.ServerMessage:
            ServerMessagePacket message = PacketSerializer.DeserializePayload<ServerMessagePacket>(responsePacket);
            Console.WriteLine($"Server message: {message.Message}");
            break;

        default:
            Console.WriteLine($"Unhandled response packet type: {responsePacket.Type}");
            break;
    }
}