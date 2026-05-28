using FayteWO.Shared.World;

namespace FayteWO.Shared.Networking.Packets;

public sealed record TileDefinitionsPacket(
    IReadOnlyList<TileDefinitionDto> Tiles);