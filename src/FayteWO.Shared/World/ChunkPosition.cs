namespace FayteWO.Shared.World;

public readonly record struct ChunkPosition(int X, int Y, int Z)
{
    public static ChunkPosition FromWorldPosition(TilePosition worldPosition)
    {
        int chunkX = Chunk.WorldToChunkCoordinate(worldPosition.X);
        int chunkY = Chunk.WorldToChunkCoordinate(worldPosition.Y);
        int chunkZ = worldPosition.Z;

        return new ChunkPosition(chunkX, chunkY, chunkZ);
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }
}