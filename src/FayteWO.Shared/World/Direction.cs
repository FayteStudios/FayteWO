namespace FayteWO.Shared.World;

public enum Direction
{
    North = 0,
    East = 1,
    South = 2,
    West = 3
}

public static class DirectionExtensions
{
    public static TilePosition ToOffset(this Direction direction)
    {
        return direction switch
        {
            Direction.North => new TilePosition(0, -1, 0),
            Direction.East => new TilePosition(1, 0, 0),
            Direction.South => new TilePosition(0, 1, 0),
            Direction.West => new TilePosition(-1, 0, 0),
            _ => TilePosition.Zero
        };
    }
}