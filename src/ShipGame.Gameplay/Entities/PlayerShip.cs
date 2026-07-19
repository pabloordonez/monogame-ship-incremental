using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class PlayerShip
{
    public EntityId Entity { get; }

    public PlayerShip(
        World world,
        EntityId entity,
        Vector2 position,
        ContentId weaponId,
        WeaponDefinition weapon,
        MobilityBehavior mobility,
        PlayerSpawnStats stats)
    {
        Entity = entity;
        world.Set(entity, new Transform2(position, 0));
        world.Set(entity, new Velocity2(Vector2.Zero));
        world.Set(entity, new Collider(18, FlightCombatContext.PlayerLayer, FlightCombatContext.HostileLayer | FlightCombatContext.ObstacleLayer | FlightCombatContext.MineLayer));
        world.Set(entity, new FlightStatistics(900, 1_100, stats.MaximumSpeed));
        world.Set(entity, new PlayerControlled());
        world.Set(entity, new ControlIntent(Vector2.Zero, Vector2.UnitX, FlightAction.None, FlightAction.None));
        world.Set(entity, new Combatant(Faction.Player));
        world.Set(entity, new Health(stats.MaximumHull, stats.MaximumHull));
        world.Set(entity, new Shield(
            stats.ShieldCapacity,
            stats.ShieldCapacity,
            stats.ShieldRechargePerSecond,
            stats.ShieldDelayTicks,
            stats.ShieldDelayTicks));
        if (stats.ReflectiveFraction > 0)
            world.Set(entity, new ReflectiveShield(stats.ReflectiveFraction));
        world.Set(entity, new WeaponMount(weaponId, weapon.Behavior));
        world.Set(entity, new WeaponState(0, 0, false, default));
        var abilityId = new ContentId(mobility == MobilityBehavior.Dash ? "MOD_ENGINE_VECTOR" : "MOD_ENGINE_BLINK");
        world.Set(
            entity,
            mobility == MobilityBehavior.Dash
                ? new MobilityAbility(abilityId, mobility, 180, 11, 240, 0, 0, Vector2.Zero)
                : new MobilityAbility(abilityId, mobility, 260, 0, 360, 0, 0, Vector2.Zero));
        world.Set(entity, FlightCombatContext.DefaultModifiers());
    }
}

public readonly record struct PlayerSpawnStats(
    float MaximumHull,
    float MaximumSpeed,
    float ShieldCapacity,
    float ShieldRechargePerSecond,
    int ShieldDelayTicks,
    float ReflectiveFraction)
{
    public static PlayerSpawnStats Defaults { get; } = new(100, 220, 60, 12, 180, 0);
}
