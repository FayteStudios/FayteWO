namespace FayteWO.Shared.World;

public sealed class WorldTile
{
    public int X { get; }
    public int Y { get; }
    public ushort TileDefinitionId { get; }

    public WorldTile(int x, int y, ushort tileDefinitionId)
    {
        X = x;
        Y = y;
        TileDefinitionId = tileDefinitionId;
    }
}