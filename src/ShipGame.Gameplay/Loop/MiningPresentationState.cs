using System.Numerics;

namespace ShipGame.Gameplay;

public enum MiningToolMode : byte
{
    Laser = 0,
    Seismic = 1
}

/// <summary>Presentation snapshot of the active mining tool for the last sim tick.</summary>
public readonly record struct MiningPresentationState(
    bool Active,
    bool Hit,
    Vector2 Origin,
    Vector2 HitPosition,
    float HitDistance,
    AsteroidCellKind Kind,
    MiningToolMode Mode = MiningToolMode.Laser,
    float BlastRadius = 0f,
    bool FiredThisTick = false,
    bool Ready = true);
