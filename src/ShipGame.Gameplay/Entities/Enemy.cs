using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class Enemy
{
    public EntityId Entity { get; }

    public Enemy(
        World world,
        EntityId entity,
        ContentId enemyId,
        EnemyDefinition definition,
        Vector2 position,
        EntityId playerTarget,
        bool elite = false,
        float environmentHullMultiplier = 1f)
    {
        Entity = entity;
        var healthMultiplier = (elite ? 2.75f : 1f) * environmentHullMultiplier;
        var speedMultiplier = elite ? 1.10f : 1;
        world.Set(entity, new Transform2(position, 0));
        world.Set(entity, new Velocity2(Vector2.Zero));
        world.Set(entity, new Collider(16 * (elite ? 1.35f : 1), FlightCombatContext.HostileLayer, FlightCombatContext.PlayerLayer | FlightCombatContext.ObstacleLayer | FlightCombatContext.ProjectileLayer));
        world.Set(entity, new FlightStatistics(definition.Speed * 5, definition.Speed * 7, definition.Speed * speedMultiplier));
        world.Set(entity, new ControlIntent(Vector2.Zero, -Vector2.UnitX, FlightAction.None, FlightAction.None));
        world.Set(entity, new Combatant(Faction.Hostile));
        world.Set(entity, new Health(definition.Hull * healthMultiplier, definition.Hull * healthMultiplier));
        world.Set(entity, new AiBrain(definition.Behavior, 0, 0, 0));
        world.Set(entity, new ThreatValue(definition.Behavior == EnemyBehavior.Gunship ? 2 : 1));
        world.Set(entity, new Target(playerTarget));
        world.Set(entity, new WeaponState(definition.CadenceTicks, 0, false, default));
        world.Set(entity, new WeaponMount(enemyId, WeaponBehavior.Pulse));
        world.Set(entity, FlightCombatContext.DefaultModifiers());
        if (elite)
            world.Set(entity, new Elite(1.35f));
    }
}
