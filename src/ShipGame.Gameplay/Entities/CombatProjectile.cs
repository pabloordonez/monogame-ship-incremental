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
        float turnDegrees,
        Vector2? originOverride = null,
        bool detonateOnHit = false)
    {
        Entity = entity;
        var sourceTransform = world.Get<Transform2>(source);
        var radius = missile ? 5 : 3;
        var normalized = FlightCombatContext.NormalizeOr(direction, Vector2.UnitX);
        var origin = originOverride ?? sourceTransform.Position;
        var muzzle = originOverride is null ? radius + 20 : radius + 6;
        world.Set(entity, new Transform2(origin + normalized * muzzle, MathF.Atan2(normalized.Y, normalized.X)));
        world.Set(entity, new Velocity2(normalized * speed));
        world.Set(entity, new Collider(radius, FlightCombatContext.ProjectileLayer, faction == Faction.Player ? FlightCombatContext.HostileLayer | FlightCombatContext.ObstacleLayer : FlightCombatContext.PlayerLayer | FlightCombatContext.ObstacleLayer, false));
        world.Set(entity, new Combatant(faction));
        world.Set(entity, new DamageSource(source, faction, damage, true));
        world.Set(entity, new Projectile(
            Math.Max(1, (int)MathF.Ceiling(range / speed * FlightCombatConstants.TickRate)),
            pierces,
            missile,
            detonateOnHit));
        if (missile)
            world.Set(entity, new Homing(target, speed, turnDegrees * MathF.PI / 180f));
    }
}
