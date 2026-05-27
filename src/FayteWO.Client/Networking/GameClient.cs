using System.Net.Sockets;
using System.Text;
using FayteWO.Shared.Networking;
using FayteWO.Shared.Networking.Packets;
using FayteWO.Shared.World;

namespace FayteWO.Client.Networking;

public sealed class GameClient
{
    private readonly string _host;
    private readonly int _port;

    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public Guid? PlayerId { get; private set; }
    public TilePosition? Position { get; private set; }

    public GameClient(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Host cannot be empty.", nameof(host));
        }

        _host = host;
        _port = port;
    }

    public void Connect()
    {
        Console.WriteLine($"Connecting to {_host}:{_port}");

        _client = new TcpClient();
        _client.Connect(_host, _port);

        NetworkStream stream = _client.GetStream();

        _reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)
        {
            AutoFlush = true
        };

        Console.WriteLine("Connected to server.");
    }

    public void Disconnect()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose();

        _reader = null;
        _writer = null;
        _client = null;

        Console.WriteLine("Disconnected from server.");
    }

    public bool Login(string username, string password)
    {
        LoginRequestPacket loginRequest = new LoginRequestPacket(username, password);

        string outgoingJson = PacketSerializer.Serialize(PacketType.LoginRequest, loginRequest);

        Console.WriteLine();
        Console.WriteLine($"Sending LoginRequest: {outgoingJson}");

        string? responseJson = SendPacketAndGetResponse(outgoingJson);

        if (string.IsNullOrWhiteSpace(responseJson))
        {
            Console.WriteLine("No login response received.");
            return false;
        }

        Console.WriteLine($"Received login response: {responseJson}");

        NetworkPacket responsePacket = PacketSerializer.DeserializeEnvelope(responseJson);

        if (responsePacket.Type != PacketType.LoginResult)
        {
            Console.WriteLine($"Expected LoginResult but received {responsePacket.Type}.");
            return false;
        }

        LoginResultPacket loginResult = PacketSerializer.DeserializePayload<LoginResultPacket>(responsePacket);

        Console.WriteLine($"Login message: {loginResult.Message}");

        if (!loginResult.Success || loginResult.PlayerId is null)
        {
            return false;
        }

        PlayerId = loginResult.PlayerId.Value;
        Position = loginResult.SpawnPosition;

        Console.WriteLine($"Logged in as PlayerId={PlayerId}");
        Console.WriteLine($"Spawn position={Position}");

        return true;
    }

    public void SendMoveRequest(Direction direction)
    {
        if (PlayerId is null)
        {
            Console.WriteLine("Cannot move before login.");
            return;
        }

        MoveRequestPacket moveRequest = new MoveRequestPacket(direction);

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

        HandleServerResponse(responseJson);
    }

    private string? SendPacketAndGetResponse(string outgoingJson)
    {
        if (_writer is null || _reader is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        _writer.WriteLine(outgoingJson);
        return _reader.ReadLine();
    }

    private void HandleServerResponse(string responseJson)
    {
        NetworkPacket responsePacket = PacketSerializer.DeserializeEnvelope(responseJson);

        switch (responsePacket.Type)
        {
            case PacketType.EntityMoved:
                HandleEntityMoved(responsePacket);
                break;

            case PacketType.ServerMessage:
                HandleServerMessage(responsePacket);
                break;

            default:
                Console.WriteLine($"Unhandled response packet type: {responsePacket.Type}");
                break;
        }
    }

    private void HandleEntityMoved(NetworkPacket responsePacket)
    {
        EntityMovedPacket moved = PacketSerializer.DeserializePayload<EntityMovedPacket>(responsePacket);

        Console.WriteLine($"Entity moved from {moved.FromPosition} to {moved.ToPosition}");

        if (PlayerId == moved.EntityId)
        {
            Position = moved.ToPosition;
            Console.WriteLine($"Local player position updated to {Position}");
        }
    }

    private static void HandleServerMessage(NetworkPacket responsePacket)
    {
        ServerMessagePacket message = PacketSerializer.DeserializePayload<ServerMessagePacket>(responsePacket);
        Console.WriteLine($"Server message: {message.Message}");
    }
}