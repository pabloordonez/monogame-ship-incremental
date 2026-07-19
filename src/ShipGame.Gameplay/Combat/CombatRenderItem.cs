using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public readonly record struct CombatRenderItem(
    EntityId Entity,
    Vector2 Position,
    float Rotation,
    Faction Faction,
    CombatRenderKind Kind,
    bool Elite,
    float Hull,
    float Shield);
