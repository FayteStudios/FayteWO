using FayteWO.Shared.World;

namespace FayteWO.Shared.Networking.Packets;

public sealed class PlayerStatePacket
{
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TilePosition Position { get; set; }

    
}