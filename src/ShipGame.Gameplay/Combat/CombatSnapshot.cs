using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct CombatSnapshot(
    long Tick,
    EntityId Entity,
    Vector2 Position,
    float Rotation,
    Faction Faction,
    float Hull,
    float Shield,
    bool Destroyed);
