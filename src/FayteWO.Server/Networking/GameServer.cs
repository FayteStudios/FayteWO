using System.Net;
using System.Net.Sockets;
using FayteWO.Server.Systems;
using FayteWO.Shared.Entities;
using FayteWO.Shared.Networking;
using FayteWO.Shared.Networking.Packets;
using FayteWO.Shared.World;

namespace FayteWO.Server.Networking;

public sealed class GameServer
{
    private readonly int _port;
    private readonly WorldMap _worldMap;
    private readonly MovementSystem _movementSystem;
    private readonly Dictionary<Guid, PlayerState> _players = new();

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
        Console.WriteLine("Start the FayteWO.Client project in another terminal.");

        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();

        while (true)
        {
            TcpClient client = _listener.AcceptTcpClient();

            Console.WriteLine();
            Console.WriteLine("Client connected.");

            ClientSession session = new ClientSession(client, HandleRawPacket);
            session.Run();

            Console.WriteLine("Client disconnected.");
        }
    }

    private string HandleRawPacket(string json)
    {
        try
        {
            NetworkPacket packet = PacketSerializer.DeserializeEnvelope(json);

            return packet.Type switch
            {
                PacketType.LoginRequest => HandleLoginRequest(
                    PacketSerializer.DeserializePayload<LoginRequestPacket>(packet)),

                PacketType.MoveRequest => HandleMoveRequest(
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

    private string HandleLoginRequest(LoginRequestPacket packet)
    {
        Console.WriteLine($"Decoded LoginRequest: Username={packet.Username}");

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

        _players.Add(playerId, player);

        Console.WriteLine($"Created player: {player}");

        LoginResultPacket successfulLogin = new(
            Success: true,
            Message: "Login successful.",
            PlayerId: player.PlayerId,
            SpawnPosition: player.Position);

        return PacketSerializer.Serialize(PacketType.LoginResult, successfulLogin);
    }

    private string HandleMoveRequest(MoveRequestPacket packet)
    {
        Console.WriteLine($"Decoded MoveRequest: Player={packet.PlayerId}, Direction={packet.Direction}");

        if (!_players.TryGetValue(packet.PlayerId, out PlayerState? player))
        {
            return PacketSerializer.Serialize(
                PacketType.ServerMessage,
                new ServerMessagePacket("Move rejected: unknown player."));
        }

        TilePosition fromPosition = player.Position;

        bool moved = _movementSystem.TryMove(player, packet.Direction, out string reason);

        Console.WriteLine(moved ? "Move accepted." : "Move rejected.");
        Console.WriteLine(reason);
        Console.WriteLine($"Player position: {player.Position}");

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