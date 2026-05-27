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

    private TcpListener? _listener;

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

    _listener = new TcpListener(IPAddress.Loopback, _port);
    _listener.Start();

    while (true)
    {
        TcpClient client = _listener.AcceptTcpClient();

        Task.Run(() => HandleClient(client));
    }
}

    private void HandleClient(TcpClient client)
{
    Console.WriteLine();
    Console.WriteLine("Client connected.");

    try
    {
        ClientSession session = new ClientSession(client, HandleRawPacket);
        session.Run();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Client session crashed: {ex.Message}");
    }
    finally
    {
        Console.WriteLine("Client disconnected.");
    }
}
    private string HandleRawPacket(ClientSession session, string json)
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
    private string HandleLoginRequest(ClientSession session, LoginRequestPacket packet)
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

        if (string.IsNullOrWhiteSpace(packet.Username))
        {
            LoginResultPacket failedLogin = new(
                Success: false,
                Message: "Username cannot be empty.",
                PlayerId: null,
                SpawnPosition: null);

            return PacketSerializer.Serialize(PacketType.LoginResult, failedLogin);
        }

        Guid playerId = Guid.NewGuid();
        TilePosition spawnPosition = new TilePosition(30, 0, 0);

        PlayerState player = new PlayerState(
            playerId,
            packet.Username,
            spawnPosition);

        _players[playerId] = player;
        session.SetPlayerId(playerId);

        Console.WriteLine($"Session {session.SessionId}: Created player: {player}");
        Console.WriteLine($"Session {session.SessionId}: Associated with PlayerId={playerId}");

        LoginResultPacket successfulLogin = new(
            Success: true,
            Message: "Login successful.",
            PlayerId: player.PlayerId,
            SpawnPosition: player.Position);

        return PacketSerializer.Serialize(PacketType.LoginResult, successfulLogin);
    }

    private string HandleMoveRequest(ClientSession session, MoveRequestPacket packet)
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

        return PacketSerializer.Serialize(PacketType.EntityMoved, movedPacket);
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
}