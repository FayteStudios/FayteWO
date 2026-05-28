namespace FayteWO.Shared.World;

public sealed record TileDefinitionDto(
    int TileId,
    string Name,
    TileFlags Flags,
    char MapSymbol);