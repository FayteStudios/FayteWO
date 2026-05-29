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
    private readonly ConcurrentDictionary<int, TileDefinitionDto> _tileDefinitions = new();
    private readonly ConcurrentDictionary<ChunkPosition, byte> _pendingChunkRequests = new();    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Thread? _receiveThread;
    private TilePosition? _selectedTilePosition;
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

    private void RequestChunkIfNeeded(TilePosition position)
    {
        ChunkPosition chunkPosition = ChunkPosition.FromWorldPosition(position);
        RequestChunkIfNeeded(chunkPosition);
    }

    public void SetSelectedTilePosition(TilePosition position)
    {
        _selectedTilePosition = position;

        Console.WriteLine();
        Console.WriteLine($"Selected tile target: {position}");

        if (TryGetKnownTileId(position, out int tileId))
        {
            char symbol = TileIdToAscii(tileId);
            Console.WriteLine($"Selected tile info: TileId={tileId}, Symbol={symbol}");
        }
        else
        {
            Console.WriteLine("Selected tile info: tile is not loaded locally yet.");
            RequestChunkIfNeeded(position);
        }
    }

    public void ClearSelectedTilePosition()
    {
        if (_selectedTilePosition is null)
        {
            Console.WriteLine("No tile target is currently selected.");
            return;
        }

        Console.WriteLine($"Cleared selected tile target: {_selectedTilePosition.Value}");
        _selectedTilePosition = null;
    }

    public void PrintSelectedTilePosition()
    {
        if (_selectedTilePosition is null)
        {
            Console.WriteLine("No tile target is currently selected.");
            return;
        }

        TilePosition selectedPosition = _selectedTilePosition.Value;

        Console.WriteLine();
        Console.WriteLine($"Selected tile target: {selectedPosition}");

        if (TryGetKnownTileId(selectedPosition, out int tileId))
        {
            char symbol = TileIdToAscii(tileId);
            Console.WriteLine($"Selected tile info: TileId={tileId}, Symbol={symbol}");
        }
        else
        {
            Console.WriteLine("Selected tile info: tile is not loaded locally.");
        }
    }
    public TilePosition? GetSelectedTilePosition()
    {
        return _selectedTilePosition;
    }

    public void SendTileInteractionRequestForSelectedTarget()
    {
        if (_selectedTilePosition is null)
        {
            Console.WriteLine("No tile target selected. Use: target <x> <y>");
            return;
        }

        SendTileInteractionRequest(_selectedTilePosition.Value);
    }

    public void InteractWithSelectedTile()
    {
        SendTileInteractionRequestForSelectedTarget();
    }

    public void RequestTileDefinitions()
    {
        if (PlayerId is null)
        {
            Console.WriteLine("Cannot request tile definitions before login.");
            return;
        }

        TileDefinitionsRequestPacket request = new TileDefinitionsRequestPacket();
        string outgoingJson = PacketSerializer.Serialize(PacketType.TileDefinitionsRequest, request);

        Console.WriteLine();
        Console.WriteLine("Requesting tile definitions.");

        SendRaw(outgoingJson);
    }

    private void RequestChunkIfNeeded(ChunkPosition chunkPosition)
    {
        if (_chunks.ContainsKey(chunkPosition))
        {
            return;
        }

        if (!_pendingChunkRequests.TryAdd(chunkPosition, 0))
        {
            return;
        }

        RequestChunk(chunkPosition);
    }

    private void RequestChunksAround(TilePosition position, int radius = 1)
    {
        ChunkPosition centerChunk = ChunkPosition.FromWorldPosition(position);

        for (int y = centerChunk.Y - radius; y <= centerChunk.Y + radius; y++)
        {
            for (int x = centerChunk.X - radius; x <= centerChunk.X + radius; x++)
            {
                ChunkPosition chunkPosition = new ChunkPosition(
                    x,
                    y,
                    centerChunk.Z);

                RequestChunkIfNeeded(chunkPosition);
            }
        }
    }

    private void RequestChunk(ChunkPosition chunkPosition)
    {
        ChunkRequestPacket request = new ChunkRequestPacket(chunkPosition);
        string outgoingJson = PacketSerializer.Serialize(PacketType.ChunkRequest, request);

        Console.WriteLine();
        Console.WriteLine($"Requesting chunk {chunkPosition}");

        SendRaw(outgoingJson);
    }

    public void RequestCurrentChunk()
    {
        if (PlayerId is null || Position is null)
        {
            Console.WriteLine("Cannot request chunk before login.");
            return;
        }

        RequestChunksAround(Position.Value);
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

                PrintIncomingPacketSummary(responseJson);

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

    private static void PrintIncomingPacketSummary(string responseJson)
    {
        try
        {
            NetworkPacket packet = PacketSerializer.DeserializeEnvelope(responseJson);

            if (packet.Type == PacketType.ChunkData)
            {
                ChunkDataPacket chunkData = PacketSerializer.DeserializePayload<ChunkDataPacket>(packet);

                Console.WriteLine();
                Console.WriteLine(
                    $"Received packet: ChunkData {chunkData.ChunkPosition} " +
                    $"Size={chunkData.Size} Height={chunkData.Height} Tiles={chunkData.TileIds.Length}");

                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Received packet: {packet.Type}");
        }
        catch
        {
            Console.WriteLine();
            Console.WriteLine($"Received packet: {responseJson}");
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

            case PacketType.TileDefinitions:
                HandleTileDefinitions(responsePacket);
                break;

            case PacketType.TileChanged:
                HandleTileChanged(responsePacket);
                break;

            default:
                Console.WriteLine($"Unhandled response packet type: {responsePacket.Type}");
                break;
        }
    }

    public void SendTileChangeRequest(TilePosition position, int tileId)
    {
        if (PlayerId is null)
        {
            Console.WriteLine("Cannot change tile before login.");
            return;
        }

        TileChangeRequestPacket request = new TileChangeRequestPacket(
            position,
            tileId);

        string outgoingJson = PacketSerializer.Serialize(PacketType.TileChangeRequest, request);

        Console.WriteLine();
        Console.WriteLine($"Sending TileChangeRequest: Position={position}, TileId={tileId}");

        SendRaw(outgoingJson);
    }

    private void HandleTileChanged(NetworkPacket responsePacket)
    {
        TileChangedPacket packet = PacketSerializer.DeserializePayload<TileChangedPacket>(responsePacket);

        bool applied = TryApplyTileChangeToKnownChunk(packet.Position, packet.TileId);

        if (applied)
        {
            Console.WriteLine($"Tile changed locally at {packet.Position} to TileId={packet.TileId}.");
        }
        else
        {
            Console.WriteLine(
                $"Tile changed at {packet.Position} to TileId={packet.TileId}, but the chunk is not loaded locally.");

            RequestChunkIfNeeded(packet.Position);
        }
    }

    public void SendTileInteractionRequest(TilePosition position)
    {
        if (PlayerId is null)
        {
            Console.WriteLine("Cannot interact before login.");
            return;
        }

        TileInteractionRequestPacket request = new TileInteractionRequestPacket(position);

        string outgoingJson = PacketSerializer.Serialize(PacketType.TileInteractionRequest, request);

        Console.WriteLine();
        Console.WriteLine($"Sending TileInteractionRequest: Position={position}");

        SendRaw(outgoingJson);
    }

    private bool TryApplyTileChangeToKnownChunk(TilePosition worldPosition, int tileId)
    {
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

        chunkData.TileIds[index] = tileId;
        return true;
    }

    private void HandleTileDefinitions(NetworkPacket responsePacket)
    {
        TileDefinitionsPacket packet = PacketSerializer.DeserializePayload<TileDefinitionsPacket>(responsePacket);

        foreach (TileDefinitionDto tile in packet.Tiles)
        {
            _tileDefinitions[tile.TileId] = tile;
        }

        Console.WriteLine($"Received {packet.Tiles.Count} tile definitions.");

        foreach (TileDefinitionDto tile in packet.Tiles.OrderBy(tile => tile.TileId))
        {
            Console.WriteLine(
                $"  {tile.TileId}: {tile.MapSymbol} = {tile.Name} [{tile.Flags}]");
        }
    }

    private void HandleChunkData(NetworkPacket responsePacket)
    {
        ChunkDataPacket chunkData = PacketSerializer.DeserializePayload<ChunkDataPacket>(responsePacket);

        _chunks[chunkData.ChunkPosition] = chunkData;
        _pendingChunkRequests.TryRemove(chunkData.ChunkPosition, out _);

        Console.WriteLine(
            $"Stored chunk {chunkData.ChunkPosition}. " +
            $"Loaded chunks: {_chunks.Count}. " +
            $"Pending chunk requests: {_pendingChunkRequests.Count}.");
    }

    public void PrintLoadedChunks()
    {
        Console.WriteLine();
        Console.WriteLine($"Loaded chunks: {_chunks.Count}");

        foreach (KeyValuePair<ChunkPosition, ChunkDataPacket> entry in _chunks
                    .OrderBy(entry => entry.Key.Z)
                    .ThenBy(entry => entry.Key.Y)
                    .ThenBy(entry => entry.Key.X))
        {
            ChunkPosition position = entry.Key;
            ChunkDataPacket chunk = entry.Value;

            Console.WriteLine(
                $"  {position} | Size={chunk.Size} Height={chunk.Height} Tiles={chunk.TileIds.Length}");
        }

        Console.WriteLine($"Pending chunk requests: {_pendingChunkRequests.Count}");
    }

    public void PrintMapAroundPlayer(int radius = 8)
    {
        if (Position is null)
        {
            Console.WriteLine("Cannot print map before login.");
            return;
        }

        if (_tileDefinitions.IsEmpty)
        {
            Console.WriteLine("Tile definitions have not been received yet. Requesting tile definitions now.");
            RequestTileDefinitions();
        }

        TilePosition center = Position.Value;

        Console.WriteLine();
        Console.WriteLine($"Map around {center}:");
        Console.WriteLine("Legend: @=you, X=selected, P=player, ?=unknown/unloaded");

        HashSet<ChunkPosition> missingChunks = new();

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

                if (_selectedTilePosition is not null && position == _selectedTilePosition.Value)
                {
                    Console.Write('X');
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
                    ChunkPosition missingChunkPosition = ChunkPosition.FromWorldPosition(position);
                    missingChunks.Add(missingChunkPosition);
                    Console.Write('?');
                }
            }

            Console.WriteLine();
        }

        if (missingChunks.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Map has unloaded tiles from {missingChunks.Count} missing chunk(s). Requesting missing chunks now.");

        foreach (ChunkPosition missingChunk in missingChunks
                    .OrderBy(chunk => chunk.Z)
                    .ThenBy(chunk => chunk.Y)
                    .ThenBy(chunk => chunk.X))
        {
            RequestChunkIfNeeded(missingChunk);
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

    private char TileIdToAscii(int tileId)
    {
        if (_tileDefinitions.TryGetValue(tileId, out TileDefinitionDto? tile))
        {
            return tile.MapSymbol;
        }

        return '?';
    }
    public TileDefinitionDto? GetTileDefinition(int tileId)
    {
        if (_tileDefinitions.TryGetValue(tileId, out TileDefinitionDto? tileDefinition))
        {
            return tileDefinition;
        }

        return null;
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
    public IReadOnlyList<ChunkDataPacket> GetLoadedChunksSnapshot()
    {
        return _chunks.Values.ToArray();
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

            RequestChunksAround(Position.Value);
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

            RequestChunksAround(moved.ToPosition);
        }
    }
    private static void HandleServerMessage(NetworkPacket responsePacket)
    {
        ServerMessagePacket message = PacketSerializer.DeserializePayload<ServerMessagePacket>(responsePacket);
        Console.WriteLine($"Server message: {message.Message}");
    }
}