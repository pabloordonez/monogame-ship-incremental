using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class DetectCombatCollisionsSystem(FlightCombatContext context) : ISystem
{
    public string Name => "DetectCombatCollisions";

    public void Update(World world, long tick)
    {
        context.PairCount = 0;
        for (var firstIndex = 0; firstIndex < context.SpatialCount; firstIndex++)
        {
            var first = context.SpatialEntities[firstIndex];
            if (!context.Has<Transform2>(first) || !context.Has<Collider>(first) || context.Has<Destroyed>(first))
                continue;
            var firstTransform = context.World.Get<Transform2>(first);
            var cellX = FlightCombatContext.CellCoordinate(firstTransform.Position.X);
            var cellY = FlightCombatContext.CellCoordinate(firstTransform.Position.Y);
            for (var y = Math.Max(0, cellY - 1); y <= Math.Min(FlightCombatContext.GridWidth - 1, cellY + 1); y++)
            for (var x = Math.Max(0, cellX - 1); x <= Math.Min(FlightCombatContext.GridWidth - 1, cellX + 1); x++)
            for (var secondIndex = context.GridHeads[y * FlightCombatContext.GridWidth + x]; secondIndex >= 0; secondIndex = context.GridNext[secondIndex])
            {
                if (secondIndex <= firstIndex)
                    continue;
                var second = context.SpatialEntities[secondIndex];
                var firstCollider = context.World.Get<Collider>(first);
                var secondCollider = context.World.Get<Collider>(second);
                if ((firstCollider.Mask & secondCollider.Layer) == 0 &&
                    (secondCollider.Mask & firstCollider.Layer) == 0)
                    continue;
                var secondTransform = context.World.Get<Transform2>(second);
                var radius = firstCollider.Radius + secondCollider.Radius;
                if (Vector2.DistanceSquared(firstTransform.Position, secondTransform.Position) > radius * radius)
                    continue;
                if (context.PairCount >= context.Pairs.Length)
                    throw new InvalidOperationException("Collision work exceeded the deterministic per-tick bound.");
                context.Pairs[context.PairCount++] = new FlightCombatContext.CollisionPair(first, second);
            }
        }
        Array.Sort(context.Pairs, 0, context.PairCount, FlightCombatContext.CollisionPairComparer.Instance);
        for (var i = 0; i < context.PairCount; i++)
            context.ResolveCollision(context.Pairs[i]);
    }
}
