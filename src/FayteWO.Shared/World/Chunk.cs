namespace FayteWO.Shared.World;

public sealed class Chunk
{
    public const int Size = 32;

    private readonly int[,,] _tileIds;

    public int ChunkX { get; }
    public int ChunkY { get; }
    public int ChunkZ { get; }
    public int Height { get; }

    public Chunk(int chunkX, int chunkY, int chunkZ = 0, int height = 1)
    {
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Chunk height must be greater than zero.");
        }

        ChunkX = chunkX;
        ChunkY = chunkY;
        ChunkZ = chunkZ;
        Height = height;

        _tileIds = new int[Size, Size, Height];
    }

    public int GetTileId(int localX, int localY, int localZ = 0)
    {
        ValidateLocalPosition(localX, localY, localZ);
        return _tileIds[localX, localY, localZ];
    }

    public void SetTileId(int localX, int localY, int tileId, int localZ = 0)
    {
        ValidateLocalPosition(localX, localY, localZ);

        if (tileId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileId), "Tile ID cannot be negative.");
        }

        _tileIds[localX, localY, localZ] = tileId;
    }

    public bool ContainsLocalPosition(int localX, int localY, int localZ = 0)
    {
        return localX >= 0 &&
               localX < Size &&
               localY >= 0 &&
               localY < Size &&
               localZ >= 0 &&
               localZ < Height;
    }

    public TilePosition GetWorldPosition(int localX, int localY, int localZ = 0)
    {
        ValidateLocalPosition(localX, localY, localZ);

        int worldX = ChunkX * Size + localX;
        int worldY = ChunkY * Size + localY;
        int worldZ = ChunkZ + localZ;

        return new TilePosition(worldX, worldY, worldZ);
    }

    public static int WorldToChunkCoordinate(int worldCoordinate)
    {
        return Math.DivRem(worldCoordinate, Size, out int remainder) switch
        {
            int quotient when worldCoordinate >= 0 || remainder == 0 => quotient,
            int quotient => quotient - 1
        };
    }

    public static int WorldToLocalCoordinate(int worldCoordinate)
    {
        int local = worldCoordinate % Size;

        if (local < 0)
        {
            local += Size;
        }

        return local;
    }

    private void ValidateLocalPosition(int localX, int localY, int localZ)
    {
        if (!ContainsLocalPosition(localX, localY, localZ))
        {
            throw new ArgumentOutOfRangeException(
                $"Local position ({localX}, {localY}, {localZ}) is outside chunk bounds.");
        }
    }

    public int[] ToFlatTileIdArray()
    {
        int[] tileIds = new int[Size * Size * Height];

        int index = 0;

        for (int z = 0; z < Height; z++)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    tileIds[index] = _tileIds[x, y, z];
                    index++;
                }
            }
        }

        return tileIds;
    }

    public void LoadFromFlatTileIdArray(int[] tileIds)
    {
        int expectedLength = Size * Size * Height;

        if (tileIds.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Expected {expectedLength} tile IDs, but received {tileIds.Length}.",
                nameof(tileIds));
        }

        int index = 0;

        for (int z = 0; z < Height; z++)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int tileId = tileIds[index];

                    if (tileId < 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(tileIds),
                            $"Tile ID cannot be negative. Invalid value {tileId} at flat index {index}.");
                    }

                    _tileIds[x, y, z] = tileId;
                    index++;
                }
            }
        }
    }

    public override string ToString()
    {
        return $"Chunk ({ChunkX}, {ChunkY}, {ChunkZ})";
    }
}