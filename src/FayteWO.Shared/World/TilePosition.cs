namespace FayteWO.Shared.World;

public readonly record struct TilePosition(int X, int Y, int Z)
{
    public static TilePosition Zero => new(0, 0, 0);

    public TilePosition Offset(int x, int y, int z = 0)
    {
        return new TilePosition(X + x, Y + y, Z + z);
    }

    public TilePosition Offset(Direction direction)
    {
        TilePosition offset = direction.ToOffset();
        return new TilePosition(X + offset.X, Y + offset.Y, Z + offset.Z);
    }

    public int ManhattanDistanceTo(TilePosition other)
    {
        return Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z);
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }
}