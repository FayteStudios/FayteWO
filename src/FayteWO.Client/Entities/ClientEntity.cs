using FayteWO.Shared.World;

namespace FayteWO.Client.Entities;

public sealed class ClientEntity
{
    public Guid EntityId { get; }
    public string Name { get; }
    public TilePosition Position { get; private set; }

    public ClientEntity(Guid entityId, string name, TilePosition position)
    {
        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("Entity ID cannot be empty.", nameof(entityId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Entity name cannot be empty.", nameof(name));
        }

        EntityId = entityId;
        Name = name;
        Position = position;
    }

    public void SetPosition(TilePosition position)
    {
        Position = position;
    }

    public override string ToString()
    {
        return $"{Name} [{EntityId}] at {Position}";
    }
}