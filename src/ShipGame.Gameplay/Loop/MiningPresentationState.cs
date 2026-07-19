using System.Numerics;

namespace ShipGame.Gameplay;

/// <summary>Presentation snapshot of the active mining beam for the last sim tick.</summary>
public readonly record struct MiningPresentationState(
    bool Active,
    bool Hit,
    Vector2 Origin,
    Vector2 HitPosition,
    float HitDistance,
    AsteroidCellKind Kind);
