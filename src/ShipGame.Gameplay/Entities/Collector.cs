using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class Collector
{
    public EntityId Entity { get; }

    public Collector(World world, EntityId entity, WorldPosition position, int radius, int pullSpeedPerTick)
    {
        Entity = entity;
        world.Set(entity, position);
        world.Set(entity, new CollectionRadius
        {
            Radius = radius,
            PullSpeedPerTick = pullSpeedPerTick
        });
    }
}
