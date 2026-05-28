using System.Net.Sockets;
using System.Text;
using FayteWO.Shared.Networking;
using FayteWO.Shared.Networking.Packets;
using FayteWO.Shared.World;
using System.Collections.Concurrent;
using FayteWO.Client.Entities;

namespace FayteWO.Client.Networking;

public sealed class GameClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly object _sendLock = new();
    private readonly ConcurrentDictionary<Guid, ClientEntity> _entities = new();
    public IReadOnlyCollection<ClientEntity> Entities => _entities.Values.ToArray();
    private readonly ConcurrentDictionary<ChunkPosition, ChunkDataPacket> _chunks = new();
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

    public void RequestCurrentChunk()
    {
        if (PlayerId is null || Position is null)
        {
            Console.WriteLine("Cannot request chunk before login.");
            return;
        }

        ChunkPosition chunkPosition = ChunkPosition.FromWorldPosition(Position.Value);

        ChunkRequestPacket request = new ChunkRequestPacket(chunkPosition);
        string outgoingJson = PacketSerializer.Serialize(PacketType.ChunkRequest, request);

        Console.WriteLine();
        Console.WriteLine($"Requesting chunk {chunkPosition}");

        SendRaw(outgoingJson);
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
            case PacketType.ChatBroadcast:
                HandleChatBroadcast(responsePacket);
                break;
            case PacketType.WhisperReceived:
                HandleWhisperReceived(responsePacket);
                break;
            case PacketType.ChunkData:
                HandleChunkData(responsePacket);
                break;
            default:
                Console.WriteLine($"Unhandled response packet type: {responsePacket.Type}");
                break;
        }
    }

    private void HandleChunkData(NetworkPacket responsePacket)
    {
        ChunkDataPacket chunkData = PacketSerializer.DeserializePayload<ChunkDataPacket>(responsePacket);

        _chunks[chunkData.ChunkPosition] = chunkData;

        Console.WriteLine($"Received chunk {chunkData.ChunkPosition} with {chunkData.TileIds.Length} tiles.");

        PrintMapAroundPlayer(radius: 8);
    }

    public void PrintMapAroundPlayer(int radius = 8)
    {
        if (Position is null)
        {
            Console.WriteLine("Cannot print map before login.");
            return;
        }

        TilePosition center = Position.Value;

        Console.WriteLine();
        Console.WriteLine($"Map around {center}:");

        for (int y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                TilePosition position = new TilePosition(x, y, center.Z);

                if (position == center)
                {
                    Console.Write('@');
                    continue;
                }

                ClientEntity? entityAtPosition = _entities.Values
                    .FirstOrDefault(entity => entity.EntityId != PlayerId && entity.Position == position);

                if (entityAtPosition is not null)
                {
                    Console.Write('P');
                    continue;
                }

                if (TryGetKnownTileId(position, out int tileId))
                {
                    Console.Write(TileIdToAscii(tileId));
                }
                else
                {
                    Console.Write('?');
                }
            }

            Console.WriteLine();
        }
    }

    private bool TryGetKnownTileId(TilePosition worldPosition, out int tileId)
    {
        tileId = 0;

        ChunkPosition chunkPosition = ChunkPosition.FromWorldPosition(worldPosition);

        if (!_chunks.TryGetValue(chunkPosition, out ChunkDataPacket? chunkData))
        {
            return false;
        }

        int localX = Chunk.WorldToLocalCoordinate(worldPosition.X);
        int localY = Chunk.WorldToLocalCoordinate(worldPosition.Y);
        int localZ = worldPosition.Z - chunkData.ChunkPosition.Z;

        if (localX < 0 ||
            localX >= chunkData.Size ||
            localY < 0 ||
            localY >= chunkData.Size ||
            localZ < 0 ||
            localZ >= chunkData.Height)
        {
            return false;
        }

        int index = (localZ * chunkData.Size * chunkData.Size) +
                    (localY * chunkData.Size) +
                    localX;

        if (index < 0 || index >= chunkData.TileIds.Length)
        {
            return false;
        }

        tileId = chunkData.TileIds[index];
        return true;
    }

    private static char TileIdToAscii(int tileId)
    {
        return tileId switch
        {
            1 => '.',
            2 => '#',
            _ => '?'
        };
    }

    private static void HandleWhisperReceived(NetworkPacket responsePacket)
    {
        WhisperReceivedPacket whisper = PacketSerializer.DeserializePayload<WhisperReceivedPacket>(responsePacket);

        if (whisper.IsOutgoingCopy)
        {
            Console.WriteLine($"[To {whisper.TargetName}] {whisper.Message}");
        }
        else
        {
            Console.WriteLine($"[From {whisper.SenderName}] {whisper.Message}");
        }
    }

    public void SendWhisperMessage(string targetUsername, string message)
    {
        if (PlayerId is null)
        {
            Console.WriteLine("Cannot whisper before login.");
            return;
        }

        WhisperMessagePacket whisperMessage = new WhisperMessagePacket(targetUsername, message);

        string outgoingJson = PacketSerializer.Serialize(PacketType.WhisperMessage, whisperMessage);

        Console.WriteLine();
        Console.WriteLine($"Sending WhisperMessage: {outgoingJson}");

        SendRaw(outgoingJson);
    }

    private static void HandleChatBroadcast(NetworkPacket responsePacket)
    {
        ChatBroadcastPacket chat = PacketSerializer.DeserializePayload<ChatBroadcastPacket>(responsePacket);

        Console.WriteLine($"[Chat] {chat.SenderName}: {chat.Message}");
    }

    private void HandleEntitySpawned(NetworkPacket responsePacket)
    {
        EntitySpawnedPacket spawned = PacketSerializer.DeserializePayload<EntitySpawnedPacket>(responsePacket);

        if (PlayerId == spawned.EntityId)
        {
            return;
        }

        ClientEntity entity = new ClientEntity(
            spawned.EntityId,
            spawned.Name,
            spawned.Position);

        _entities[spawned.EntityId] = entity;

        Console.WriteLine($"Entity spawned: {entity}");
    }

    private void HandleEntityDespawned(NetworkPacket responsePacket)
    {
        EntityDespawnedPacket despawned = PacketSerializer.DeserializePayload<EntityDespawnedPacket>(responsePacket);

        if (_entities.TryRemove(despawned.EntityId, out ClientEntity? removedEntity))
        {
            Console.WriteLine($"Entity despawned: {removedEntity.Name} [{removedEntity.EntityId}]. Reason: {despawned.Reason}");
        }
        else
        {
            Console.WriteLine($"Entity despawned: {despawned.EntityId}. Reason: {despawned.Reason}");
        }
    }

    public void SendChatMessage(string message)
    {
        if (PlayerId is null)
        {
            Console.WriteLine("Cannot chat before login.");
            return;
        }

        ChatMessagePacket chatMessage = new ChatMessagePacket(message);

        string outgoingJson = PacketSerializer.Serialize(PacketType.ChatMessage, chatMessage);

        Console.WriteLine();
        Console.WriteLine($"Sending ChatMessage: {outgoingJson}");

        SendRaw(outgoingJson);
    }

    public void PrintKnownEntities()
    {
        Console.WriteLine();
        Console.WriteLine($"Known entities: {_entities.Count}");

        foreach (ClientEntity entity in _entities.Values.OrderBy(entity => entity.Name))
        {
            string marker = PlayerId == entity.EntityId ? "local" : "remote";
            Console.WriteLine($"  [{marker}] {entity}");
        }
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
        if (Position is not null)
        {
            _entities[PlayerId.Value] = new ClientEntity(
                PlayerId.Value,
                "You",
                Position.Value);
        }

        Console.WriteLine($"Logged in as PlayerId={PlayerId}");
        Console.WriteLine($"Spawn position={Position}");
    }

    private void HandleEntityMoved(NetworkPacket responsePacket)
    {
        EntityMovedPacket moved = PacketSerializer.DeserializePayload<EntityMovedPacket>(responsePacket);

        if (_entities.TryGetValue(moved.EntityId, out ClientEntity? entity))
        {
            entity.SetPosition(moved.ToPosition);
        }
        else
        {
            entity = new ClientEntity(
                moved.EntityId,
                "Unknown Entity",
                moved.ToPosition);

            _entities[moved.EntityId] = entity;
        }

        Console.WriteLine($"Entity {entity.Name} [{entity.EntityId}] moved from {moved.FromPosition} to {moved.ToPosition}");

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