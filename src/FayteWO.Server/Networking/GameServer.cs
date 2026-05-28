using System.Net;
using System.Net.Sockets;
using FayteWO.Server.Systems;
using FayteWO.Shared.Entities;
using FayteWO.Shared.Networking;
using FayteWO.Shared.Networking.Packets;
using FayteWO.Shared.World;
using System.Collections.Concurrent;

namespace FayteWO.Server.Networking;

public sealed class GameServer
{
    private readonly int _port;
    private readonly WorldMap _worldMap;
    private readonly MovementSystem _movementSystem;
    private readonly ConcurrentDictionary<Guid, PlayerState> _players = new();
    private readonly ConcurrentDictionary<Guid, ClientSession> _sessions = new();
    private readonly ConcurrentDictionary<string, Guid> _onlineUsernames = new(
    StringComparer.OrdinalIgnoreCase);
    private TcpListener? _listener;
    private bool _isRunning;


    public GameServer(int port)
    {
        _port = port;

        Tile grass = new Tile(1, "Grass");
        Tile wall = new Tile(2, "Stone Wall", TileFlags.BlocksMovement | TileFlags.BlocksSight);

        List<Tile> tileDefinitions =
        [
            grass,
            wall
        ];

        _worldMap = new WorldMap();

        for (int chunkY = -1; chunkY <= 1; chunkY++)
        {
            for (int chunkX = -1; chunkX <= 1; chunkX++)
            {
                Chunk chunk = CreateFilledChunk(chunkX, chunkY, grass.TileId);
                _worldMap.AddChunk(chunk);
            }
        }

        // Block movement at world position 33,0.
        _worldMap.TrySetTileId(new TilePosition(33, 0, 0), wall.TileId);

        // Add a small vertical wall for map visibility.
        for (int y = -3; y <= 3; y++)
        {
            _worldMap.TrySetTileId(new TilePosition(36, y, 0), wall.TileId);
        }

        _movementSystem = new MovementSystem(_worldMap, tileDefinitions);
    }

    public void Start()
    {
        Console.WriteLine("FayteWO Server Starting...");
        Console.WriteLine($"Listening on 127.0.0.1:{_port}");
        Console.WriteLine("Start one or more FayteWO.Client instances in other terminals.");
        Console.WriteLine("Type 'help' for server commands.");

        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();

        _isRunning = true;

        Task.Run(AcceptClientsLoop);
        RunConsoleCommandLoop();
    }

    private void SendExistingEntitiesToSession(ClientSession session, Guid newPlayerId)
    {
        foreach (PlayerState existingPlayer in _players.Values)
        {
            if (existingPlayer.PlayerId == newPlayerId)
            {
                continue;
            }

            EntitySpawnedPacket spawnPacket = new EntitySpawnedPacket(
                existingPlayer.PlayerId,
                existingPlayer.Name,
                existingPlayer.Position);

            string spawnJson = PacketSerializer.Serialize(PacketType.EntitySpawned, spawnPacket);

            session.SendRaw(spawnJson);
        }
    }

    private void AcceptClientsLoop()
    {
        if (_listener is null)
        {
            throw new InvalidOperationException("Server listener has not been started.");
        }

        while (_isRunning)
        {
            try
            {
                TcpClient client = _listener.AcceptTcpClient();
                Task.Run(() => HandleClient(client));
            }
            catch (SocketException)
            {
                if (_isRunning)
                {
                    Console.WriteLine("Socket error while accepting client.");
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private void RunConsoleCommandLoop()
    {
        while (_isRunning)
        {
            Console.WriteLine();
            Console.Write("server> ");

            string? input = Console.ReadLine();

            if (input is null)
            {
                continue;
            }

            input = input.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string command = parts[0].ToLowerInvariant();

            switch (command)
            {
                case "help":
                case "h":
                    PrintServerHelp();
                    break;

                case "players":
                case "p":
                    PrintPlayers();
                    break;

                case "sessions":
                case "s":
                    PrintSessions();
                    break;

                case "announce":
                case "a":
                    HandleAnnounceCommand(parts);
                    break;

                case "stop":
                case "exit":
                case "quit":
                case "q":
                    Stop();
                    return;

                default:
                    Console.WriteLine($"Unknown server command: {command}");
                    Console.WriteLine("Type 'help' for available commands.");
                    break;
            }
        }
    }

    private void HandleAnnounceCommand(string[] parts)
    {
        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: announce <message>");
            return;
        }

        string message = string.Join(' ', parts.Skip(1)).Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            Console.WriteLine("Announcement message cannot be empty.");
            return;
        }

        if (message.Length > 300)
        {
            Console.WriteLine("Announcement message cannot be longer than 300 characters.");
            return;
        }

        ServerMessagePacket packet = new ServerMessagePacket($"[Announcement] {message}");
        string json = PacketSerializer.Serialize(PacketType.ServerMessage, packet);

        BroadcastToLoggedInSessions(json);

        Console.WriteLine($"Announcement sent to logged-in clients: {message}");
    }

    private void PrintServerHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Server commands:");
        Console.WriteLine("  players               List online players");
        Console.WriteLine("  sessions              List active client sessions");
        Console.WriteLine("  announce <message>    Send a message to all logged-in clients");
        Console.WriteLine("  help                  Show server commands");
        Console.WriteLine("  stop                  Stop the server");
    }

    private void PrintPlayers()
    {
        Console.WriteLine();
        Console.WriteLine($"Online players: {_players.Count}");

        foreach (PlayerState player in _players.Values.OrderBy(player => player.Name))
        {
            Console.WriteLine($"  {player.Name} [{player.PlayerId}] at {player.Position}");
        }
    }

    private void PrintSessions()
    {
        Console.WriteLine();
        Console.WriteLine($"Active sessions: {_sessions.Count}");

        foreach (ClientSession session in _sessions.Values)
        {
            string playerText = session.PlayerId?.ToString() ?? "not logged in";
            Console.WriteLine($"  Session {session.SessionId} | Player: {playerText}");
        }
    }

    private void Stop()
    {
        Console.WriteLine("Stopping server...");

        _isRunning = false;

        try
        {
            _listener?.Stop();
        }
        catch
        {
            // Ignore shutdown cleanup errors for now.
        }

        Console.WriteLine("Server stopped.");
    }

    private void BroadcastEntitySpawned(PlayerState player, Guid excludeSessionId)
    {
        EntitySpawnedPacket spawnPacket = new EntitySpawnedPacket(
            player.PlayerId,
            player.Name,
            player.Position);

        string spawnJson = PacketSerializer.Serialize(PacketType.EntitySpawned, spawnPacket);

        foreach (ClientSession session in _sessions.Values)
        {
            if (session.SessionId == excludeSessionId)
            {
                continue;
            }

            if (!session.IsLoggedIn)
            {
                continue;
            }

            session.SendRaw(spawnJson);
        }
    }

    private void HandleClient(TcpClient client)
    {
        ClientSession? session = null;

        try
        {
            session = new ClientSession(client, HandleRawPacket);

            _sessions[session.SessionId] = session;

            Console.WriteLine();
            Console.WriteLine($"Session {session.SessionId}: Client connected.");

            session.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client session crashed: {ex.Message}");
        }
        finally
        {
            if (session is not null)
            {
                HandleSessionDisconnected(session);
            }
            else
            {
                Console.WriteLine("Client disconnected before session was created.");
            }
        }
    }   
    private string? HandleRawPacket(ClientSession session, string json)
    {
        try
        {
            NetworkPacket packet = PacketSerializer.DeserializeEnvelope(json);

            return packet.Type switch
            {
                PacketType.LoginRequest => HandleLoginRequest(
                    session,
                    PacketSerializer.DeserializePayload<LoginRequestPacket>(packet)),

                PacketType.MoveRequest => HandleMoveRequest(
                    session,
                    PacketSerializer.DeserializePayload<MoveRequestPacket>(packet)),

                PacketType.ChatMessage => HandleChatMessage(
                    session,
                    PacketSerializer.DeserializePayload<ChatMessagePacket>(packet)),

                PacketType.WhisperMessage => HandleWhisperMessage(
                    session,
                    PacketSerializer.DeserializePayload<WhisperMessagePacket>(packet)),

                PacketType.ChunkRequest => HandleChunkRequest(
                    session,
                    PacketSerializer.DeserializePayload<ChunkRequestPacket>(packet)),

                _ => PacketSerializer.Serialize(
                    PacketType.ServerMessage,
                    new ServerMessagePacket($"Unhandled packet type: {packet.Type}"))
            };
        }
        catch (Exception ex)
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket($"Server failed to process packet: {ex.Message}"));
        }
    }    

    private string HandleChunkRequest(ClientSession session, ChunkRequestPacket packet)
    {
        Console.WriteLine($"Session {session.SessionId}: Decoded ChunkRequest: {packet.ChunkPosition}");

        if (session.PlayerId is null)
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Chunk request rejected: client is not logged in."));
        }

        if (!_worldMap.TryGetChunk(packet.ChunkPosition, out Chunk? chunk) || chunk is null)
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket($"Chunk request rejected: chunk {packet.ChunkPosition} is not loaded."));
        }

        ChunkDataPacket chunkData = new ChunkDataPacket(
            packet.ChunkPosition,
            Chunk.Size,
            chunk.Height,
            chunk.ToFlatTileIdArray());

        return PacketSerializer.Serialize(PacketType.ChunkData, chunkData);
    }
    
    private bool TryGetSessionByPlayerId(Guid playerId, out ClientSession? targetSession)
    {
        foreach (ClientSession session in _sessions.Values)
        {
            if (session.PlayerId == playerId)
            {
                targetSession = session;
                return true;
            }
        }

        targetSession = null;
        return false;
    }

    private string? HandleWhisperMessage(ClientSession session, WhisperMessagePacket packet)
    {
        Console.WriteLine(
            $"Session {session.SessionId}: Decoded WhisperMessage: Target={packet.TargetUsername}, Message={packet.Message}");

        if (session.PlayerId is null)
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Whisper rejected: client is not logged in."));
        }

        if (!_players.TryGetValue(session.PlayerId.Value, out PlayerState? sender))
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Whisper rejected: player session is invalid."));
        }

        string targetUsername = packet.TargetUsername.Trim();
        string message = packet.Message.Trim();

        if (string.IsNullOrWhiteSpace(targetUsername))
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Whisper rejected: target username cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Whisper rejected: message cannot be empty."));
        }

        if (message.Length > 300)
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Whisper rejected: message is too long."));
        }

        if (!_onlineUsernames.TryGetValue(targetUsername, out Guid targetPlayerId))
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket($"Whisper rejected: '{targetUsername}' is not online."));
        }

        if (!_players.TryGetValue(targetPlayerId, out PlayerState? targetPlayer))
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket($"Whisper rejected: '{targetUsername}' session is invalid."));
        }

        if (!TryGetSessionByPlayerId(targetPlayerId, out ClientSession? targetSession) || targetSession is null)
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket($"Whisper rejected: '{targetUsername}' is not connected."));
        }

        WhisperReceivedPacket incomingWhisper = new WhisperReceivedPacket(
            sender.PlayerId,
            sender.Name,
            targetPlayer.Name,
            message,
            IsOutgoingCopy: false);

        string incomingJson = PacketSerializer.Serialize(PacketType.WhisperReceived, incomingWhisper);

        targetSession.SendRaw(incomingJson);

        WhisperReceivedPacket outgoingCopy = new WhisperReceivedPacket(
            sender.PlayerId,
            sender.Name,
            targetPlayer.Name,
            message,
            IsOutgoingCopy: true);

        string outgoingCopyJson = PacketSerializer.Serialize(PacketType.WhisperReceived, outgoingCopy);

        session.SendRaw(outgoingCopyJson);

        return null;
    }

    private string? HandleChatMessage(ClientSession session, ChatMessagePacket packet)
    {
        Console.WriteLine($"Session {session.SessionId}: Decoded ChatMessage: {packet.Message}");

        if (session.PlayerId is null)
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Chat rejected: client is not logged in."));
        }

        if (!_players.TryGetValue(session.PlayerId.Value, out PlayerState? sender))
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Chat rejected: player session is invalid."));
        }

        string message = packet.Message.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Chat rejected: message cannot be empty."));
        }

        if (message.Length > 300)
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Chat rejected: message is too long."));
        }

        ChatBroadcastPacket broadcastPacket = new ChatBroadcastPacket(
            sender.PlayerId,
            sender.Name,
            message);

        string broadcastJson = PacketSerializer.Serialize(PacketType.ChatBroadcast, broadcastPacket);

        BroadcastToLoggedInSessions(broadcastJson);

        return null;
    }

    private void HandleSessionDisconnected(ClientSession session)
    {
        _sessions.TryRemove(session.SessionId, out _);

        Console.WriteLine($"Session {session.SessionId}: Client disconnected.");

        if (session.PlayerId is null)
        {
            return;
        }

        if (_players.TryRemove(session.PlayerId.Value, out PlayerState? player))
        {
            _onlineUsernames.TryRemove(player.Name, out _);

            Console.WriteLine($"Session {session.SessionId}: Removed player {player.Name} [{player.PlayerId}].");

            BroadcastEntityDespawned(
                player.PlayerId,
                "Disconnected",
                excludeSessionId: session.SessionId);
        }
    }

    private void BroadcastEntityDespawned(Guid entityId, string reason, Guid excludeSessionId)
    {
        EntityDespawnedPacket despawnedPacket = new EntityDespawnedPacket(
            entityId,
            reason);

        string despawnJson = PacketSerializer.Serialize(PacketType.EntityDespawned, despawnedPacket);

        foreach (ClientSession session in _sessions.Values)
        {
            if (session.SessionId == excludeSessionId)
            {
                continue;
            }

            if (!session.IsLoggedIn)
            {
                continue;
            }

            session.SendRaw(despawnJson);
        }
    }

    private string? HandleLoginRequest(ClientSession session, LoginRequestPacket packet)
    {
        Console.WriteLine($"Session {session.SessionId}: Decoded LoginRequest: Username={packet.Username}");

        if (session.IsLoggedIn)
        {
            LoginResultPacket alreadyLoggedIn = new(
                Success: false,
                Message: "This connection is already logged in.",
                PlayerId: session.PlayerId,
                SpawnPosition: null);

            return PacketSerializer.Serialize(PacketType.LoginResult, alreadyLoggedIn);
        }

    string username = packet.Username.Trim();

    if (string.IsNullOrWhiteSpace(username))
    {
        LoginResultPacket failedLogin = new(
            Success: false,
            Message: "Username cannot be empty.",
            PlayerId: null,
            SpawnPosition: null);

        return PacketSerializer.Serialize(PacketType.LoginResult, failedLogin);
    }

    if (username.Length > 20)
    {
        LoginResultPacket failedLogin = new(
            Success: false,
            Message: "Username cannot be longer than 20 characters.",
            PlayerId: null,
            SpawnPosition: null);

        return PacketSerializer.Serialize(PacketType.LoginResult, failedLogin);
    }

        Guid playerId = Guid.NewGuid();

        if (!_onlineUsernames.TryAdd(username, playerId))
        {
            LoginResultPacket failedLogin = new(
                Success: false,
                Message: $"Username '{username}' is already online.",
                PlayerId: null,
                SpawnPosition: null);

            return PacketSerializer.Serialize(PacketType.LoginResult, failedLogin);
        }

        TilePosition spawnPosition = new TilePosition(30, 0, 0);
        PlayerState player;

        try
        {
            player = new PlayerState(
                playerId,
                username,
                spawnPosition);

            _players[playerId] = player;
            session.SetPlayerId(playerId);
        }
        catch
        {
            _onlineUsernames.TryRemove(username, out _);
            throw;
        }

        Console.WriteLine($"Session {session.SessionId}: Created player: {player}");
        Console.WriteLine($"Session {session.SessionId}: Associated with PlayerId={playerId}");
        LoginResultPacket successfulLogin = new(
            Success: true,
            Message: "Login successful.",
            PlayerId: player.PlayerId,
            SpawnPosition: player.Position);

        string loginResultJson = PacketSerializer.Serialize(PacketType.LoginResult, successfulLogin);

        // First, tell this new client about itself/login.
        session.SendRaw(loginResultJson);

        // Then, tell the new client about already-existing players.
        SendExistingEntitiesToSession(session, player.PlayerId);

        // Finally, tell everyone else about the new player.
        BroadcastEntitySpawned(player, excludeSessionId: session.SessionId);

        // We already sent the login response manually.
        return null;
    }

    private string? HandleMoveRequest(ClientSession session, MoveRequestPacket packet)
    {
        Console.WriteLine($"Session {session.SessionId}: Decoded MoveRequest: Player={session.PlayerId}, Direction={packet.Direction}");

        if (session.PlayerId is null)
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Move rejected: client is not logged in."));
        }

        if (!_players.TryGetValue(session.PlayerId.Value, out PlayerState? player))
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Move rejected: player session is invalid."));
        }

        TilePosition fromPosition = player.Position;

        bool moved = _movementSystem.TryMove(player, packet.Direction, out string reason);

        Console.WriteLine($"Session {session.SessionId}: {(moved ? "Move accepted." : "Move rejected.")}");
        Console.WriteLine($"Session {session.SessionId}: {reason}");
        Console.WriteLine($"Session {session.SessionId}: Player position: {player.Position}");

        if (!moved)
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket(reason));
        }

        EntityMovedPacket movedPacket = new EntityMovedPacket(
            player.PlayerId,
            fromPosition,
            player.Position,
            packet.Direction);

        string outgoingJson = PacketSerializer.Serialize(PacketType.EntityMoved, movedPacket);

        BroadcastToLoggedInSessions(outgoingJson);

        return null;
    }
    private static Chunk CreateFilledChunk(int chunkX, int chunkY, int tileId)
    {
        Chunk chunk = new Chunk(chunkX, chunkY);

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int y = 0; y < Chunk.Size; y++)
            {
                chunk.SetTileId(x, y, tileId);
            }
        }

        return chunk;
    }

    private void BroadcastToLoggedInSessions(string json)
    {
        foreach (ClientSession session in _sessions.Values)
        {
            if (!session.IsLoggedIn)
            {
                continue;
            }

            session.SendRaw(json);
        }
    }
}