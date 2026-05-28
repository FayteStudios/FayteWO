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

        Chunk chunk00 = CreateFilledChunk(0, 0, grass.TileId);
        Chunk chunk10 = CreateFilledChunk(1, 0, grass.TileId);

        _worldMap.AddChunk(chunk00);
        _worldMap.AddChunk(chunk10);

        // Block movement at world position 33,0.
        _worldMap.TrySetTileId(new TilePosition(33, 0, 0), wall.TileId);

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

            string command = input.ToLowerInvariant();

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

    private void PrintServerHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Server commands:");
        Console.WriteLine("  players    List online players");
        Console.WriteLine("  sessions   List active client sessions");
        Console.WriteLine("  help       Show server commands");
        Console.WriteLine("  stop       Stop the server");
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