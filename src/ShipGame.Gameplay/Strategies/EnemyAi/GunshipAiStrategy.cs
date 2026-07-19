using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class GunshipAiStrategy : IEnemyAiStrategy
{
    public EnemyBehavior Behavior => EnemyBehavior.Gunship;

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
        move = distance > definition.PreferredRange + 20
            ? direction
            : distance < definition.PreferredRange - 20 ? -direction : FlightCombatMath.Perpendicular(direction);
        if (weapon.CooldownTicks == 0)
        {
            combat.SpawnHostileProjectile(entity, direction, effectiveDamage, 420);
            weapon = weapon with { CooldownTicks = effectiveCadence };
        }
    }
}
