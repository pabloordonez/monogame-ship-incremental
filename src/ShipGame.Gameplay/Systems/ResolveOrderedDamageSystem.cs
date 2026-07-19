using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class ResolveOrderedDamageSystem(FlightCombatContext context) : ISystem
{
    public string Name => "ResolveOrderedDamage";

    public void Update(World world, long tick)
    {
        Array.Sort(context.Damage, 0, context.DamageCount, FlightCombatContext.DamageRequestComparer.Instance);
        var playerHullDamaged = false;
        for (var i = 0; i < context.DamageCount; i++)
        {
            var request = context.Damage[i];
            if (!context.IsTargetable(request.Target) || !context.Has<Health>(request.Target) ||
                context.Has<Invulnerability>(request.Target) && context.World.Get<Invulnerability>(request.Target).TicksRemaining > 0)
                continue;
            var remaining = request.Amount;
            if (context.Has<Shield>(request.Target))
            {
                ref var shield = ref context.World.Get<Shield>(request.Target);
                var absorbed = MathF.Min(shield.Current, remaining);
                if (absorbed > 0)
                {
                    var before = shield.Current;
                    shield = shield with { Current = before - absorbed, TicksSinceDamage = 0 };
                    remaining -= absorbed;
                    context.AddEvent(CombatEvent.Create(
                        CombatEventKind.ShieldDamaged,
                        tick,
                        request.Target,
                        request.Source,
                        amount: absorbed,
                        remaining: shield.Current));
                    if (before > 0 && shield.Current <= 0)
                        context.AddEvent(CombatEvent.Create(CombatEventKind.ShieldDepleted, tick, request.Target, request.Source));
                    if (request.Projectile &&
                        absorbed > 0 &&
                        context.Has<ReflectiveShield>(request.Target) &&
                        request.Source != default &&
                        context.IsTargetable(request.Source) &&
                        context.Has<Health>(request.Source))
                    {
                        var reflected = absorbed * context.World.Get<ReflectiveShield>(request.Target).ReflectFraction;
                        if (reflected > 0)
                            context.QueueDamage(request.Source, request.Target, reflected, projectile: false);
                    }
                }
            }
            if (remaining <= 0)
                continue;
            ref var health = ref context.World.Get<Health>(request.Target);
            var applied = MathF.Min(health.Current, remaining);
            health = health with { Current = health.Current - applied };
            context.AddEvent(CombatEvent.Create(
                CombatEventKind.HullDamaged,
                tick,
                request.Target,
                request.Source,
                amount: applied,
                remaining: health.Current));
            if (request.Target == context.Player && applied > 0)
                playerHullDamaged = true;
            if (health.Current <= 0)
                context.MarkDestroyed(request.Target, request.Source);
        }
        if (playerHullDamaged && context.Player != default && context.World.IsAlive(context.Player))
            context.World.Set(context.Player, new Invulnerability(21));
    }
}
