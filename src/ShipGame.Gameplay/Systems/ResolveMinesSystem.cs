using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class ResolveMinesSystem(FlightCombatContext context) : ISystem
{
    public string Name => "ResolveMines";

    public void Update(World world, long tick)
    {
        context.RebuildSortedLive();
        var count = context.SortedLive.Count;
        for (var i = 0; i < count; i++)
        {
            var entity = context.SortedLive[i];
            if (!context.Has<Mine>(entity) || context.Has<Destroyed>(entity))
                continue;
            var mine = context.World.Get<Mine>(entity);
            if (mine.ArmTicks != 0 || context.Player == default || !context.IsTargetable(context.Player))
                continue;
            var position = context.World.Get<Transform2>(entity).Position;
            var playerPosition = context.World.Get<Transform2>(context.Player).Position;
            if (Vector2.DistanceSquared(position, playerPosition) > mine.Radius * mine.Radius)
                continue;
            context.QueueDamage(context.Player, mine.Owner, mine.Damage, false);
            context.MarkDestroyed(entity, mine.Owner);
            if (context.World.IsAlive(mine.Owner) && context.Has<AiBrain>(mine.Owner))
            {
                ref var brain = ref context.World.Get<AiBrain>(mine.Owner);
                brain = brain with { ActiveMines = Math.Max(0, brain.ActiveMines - 1) };
            }
        }
    }
}
