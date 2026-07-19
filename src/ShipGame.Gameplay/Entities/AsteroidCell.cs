using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class AsteroidCell
{
    public EntityId Entity { get; }

    public AsteroidCell(World world, EntityId entity, int cellId, AsteroidCellKind kind, int health, WorldPosition position)
    {
        Entity = entity;
        world.Set(entity, new MineableCell
        {
            CellId = cellId,
            Kind = kind,
            Health = health,
            Broken = false
        });
        world.Set(entity, position);
    }
}
