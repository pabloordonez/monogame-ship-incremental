using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class IntegrateFlightMovementSystem(FlightCombatContext context) : ISystem
{
    public string Name => "IntegrateFlightMovement";

    public void Update(World world, long tick)
    {
        context.RebuildSortedLive();
        for (var i = 0; i < context.SortedLive.Count; i++)
        {
            var entity = context.SortedLive[i];
            if (!context.Has<Velocity2>(entity) || !context.Has<Transform2>(entity) || context.Has<Destroyed>(entity))
                continue;
            ref var velocity = ref context.World.Get<Velocity2>(entity);
            ref var transform = ref context.World.Get<Transform2>(entity);
            if (context.Has<FlightStatistics>(entity) && context.Has<ControlIntent>(entity))
            {
                var statistics = context.World.Get<FlightStatistics>(entity);
                var intent = context.World.Get<ControlIntent>(entity);
                var modifiers = context.Has<TemporaryCombatModifiers>(entity)
                    ? context.World.Get<TemporaryCombatModifiers>(entity)
                    : FlightCombatContext.DefaultModifiers();
                var maxSpeed = statistics.MaximumSpeed * modifiers.SpeedMultiplier;
                var facing = ShipRelativeMovement.FacingFromAim(intent.Aim, transform.Rotation);
                var move = context.Has<PlayerControlled>(entity)
                    ? ShipRelativeMovement.ToWorld(intent.Move, facing)
                    : intent.Move;
                var target = move * maxSpeed;
                var rate = move.LengthSquared() > 0.0001f ? statistics.Acceleration : statistics.Braking;
                velocity = new Velocity2(FlightCombatContext.MoveTowards(velocity.Value, target, rate * FlightCombatConstants.TickSeconds));
                if (velocity.Value.LengthSquared() > maxSpeed * maxSpeed)
                    velocity = new Velocity2(Vector2.Normalize(velocity.Value) * maxSpeed);
                transform = transform with { Rotation = facing };
            }
            if (context.Has<Homing>(entity))
                context.GuideProjectile(entity);
            // Missiles may fly straight without Homing; keep Transform.Rotation on velocity heading.
            if (context.Has<Projectile>(entity) &&
                context.World.Get<Projectile>(entity).IsMissile &&
                velocity.Value.LengthSquared() > 0.0001f)
            {
                transform = transform with
                {
                    Rotation = MathF.Atan2(velocity.Value.Y, velocity.Value.X)
                };
            }

            transform = transform with { Position = transform.Position + velocity.Value * FlightCombatConstants.TickSeconds };
        }
    }
}
