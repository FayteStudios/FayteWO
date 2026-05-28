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
    private readonly object _sendLock = new();

    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Thread? _receiveThread;
    private bool _isRunning;

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

        _isRunning = true;

        _receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = "FayteWO Client Receive Thread"
        };

        _receiveThread.Start();

        Console.WriteLine("Connected to server.");
    }

    public void Disconnect()
    {
        _isRunning = false;

        try
        {
            _client?.Close();
        }
        catch
        {
            // Ignore disconnect cleanup errors for now.
        }

        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose();

        _reader = null;
        _writer = null;
        _client = null;

        Console.WriteLine("Disconnected from server.");
    }

    public void Login(string username, string password)
    {
        LoginRequestPacket loginRequest = new LoginRequestPacket(username, password);

        string outgoingJson = PacketSerializer.Serialize(PacketType.LoginRequest, loginRequest);

        Console.WriteLine();
        Console.WriteLine($"Sending LoginRequest: {outgoingJson}");

        SendRaw(outgoingJson);
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

        SendRaw(outgoingJson);
    }

    private void SendRaw(string outgoingJson)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        lock (_sendLock)
        {
            _writer.WriteLine(outgoingJson);
        }
    }

    private void ReceiveLoop()
    {
        while (_isRunning)
        {
            try
            {
                if (_reader is null)
                {
                    return;
                }

                string? responseJson = _reader.ReadLine();

                if (responseJson is null)
                {
                    if (_isRunning)
                    {
                        Console.WriteLine("Server closed the connection.");
                    }

                    return;
                }

                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine($"Received packet: {responseJson}");

                HandleServerResponse(responseJson);
            }
            catch (IOException)
            {
                if (_isRunning)
                {
                    Console.WriteLine("Lost connection to server.");
                }

                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Receive loop error: {ex.Message}");
            }
        }
    }

    private void HandleServerResponse(string responseJson)
    {
        NetworkPacket responsePacket = PacketSerializer.DeserializeEnvelope(responseJson);

        switch (responsePacket.Type)
        {
            case PacketType.LoginResult:
                HandleLoginResult(responsePacket);
                break;

            case PacketType.EntityMoved:
                HandleEntityMoved(responsePacket);
                break;

            case PacketType.ServerMessage:
                HandleServerMessage(responsePacket);
                break;
                case PacketType.EntitySpawned:
                    HandleEntitySpawned(responsePacket);
                    break;
            case PacketType.EntityDespawned:
                HandleEntityDespawned(responsePacket);
                break;
            default:
                Console.WriteLine($"Unhandled response packet type: {responsePacket.Type}");
                break;
        }
    }

    private void HandleEntitySpawned(NetworkPacket responsePacket)
    {
        EntitySpawnedPacket spawned = PacketSerializer.DeserializePayload<EntitySpawnedPacket>(responsePacket);

        Console.WriteLine($"Entity spawned: {spawned.Name} [{spawned.EntityId}] at {spawned.Position}");
    }

    private void HandleEntityDespawned(NetworkPacket responsePacket)
    {
        EntityDespawnedPacket despawned = PacketSerializer.DeserializePayload<EntityDespawnedPacket>(responsePacket);

        Console.WriteLine($"Entity despawned: {despawned.EntityId}. Reason: {despawned.Reason}");
    }

    private void HandleLoginResult(NetworkPacket responsePacket)
    {
        LoginResultPacket loginResult = PacketSerializer.DeserializePayload<LoginResultPacket>(responsePacket);

        Console.WriteLine($"Login message: {loginResult.Message}");

        if (!loginResult.Success || loginResult.PlayerId is null)
        {
            Console.WriteLine("Login failed.");
            return;
        }

        PlayerId = loginResult.PlayerId.Value;
        Position = loginResult.SpawnPosition;

        Console.WriteLine($"Logged in as PlayerId={PlayerId}");
        Console.WriteLine($"Spawn position={Position}");
    }

    private void HandleEntityMoved(NetworkPacket responsePacket)
    {
        EntityMovedPacket moved = PacketSerializer.DeserializePayload<EntityMovedPacket>(responsePacket);

        Console.WriteLine($"Entity {moved.EntityId} moved from {moved.FromPosition} to {moved.ToPosition}");

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