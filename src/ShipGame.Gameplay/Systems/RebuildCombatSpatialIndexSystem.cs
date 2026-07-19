using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class RebuildCombatSpatialIndexSystem(FlightCombatContext context) : ISystem
{
    public string Name => "RebuildCombatSpatialIndex";

    public void Update(World world, long tick)
    {
        context.RebuildSortedLive();
        Array.Fill(context.GridHeads, -1);
        Array.Fill(context.GridNext, -1);
        context.SpatialCount = 0;
        for (var i = 0; i < context.SortedLive.Count; i++)
        {
            var entity = context.SortedLive[i];
            if (!context.Has<Transform2>(entity) || !context.Has<Collider>(entity) || context.Has<Destroyed>(entity))
                continue;
            var spatialIndex = context.SpatialCount++;
            context.SpatialEntities[spatialIndex] = entity;
            var cell = context.Cell(context.World.Get<Transform2>(entity).Position);
            context.GridNext[spatialIndex] = context.GridHeads[cell];
            context.GridHeads[cell] = spatialIndex;
        }
    }
}
