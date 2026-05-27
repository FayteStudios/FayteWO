using FayteWO.Shared.World;

namespace FayteWO.Shared.Networking.Packets;

public sealed record ChunkDataPacket(
    ChunkPosition ChunkPosition,
    int Size,
    int Height,
    int[] TileIds);