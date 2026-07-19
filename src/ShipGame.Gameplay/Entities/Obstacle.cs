using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class Obstacle
{
    public EntityId Entity { get; }

    public Obstacle(World world, EntityId entity, Vector2 position, float radius)
    {
        if (!float.IsFinite(radius) || radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        Entity = entity;
        world.Set(entity, new Transform2(position, 0));
        world.Set(entity, new Velocity2(Vector2.Zero));
        world.Set(entity, new Collider(
            radius,
            FlightCombatContext.ObstacleLayer,
            FlightCombatContext.PlayerLayer | FlightCombatContext.HostileLayer | FlightCombatContext.ProjectileLayer | FlightCombatContext.ObstacleLayer));
        world.Set(entity, new Combatant(Faction.Neutral));
    }
}
