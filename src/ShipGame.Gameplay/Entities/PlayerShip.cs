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
        MobilityBehavior mobility)
    {
        Entity = entity;
        world.Set(entity, new Transform2(position, 0));
        world.Set(entity, new Velocity2(Vector2.Zero));
        world.Set(entity, new Collider(18, FlightCombatContext.PlayerLayer, FlightCombatContext.HostileLayer | FlightCombatContext.ObstacleLayer | FlightCombatContext.MineLayer));
        world.Set(entity, new FlightStatistics(900, 1_100, mobility == MobilityBehavior.Dash ? 220 : 200));
        world.Set(entity, new PlayerControlled());
        world.Set(entity, new ControlIntent(Vector2.Zero, Vector2.UnitX, FlightAction.None, FlightAction.None));
        world.Set(entity, new Combatant(Faction.Player));
        world.Set(entity, new Health(100, 100));
        world.Set(entity, new Shield(60, 60, 12, 180, 180));
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
