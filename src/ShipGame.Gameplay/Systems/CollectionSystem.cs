using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class CollectionSystem
{
    public IReadOnlyList<ResourceCollectedFact> Resolve(World world, EntityId collector, long currentTick = long.MaxValue)
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
        var pullRange = Math.Max(radius * 3, radius + 80);
        var pullRangeSquared = (long)pullRange * pullRange;
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

            // Tractor pulls immediately (including grace); credit only after grace.
            if (pull > 0 && distanceSquared > 0 && distanceSquared <= pullRangeSquared)
            {
                position.X += Math.Clamp(dx, -pull, pull);
                position.Y += Math.Clamp(dy, -pull, pull);
                dx = collectorPosition.X - position.X;
                dy = collectorPosition.Y - position.Y;
                distanceSquared = (long)dx * dx + (long)dy * dy;
            }

            if (currentTick < item.CollectibleAfterTick)
                continue;

            if (distanceSquared <= (long)radius * radius)
            {
                item.Credited = true;
                collected.Add(new(pickup, item.ResourceId, item.Quantity));
                destroy.Add(pickup);
            }
        }
        foreach (var pickup in destroy)
            world.Destroy(pickup);
        return collected;
    }
}
