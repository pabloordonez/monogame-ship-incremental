using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

internal sealed class SapperAiStrategy : IEnemyAiStrategy
{
    public EnemyBehavior Behavior => EnemyBehavior.Sapper;

    public void Update(
        long tick,
        EntityId entity,
        EnemyDefinition definition,
        Vector2 position,
        float distance,
        Vector2 direction,
        float effectiveDamage,
        int effectiveCadence,
        ref AiBrain brain,
        ref WeaponState weapon,
        ref Vector2 move,
        ref FlightAction actions,
        EnemyAiCombatActions combat)
    {
        move = distance > definition.PreferredRange ? direction : -direction;
        if (weapon.CooldownTicks == 0 && brain.ActiveMines < 2)
        {
            combat.SpawnMine(entity, position, effectiveDamage);
            brain = brain with { ActiveMines = brain.ActiveMines + 1 };
            weapon = weapon with { CooldownTicks = effectiveCadence };
        }
    }
}
