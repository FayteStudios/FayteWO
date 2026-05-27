namespace FayteWO.Shared.World;

[Flags]
public enum TileFlags
{
    None = 0,
    BlocksMovement = 1 << 0,
    BlocksSight = 1 << 1,
    Water = 1 << 2,
    Resource = 1 << 3,
    Indoor = 1 << 4,
    Road = 1 << 5
}

public sealed class Tile
{
    public int TileId { get; }
    public string Name { get; }
    public TileFlags Flags { get; }

    public bool BlocksMovement => Flags.HasFlag(TileFlags.BlocksMovement);
    public bool BlocksSight => Flags.HasFlag(TileFlags.BlocksSight);

    public Tile(int tileId, string name, TileFlags flags = TileFlags.None)
    {
        if (tileId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileId), "Tile ID cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tile name cannot be empty.", nameof(name));
        }

        TileId = tileId;
        Name = name;
        Flags = flags;
    }

    public override string ToString()
    {
        return $"{Name} [{TileId}]";
    }
}