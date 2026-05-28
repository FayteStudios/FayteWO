namespace FayteWO.Shared.Networking;

public enum PacketType
{
    Unknown = 0,

    // Client to server
    LoginRequest = 100,
    MoveRequest = 101,
    ChatMessage = 102,
    WhisperMessage = 103,
    ChunkRequest = 104,
    TileDefinitionsRequest = 105,
    TileChangeRequest = 106,
    TileInteractionRequest = 107,

    // Server to client
    LoginResult = 200,
    EntityMoved = 201,
    ChunkData = 202,
    ServerMessage = 203,
    EntitySpawned = 204,
    EntityDespawned = 205,
    ChatBroadcast = 206,
    WhisperReceived = 207,
    TileDefinitions = 208,
    TileMapChunk = 209,
    TileChanged = 210
}