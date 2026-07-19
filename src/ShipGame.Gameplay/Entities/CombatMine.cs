using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class CombatMine
{
    public EntityId Entity { get; }

    public CombatMine(World world, EntityId entity, EntityId owner, Vector2 position, float damage)
    {
        Entity = entity;
        world.Set(entity, new Transform2(position, 0));
        world.Set(entity, new Collider(8, FlightCombatContext.MineLayer, FlightCombatContext.PlayerLayer, false));
        world.Set(entity, new Combatant(Faction.Hostile));
        world.Set(entity, new Mine(60, 480, 75, damage, owner));
    }
}
