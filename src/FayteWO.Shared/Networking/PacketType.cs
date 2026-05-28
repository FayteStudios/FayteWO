namespace FayteWO.Shared.Networking;

public enum PacketType
{
    Unknown = 0,

    // Client to server
    LoginRequest = 100,
    MoveRequest = 101,
    ChatMessage = 102,

    // Server to client
    LoginResult = 200,
    EntityMoved = 201,
    ChunkData = 202,
    ServerMessage = 203,
    EntitySpawned = 204,
    EntityDespawned = 205,
    ChatBroadcast = 206
}