using FayteWO.Shared.World;

namespace FayteWO.Shared.Entities;

public sealed class PlayerState
{
    public Guid PlayerId { get; }
    public string Name { get; }
    public TilePosition Position { get; private set; }

    public PlayerState(Guid playerId, string name, TilePosition startPosition)
    {
        if (playerId == Guid.Empty)
        {
            throw new ArgumentException("Player ID cannot be empty.", nameof(playerId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Player name cannot be empty.", nameof(name));
        }

        PlayerId = playerId;
        Name = name;
        Position = startPosition;
    }

    public void SetPosition(TilePosition position)
    {
        Position = position;
    }

    public override string ToString()
    {
        return $"{Name} [{PlayerId}] at {Position}";
    }
}