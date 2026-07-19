using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct EnemyDefinition(
    ContentId Id,
    EnemyBehavior Behavior,
    float Hull,
    float Speed,
    float PreferredRange,
    float Damage,
    int CadenceTicks);
