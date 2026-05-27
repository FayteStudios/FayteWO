using System.Net.Sockets;
using System.Text;
using FayteWO.Shared.Networking;
using FayteWO.Shared.Networking.Packets;
using FayteWO.Shared.World;

const string host = "127.0.0.1";
const int port = 7777;

Console.WriteLine("FayteWO Client Starting...");
Console.WriteLine($"Connecting to {host}:{port}");

using TcpClient client = new TcpClient();
client.Connect(host, port);

using NetworkStream stream = client.GetStream();
using StreamReader reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)
{
    AutoFlush = true
};

Guid? playerId = Login("TestPlayer", "password");

if (playerId is null)
{
    Console.WriteLine("Login failed. Client shutting down.");
    return;
}

SendMoveRequest(playerId.Value, Direction.East);
SendMoveRequest(playerId.Value, Direction.East);
SendMoveRequest(playerId.Value, Direction.East);

Console.WriteLine("Client test complete.");

Guid? Login(string username, string password)
{
    LoginRequestPacket loginRequest = new LoginRequestPacket(username, password);

    string outgoingJson = PacketSerializer.Serialize(PacketType.LoginRequest, loginRequest);

    Console.WriteLine();
    Console.WriteLine($"Sending LoginRequest: {outgoingJson}");

    string? responseJson = SendPacketAndGetResponse(outgoingJson);

    if (string.IsNullOrWhiteSpace(responseJson))
    {
        Console.WriteLine("No login response received.");
        return null;
    }

    Console.WriteLine($"Received login response: {responseJson}");

    NetworkPacket responsePacket = PacketSerializer.DeserializeEnvelope(responseJson);

    if (responsePacket.Type != PacketType.LoginResult)
    {
        Console.WriteLine($"Expected LoginResult but received {responsePacket.Type}.");
        return null;
    }

    LoginResultPacket loginResult = PacketSerializer.DeserializePayload<LoginResultPacket>(responsePacket);

    Console.WriteLine($"Login message: {loginResult.Message}");

    if (!loginResult.Success || loginResult.PlayerId is null)
    {
        return null;
    }

    Console.WriteLine($"Logged in as PlayerId={loginResult.PlayerId}");
    Console.WriteLine($"Spawn position={loginResult.SpawnPosition}");

    return loginResult.PlayerId.Value;
}

void SendMoveRequest(Guid playerId, Direction direction)
{
    MoveRequestPacket moveRequest = new MoveRequestPacket(playerId, direction);

    string outgoingJson = PacketSerializer.Serialize(PacketType.MoveRequest, moveRequest);

    Console.WriteLine();
    Console.WriteLine($"Sending MoveRequest: {outgoingJson}");

    string? responseJson = SendPacketAndGetResponse(outgoingJson);

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

string? SendPacketAndGetResponse(string outgoingJson)
{
    writer.WriteLine(outgoingJson);
    return reader.ReadLine();
}