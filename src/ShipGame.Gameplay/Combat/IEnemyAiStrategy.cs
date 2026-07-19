using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

internal interface IEnemyAiStrategy
{
    EnemyBehavior Behavior { get; }

    void Update(
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
        EnemyAiCombatActions combat);
}
