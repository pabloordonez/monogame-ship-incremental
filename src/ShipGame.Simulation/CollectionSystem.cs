using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public sealed class CollectionSystem
{
    public IReadOnlyList<ResourceCollectedFact> Resolve(World world, EntityId collector)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.IsAlive(collector) ||
            !world.Store<WorldPosition>().Has(collector) ||
            !world.Store<CollectionRadius>().Has(collector))
            return Array.Empty<ResourceCollectedFact>();

        var collectorPosition = world.Store<WorldPosition>().Read(collector);
        var collection = world.Store<CollectionRadius>().Read(collector);
        var radius = Math.Clamp(collection.Radius, 0, 10_000);
        var pull = Math.Clamp(collection.PullSpeedPerTick, 0, 1_000);
        var collected = new List<ResourceCollectedFact>();
        var destroy = new List<EntityId>();
        foreach (var pickup in world.Query<Collectible, WorldPosition>())
        {
            ref var item = ref world.Get<Collectible>(pickup);
            if (item.Credited || item.Quantity <= 0)
                continue;
            ref var position = ref world.Get<WorldPosition>(pickup);
            var dx = collectorPosition.X - position.X;
            var dy = collectorPosition.Y - position.Y;
            var distanceSquared = (long)dx * dx + (long)dy * dy;
            if (distanceSquared <= (long)radius * radius)
            {
                item.Credited = true;
                collected.Add(new(pickup, item.ResourceId, item.Quantity));
                destroy.Add(pickup);
                continue;
            }
            if (pull == 0)
                continue;
            position.X += Math.Clamp(dx, -pull, pull);
            position.Y += Math.Clamp(dy, -pull, pull);
        }
        foreach (var pickup in destroy)
            world.Destroy(pickup);
        return collected;
    }
}
