using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class InterceptorAiStrategy : IEnemyAiStrategy
{
    public EnemyBehavior Behavior => EnemyBehavior.Interceptor;

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
        if (brain.StateTicks > 0)
        {
            move = -direction;
            brain = brain with { StateTicks = brain.StateTicks - 1 };
        }
        else
        {
            move = distance > definition.PreferredRange ? direction : FlightCombatMath.Perpendicular(direction);
            if (weapon.CooldownTicks == 0)
            {
                brain = brain with { BurstShotsRemaining = 3, StateTicks = 60 };
                weapon = weapon with { CooldownTicks = definition.CadenceTicks };
            }
        }

        if (brain.BurstShotsRemaining > 0 && tick % 9 == 0)
        {
            combat.SpawnHostileProjectile(entity, direction, definition.Damage, 520);
            brain = brain with { BurstShotsRemaining = brain.BurstShotsRemaining - 1 };
        }
    }
}
