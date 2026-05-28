namespace FayteWO.Shared.World;

public sealed class WorldMap
{
    private readonly Dictionary<ChunkPosition, Chunk> _chunks = new();

    public IReadOnlyDictionary<ChunkPosition, Chunk> Chunks => _chunks;

    public void AddChunk(Chunk chunk)
    {
        ChunkPosition position = new ChunkPosition(
            chunk.ChunkX,
            chunk.ChunkY,
            chunk.ChunkZ);

        if (_chunks.ContainsKey(position))
        {
            throw new InvalidOperationException($"Chunk already exists at {position}.");
        }

        _chunks.Add(position, chunk);
    }

    public bool TryGetChunk(ChunkPosition position, out Chunk? chunk)
    {
        return _chunks.TryGetValue(position, out chunk);
    }

    public bool TryGetChunkAtWorldPosition(TilePosition worldPosition, out Chunk? chunk)
    {
        ChunkPosition chunkPosition = ChunkPosition.FromWorldPosition(worldPosition);
        return TryGetChunk(chunkPosition, out chunk);
    }

    public bool TryGetTileId(TilePosition worldPosition, out int tileId)
    {
        tileId = 0;

        if (!TryGetChunkAtWorldPosition(worldPosition, out Chunk? chunk))
        {
            return false;
        }

        int localX = Chunk.WorldToLocalCoordinate(worldPosition.X);
        int localY = Chunk.WorldToLocalCoordinate(worldPosition.Y);
        int localZ = worldPosition.Z - chunk.ChunkZ;

        if (!chunk.ContainsLocalPosition(localX, localY, localZ))
        {
            return false;
        }

        tileId = chunk.GetTileId(localX, localY, localZ);
        return true;
    }

    public bool TrySetTileId(TilePosition worldPosition, int tileId)
    {
        if (!TryGetChunkAtWorldPosition(worldPosition, out Chunk? chunk))
        {
            return false;
        }

        int localX = Chunk.WorldToLocalCoordinate(worldPosition.X);
        int localY = Chunk.WorldToLocalCoordinate(worldPosition.Y);
        int localZ = worldPosition.Z - chunk.ChunkZ;

        if (!chunk.ContainsLocalPosition(localX, localY, localZ))
        {
            return false;
        }

        chunk.SetTileId(localX, localY, tileId, localZ);
        return true;
    }

    public IReadOnlyList<WorldTile> GetTilesInRange(int centerX, int centerY, int radius, int z = 0)
    {
        List<WorldTile> tilesInRange = new();

        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                TilePosition position = new TilePosition(x, y, z);

                if (TryGetTileId(position, out int tileId))
                {
                    tilesInRange.Add(new WorldTile(x, y, (ushort)tileId));
                }
            }
        }

        return tilesInRange;
    }
}