using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class ResourcePickup
{
    public EntityId Entity { get; }

    public ResourcePickup(
        World world,
        EntityId entity,
        WorldPosition position,
        ContentId resourceId,
        int quantity,
        long collectibleAfterTick = 0)
    {
        Entity = entity;
        world.Set(entity, position);
        world.Set(entity, new Collectible
        {
            ResourceId = resourceId,
            Quantity = quantity,
            CollectibleAfterTick = collectibleAfterTick
        });
    }
}
