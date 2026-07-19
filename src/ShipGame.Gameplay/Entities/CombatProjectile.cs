using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class CombatProjectile
{
    public EntityId Entity { get; }

    public CombatProjectile(
        World world,
        EntityId entity,
        EntityId source,
        Vector2 direction,
        float damage,
        float speed,
        float range,
        Faction faction,
        int pierces,
        bool missile,
        EntityId target,
        float turnDegrees)
    {
        Entity = entity;
        var sourceTransform = world.Get<Transform2>(source);
        var radius = missile ? 5 : 3;
        var normalized = FlightCombatContext.NormalizeOr(direction, Vector2.UnitX);
        world.Set(entity, new Transform2(sourceTransform.Position + normalized * (radius + 20), MathF.Atan2(normalized.Y, normalized.X)));
        world.Set(entity, new Velocity2(normalized * speed));
        world.Set(entity, new Collider(radius, FlightCombatContext.ProjectileLayer, faction == Faction.Player ? FlightCombatContext.HostileLayer | FlightCombatContext.ObstacleLayer : FlightCombatContext.PlayerLayer | FlightCombatContext.ObstacleLayer, false));
        world.Set(entity, new Combatant(faction));
        world.Set(entity, new DamageSource(source, faction, damage, true));
        world.Set(entity, new Projectile(Math.Max(1, (int)MathF.Ceiling(range / speed * FlightCombatConstants.TickRate)), pierces, missile));
        if (missile)
            world.Set(entity, new Homing(target, speed, turnDegrees * MathF.PI / 180f));
    }
}
