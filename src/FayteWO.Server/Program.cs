using System.Net;
using System.Net.Sockets;
using System.Text;
using FayteWO.Server.Systems;
using FayteWO.Shared.Entities;
using FayteWO.Shared.Networking;
using FayteWO.Shared.Networking.Packets;
using FayteWO.Shared.World;

const int port = 7777;

Console.WriteLine("FayteWO Server Starting...");

Tile grass = new Tile(1, "Grass");
Tile wall = new Tile(2, "Stone Wall", TileFlags.BlocksMovement | TileFlags.BlocksSight);

List<Tile> tileDefinitions =
[
    grass,
    wall
];

WorldMap worldMap = new WorldMap();

Chunk chunk00 = CreateFilledChunk(0, 0, grass.TileId);
Chunk chunk10 = CreateFilledChunk(1, 0, grass.TileId);

worldMap.AddChunk(chunk00);
worldMap.AddChunk(chunk10);

// Block movement at world position 33,0.
worldMap.TrySetTileId(new TilePosition(33, 0, 0), wall.TileId);

Dictionary<Guid, PlayerState> players = new();

MovementSystem movementSystem = new MovementSystem(worldMap, tileDefinitions);

Console.WriteLine($"Listening on 127.0.0.1:{port}");
Console.WriteLine("Start the FayteWO.Client project in another terminal.");

TcpListener listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();

while (true)
{
    using TcpClient client = listener.AcceptTcpClient();

    Console.WriteLine();
    Console.WriteLine("Client connected.");

    using NetworkStream stream = client.GetStream();
    using StreamReader reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
    using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)
    {
        AutoFlush = true
    };

    string? incomingJson = reader.ReadLine();

    if (string.IsNullOrWhiteSpace(incomingJson))
    {
        Console.WriteLine("Received empty packet.");
        continue;
    }

    Console.WriteLine($"Received raw packet: {incomingJson}");

    string responseJson = HandleRawPacket(incomingJson);

    Console.WriteLine($"Sending response: {responseJson}");
    writer.WriteLine(responseJson);

    Console.WriteLine("Client disconnected.");
}

Chunk CreateFilledChunk(int chunkX, int chunkY, int tileId)
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

string HandleRawPacket(string json)
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

string HandleLoginRequest(LoginRequestPacket packet)
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

    players.Add(playerId, player);

    Console.WriteLine($"Created player: {player}");

    LoginResultPacket successfulLogin = new(
        Success: true,
        Message: "Login successful.",
        PlayerId: player.PlayerId,
        SpawnPosition: player.Position);

    return PacketSerializer.Serialize(PacketType.LoginResult, successfulLogin);
}

string HandleMoveRequest(MoveRequestPacket packet)
{
    Console.WriteLine($"Decoded MoveRequest: Player={packet.PlayerId}, Direction={packet.Direction}");

    if (!players.TryGetValue(packet.PlayerId, out PlayerState? player))
    {
        return PacketSerializer.Serialize(
            PacketType.ServerMessage,
            new ServerMessagePacket("Move rejected: unknown player."));
    }

    TilePosition fromPosition = player.Position;

    bool moved = movementSystem.TryMove(player, packet.Direction, out string reason);

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