using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class AiAndThreatDecisionsSystem(FlightCombatContext context) : ISystem
{
    public string Name => "AiAndThreatDecisions";

    public void Update(World world, long tick)
    {
        context.RebuildSortedLive();
        var count = context.SortedLive.Count;
        for (var i = 0; i < count; i++)
        {
            var entity = context.SortedLive[i];
            if (!context.Has<AiBrain>(entity) || context.Has<Destroyed>(entity))
                continue;
            if (context.Player == default || !context.IsTargetable(context.Player))
            {
                ref var noTargetIntent = ref context.World.Get<ControlIntent>(entity);
                noTargetIntent = noTargetIntent with { Move = Vector2.Zero, Actions = FlightAction.None };
                continue;
            }
            var definition = context.Registry.Enemy(context.World.Get<WeaponMount>(entity).BehaviorId);
            var transform = context.World.Get<Transform2>(entity);
            var targetTransform = context.World.Get<Transform2>(context.Player);
            var delta = targetTransform.Position - transform.Position;
            var distance = delta.Length();
            var direction = FlightCombatContext.NormalizeOr(delta, Vector2.UnitX);
            ref var brain = ref context.World.Get<AiBrain>(entity);
            ref var intent = ref context.World.Get<ControlIntent>(entity);
            ref var weapon = ref context.World.Get<WeaponState>(entity);
            var move = Vector2.Zero;
            var actions = FlightAction.None;
            context.EnemyAiStrategies.Get(brain.Behavior).Update(
                tick,
                entity,
                definition,
                transform.Position,
                distance,
                direction,
                context.EffectiveEnemyDamage(entity, definition.Damage),
                context.EffectiveEnemyCadence(entity, definition.CadenceTicks),
                ref brain,
                ref weapon,
                ref move,
                ref actions,
                context.EnemyAiCombat);
            intent = new ControlIntent(move, direction, actions, intent.Actions);
        }

        if (!context.ThreatEnabled || tick == 0 || tick % context.ThreatIntervalTicks != 0 || context.Player == default)
            return;
        var hostileCount = 0;
        for (var i = 0; i < context.SortedLive.Count; i++)
            if (context.Has<AiBrain>(context.SortedLive[i]) && !context.Has<Destroyed>(context.SortedLive[i]))
                hostileCount++;
        if (hostileCount >= context.ThreatCap)
            return;
        Span<int> valid = stackalloc int[64];
        var validCount = 0;
        var playerPosition = context.World.Get<Transform2>(context.Player).Position;
        for (var i = 0; i < context.Anchors.Count; i++)
            if (context.Anchors[i].OutsideCamera && Vector2.DistanceSquared(context.Anchors[i].Position, playerPosition) >= 450 * 450)
                valid[validCount++] = i;
        if (validCount == 0)
            return;
        var rng = context.Random.Get(RngStream.Encounter);
        var anchor = context.Anchors[valid[(int)(rng.NextUInt() % (uint)validCount)]];
        var enemy = (rng.NextUInt() % 3) switch
        {
            0 => new ContentId("ENM_INTERCEPTOR"),
            1 => new ContentId("ENM_GUNSHIP"),
            _ => new ContentId("ENM_SAPPER")
        };
        context.SpawnEnemy(enemy, anchor.Position);
    }
}
