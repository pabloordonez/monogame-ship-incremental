using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public enum CombatEventKind : byte
{
    WeaponFired,
    CollisionDetected,
    ShieldDamaged,
    ShieldDepleted,
    HullDamaged,
    EntityDestroyed,
    AbilityActivated,
    AbilityRejected,
    EnemySpawned,
    EliteActivated,
    MineTelegraphed,
    CommandRejected
}
