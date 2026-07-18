using System.Numerics;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

/// <summary>Spawn intent from a weapon strategy — host must not re-branch on weapon kind.</summary>
internal readonly record struct PlayerProjectileSpawnRequest(
    EntityId Source,
    Vector2 Aim,
    WeaponDefinition Definition,
    TemporaryCombatModifiers Modifiers,
    EntityId Target,
    bool Homing,
    float TurnDegreesPerSecond);
